using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Ghosts;

/// <summary>Crew-Geist: nagelt Vents unsichtbar zu - niemand kommt eine Weile hinein, heraus oder hindurch.</summary>
public sealed class GuardianSpirit : CustomRole
{
    public override string ConfigSection => "Waechtergeist";
    public override string Name => Lang.T("Wächtergeist", "Guardian Spirit");
    public override Color Color => new Color32(170, 200, 140, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override bool IsGhostRole => true;
    public override bool CanHaunt(PlayerControl dead) => !IsImpostor(dead) && !IsNeutral(dead);
    public override string IntroText => Lang.T("Kein Impostor kommt hier durch", "No impostor gets through here");
    public override string Description => Lang.T("Geist: Schweb an ein Vent und\nnagle es unsichtbar zu (F).", "Ghost: float to a vent and\nboard it up invisibly (F).");
    public override string LongDescription => Lang.T(
        "Geisterrolle für tote Crewmates. Schweb an ein Vent und drück F: Es ist eine Weile zu - niemand kann hinein, heraus oder durch die Gänge dorthin.\n\nMan sieht es dem Vent nicht an. Ein Impostor merkt es erst, wenn er es benutzen will.",
        "Ghost role for dead crewmates. Float to a vent and press F: it is closed for a while - nobody can get in, out or travel to it.\n\nThe vent looks normal. An impostor only notices when trying to use it - then boards flash up briefly. You see the boards and how long the vent stays closed.");

    private const byte ActionBlock = 0;
    private const float Reach = 1.3f;

    internal static GuardianSpirit Instance { get; private set; }

    private RoleOption cooldown, duration;

    // bei allen Clients: Vent-Id -> zu bis
    private readonly Dictionary<int, float> blockedUntil = new();

    protected override void Setup()
    {
        Instance = this;
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 30f, 10f, 90f, 2.5f, "s");
        duration = AddOption("Duration", "Vent zu für|Vent closed for", 20f, 5f, 60f, 5f, "s");

        AddButton(new RoleButton
        {
            Label = "Zunageln|Board up",
            Icon = "boards",
            NeedsTarget = false,
            UsableWhenDead = true,
            Visible = () => !IsAlive(PlayerControl.LocalPlayer),
            Cooldown = () => cooldown.Value,
            Duration = () => duration.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => NearestVent() != null,
            OnClick = _ =>
            {
                var vent = NearestVent();
                if (vent) SendToAll(ActionBlock, (byte)vent.Id);
            },
        });
    }

    public override void ResetState() => ClearAll();

    public override void OnMeetingStart() => ClearAll();

    private void ClearAll()
    {
        blockedUntil.Clear();
        foreach (var m in markers.Values)
            if (m.Boards) Object.Destroy(m.Boards);
        markers.Clear();
        foreach (var f in flashes)
            if (f.Boards) Object.Destroy(f.Boards);
        flashes.Clear();
    }

    // Anzeige: Waechtergeister sehen Bretter + Restzeit auf zugenagelten Vents
    private sealed class Marker
    {
        public GameObject Boards;
        public TMPro.TextMeshPro Timer;
    }

    private readonly Dictionary<int, Marker> markers = new();

    // Wer ein zugenageltes Vent benutzen will, sieht kurz die Bretter aufblitzen
    private readonly List<(GameObject Boards, float Start)> flashes = new();
    private const float FlashTime = 1.2f;

    /// <summary>Lokal: Bretter am Vent kurz aufblitzen lassen (Versuch, ein zugenageltes Vent zu benutzen).</summary>
    internal void Flash(Vent vent)
    {
        if (!vent) return;
        Sfx.Play(Sfx.Fail);
        var go = WorldMarker.Spawn("boards", vent.transform.position, 0.6f, "VentBoardsFlash");
        flashes.Add((go, Time.time));
    }

