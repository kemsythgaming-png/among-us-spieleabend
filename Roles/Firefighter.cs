using AmongUs.GameOptions;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>Crew: behebt per Knopf die gerade aktive Sabotage (Reaktor, O2, Kommunikation).</summary>
public sealed class Firefighter : CustomRole
{
    public override string ConfigSection => "Feuerwehrmann";
    public override string Name => Lang.T("Feuerwehrmann", "Firefighter");
    public override Color Color => new Color32(220, 80, 40, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Du löschst jedes Feuer", "You put out every fire");
    public override string Description => Lang.T("Behebe die aktive Sabotage (F).", "Fix the active sabotage (F).");
    public override string LongDescription => Lang.T(
        "Du bist Crew. Mit dem Knopf behebst du die gerade laufende Sabotage von überall - Reaktor, Sauerstoff, Kommunikation und Licht sofort. Du hast dafür nur eine begrenzte Anzahl Ladungen (Zahl am Knopf).",
        "You are crew. The button fixes the current sabotage from anywhere - reactor, oxygen, communications and lights at once. You only have a limited number of charges (shown on the button).");

    private RoleOption cooldown, charges;
    private int chargesLeft;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 50f, 10f, 120f, 5f, "s");
        charges = AddOption("Charges", "Ladungen|Charges", 3f, 1f, 10f, 1f);

        AddButton(new RoleButton
        {
            Label = "Löschen|Extinguish",
            Icon = "extinguish",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 15f,
            Hotkey = KeyCode.F,
            UsesLeft = () => chargesLeft,
            CanUse = () => SabotageActive() && chargesLeft > 0,
            OnClick = _ => { if (chargesLeft > 0) { chargesLeft--; FixSabotages(); } },
        });
    }

    public override void OnAssigned(PlayerControl player)
    {
        if (player == PlayerControl.LocalPlayer) chargesLeft = charges.Int;
    }

    internal static bool SabotageActive()
    {
        var ship = ShipStatus.Instance;
        if (!ship || ship.Systems == null) return false;
        try
        {
            return ship.Systems.ContainsKey(SystemTypes.Sabotage)
                && ship.Systems[SystemTypes.Sabotage].TryCast<SabotageSystemType>() is { AnyActive: true };
        }
        catch { return false; }
    }

    internal static void FixSabotages()
    {
        var ship = ShipStatus.Instance;
        if (!ship) return;
        // Reaktor/Labor: beide Konsolen "halten" -> Sabotage endet
        Repair(ship, SystemTypes.Reactor, 0x10);
        Repair(ship, SystemTypes.Reactor, 0x11);
        Repair(ship, SystemTypes.Laboratory, 0x10);
        Repair(ship, SystemTypes.Laboratory, 0x11);
        // Sauerstoff
        Repair(ship, SystemTypes.LifeSupp, 0x40);
        Repair(ship, SystemTypes.LifeSupp, 0x41);
        // Kommunikation - aber NICHT die Störsender-Störung (die ist keine echte Sabotage)
        if (!(MapSaboteur.Instance?.JamActive ?? false)) Repair(ship, SystemTypes.Comms, 0);
        // Licht: alle falsch stehenden Schalter umlegen
        FixLights(ship);
        Sfx.Play(Sfx.TaskComplete, 0.7f);
    }

    private static void FixLights(ShipStatus ship)
    {
        try
        {
            if (ship.Systems == null || !ship.Systems.ContainsKey(SystemTypes.Electrical)) return;
            var sw = ship.Systems[SystemTypes.Electrical].TryCast<SwitchSystem>();
            if (sw == null) return;
            for (byte i = 0; i < 5; i++)
            {
                bool actual = (sw.ActualSwitches & (1 << i)) != 0;
                bool expected = (sw.ExpectedSwitches & (1 << i)) != 0;
                if (actual != expected) ship.RpcUpdateSystem(SystemTypes.Electrical, i);
            }
        }
        catch { /* Map ohne Schalter */ }
    }

    private static void Repair(ShipStatus ship, SystemTypes type, byte tag)
    {
        try
        {
            if (ship.Systems != null && ship.Systems.ContainsKey(type)) ship.RpcUpdateSystem(type, tag);
        }
        catch { /* System auf dieser Map nicht vorhanden */ }
    }
}
