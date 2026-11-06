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
        "VengefulSpirit",
    };

    /// <summary>Anpassungen, die trotz der Sammelfreigabe noch NICHT getestet sind (z. B. gemeldeter Bug).</summary>
    private static readonly HashSet<string> UntestedModifiers = new()
    {
        "Lover",
    };

    // An/Aus-Schalter: dürfen ungetestete Rollen benutzt/eingestellt werden? Standard: AUS.
    private static ConfigEntry<bool> allowUntested;

    public static void Bind(ConfigFile config)
    {
        allowUntested = config.Bind("Allgemein", "UngetesteteRollen", false,
            "Ungetestete Rollen zulassen (An) oder sperren und ausschalten (Aus). Standard: Aus.");
    }

    /// <summary>Dürfen ungetestete Rollen gerade benutzt/eingestellt werden?</summary>
    public static bool AllowUntested => allowUntested != null && allowUntested.Value;

    /// <summary>An/Aus umschalten. Beim Ausschalten werden alle ungetesteten Rollen abgeschaltet.</summary>
    public static void SetAllowUntested(bool on)
    {
        if (!SettingsWindow.CanEdit || allowUntested == null) return;
        allowUntested.Value = on;
        if (!on) DisableUntested();
        RoleRpc.MarkOptionsDirty();
        ChatNote.Message(on
            ? Lang.T("Ungetestete Rollen: AN", "Untested roles: ON")
            : Lang.T("Ungetestete Rollen: AUS", "Untested roles: OFF"));
    }

    /// <summary>Alle ungetesteten Rollen ausschalten (Anzahl 0).</summary>
    private static void DisableUntested()
    {
        foreach (var r in RoleRegistry.Roles)
            if (!IsTested(r)) r.Count.SetValue(0);
    }

    /// <summary>Host in der Lobby: solange "ungetestet" AUS ist, deren Anzahl auf 0 halten (auch nach Sets/Sammelaktionen).</summary>
    public static void EnforceUntestedOff()
    {
        if (AllowUntested || !SettingsWindow.CanEdit) return;
        foreach (var r in RoleRegistry.Roles)
            if (!IsTested(r) && r.Count.Int > 0) r.Count.SetValue(0);
    }

    /// <summary>UI: ist diese Rolle gerade gesperrt (ungetestet + Schalter AUS)?</summary>
    public static bool IsLocked(CustomRole r) => !AllowUntested && !IsTested(r);

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
}
