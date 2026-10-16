using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Halloween;

/// <summary>Impostor: stellt Kuerbisfallen auf, die nur Impostor sehen. Wer hineintappt, steckt kurz fest.</summary>
public sealed class PumpkinHead : CustomRole
{
    public override string ConfigSection => "Kuerbiskopf";
    public override string Name => Lang.T("Kürbiskopf", "Pumpkin Head");
    public override Color Color => new Color32(240, 130, 30, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Ein Kürbis für jeden", "A pumpkin for everyone");
    public override string Description => Lang.T("Stelle Kürbisfallen auf (F).\nOpfer stecken kurz fest.", "Place pumpkin traps (F).\nVictims get stuck briefly.");
    public override string LongDescription => Lang.T(
        "Halloween-Impostor. Mit Taste F stellst du dort, wo du stehst, eine Kürbisfalle auf. Nur Impostor sehen die Fallen.\n\nTappt jemand hinein, kann er sich ein paar Sekunden nicht bewegen und du erfährst, wer es war. Jede Falle schnappt nur einmal zu.",
        "Halloween impostor. Press F to place a pumpkin trap where you stand. Only impostors can see the traps.\n\nWhoever steps into one can't move for a few seconds and you learn who it was. Each trap snaps only once.");

    private const byte ActionPlace = 0, ActionTrigger = 1;
    private const float TrapRange = 0.6f;

    private RoleOption cooldown, maxTraps, rootTime;

    private sealed class Trap
    {
        public byte Owner, Id;
        public Vector2 Pos;
        public GameObject Visual;
    }

    // bei allen Clients
    private readonly List<Trap> traps = new();
    private byte nextId;
    private float rootedUntil;
    private GameObject rootVisual;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 20f, 5f, 60f, 2.5f, "s");
        maxTraps = AddOption("MaxTraps", "Fallen gleichzeitig|Traps at once", 3f, 1f, 6f, 1f);
        rootTime = AddOption("RootTime", "Festhalte-Zeit|Stuck time", 4f, 1f, 10f, 0.5f, "s");

        AddButton(new RoleButton
        {
            Label = "Falle|Trap",
            Icon = "pumpkin",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            Hotkey = KeyCode.F,
            CanUse = () => !PlayerControl.LocalPlayer.inVent && traps.Count(t => t.Owner == PlayerControl.LocalPlayer.PlayerId) < maxTraps.Int,
            OnClick = _ =>
            {
                var local = PlayerControl.LocalPlayer;
                var data = WorldMarker.Concat(nextId++, WorldMarker.Encode(local.GetTruePosition()));
                SendToAll(ActionPlace, data);
            },
        });
    }

    public override void ResetState()
    {
        foreach (var t in traps)
            if (t.Visual) Object.Destroy(t.Visual);
        traps.Clear();
        nextId = 0;
        rootedUntil = 0f;
        if (rootVisual) Object.Destroy(rootVisual);
        rootVisual = null;
    }

    public override float SpeedMultiplier(PlayerControl local) => rootedUntil > Time.time && !InMeeting ? 0f : 1f;

    public override void OnMeetingStart() => rootedUntil = 0f;

    public override void Update()
    {
        if (rootVisual && rootedUntil <= Time.time)
        {
            Object.Destroy(rootVisual);
            rootVisual = null;
        }

        var local = PlayerControl.LocalPlayer;
        if (traps.Count == 0 || !IsAlive(local) || InMeeting) return;

        // Impostor sehen die Fallen, alle anderen nicht
        bool seesTraps = IsImpostor(local);
        foreach (var t in traps)
        {
            if (seesTraps && !t.Visual) t.Visual = WorldMarker.Spawn("pumpkin", t.Pos, 0.35f, "PumpkinTrap");
            if (t.Visual) t.Visual.SetActive(seesTraps);
        }

        if (seesTraps || local.inVent || rootedUntil > Time.time) return;
        Vector2 me = local.GetTruePosition();
        var hit = traps.FirstOrDefault(t => Vector2.Distance(me, t.Pos) <= TrapRange);
        if (hit != null) SendToAll(ActionTrigger, hit.Owner, hit.Id, local.PlayerId);
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionPlace && data.Length >= 5 && RoleRegistry.Has(sender, this))
        {
            traps.Add(new Trap { Owner = sender.PlayerId, Id = data[0], Pos = WorldMarker.Decode(data, 1) });
            if (sender == PlayerControl.LocalPlayer) Sfx.Play(Sfx.Splash, 0.6f);
            return;
        }
        if (action != ActionTrigger || data.Length < 3) return;

        var trap = traps.FirstOrDefault(t => t.Owner == data[0] && t.Id == data[1]);
        if (trap == null) return; // schon ausgeloest
        traps.Remove(trap);
        if (trap.Visual) Object.Destroy(trap.Visual);

        var local = PlayerControl.LocalPlayer;
        var victim = FindPlayer(data[2]);
        if (!local || !victim) return;

        if (local.PlayerId == victim.PlayerId)
        {
            rootedUntil = Time.time + rootTime.Value;
            if (rootVisual) Object.Destroy(rootVisual);
            rootVisual = WorldMarker.Spawn("pumpkin", trap.Pos, 0.35f, "PumpkinSnap");
            Sfx.Play(Sfx.Fail);
            Notifier.Show($"<color={ColorHex}>{Lang.T("Kürbisfalle! Du steckst fest!", "Pumpkin trap! You are stuck!")}</color>", rootTime.Value);
        }
        else if (local.PlayerId == trap.Owner)
        {
            Sfx.Play(Sfx.TaskUpdate);
            string who = Camouflage.Active ? Lang.T("Jemand", "Someone") : victim.Data.PlayerName;
            Notifier.Show($"<color={ColorHex}>{who} {Lang.T("ist in deine Falle getappt!", "stepped into your trap!")}</color>");
        }
    }
}
