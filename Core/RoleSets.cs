using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Configuration;

namespace Spieleabend.Core;

/// <summary>
/// Frei benennbare Rollen-Sets (Profile). Ein Set ist eine gespeicherte Rollen-Konfiguration (je Rolle Anzahl + Chance).
/// „Aktivieren" wendet es an: die enthaltenen Rollen bekommen ihre gespeicherte Anzahl/Chance, alle anderen Mod-Rollen
/// werden ausgeschaltet (Anzahl 0). So kann man vorne einfach „Halloween-Set" wählen. Gespeichert in der Host-Config.
/// </summary>
internal static class RoleSets
{
    public sealed class Set
    {
        public string Name;
        public readonly Dictionary<string, (int Count, int Chance)> Roles = new();
    }

    public static readonly List<Set> Sets = new();
    private static ConfigEntry<string> store;

    // ---------- Eingebaute Vorlagen (immer da, nicht löschbar) ----------
    /// <summary>Ein vorgefertigtes Set: Klassenname der Rolle -> Chance (Anzahl immer 1). Enthält nur getestete Rollen (online sicher).</summary>
    public sealed class BuiltIn
    {
        public readonly string NameDe, NameEn;
        public readonly Dictionary<string, int> Roles; // GetType().Name -> Chance
        public readonly bool HalloweenTheme;
        public BuiltIn(string de, string en, bool halloween, Dictionary<string, int> roles) { NameDe = de; NameEn = en; HalloweenTheme = halloween; Roles = roles; }
        public string Name => Lang.T(NameDe, NameEn);
    }

    public static readonly List<BuiltIn> BuiltIns = new()
    {
        // Ausgewogen, online erprobt - ein paar beliebte Rollen mit moderaten Chancen.
        new BuiltIn("Standard", "Standard", false, new()
        {
            ["Sheriff"] = 60, ["NiceGuesser"] = 50, ["Medic"] = 50, ["Seer"] = 40,
            ["Swapper"] = 40, ["Jester"] = 40, ["Executioner"] = 30,
            ["Vampire"] = 50, ["Witch"] = 40, ["Arsonist"] = 25,
        }),
        // Viele Crew-Hilfsrollen, keine Mod-Impostor-Rollen (die Impostor-Zahl stellst du im Reiter Spiel ein).
        new BuiltIn("Starke Crew", "Strong Crew", false, new()
        {
            ["Sheriff"] = 70, ["Medic"] = 60, ["Seer"] = 60, ["NiceGuesser"] = 60,
            ["Radar"] = 50, ["FrostDetector"] = 50, ["Administrator"] = 50, ["Cameraman"] = 50,
            ["ShieldTech"] = 50, ["Firefighter"] = 40, ["Teleporter"] = 40, ["BountyHunter"] = 40,
        }),
        // Gruselige Rollen + Halloween-Design an.
        new BuiltIn("Halloween", "Halloween", true, new()
        {
            ["Vampire"] = 60, ["Witch"] = 50, ["Zombie"] = 40, ["PumpkinHead"] = 40,
            ["PumpkinBomber"] = 40, ["CandyKid"] = 50, ["Arsonist"] = 40, ["Scarecrow"] = 30,
            ["Werewolf"] = 30, ["BlackCat"] = 30, ["Jester"] = 40,
        }),
    };

    /// <summary>Ein eingebautes Set anwenden: enthaltene Rollen auf Anzahl 1 + Chance, alle anderen aus.</summary>
    public static void ApplyBuiltIn(BuiltIn preset)
    {
        if (!SettingsWindow.CanEdit || preset == null) return;
        foreach (var r in RoleRegistry.Roles)
        {
            if (preset.Roles.TryGetValue(r.GetType().Name, out int chance))
            {
                r.Count.SetValue(1);
                r.Chance.SetValue(chance);
            }
            else r.Count.SetValue(0);
        }
        if (preset.HalloweenTheme) GeneralOptions.Halloween.SetValue(1);
        RoleRpc.MarkOptionsDirty();
        ChatNote.Message(Lang.T($"Set aktiviert: {preset.Name}", $"Set activated: {preset.Name}"));
    }

    public static void Bind(ConfigFile cfg)
    {
        store = cfg.Bind("Sets", "Data", "", "Gespeicherte Rollen-Sets (nicht von Hand bearbeiten).");
        Parse();
    }

    private static void Parse()
    {
        Sets.Clear();
        if (store == null) return;
        foreach (var line in store.Value.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            int tab = line.IndexOf('\t');
            if (tab < 0) continue;
            var set = new Set { Name = line.Substring(0, tab) };
            foreach (var part in line.Substring(tab + 1).Split(';'))
            {
                var p = part.Split('=');
                if (p.Length == 3 && int.TryParse(p[1], out int c) && int.TryParse(p[2], out int ch))
                    set.Roles[p[0]] = (c, ch);
            }
            Sets.Add(set);
        }
    }

    private static void Save()
    {
        if (store == null) return;
        var sb = new StringBuilder();
        foreach (var s in Sets)
        {
            sb.Append(s.Name).Append('\t');
            sb.Append(string.Join(";", s.Roles.Select(kv => $"{kv.Key}={kv.Value.Count}={kv.Value.Chance}")));
            sb.Append('\n');
        }
        store.Value = sb.ToString();
    }

    public static Set Find(string name) => Sets.FirstOrDefault(s => s.Name == name);

    /// <summary>Aktuelle Rollen-Konfiguration als Set speichern (überschreibt ein gleichnamiges).</summary>
    public static void SaveCurrent(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) return;
        var set = Find(name);
        if (set == null) { set = new Set { Name = name }; Sets.Add(set); }
        else set.Roles.Clear();
        foreach (var r in RoleRegistry.Roles)
            set.Roles[r.ConfigSection] = (r.Count.Int, r.Chance.Int);
        Save();
    }

    /// <summary>Set anwenden: enthaltene Rollen auf gespeicherte Anzahl/Chance, alle anderen Mod-Rollen auf 0.</summary>
    public static void Apply(string name)
    {
        if (!SettingsWindow.CanEdit) return;
        var set = Find(name);
        if (set == null) return;
        foreach (var r in RoleRegistry.Roles)
        {
            if (set.Roles.TryGetValue(r.ConfigSection, out var v))
            {
                r.Count.SetValue(v.Count);
                r.Chance.SetValue(v.Chance);
            }
            else r.Count.SetValue(0);
        }
        Notifier.Show(Lang.T($"Set aktiviert: {set.Name}", $"Set activated: {set.Name}"), 3f);
    }

    public static void Delete(string name)
    {
        var s = Find(name);
        if (s != null) { Sets.Remove(s); Save(); }
    }

    public static void Rename(string oldName, string newName)
    {
        newName = (newName ?? "").Trim();
        var s = Find(oldName);
        if (s != null && newName.Length > 0 && Find(newName) == null) { s.Name = newName; Save(); }
    }
}
