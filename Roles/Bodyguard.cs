using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>Crew: bewacht einen Spieler. Wird er angegriffen, sterben stattdessen Bodyguard UND Angreifer.</summary>
public sealed class Bodyguard : CustomRole
{
    public override string Name =>
        Lang.T("Bodyguard", "Bodyguard");
    public override Color Color => new Color32(93, 129, 217, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText =>
        Lang.T("Wirf dich vor deinen Schützling", "Take the hit for them");
    public override string Description =>
        Lang.T("Bewache einen Spieler (F).", "Guard a player (F).");
    public override string LongDescription =>
        Lang.T("Du bist Crew und bewachst einen Spieler (Taste F).\n\nWird er angegriffen, stirbst du statt ihm - und nimmst den Angreifer mit in den Tod.\n\nJe nach Einstellung endet der Schutz beim nächsten Meeting.", "You are crew and guard one player (key F).\n\nIf they get attacked, you die instead - and take the attacker with you.\n\nDepending on the settings the guard ends at the next meeting.");

    private const byte ActionGuard = 0, ActionClear = 1;
    private const byte None = byte.MaxValue;

    private RoleOption cooldown, endsOnMeeting;

    private byte guarded = None;
    private byte guardId = None;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 20f, 0f, 60f, 5f, "s");
        endsOnMeeting = AddToggle("EndsOnMeeting", "Schutz endet beim Meeting|Guard ends at meeting", true);

        AddButton(new RoleButton
        {
            Label = "Bewachen|Guard",
            Icon = "guard",
            Cooldown = () => cooldown.Value,
            Hotkey = KeyCode.F,
            CanUse = () => guarded == None,
            OnClick = target => SendToAll(ActionGuard, target.PlayerId),
        });
    }

    public override void ResetState()
    {
        guarded = None;
        guardId = None;
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionGuard && data.Length > 0 && RoleRegistry.Has(sender, this))
        {
            guarded = data[0];
            guardId = sender.PlayerId;
            if (sender == PlayerControl.LocalPlayer) Sfx.Play(Sfx.Protect);
        }
        else if (action == ActionClear)
        {
            // Aufprall-Effekt nur fuer den Bodyguard selbst
            if (PlayerControl.LocalPlayer && PlayerControl.LocalPlayer.PlayerId == guardId)
            {
                ProtectionVisuals.ShowBurst(FindPlayer(guarded), Color);
                Sfx.Play(Sfx.Protect);
            }
            guarded = None;
        }
    }

    public override void OnMeetingStart()
    {
        if (endsOnMeeting.Bool) guarded = None;
    }

    public override KillIntercept InterceptKill(PlayerControl killer, PlayerControl target)
    {
        if (guarded == None || target.PlayerId != guarded) return KillIntercept.None;
        var guard = FindPlayer(guardId);
        if (!IsAlive(guard) || killer == guard) return KillIntercept.None;

        // Bodyguard stirbt fuer den Schuetzling und nimmt den Angreifer mit
        KillSystem.ForceKill(killer, guard);
        KillSystem.ForceKill(killer, killer);
        SendToAll(ActionClear);
        return KillIntercept.Handled;
    }

    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        if (victim.PlayerId == guarded || victim.PlayerId == guardId) guarded = None;
    }

    /// <summary>Die Schutzblase sieht nur der Bodyguard selbst.</summary>
    public override IEnumerable<(PlayerControl Target, Color Color)> GetProtectionBubbles(PlayerControl viewer)
    {
        if (guarded != None && viewer && viewer.PlayerId == guardId) yield return (FindPlayer(guarded), Color);
    }

    public override string GetNameMark(PlayerControl viewer, PlayerControl target) =>
        guarded != None && target.PlayerId == guarded && viewer.PlayerId == guardId ? $"<color={ColorHex}>{Lang.T("[Bewacht]", "[Guarded]")}</color>" : null;
}
