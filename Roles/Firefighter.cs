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
        "Du bist Crew. Stell dich zu der Konsole der laufenden Sabotage (Reaktor, Sauerstoff, Kommunikation oder Licht) - mit dem Knopf erledigst du die Reparatur dort sofort allein, ohne das Minispiel.",
        "You are crew. Stand at the console of the current sabotage (reactor, oxygen, communications or lights) - the button instantly completes the repair there by yourself, without the minigame.");

    private RoleOption cooldown;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 30f, 10f, 90f, 2.5f, "s");

        AddButton(new RoleButton
        {
            Label = "Löschen|Extinguish",
            Icon = "extinguish",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 15f,
            Hotkey = KeyCode.F,
            CanUse = () => SabotageActive() && NearConsole(),
            OnClick = _ => FixSabotages(),
        });
    }

    /// <summary>Steht der Feuerwehrmann an einer Sabotage-Konsole (Reaktor, O2, Komm, Licht)?</summary>
    private static bool NearConsole()
    {
        var ship = ShipStatus.Instance;
        var local = PlayerControl.LocalPlayer;
        if (!ship || !local) return false;
        Vector2 me = local.GetTruePosition();
        foreach (var con in ship.GetComponentsInChildren<Console>(true))
        {
            if (!con) continue;
            string n = con.name.ToLower();
            bool relevant = n.Contains("reactor") || n.Contains("nuclear") || n.Contains("seismic")
                || n.Contains("oxygen") || n.Contains("o2") || n.Contains("comm")
                || n.Contains("switch") || n.Contains("light") || n.Contains("elec");
            if (relevant && Vector2.Distance(me, con.transform.position) <= 3f) return true;
        }
        return false;
    }

    private static bool SabotageActive()
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

    private static void FixSabotages()
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
        // Kommunikation
        Repair(ship, SystemTypes.Comms, 0);
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