    public override void Update()
    {
        float t = Time.time;

        // Aufblitzen ausblenden
        for (int i = flashes.Count - 1; i >= 0; i--)
        {
            var (boards, start) = flashes[i];
            float k = (t - start) / FlashTime;
            if (!boards || k >= 1f)
            {
                if (boards) Object.Destroy(boards);
                flashes.RemoveAt(i);
                continue;
            }
            var sr = boards.GetComponent<SpriteRenderer>();
            if (sr) sr.color = new Color(1f, 1f, 1f, k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f);
        }

        // Waechtergeist: Bretter + Restzeit auf allen zugenagelten Vents
        var local = PlayerControl.LocalPlayer;
        bool showMarkers = IsLocalRole && !IsAlive(local) && !InMeeting && ShipStatus.Instance;
        foreach (var id in new List<int>(markers.Keys))
        {
            if (showMarkers && blockedUntil.TryGetValue(id, out float until) && until > t) continue;
            if (markers[id].Boards) Object.Destroy(markers[id].Boards);
            markers.Remove(id);
        }
        if (!showMarkers) return;

        foreach (var pair in blockedUntil)
        {
            if (pair.Value <= t) continue;
            if (!markers.TryGetValue(pair.Key, out var m))
            {
                var vent = ShipStatus.Instance.AllVents.FirstOrDefault(v => v && v.Id == pair.Key);
                if (!vent) continue;
                markers[pair.Key] = m = CreateMarker(vent, local);
            }
            if (m.Timer) m.Timer.text = Mathf.CeilToInt(pair.Value - t) + "s";
        }
    }

    private static Marker CreateMarker(Vent vent, PlayerControl local)
    {
        var m = new Marker { Boards = WorldMarker.Spawn("boards", vent.transform.position, 0.6f, "VentBoards") };
        var source = local.cosmetics ? local.cosmetics.nameText : null;
        if (source)
        {
            var text = Object.Instantiate(source.gameObject, m.Boards.transform);
            text.name = "Spieleabend_VentTimer";
            text.SetActive(true);
            m.Timer = text.GetComponent<TMPro.TextMeshPro>();
            m.Timer.enabled = true;
            m.Timer.color = Color.white;
            var parentScale = m.Boards.transform.lossyScale;
            var want = source.transform.lossyScale;
            text.transform.localScale = new Vector3(want.x / parentScale.x, want.y / parentScale.y, 1f);
            text.transform.position = vent.transform.position + new Vector3(0f, 0.55f, -0.02f);
        }
        return m;
    }

    private static Vent NearestVent()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !ShipStatus.Instance) return null;
        Vector2 me = local.GetTruePosition();
        return ShipStatus.Instance.AllVents.Where(v => v && Vector2.Distance(me, v.transform.position) <= Reach)
            .OrderBy(v => Vector2.Distance(me, v.transform.position)).FirstOrDefault();
    }

    internal bool IsBlocked(Vent vent) =>
        vent && blockedUntil.TryGetValue(vent.Id, out float until) && until > Time.time;

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionBlock || data.Length < 1 || !RoleRegistry.Has(sender, this)) return;
        blockedUntil[data[0]] = Time.time + duration.Value;
        if (sender == PlayerControl.LocalPlayer)
        {
            Sfx.Play(Sfx.TaskComplete, 0.6f);
            Notifier.Show($"<color={ColorHex}>{Lang.T("Vent zugenagelt!", "Vent boarded up!")}</color>", 2.5f);
        }
    }
}

/// <summary>
/// Zugenagelte Vents: Der Knopf sieht normal aus - erst beim Benutzen (hinein oder heraus) passiert nichts,
/// und die Bretter blitzen kurz auf.
/// </summary>
[HarmonyPatch(typeof(Vent), nameof(Vent.Use))]
internal static class GuardianVentUsePatch
{
    public static bool Prefix(Vent __instance)
    {
        var gs = GuardianSpirit.Instance;
        if (gs == null || !gs.IsBlocked(__instance)) return true;
        gs.Flash(__instance);
        return false;
    }
}

/// <summary>Zugenagelte Vents: auch durch die Gaenge kommt man nicht hinein.</summary>
[HarmonyPatch(typeof(Vent), nameof(Vent.TryMoveToVent))]
internal static class GuardianVentMovePatch
{
    public static bool Prefix(Vent otherVent, ref string error, ref bool __result)
    {
        var gs = GuardianSpirit.Instance;
        if (gs == null || !gs.IsBlocked(otherVent)) return true;
        gs.Flash(otherVent);
        error = "";
        __result = false;
        return false;
    }
}
