using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Crew (Thermospezialist): gibt einem Spieler in Reichweite ein Heissgetraenk - der ist dann 5 Sekunden immun gegen
/// Verlangsamungs- und Frost-Effekte. Das Getraenk hilft jedem, auch dem Impostor. Zwei Anwendungen pro Runde.
/// </summary>
public sealed class ThermoSpecialist : CustomRole
{
    public override string ConfigSection => "Thermospezialist";
    public override string Name => Lang.T("Thermospezialist", "Thermo Specialist");
    public override Color Color => new Color32(210, 140, 60, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Heiß hält wach", "Hot keeps you going");
    public override string Description => Lang.T("Gib ein Heißgetränk (F):\n5 s frost-immun.", "Give a hot drink (F):\n5 s frost-immune.");
    public override string LongDescription => Lang.T(
        "Du bist Crew. Mit Taste F gibst du einem Spieler in der Nähe ein Heißgetränk - er ist dann 5 Sekunden immun gegen Verlangsamungs- und Frost-Effekte. Das Getränk hilft jedem, also auch einem Impostor. Zwei Anwendungen pro Runde.",
        "You are crew. Press F to give a nearby player a hot drink - they are then immune to slow and frost effects for 5 seconds. The drink helps anyone, including an impostor. Two uses per round.");

    private const byte ActionDrink = 0;

    private RoleOption duration, usesPerRound;
    private int usesLeft;

    protected override void Setup()
    {
        duration = AddOption("Duration", "Schutzdauer|Protection time", 5f, 2f, 15f, 1f, "s");
        usesPerRound = AddOption("Uses", "Anwendungen pro Runde|Uses per round", 2f, 1f, 6f, 1f);

        AddButton(new RoleButton
        {
            Label = "Heißgetränk|Hot drink",
            Icon = "cauldron",
            NeedsTarget = true,
            TargetFilter = p => IsAlive(p),
            Cooldown = () => 10f,
            StartCooldown = 5f,
            Hotkey = KeyCode.F,
            UsesLeft = () => usesLeft,
            CanUse = () => !InMeeting && usesLeft > 0,
            OnClick = target =>
            {
                if (!target || usesLeft <= 0) return;
                usesLeft--;
                SendToAll(ActionDrink, target.PlayerId);
            },
        });
    }

    public override void ResetState() => usesLeft = Mathf.RoundToInt(usesPerRound.Value);
    public override void OnMeetingEnd(PlayerControl exiled) => usesLeft = Mathf.RoundToInt(usesPerRound.Value);

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionDrink || data.Length < 1 || !RoleRegistry.Has(sender, this)) return;
        byte id = data[0];
        WinterImmunity.Grant(id, duration.Value);
        var who = FindPlayer(id);
        if (who) ProtectionVisuals.ShowBurst(who, Color);
        if (who == PlayerControl.LocalPlayer) { Sfx.Play(Sfx.TaskUpdate, 0.6f); Notifier.Show($"<color={ColorHex}>{Lang.T("Heißgetränk: kurz frost-immun.", "Hot drink: briefly frost-immune.")}</color>", 3f); }
    }
}
