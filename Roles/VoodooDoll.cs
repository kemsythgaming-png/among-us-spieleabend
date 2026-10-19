using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor: öffnet (von überall) ein Shapeshifter-Menü und übernimmt einen Spieler. Für die Dauer siehst du durch
/// seine Augen und steuerst ihn; er kann nur zuschauen. Du kannst dabei jeden in seiner Nähe töten - auch Impostor
/// oder dich selbst. Alle Namen erscheinen in dieser Zeit in Crew-Farbe.
/// Hinweis: Echtes Bewegen funktioniert zuverlässig nur bei Dummys (Host) - echte Mitspieler kann das Netzwerk nicht fremdsteuern.
/// </summary>
public sealed class VoodooDoll : CustomRole
{
    public override string ConfigSection => "VoodooPuppe";
    public override string Name => Lang.T("Voodoo-Puppe", "Voodoo Doll");
    public override Color Color => new Color32(120, 40, 120, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Zieh die Fäden", "Pull the strings");
    public override string Description => Lang.T("Übernimm einen Spieler (F)\nund steuere ihn.", "Take over a player (F)\nand control them.");
    public override string LongDescription => Lang.T(
        "Impostor: Mit dem Knopf öffnest du von überall ein Auswahl-Menü (wie der Shapeshifter). Wählst du einen Spieler, übernimmst du ihn für kurze Zeit: Du siehst durch seine Augen und steuerst ihn, er kann nur zuschauen. Du kannst dabei jeden in seiner Nähe töten - auch Impostor oder dich selbst. Alle Namen sind in dieser Zeit in Crew-Farbe. (Echtes Steuern geht zuverlässig nur bei Test-Dummys.)",
        "Impostor: the button opens a selection menu from anywhere (like the Shapeshifter). Pick a player to take them over briefly: you see through their eyes and control them, they can only watch. You can kill anyone near them - including impostors or yourself. All names show in crew color meanwhile. (Real control only works reliably on test dummies.)");

    private const byte ActionPossess = 0, ActionKill = 1;
    private RoleOption cooldown, duration;

    /// <summary>Läuft gerade eine Übernahme beim lokalen Spieler (für die Melde-Sperre)?</summary>
    internal static bool LocalPossessing;

    // bei allen: wer ist bis wann besessen (steht fest)
    private readonly Dictionary<byte, float> possessedUntil = new();

    // lokal beim Voodoo
    private byte myTarget = byte.MaxValue;
    private float myUntil;
    private ShapeshifterMinigame menu;
    private bool namesTinted;
    private Vector3 homePos;
    private bool homeSet;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 35f, 15f, 90f, 2.5f, "s");
        duration = AddOption("Duration", "Dauer|Duration", 12f, 5f, 25f, 1f, "s");

        AddButton(new RoleButton
        {
            Label = "Übernehmen|Take over",
            Icon = "curse",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => myTarget == byte.MaxValue,
            Visible = () => myTarget == byte.MaxValue,
            OnClick = _ => OpenMenu(),
        });

