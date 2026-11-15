using System.Linq;
using AmongUs.GameOptions;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Neutraler Einzelgänger ("Loner"): spielt wie ein Impostor, aber ganz allein - er kennt keine Teammates und
/// gewinnt erst, wenn ALLE anderen (auch echte Impostor) tot sind. Eigener Töten-Knopf.
/// </summary>
public sealed class Loner : CustomRole
{
    public override string ConfigSection => "Loner";
    public override string Name => Lang.T("Loner", "Loner");
    public override Color Color => new Color32(150, 40, 40, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override string IntroText => Lang.T("Ganz allein", "All alone");
    public override string Description => Lang.T("Töte alle (F).\nDu gewinnst als Letzter.", "Kill everyone (F).\nYou win as the last one.");
    public override string LongDescription => Lang.T(
        "Du bist ein Einzelgänger und spielst ganz allein - du weißt nicht, wer sonst noch Impostor ist. Mit dem Töten-Knopf tötest du andere. Du gewinnst erst, wenn ALLE anderen tot sind - auch die echten Impostor.",
        "You are a loner and play entirely alone - you don't know who else is impostor. Use the kill button to kill others. You only win once EVERYONE else is dead - including the real impostors.");

    private const byte ActionKill = 0;

    private RoleOption cooldown;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 25f, 10f, 60f, 5f, "s");

        AddButton(new RoleButton
        {
            Label = "Töten|Kill",
            GameIcon = hud => hud && hud.KillButton ? hud.KillButton.graphic.sprite : null,
            NeedsTarget = true,
            TargetFilter = p => IsAlive(p),
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer),
            OnClick = target => { if (target) SendToHost(ActionKill, target.PlayerId); },
        });
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionKill || !AmHost || data.Length < 1 || !RoleRegistry.Has(sender, this)) return;
        var victim = FindPlayer(data[0]);
        if (IsAlive(victim)) KillSystem.TryKill(sender, victim); // normale Kill-Animation inkl. Teleport
    }

    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause) => CheckWin();
    public override void OnMeetingEnd(PlayerControl exiled) => CheckWin();

    private void CheckWin()
    {
        if (!AmHost) return;
        var loners = Players.Where(IsAlive).ToArray();
        if (loners.Length == 0) return;
        // Gewinnt, wenn sonst niemand mehr lebt
        foreach (var p in PlayerControl.AllPlayerControls)
            if (IsAlive(p) && !RoleRegistry.Has(p, this)) return;
        CustomWin.Trigger(this, loners);
    }

    // Teammates verbergen: der Loner sieht andere Impostor NICHT rot, und andere sehen den Loner nicht als Mitimpostor.
    public override Color? GetNameColor(PlayerControl viewer, PlayerControl target)
    {
        if (!viewer || !target) return null;
        bool viewerLoner = RoleRegistry.Has(viewer, this);
        bool targetLoner = RoleRegistry.Has(target, this);
        if (viewerLoner && target != viewer && IsImpostor(target)) return CrewBlue;   // Teammates wirken wie Crew
        if (targetLoner && viewer != target && IsImpostor(viewer)) return CrewBlue;    // der Loner wirkt wie Crew
        return null;
    }
}
