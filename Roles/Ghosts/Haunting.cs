using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Ghosts;

/// <summary>
/// Neutral-Geist: ein toter Crewmate, den alle Lebenden halb durchsichtig sehen. Schafft er alle seine Aufgaben,
/// gewinnt er allein. Klickt ihn ein Lebender an, ist er erloest und kann nicht mehr gewinnen.
/// </summary>
public sealed class Haunting : CustomRole
{
    public override string ConfigSection => "Spukgestalt";
    public override string Name => Lang.T("Spukgestalt", "Haunting");
    public override Color Color => new Color32(200, 170, 255, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override bool IsGhostRole => true;
    public override bool CanHaunt(PlayerControl dead) => !IsImpostor(dead) && !IsNeutral(dead);
    public override string IntroText => Lang.T("Spuk dich zum Sieg", "Haunt your way to victory");
    public override string Description => Lang.T("Geist: fast unsichtbar. Schaff deine neuen\nAufgaben, ohne angeklickt zu werden!", "Ghost: almost invisible. Finish your new\ntasks without being clicked!");
    public override string LongDescription => Lang.T(
        "Geisterrolle für tote Crewmates. Du läufst wie eine normale Figur herum, bist für die Lebenden aber fast unsichtbar - tarn dich gut!\n\nDu bekommst neue Aufgaben. Schaffst du sie alle, gewinnst du allein und das Spiel endet. Klickt dich aber ein Lebender an, bist du erlöst - dann bist du ein ganz normaler Geist und kannst nicht mehr gewinnen.",
        "Ghost role for dead crewmates. You walk around like a normal figure, but the living can barely see you - blend in!\n\nYou get new tasks. Finish them all and you win alone - the game ends. But if a living player clicks you, you are released - then you are a normal ghost and can't win anymore.");

    private const byte ActionRelease = 0;
    private const float ClickRadius = 0.7f, ClickReach = 3f;
    private const float ReleaseGrace = 3f; // kurz nach dem Werden nicht erlösbar (sonst erlöst der Kill-Klick sofort)

    internal static Haunting Instance { get; private set; }

    private readonly HashSet<byte> released = new();
    private readonly HashSet<byte> shownToLiving = new();
    private readonly Dictionary<byte, float> hauntSince = new(); // ab wann spukt wer (für die Schutzzeit)

    private RoleOption visibility;

    protected override void Setup()
    {
        Instance = this;
        visibility = AddOption("Visibility", "Sichtbarkeit für Lebende|Visibility for the living", 12f, 5f, 50f, 1f, "%");
    }

    /// <summary>Beim Werden: neue Aufgaben (nur der Host verteilt sie, an alle).</summary>
    public override void OnAssigned(PlayerControl player)
    {
        // Echter Spielwechsel (Tod) ODER Freeplay-Test: neue Aufgaben geben (im Freeplay zum Prüfen)
        if (AmHost && (RoleRegistry.IsRoleChange || FreeplayTest.IsFreeplay)) GiveNewTasks(player);
    }

    private static void GiveNewTasks(PlayerControl p)
    {
        var ship = ShipStatus.Instance;
        if (!ship || p.Data == null) return;
        var opts = GameOptionsManager.Instance.CurrentGameOptions;
        var ids = new List<byte>();
        var usedTypes = new HashSet<TaskTypes>();
        void Pick(IEnumerable<NormalPlayerTask> pool, int count)
        {
            foreach (var t in pool.Where(t => t).OrderBy(_ => Random.value))
            {
                if (count <= 0) break;
                if (!usedTypes.Add(t.TaskType)) continue;
                ids.Add((byte)t.Index);
                count--;
            }
        }
        Pick(ship.CommonTasks, opts.GetInt(Int32OptionNames.NumCommonTasks));
        Pick(ship.LongTasks, opts.GetInt(Int32OptionNames.NumLongTasks));
        Pick(ship.ShortTasks, opts.GetInt(Int32OptionNames.NumShortTasks));
        if (ids.Count == 0) return;
        try { p.Data.RpcSetTasks(ids.ToArray()); }
        catch (System.Exception e) { Plugin.Logger.LogWarning($"Spukgestalt-Aufgaben: {e.Message}"); }
    }

    /// <summary>Laeuft wie eine lebende Figur (statt als schwebender Geist).</summary>
    internal bool WalksAsLiving(PlayerControl p) => Haunts(p);

    public override void ResetState()
    {
        HideAll();
        released.Clear();
        hauntSince.Clear();
        localCollider = false;
        localFaded = false;
        spawnedInRoom = false;
        spawnAt = 0f;
        wasInMeeting = false;
    }

    /// <summary>Spukt dieser Spieler gerade (Geist mit dieser Rolle, nicht erloest)?</summary>
    private bool Haunts(PlayerControl p) =>
        p && p.Data != null && p.Data.IsDead && !p.Data.Disconnected && RoleRegistry.Has(p, this) && !released.Contains(p.PlayerId);

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionRelease || data.Length < 1 || !IsAlive(sender)) return;
        var ghost = FindPlayer(data[0]);
        if (!Haunts(ghost)) return;
        released.Add(ghost.PlayerId);
        Hide(ghost);