        AddButton(new RoleButton
        {
            Label = "Töten|Kill",
            Icon = "stake",
            NeedsTarget = false,
            SharesKillCooldown = true, // teilt sich die Abklingzeit mit dem normalen Impostor-Kill
            Visible = () => myTarget != byte.MaxValue && Time.time < myUntil,
            Cooldown = () => GameManager.Instance ? GameManager.Instance.LogicOptions.GetKillCooldown() : 20f,
            StartCooldown = 0f,
            CanUse = () => VictimNearTarget() != null,
            OnClick = _ =>
            {
                var v = VictimNearTarget();
                if (v) { SendToHost(ActionKill, myTarget, v.PlayerId); Sfx.Play(Sfx.Kill); }
            },
        });
    }

    public override void ResetState()
    {
        possessedUntil.Clear();
        EndPossession();
        if (menu) { menu.Close(); menu = null; }
    }

    public override void OnMeetingStart() => ResetState();

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionPossess && data.Length >= 1 && RoleRegistry.Has(sender, this))
        {
            possessedUntil[data[0]] = Time.time + duration.Value;
            if (sender == PlayerControl.LocalPlayer) BeginPossession(data[0]);
            var local = PlayerControl.LocalPlayer;
            if (local && local.PlayerId == data[0])
                Notifier.Show($"<color={ColorHex}>{Lang.T("Du wirst übernommen - du kannst nur zusehen!", "You are taken over - you can only watch!")}</color>", duration.Value);
        }
        else if (action == ActionKill && AmHost && data.Length >= 2)
        {
            var victim = FindPlayer(data[1]);
            if (IsAlive(victim)) KillSystem.ForceKill(victim, victim); // nur das Opfer stirbt, kein Teleport
        }
    }

    // ---------- Übernahme ----------

    private void BeginPossession(byte targetId)
    {
        var local = PlayerControl.LocalPlayer;
        myTarget = targetId;
        myUntil = Time.time + duration.Value;
        if (local)
        {
            homePos = local.transform.position;
            homeSet = true;
            local.Visible = false; // dein eigener Körper ist unsichtbar - du steckst im Ziel
        }
        LocalPossessing = true;
        Sfx.Play(Sfx.Shapeshift);
    }

    private void EndPossession()
    {
        var local = PlayerControl.LocalPlayer;
        if (local)
        {
            local.Visible = true;
            if (homeSet)
            {
                local.transform.position = homePos; // zurück an deinen Platz
                if (local.MyPhysics && local.MyPhysics.body) local.MyPhysics.body.velocity = Vector2.zero;
            }
        }
        homeSet = false;
        myTarget = byte.MaxValue;
        myUntil = 0f;
        LocalPossessing = false;
        RestoreNames();

        // normalen Kill- und Vent-Knopf wieder einblenden
        var hud = HudManager.InstanceExists ? HudManager.Instance : null;
        if (hud)
        {
            if (hud.KillButton) hud.KillButton.ToggleVisible(true);
            if (hud.ImpostorVentButton) hud.ImpostorVentButton.ToggleVisible(true);
        }
    }

    public override float SpeedMultiplier(PlayerControl local)
    {
        if (!IsAlive(local) || InMeeting) return 1f;
        if (possessedUntil.TryGetValue(local.PlayerId, out var t) && t > Time.time) return 0f; // Besessener steht fest
        if (myTarget != byte.MaxValue && Time.time < myUntil && local.PlayerId != myTarget) return 0f; // Voodoo-Körper steht fest
        return 1f;
    }

    public override void Update()
    {
        // Übernahme vorbei?
        if (myTarget != byte.MaxValue && Time.time >= myUntil) EndPossession();

        if (myTarget == byte.MaxValue) { if (namesTinted) RestoreNames(); return; }

        // Alle Namen in Crew-Farbe, solange ich jemanden übernommen habe
        TintNames();

        var local = PlayerControl.LocalPlayer;
        var target = FindPlayer(myTarget);
        if (!local || !target || !IsAlive(target)) return;

        // Das Ziel steuern (zuverlässig nur Dummys/als Host)
        if (target.isDummy || AmHost)
        {
            var move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (move.sqrMagnitude > 0.01f)
            {
                float speed = 2.5f;
                Vector3 np = target.transform.position + (Vector3)(move.normalized * speed * Time.deltaTime);
                target.transform.position = new Vector3(np.x, np.y, target.transform.position.z);
                if (target.MyPhysics && target.MyPhysics.body) target.MyPhysics.body.velocity = Vector2.zero;
            }
        }

        // Sicht folgt dem Ziel: dein (unsichtbarer) Körper + Kamera sitzen auf dem Ziel, damit du etwas siehst
        local.transform.position = new Vector3(target.transform.position.x, target.transform.position.y, local.transform.position.z);
        if (local.MyPhysics && local.MyPhysics.body) local.MyPhysics.body.velocity = Vector2.zero;

        // Während der Übernahme nur den Töten-Knopf: normalen Kill + Vent ausblenden (der Klon hat sein Bild schon)
        var hud = HudManager.InstanceExists ? HudManager.Instance : null;
        if (hud)
        {
            if (hud.KillButton) hud.KillButton.ToggleVisible(false);
            if (hud.ImpostorVentButton) hud.ImpostorVentButton.ToggleVisible(false);
        }
    }

    private void TintNames()
    {
        namesTinted = true;
        var local = PlayerControl.LocalPlayer;
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!p || !p.cosmetics || !p.cosmetics.nameText) continue;
            // eigenen (unsichtbaren) Namen ausblenden, alle anderen in Crew-Farbe
            p.cosmetics.nameText.color = p == local ? new Color(1f, 1f, 1f, 0f) : CrewBlue;
        }
    }

    private void RestoreNames()
    {
        namesTinted = false;
        foreach (var p in PlayerControl.AllPlayerControls)
            if (p && p.cosmetics && p.cosmetics.nameText) p.cosmetics.nameText.color = Color.white;
    }

    private PlayerControl VictimNearTarget()
    {
        if (myTarget == byte.MaxValue) return null;
        var target = FindPlayer(myTarget);
        if (!IsAlive(target)) return null;
        const float range = 1.0f; // enge Reichweite
        Vector2 at = target.GetTruePosition();
        var local = PlayerControl.LocalPlayer;
        // jeder in der Nähe des Übernommenen kann sterben - aber NICHT das Ziel und NICHT dein (mitreisender) Körper
        return PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p && p != target && p != local && IsAlive(p) && Vector2.Distance(at, p.GetTruePosition()) <= range)
            .OrderBy(p => Vector2.Distance(at, p.GetTruePosition()))
            .FirstOrDefault();
    }

    // ---------- Auswahl-Menü (Shapeshifter-Stil) ----------

    private static ShapeshifterMinigame MenuPrefab()
    {
        if (!RoleManager.Instance) return null;
        foreach (var role in RoleManager.Instance.AllRoles)
        {
            var ss = role ? role.TryCast<ShapeshifterRole>() : null;
            if (ss && ss.ShapeshifterMenu) return ss.ShapeshifterMenu;
        }
        return null;
    }

    private void OpenMenu()
    {
        try
        {
            var prefab = MenuPrefab();
            var local = PlayerControl.LocalPlayer;
            if (!prefab || !Camera.main || Minigame.Instance || !IsAlive(local) || InMeeting) return;

            menu = Object.Instantiate(prefab, Camera.main.transform, false);
            menu.transform.localPosition = new Vector3(0f, 0f, -50f);
            menu.Begin(null);

            foreach (var panel in menu.potentialVictims)
                if (panel) Object.Destroy(panel.gameObject);
            menu.potentialVictims.Clear();

            // alle lebenden Spieler, inkl. einem selbst und Impostoren
            var players = new List<PlayerControl> { local };
            players.AddRange(PlayerControl.AllPlayerControls.ToArray().Where(p => IsAlive(p) && p != local));
            for (int i = 0; i < players.Count; i++)
            {
                var target = players[i];
                var panel = Object.Instantiate(menu.PanelPrefab, menu.transform);
                panel.transform.localPosition = new Vector3(menu.XStart + i % 3 * menu.XOffset, menu.YStart + i / 3 * menu.YOffset, -1f);
                panel.SetPlayer(i, target.Data, (System.Action)(() => Pick(target)));
                menu.potentialVictims.Add(panel);
            }
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogWarning($"Voodoo-Menü: {e.Message}");
            if (menu) Object.Destroy(menu.gameObject);
        }
    }

    private void Pick(PlayerControl target)
    {
        if (menu) menu.Close();
        menu = null;
        var local = PlayerControl.LocalPlayer;
        if (!target || target.Data == null || !IsAlive(local) || InMeeting) return;
        SendToAll(ActionPossess, target.PlayerId);
    }
}

/// <summary>Während einer Übernahme kann der steuernde Voodoo-Spieler keine Leiche melden / kein Meeting rufen.</summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdReportDeadBody))]
internal static class VoodooNoReportPatch
{
    public static bool Prefix(PlayerControl __instance) =>
        !(VoodooDoll.LocalPossessing && __instance == PlayerControl.LocalPlayer);
}
