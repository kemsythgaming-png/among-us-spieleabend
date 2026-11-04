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