        // Erloest -> seine (neuen) Aufgaben verschwinden, sonst schleppt der normale Geist noch Crew-Aufgaben mit
        if (AmHost && ghost.Data != null)
        {
            try { ghost.Data.RpcSetTasks(new byte[0]); }
            catch (System.Exception e) { Plugin.Logger.LogWarning($"Spukgestalt-Aufgaben leeren: {e.Message}"); }
        }

        var local = PlayerControl.LocalPlayer;
        if (local == sender || local == ghost) Sfx.Play(Sfx.Protect);
        if (local == sender)
            Notifier.Show($"<color={ColorHex}>{Lang.T("Du hast", "You released")} {ghost.Data.PlayerName}{Lang.T(" erlöst.", ".")}</color>", 3f);
        else if (local == ghost)
            Notifier.Show($"<color={ColorHex}>{Lang.T("Du wurdest erlöst - du kannst nicht mehr gewinnen.", "You were released - you can't win anymore.")}</color>", 5f);
    }

    private bool wasInMeeting;

    // Nur beim Spukgeist selbst: Collider wieder an, damit er wie eine lebende Figur an Waenden haengen bleibt
    private bool localCollider;
    private bool localFaded;     // Spukgestalt sieht auch sich selbst nur schwach
    private bool spawnedInRoom;  // beim Werden einmal in einen leeren Raum versetzen
    private float spawnAt;        // Zeitpunkt für das verzögerte Vent-Spawnen (nach der Kill-Animation)
    private int playersLayer = -2, ghostLayer = -2; // -2 = noch nicht ermittelt

    /// <summary>
    /// Kernproblem: Tote liegen auf dem Layer "Ghost", der laut Kollisionsmatrix NICHT mit den Schiffs-Wänden
    /// (Layer "Ship") kollidiert - deshalb reicht es NICHT, nur den Collider einzuschalten. Wir legen die eigene
    /// Spukgestalt zusätzlich lokal auf den "Players"-Layer, dann bleibt sie wie ein Crewmate an Wänden hängen.
    /// (Players kollidieren untereinander nicht, also blockiert man keine Lebenden und bleibt trotzdem unsichtbar.)
    /// </summary>
    internal void ForceLiveCollision(PlayerControl local)
    {
        if (!local || !local.Collider) return;
        if (playersLayer == -2) { playersLayer = LayerMask.NameToLayer("Players"); ghostLayer = LayerMask.NameToLayer("Ghost"); }
        if (!local.Collider.enabled) local.Collider.enabled = true;
        var go = local.Collider.gameObject;
        if (playersLayer >= 0 && go.layer != playersLayer) go.layer = playersLayer;
    }

