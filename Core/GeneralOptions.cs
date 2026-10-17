using System.Collections.Generic;
using BepInEx.Configuration;

namespace Spieleabend.Core;

/// <summary>Allgemeine Spielregeln des Mods (Reiter "Allgemein" im F1-Fenster). Werden wie Rollenoptionen synchronisiert.</summary>
public static class GeneralOptions
{
    public const string Section = "Allgemein";

    public static readonly List<RoleOption> Options = new();

    /// <summary>Wahrscheinlichkeit, dass in einer Runde ueberhaupt Mod-Rollen verteilt werden.</summary>
    public static RoleOption ModRolesChance { get; private set; }

    public static RoleOption DeadSeeRoles { get; private set; }
    public static RoleOption ShowOwnRole { get; private set; }

    /// <summary>Hoechstzahl je Team (0 = keine Grenze).</summary>
    public static RoleOption MaxCrewRoles { get; private set; }
    public static RoleOption MaxImpostorRoles { get; private set; }
    public static RoleOption MaxNeutralRoles { get; private set; }
    public static RoleOption MaxModifiers { get; private set; }

    /// <summary>Design fuer Map und Lobby: 0 = Standard, 1 = Halloween (Kuerbisse, Geister, Farbstimmung). Weitere Designs bekommen die naechsten Nummern.</summary>
    public static RoleOption Halloween { get; private set; }

    /// <summary>Wer letzte Runde zuerst getoetet wurde, ist in der naechsten Runde bis zum ersten Meeting geschuetzt.</summary>
    public static RoleOption FirstDeathShield { get; private set; }

    /// <summary>Karte durchziehen: nach so vielen Fehlversuchen gilt die Aufgabe als geschafft (0 = aus). Steht im Reiter "Spiel".</summary>
    public static RoleOption CardSwipeHelp { get; private set; }

    /// <summary>Mod-Einstellungen, die im Reiter "Spiel" (statt "Allgemein") stehen. Werden trotzdem mit allen synchronisiert.</summary>
    internal static readonly System.Collections.Generic.List<RoleOption> GameTab = new();

    /// <summary>Chance, dass ein normaler Crewmate nach allen Aufgaben zum Sheriff wird (0 = aus).</summary>
    public static RoleOption TaskSheriffChance { get; private set; }

    internal static void Init(ConfigFile config)
    {
        ModRolesChance = Add(config, "ModRolesChance", "Chance auf Mod-Rollen pro Runde|Chance of mod roles per round", 100, 0, 100, 10, "%");
        DeadSeeRoles = Add(config, "DeadSeeRoles", "Tote sehen alle Rollen|Dead players see all roles", 1, 0, 1, 1, "", true);
        ShowOwnRole = Add(config, "ShowOwnRole", "Eigene Rolle über dem Namen|Show own role above name", 1, 0, 1, 1, "", true);
        MaxCrewRoles = Add(config, "MaxCrewRoles", "Max. Crew-Rollen (0 = egal)|Max. crew roles (0 = any)", 0, 0, 15, 1);
        MaxImpostorRoles = Add(config, "MaxImpostorRoles", "Max. Impostor-Rollen (0 = egal)|Max. impostor roles (0 = any)", 0, 0, 3, 1);
        MaxNeutralRoles = Add(config, "MaxNeutralRoles", "Max. Neutrale (0 = egal)|Max. neutral roles (0 = any)", 0, 0, 15, 1);
        MaxModifiers = Add(config, "MaxModifiers", "Max. Anpassungen (0 = egal)|Max. modifiers (0 = any)", 0, 0, 15, 1);
        Halloween = Add(config, "Halloween", "Design|Theme", 0, 0, 1, 1); // Schluessel bleibt "Halloween", damit alte Einstellungen gelten
        Halloween.ValueNames = new[] { "Standard|Default", "Halloween|Halloween" };
        FirstDeathShield = Add(config, "FirstDeathShield", "Schutz für die ersten Toten|Shield for first deaths", 0, 0, 1, 1, "", true);
        TaskSheriffChance = Add(config, "TaskSheriffChance", "Sheriff-Chance nach allen Aufgaben|Sheriff chance after all tasks", 0, 0, 100, 10, "%");
        // Neue Optionen immer hinten anhaengen (Reihenfolge = Reihenfolge beim Synchronisieren)
        CardSwipeHelp = Add(config, "CardSwipeHelp", "Karte gilt nach X Fehlversuchen (0 = aus)|Card counts after X failed swipes (0 = off)", 25, 0, 50, 5);
        GameTab.Add(CardSwipeHelp);
    }

    public static int MaxFor(RoleTeam team)
    {
        int max = team switch
        {
            RoleTeam.Crewmate => MaxCrewRoles.Int,
            RoleTeam.Impostor => MaxImpostorRoles.Int,
            RoleTeam.Neutral => MaxNeutralRoles.Int,
            _ => MaxModifiers.Int,
        };
        return max <= 0 ? int.MaxValue : max;
    }

    private static RoleOption Add(ConfigFile config, string key, string name, float def, float min, float max, float step, string suffix = "", bool toggle = false)
    {
        var option = new RoleOption(config, Section, key, name, def, min, max, step, suffix, toggle);
        Options.Add(option);
        return option;
    }
}
