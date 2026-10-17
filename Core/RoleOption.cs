using BepInEx.Configuration;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Eine einstellbare Zahl oder ein Schalter einer Rolle (wie die Rollen-Einstellungen im echten Spiel).
/// Der Host bearbeitet sie im Einstellungsfenster (F1); gespeichert wird in seiner BepInEx-Config.
/// Alle anderen Spieler bekommen den Wert vom Host geschickt.
/// </summary>
public class RoleOption : ISetting
{
    public string Key { get; }
    private readonly string rawName;

    /// <summary>Anzeigename in der aktuellen Sprache (aus "Deutsch|English").</summary>
    public string Name => Lang.Pick(rawName);
    public float Min { get; }
    public float Max { get; }
    public float Step { get; }
    public string Suffix { get; }
    public bool IsToggle { get; }

    private readonly ConfigEntry<float> entry;

    /// <summary>Vom Host empfangener Wert (nur bei Nicht-Hosts gesetzt).</summary>
    internal float? Synced;

    internal RoleOption(ConfigFile config, string section, string key, string name, float def, float min, float max, float step, string suffix, bool isToggle)
    {
        Key = key;
        rawName = name;
        Min = min;
        Max = max;
        Step = step;
        Suffix = suffix;
        IsToggle = isToggle;
        entry = config.Bind(section, key, def, $"{Lang.Pick(name)} ({min}-{max})");
    }

    public float Value
    {
        get
        {
            bool isClient = AmongUsClient.Instance && AmongUsClient.Instance.AmConnected && !AmongUsClient.Instance.AmHost;
            return isClient && Synced.HasValue ? Synced.Value : Mathf.Clamp(entry.Value, Min, Max);
        }
    }

    public int Int => Mathf.RoundToInt(Value);
    public bool Bool => Value >= 0.5f;

    /// <summary>Optional: Namen statt Zahlen ("Deutsch|English" je Wert ab Min), z. B. beim Design.</summary>
    public string[] ValueNames;

    public string Display => IsToggle ? (Bool ? Lang.T("An", "On") : Lang.T("Aus", "Off"))
        : ValueNames != null && Int >= 0 && Int < ValueNames.Length ? Lang.Pick(ValueNames[Int])
        : $"{Value:0.##}{Suffix}";

    /// <summary>Nur Host: Wert um einen Schritt erhoehen/verringern.</summary>
    public void Change(int direction)
    {
        if (!SettingsWindow.CanEdit) return; // nur Lobby (Host) oder Freeplay

        float next = IsToggle ? (Bool ? 0f : 1f) : Value + direction * Step;
        next = Mathf.Clamp(Mathf.Round(next / Step) * Step, Min, Max);
        if (Mathf.Approximately(next, entry.Value)) return;
        entry.Value = next;
        RoleRpc.MarkOptionsDirty();
    }

    /// <summary>Nur Host/Freeplay: Wert direkt setzen (fuer Sammel-Aktionen, z. B. "alle Chancen auf X").</summary>
    public void SetValue(float value)
    {
        if (!SettingsWindow.CanEdit) return;
        if (IsToggle) value = value >= 0.5f ? 1f : 0f;
        float next = Mathf.Clamp(Mathf.Round(value / Step) * Step, Min, Max);
        if (Mathf.Approximately(next, entry.Value)) return;
        entry.Value = next;
        RoleRpc.MarkOptionsDirty();
    }
}
