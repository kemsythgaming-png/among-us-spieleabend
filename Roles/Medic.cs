using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>Crew: gibt einem Spieler einen Schild. Der naechste Kill-Versuch auf ihn scheitert.</summary>
public sealed class Medic : CustomRole
{
    public override string Name =>
        Lang.T("Medic", "Medic");
    public override Color Color => new Color32(126, 251, 194, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText =>
        Lang.T("Beschütze die Crew", "Protect the crew");
    public override string Description =>
        Lang.T("Gib einem Spieler einen Schild (F).", "Give a player a shield (F).");
    public override string LongDescription =>
        Lang.T("Du bist Crew und schützt einen Spieler mit einem Schild (Taste F).\n\nDer nächste Angriff auf ihn scheitert. Je nach Einstellung bricht der Schild dabei und du wirst gewarnt.\n\nEs gibt immer nur einen Schild gleichzeitig.", "You are crew and protect one player with a shield (key F).\n\nThe next attack on them fails. Depending on the settings the shield breaks and you get a warning.\n\nOnly one shield at a time.");

    private const byte ActionShield = 0, ActionBreak = 1;
    private const byte None = byte.MaxValue;

    private RoleOption cooldown, breaksOnAttack, shieldedSees, medicNotified, endsOnDeath;

    private byte shielded = None;
    private byte medicId = None;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 10f, 0f, 60f, 5f, "s");
        breaksOnAttack = AddToggle("BreaksOnAttack", "Schild bricht beim Angriff|Shield breaks on attack", true);
        shieldedSees = AddToggle("ShieldedSees", "Geschützter sieht den Schild|Shielded player sees it", true);
        medicNotified = AddToggle("MedicNotified", "Medic wird bei Angriff gewarnt|Medic is warned on attack", true);
        endsOnDeath = AddToggle("EndsOnDeath", "Schild endet mit dem Tod des Medics|Shield ends when Medic dies", true);

        AddButton(new RoleButton
        {
            Label = "Schild|Shield",
            Icon = "shield",
            Cooldown = () => cooldown.Value,
            Hotkey = KeyCode.F,
            CanUse = () => shielded == None, // immer nur ein Schild
            OnClick = target => SendToAll(ActionShield, target.PlayerId),
        });
    }

    public override void ResetState()
    {
        shielded = None;
        medicId = None;
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        switch (action)
        {
            case ActionShield when data.Length > 0 && RoleRegistry.Has(sender, this):
                shielded = data[0];
                if (sender == PlayerControl.LocalPlayer) Sfx.Play(Sfx.Protect);
                medicId = sender.PlayerId;
                break;
            case ActionBreak:
                bool wasMine = PlayerControl.LocalPlayer && PlayerControl.LocalPlayer.PlayerId == medicId;
                // Aufprall-Effekt nur fuer die, die den Schild auch sehen
                if (CanSeeShield(PlayerControl.LocalPlayer))
                {
                    ProtectionVisuals.ShowBurst(FindPlayer(shielded), Color);
                    Sfx.Play(Sfx.Protect);
                }
                if (wasMine && medicNotified.Bool) Notifier.Show($"<color={ColorHex}>{Lang.T("Dein Schild wurde angegriffen!", "Your shield was attacked!")}</color>");
                if (data.Length > 0 && data[0] == 1) shielded = None;
                break;
        }
    }

    public override KillIntercept InterceptKill(PlayerControl killer, PlayerControl target)
    {
        if (shielded == None || target.PlayerId != shielded) return KillIntercept.None;
        if (endsOnDeath.Bool && !IsAlive(FindPlayer(medicId))) return KillIntercept.None;

        killer.RpcMurderPlayer(target, false); // Kill scheitert sichtbar, Abklingzeit des Killers startet neu
        SendToAll(ActionBreak, (byte)(breaksOnAttack.Bool ? 1 : 0));
        return KillIntercept.Handled;
    }

    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        if (victim.PlayerId == shielded || (endsOnDeath.Bool && victim.PlayerId == medicId)) shielded = None;
    }

    /// <summary>Den Schild sehen nur der Medic und (einstellbar) der Geschuetzte selbst.</summary>
    private bool CanSeeShield(PlayerControl viewer) =>
        viewer && shielded != None && (viewer.PlayerId == medicId || (viewer.PlayerId == shielded && shieldedSees.Bool));

    public override string GetNameMark(PlayerControl viewer, PlayerControl target) =>
        target.PlayerId == shielded && CanSeeShield(viewer) ? $"<color={ColorHex}>{Lang.T("[Schild]", "[Shield]")}</color>" : null;

    public override IEnumerable<(PlayerControl Target, Color Color)> GetProtectionBubbles(PlayerControl viewer)
    {
        if (CanSeeShield(viewer)) yield return (FindPlayer(shielded), Color);
    }
}