    private void UpdateLocalCollision()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !local.Collider) return;
        if (playersLayer == -2) { playersLayer = LayerMask.NameToLayer("Players"); ghostLayer = LayerMask.NameToLayer("Ghost"); }
        bool want = Haunts(local) && !InMeeting;
        if (want)
        {
            ForceLiveCollision(local);
        }
        else if (localCollider && !IsAlive(local))
        {
            // erloest/Meeting -> wieder normaler Geist (durch Waende)
            local.Collider.enabled = false;
            var go = local.Collider.gameObject;
            if (ghostLayer >= 0 && go.layer != ghostLayer) go.layer = ghostLayer;
        }
        localCollider = want;
    }

    public override void Update()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || local.Data == null) return;
        UpdateLocalCollision();

        // Spukgestalt sieht auch SICH SELBST nur schwach (gleiche Sichtbarkeit wie fuer andere)
        bool fadeLocal = Haunts(local) && !InMeeting;
        if (local.cosmetics)
        {
            if (fadeLocal) local.cosmetics.SetPhantomRoleAlpha(visibility.Value / 100f);
            else if (localFaded) local.cosmetics.SetPhantomRoleAlpha(1f);
        }
        localFaded = fadeLocal;

        // Beim Werden einmal auf ein zufälliges Vent versetzen - aber ERST nach der Kill-Animation (sonst zieht es den
        // Mörder mit, weil die Animation ihn kurz am Opfer festhält).
        if (Haunts(local) && !spawnedInRoom && !InMeeting)
        {
            if (spawnAt <= 0f) spawnAt = Time.time + 1.6f;
            if (Time.time >= spawnAt)
            {
                spawnedInRoom = true;
                spawnAt = 0f;
                SpawnInEmptyRoom(local);
            }
        }

        // Nach einem Meeting erscheint die Spukgestalt wieder auf einem zufälligen Vent (spawnedInRoom zurücksetzen
        // -> der Spawn-Block oben läuft im nächsten Frame erneut).
        bool meetingNow = InMeeting;
        if (wasInMeeting && !meetingNow && Haunts(local)) spawnedInRoom = false;
        wasInMeeting = meetingNow;

        bool livingViewer = IsAlive(local) && !InMeeting;

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!p || p == local) continue;
            if (livingViewer && Haunts(p)) Show(p);
            else if (shownToLiving.Contains(p.PlayerId)) Hide(p);
        }

        // Lebende klicken die Spukgestalt an
        // Schutzzeit mitführen: ab wann spukt wer (auf jedem Client)
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!p) continue;
            if (Haunts(p)) { if (!hauntSince.ContainsKey(p.PlayerId)) hauntSince[p.PlayerId] = Time.time; }
            else hauntSince.Remove(p.PlayerId);
        }

        if (livingViewer && Input.GetMouseButtonDown(0) && !Ui.IsMouseBlocked() && Camera.main && !Minigame.Instance)
        {
            Vector2 click = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 me = local.GetTruePosition();
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (!Haunts(p)) continue;
                // Frisch gespukt? Dann noch nicht erlösbar - sonst erlöst der Kill-Klick den Geist sofort nach dem Kill.
                if (!hauntSince.TryGetValue(p.PlayerId, out var since) || Time.time - since < ReleaseGrace) continue;
                Vector2 pos = p.transform.position;
                if (Vector2.Distance(click, pos) > ClickRadius || Vector2.Distance(me, pos) > ClickReach) continue;
                SendToAll(ActionRelease, p.PlayerId);
                break;
            }
        }

        // Host: alle Aufgaben geschafft -> Sieg
        if (AmHost && !InMeeting)
            foreach (var p in PlayerControl.AllPlayerControls)
                if (Haunts(p) && AllTasksDone(p)) CustomWin.Trigger(this, new[] { p });
    }

    /// <summary>Beim Werden: einfach auf ein zufälliges Vent setzen - Vents sind immer frei begehbar (nie in einer Wand).</summary>
    private void SpawnInEmptyRoom(PlayerControl local)
    {
        var ship = ShipStatus.Instance;
        if (!ship || ship.AllVents == null) return;
        var vents = ship.AllVents.ToArray().Where(v => v).ToList();
        if (vents.Count == 0) return;

        var vent = vents[Random.Range(0, vents.Count)];
        Vector2 pick = (Vector2)(vent.transform.position + vent.Offset);
        local.transform.position = new Vector3(pick.x, pick.y, local.transform.position.z);
        if (local.MyPhysics && local.MyPhysics.body) local.MyPhysics.body.velocity = Vector2.zero;
    }

    private static bool AllTasksDone(PlayerControl p)
    {
        var tasks = p.Data.Tasks;
        if (tasks == null || tasks.Count == 0) return false;
        foreach (var t in tasks)
            if (t == null || !t.Complete) return false;
        return true;
    }

    /// <summary>Fuer Lebende sichtbar machen: halb durchsichtig, auf der Spieler-Ebene (Geister-Ebene sieht man lebend nicht).</summary>
    private void Show(PlayerControl p)
    {
        shownToLiving.Add(p.PlayerId);
        if (!p.cosmetics) return;
        if (!p.Visible) p.Visible = true;
        SetLayer(p.cosmetics.gameObject, LayerMask.NameToLayer("Players"));
        p.cosmetics.SetPhantomRoleAlpha(visibility.Value / 100f);
        // Name NICHT zeigen - sonst wüssten Lebende sofort, wer spukt (die Figur ist nur schwach sichtbar).
        if (p.cosmetics.nameText) p.cosmetics.nameText.gameObject.SetActive(false);
    }

    private void Hide(PlayerControl p)
    {
        if (!shownToLiving.Remove(p.PlayerId) || !p || !p.cosmetics) return;
        p.cosmetics.SetPhantomRoleAlpha(1f);
        if (p.cosmetics.nameText) p.cosmetics.nameText.gameObject.SetActive(true); // Name wieder zulassen (z. B. im Meeting)
        var local = PlayerControl.LocalPlayer;
        if (p.Data != null && p.Data.IsDead && IsAlive(local))
        {
            SetLayer(p.cosmetics.gameObject, LayerMask.NameToLayer("Ghost"));
            p.Visible = false;
        }
    }

    private void HideAll()
    {
        foreach (var id in new List<byte>(shownToLiving)) Hide(FindPlayer(id));
        shownToLiving.Clear();
    }

    private static void SetLayer(GameObject go, int layer)
    {
        if (layer < 0 || !go || go.layer == layer) return;
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    public override string GetNameMark(PlayerControl viewer, PlayerControl target) =>
        viewer == target && Haunts(target) ? $"<color={ColorHex}>[{Lang.T("Spuk", "Haunt")}]</color>" : null;
}

