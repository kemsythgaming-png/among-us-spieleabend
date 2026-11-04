using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Test-Umschalter: teilt die Mod-Rollen in "schon getestet" und "noch nicht getestet" (Klassenname). Der Knopf im
/// Reiter Sets schaltet mit EINEM Klick entweder alle ungetesteten oder alle getesteten Rollen ein (Anzahl 1,
/// Chance 100 %) und den Rest aus - so kann man gezielt die neuen Rollen durchtesten.
/// Neue Rolle gebaut -> ihren Klassennamen hier in Untested eintragen; nach dem Testen wieder herausnehmen.
/// </summary>
internal static class TestStatus
{
    /// <summary>
    /// Rollen/Modifier, die wir gemeinsam ausdrücklich als getestet abgehakt haben (Klassenname).
    /// ALLES andere gilt als ungetestet. Nach dem Bestätigen einer Rolle hier den Klassennamen eintragen.
    /// </summary>
    private static readonly HashSet<string> Tested = new()
    {
        "Administrator", "Cameraman", "Firefighter", "Gambler", "CandyKid",
        "Teleporter", "ShieldTech", "Sheriff", "Radar",
        "Swapper", "Paranoid", "VoteSwapper", "Seer", "FrostDetector", "Healer",
        "PumpkinHead", "Medic",
        "NiceGuesser", "Arsonist", "Jester", "Executioner",
        "Camouflager", "Poltergeist", "PumpkinBomber", "Vampire", "Witch", "SuicideBomber",
        "FastImpostor",
        "Assassin", "BlackCat", "LeafSpreader", "RoleDisabler", "Glutton",
        "CrimeCleaner", "Scarecrow", "BountyHunter", "Clown", "ThorRole",
        "Boo", "EvilGuesser", "Wizard", "Werewolf", "Sniper",
        "BodyDragger", "FalseSanta", "GuardianSpirit", "WailingSpirit",
        "Haunting", "Zombie", "Mummy", "Oracle",
    };

    /// <summary>Anpassungen, die trotz der Sammelfreigabe noch NICHT getestet sind (z. B. gemeldeter Bug).</summary>
    private static readonly HashSet<string> UntestedModifiers = new()
    {
        "Lover",
        // Noch offen (gemeldet + gefixt, aber noch nicht erneut getestet):
        "Bait",          // Köder
    };

    // Einmaliger Standard: beim ersten Mal nur die GETESTETEN Rollen anschalten.
    private static ConfigEntry<bool> defaultApplied;

    public static void Bind(ConfigFile config)
    {
        defaultApplied = config.Bind("Allgemein", "StandardNurGetestet", false,
            "Intern: wurde der Standard \"nur getestete Rollen an\" schon einmal angewandt?");
    }

    /// <summary>Beim ersten Mal (Host in der Lobby): nur getestete Rollen anschalten - danach nie wieder von selbst.</summary>
    public static void ApplyDefaultOnce()
    {
        if (defaultApplied == null || defaultApplied.Value || !SettingsWindow.CanEdit) return;
        defaultApplied.Value = true;
        Apply(true); // nur getestete an
    }

    public static bool IsTested(CustomRole r)
    {
        if (r == null) return false;
        // Alle Anpassungen (Modifier) gelten als getestet - außer den noch offenen (UntestedModifiers).
        if (r.Team == RoleTeam.Modifier) return !UntestedModifiers.Contains(r.GetType().Name);
        return Tested.Contains(r.GetType().Name);
    }

    public static int UntestedCount
    {
        get { int n = 0; foreach (var r in RoleRegistry.Roles) if (!IsTested(r)) n++; return n; }
    }

    /// <summary>Alle Rollen der gewaehlten Gruppe einschalten (Anzahl 1, Chance 100 %), den Rest aus.</summary>
    public static void Apply(bool testedGroup)
    {
        if (!SettingsWindow.CanEdit) return;
        foreach (var r in RoleRegistry.Roles)
        {
            bool on = IsTested(r) == testedGroup;
            r.Count.SetValue(on ? Mathf.Max(1, r.Count.Int) : 0);
            if (on) r.Chance.SetValue(100);
        }
        ChatNote.Message(testedGroup
            ? Lang.T("Test-Umschalter: getestete Rollen an", "Test toggle: tested roles on")
            : Lang.T("Test-Umschalter: ungetestete Rollen an", "Test toggle: untested roles on"));
    }
}