/// <summary>Die Spukgestalt laeuft mit den normalen Lauf-Animationen statt als schwebender Geist.</summary>
[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleAnimation))]
internal static class HauntingWalkPatch
{
    public static void Prefix(PlayerPhysics __instance, ref bool amDead)
    {
        if (amDead && Haunting.Instance != null && Haunting.Instance.WalksAsLiving(__instance.myPlayer)) amDead = false;
    }
}

/// <summary>
/// Collider der eigenen Spukgestalt genau im Physik-Schritt (FixedUpdate) erzwingen.
/// HudManager.Update laeuft nur einmal pro Frame - schaltet das Spiel den Collider in FixedUpdate kurz aus,
/// liefe man im selben Physik-Schritt durch die Wand. Hier sitzt der Collider garantiert vor der Bewegung.
/// </summary>
[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.FixedUpdate))]
internal static class HauntingColliderPatch
{
    public static void Prefix(PlayerPhysics __instance)
    {
        var inst = Haunting.Instance;
        var p = __instance ? __instance.myPlayer : null;
        if (inst == null || p != PlayerControl.LocalPlayer || !inst.WalksAsLiving(p)) return;
        if (MeetingHud.Instance || ExileController.Instance) return; // im Meeting wie ein normaler Geist
        if (!p.inVent) inst.ForceLiveCollision(p); // Collider AN + Layer "Players" genau vor dem Physik-Schritt
    }
}
