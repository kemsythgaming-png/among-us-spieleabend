using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>Alles, was im F1-Fenster als Zeile mit -/+ oder An/Aus angezeigt wird.</summary>
public interface ISetting
{
    string Name { get; }
    string Display { get; }
    bool IsToggle { get; }
    bool Bool { get; }
    void Change(int direction);

    /// <summary>true = jeder darf aendern (z. B. Sprache), nicht nur der Host.</summary>
    bool AlwaysEditable => false;
}

/// <summary>
/// Eine Einstellung aus dem normalen Spiel (Kill-Abklingzeit, Spiel-Rollen ...), bearbeitbar im F1-Fenster.
/// Wird direkt in den Spiel-Optionen gesetzt; das Spiel schickt sie selbst an alle (SyncOptions).
/// </summary>
internal sealed class VanillaSetting : ISetting
{
    private enum Kind { Float, Int, Bool, RoleCount, RoleChance }

    private readonly Kind kind;
    private readonly FloatOptionNames floatName;
    private readonly Int32OptionNames intName;
    private readonly BoolOptionNames boolName;
    private readonly RoleTypes role;
    private readonly float min, max, step;
    private readonly string suffix;
    private readonly string[] labels;

    private readonly string rawName;
    public string Name => Lang.Pick(rawName);
    public bool IsToggle => kind == Kind.Bool;

    private VanillaSetting(Kind kind, string name, float min, float max, float step, string suffix, string[] labels = null,
        FloatOptionNames f = default, Int32OptionNames i = default, BoolOptionNames b = default, RoleTypes role = default)
    {
        this.kind = kind;
        rawName = name;
        this.min = min;
        this.max = max;
        this.step = step;
        this.suffix = suffix;
        this.labels = labels;
        floatName = f;
        intName = i;
        boolName = b;
        this.role = role;
    }

    public static VanillaSetting Float(FloatOptionNames n, string name, float min, float max, float step, string suffix = "") =>
        new(Kind.Float, name, min, max, step, suffix, f: n);

    public static VanillaSetting Int(Int32OptionNames n, string name, int min, int max, int step = 1, string suffix = "", string[] labels = null) =>
        new(Kind.Int, name, min, max, step, suffix, labels, i: n);

    public static VanillaSetting Toggle(BoolOptionNames n, string name) => new(Kind.Bool, name, 0, 1, 1, "", b: n);

    public static VanillaSetting RoleCount(RoleTypes r) => new(Kind.RoleCount, "Anzahl|Count", 0, 15, 1, "", role: r);

    public static VanillaSetting RoleChance(RoleTypes r) => new(Kind.RoleChance, "Wahrscheinlichkeit|Chance", 0, 100, 10, "%", role: r);

    private static IGameOptions Options => GameOptionsManager.Instance?.CurrentGameOptions;

    /// <summary>Aktueller Wert, oder null wenn das Spiel diese Einstellung gerade nicht kennt.</summary>
    public float? Value
    {
        get
        {
            var o = Options;
            if (o == null) return null;
            try
            {
                switch (kind)
                {
                    case Kind.Float: return o.TryGetFloat(floatName, out float f) ? f : null;
                    case Kind.Int: return o.TryGetInt(intName, out int i) ? i : null;
                    case Kind.Bool: return o.TryGetBool(boolName, out bool b) ? (b ? 1f : 0f) : null;
                    case Kind.RoleCount: return o.RoleOptions.GetNumPerGame(role);
                    case Kind.RoleChance: return o.RoleOptions.GetChancePerGame(role);
                }
            }
            catch { }
            return null;
        }
    }

    public bool Bool => (Value ?? 0f) >= 0.5f;

    public string Display
    {
        get
        {
            var v = Value;
            if (v == null) return "-";
            if (IsToggle) return v.Value >= 0.5f ? Lang.T("An", "On") : Lang.T("Aus", "Off");
            if (labels != null) return Lang.Pick(labels[Mathf.Clamp(Mathf.RoundToInt(v.Value), 0, labels.Length - 1)]);
            return $"{v.Value:0.##}{suffix}";
        }
    }

    public void Change(int direction)
    {
        if (!SettingsWindow.CanEdit) return;
        var current = Value;
        if (current == null) return;

        float next = IsToggle ? (current.Value >= 0.5f ? 0f : 1f) : current.Value + direction * step;
        next = Mathf.Clamp(Mathf.Round(next / step) * step, min, max);

        var o = Options;
        try
        {
            switch (kind)
            {
                case Kind.Float: o.SetFloat(floatName, next); break;
                case Kind.Int: o.SetInt(intName, Mathf.RoundToInt(next)); break;
                case Kind.Bool: o.SetBool(boolName, next >= 0.5f); break;
                case Kind.RoleCount:
                    o.RoleOptions.SetRoleRate(role, Mathf.RoundToInt(next), o.RoleOptions.GetChancePerGame(role));
                    break;
                case Kind.RoleChance:
                    o.RoleOptions.SetRoleRate(role, o.RoleOptions.GetNumPerGame(role), Mathf.RoundToInt(next));
                    break;
            }
            Sync();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"Spiel-Einstellung '{Name}' konnte nicht gesetzt werden: {e.Message}");
        }
    }

    /// <summary>Geaenderte Spiel-Optionen an alle schicken (wie das Original-Menue).</summary>
    private static void Sync()
    {
        try
        {
            if (GameManager.Instance && GameManager.Instance.LogicOptions != null) GameManager.Instance.LogicOptions.SyncOptions();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"Spiel-Einstellungen konnten nicht synchronisiert werden: {e.Message}");
        }
    }
}

/// <summary>Eine Rolle aus dem Spiel (Engineer, Shapeshifter ...) mit Anzahl, Chance und ihren Einstellungen.</summary>
internal sealed class VanillaRoleEntry
{
    public string Name { get; }
    public Color Color { get; }
    private readonly string rawDescription;
    public string Description => Lang.Pick(rawDescription);
    public RoleTypes Role { get; }
    public List<ISetting> Settings { get; } = new();

    private VanillaRoleEntry(RoleTypes role, string name, bool impostor, string description, params ISetting[] extra)
    {
        Role = role;
        Name = name;
        Color = impostor ? new Color(1f, 0.1f, 0.1f) : new Color(0.55f, 1f, 1f);
        rawDescription = description;
        Settings.Add(VanillaSetting.RoleCount(role));
        Settings.Add(VanillaSetting.RoleChance(role));
        Settings.AddRange(extra);
    }

    public string Summary
    {
        get
        {
            var count = Settings[0] as VanillaSetting;
            var chance = Settings[1] as VanillaSetting;
            return $"{count?.Display}x, {chance?.Display}";
        }
    }

    public static readonly List<VanillaRoleEntry> All = new()
    {
        new(RoleTypes.Engineer, "Engineer", false, "Crew: darf die Vents benutzen.|Crew: can use the vents.",
            VanillaSetting.Float(FloatOptionNames.EngineerCooldown, "Vent-Abklingzeit|Vent cooldown", 0, 60, 2.5f, "s"),
            VanillaSetting.Float(FloatOptionNames.EngineerInVentMaxTime, "Max. Zeit im Vent|Max. time in vent", 0, 60, 2.5f, "s")),
        new(RoleTypes.Scientist, "Scientist", false, "Crew: kann jederzeit die Vitalwerte (Vitals) ansehen.|Crew: can check vitals anywhere.",
            VanillaSetting.Float(FloatOptionNames.ScientistCooldown, "Abklingzeit|Cooldown", 0, 60, 2.5f, "s"),
            VanillaSetting.Float(FloatOptionNames.ScientistBatteryCharge, "Akku-Dauer|Battery duration", 0, 60, 2.5f, "s")),
        new(RoleTypes.Noisemaker, "Noisemaker", false, "Crew: wenn er stirbt, gibt es einen Alarm mit Richtungspfeil.|Crew: when killed, an alarm with an arrow goes off.",
            VanillaSetting.Float(FloatOptionNames.NoisemakerAlertDuration, "Alarm-Dauer|Alert duration", 1, 20, 1, "s"),
            VanillaSetting.Toggle(BoolOptionNames.NoisemakerImpostorAlert, "Impostor hören den Alarm|Impostors get the alert")),
        new(RoleTypes.Tracker, "Tracker", false, "Crew: verfolgt einen Spieler auf der Karte.|Crew: tracks a player on the map.",
            VanillaSetting.Float(FloatOptionNames.TrackerCooldown, "Abklingzeit|Cooldown", 0, 60, 2.5f, "s"),
            VanillaSetting.Float(FloatOptionNames.TrackerDuration, "Dauer|Duration", 0, 60, 2.5f, "s"),
            VanillaSetting.Float(FloatOptionNames.TrackerDelay, "Verzögerung|Delay", 0, 10, 0.5f, "s")),
        new(RoleTypes.Detective, "Detective", false, "Crew: befragt Spieler und sammelt Hinweise auf den Täter.|Crew: questions players and collects clues.",
            VanillaSetting.Float(FloatOptionNames.DetectiveSuspectLimit, "Verdächtige|Suspects", 1, 10, 1)),
        new(RoleTypes.Judge, "Judge", false, "Crew: kann nach erledigten Aufgaben eine Abstimmung überstimmen.|Crew: can overrule a vote after finishing tasks."),
        new(RoleTypes.GuardianAngel, "Guardian Angel", false, "Toter Crewmate: kann lebende Spieler kurz beschützen.|Dead crewmate: can briefly protect living players.",
            VanillaSetting.Float(FloatOptionNames.GuardianAngelCooldown, "Abklingzeit|Cooldown", 0, 120, 2.5f, "s"),
            VanillaSetting.Float(FloatOptionNames.ProtectionDurationSeconds, "Schutzdauer|Protection duration", 0, 60, 2.5f, "s"),
            VanillaSetting.Toggle(BoolOptionNames.ImpostorsCanSeeProtect, "Impostor sehen den Schutz|Impostors see the protection")),
        new(RoleTypes.SpiritGuide, "Spirit Guide", false, "Toter Crewmate: schickt Bild-Hinweise an Lebende.|Dead crewmate: sends picture hints to the living.",
            VanillaSetting.Float(FloatOptionNames.SpiritGuideCooldownSeconds, "Abklingzeit|Cooldown", 0, 120, 5, "s")),
        new(RoleTypes.Shapeshifter, "Shapeshifter", true, "Impostor: verwandelt sich in andere Spieler.|Impostor: transforms into other players.",
            VanillaSetting.Float(FloatOptionNames.ShapeshifterCooldown, "Abklingzeit|Cooldown", 0, 60, 2.5f, "s"),
            VanillaSetting.Float(FloatOptionNames.ShapeshifterDuration, "Dauer|Duration", 0, 60, 2.5f, "s"),
            VanillaSetting.Toggle(BoolOptionNames.ShapeshifterLeaveSkin, "Hinterlässt Spuren|Leaves evidence")),
        new(RoleTypes.Phantom, "Phantom", true, "Impostor: kann kurz unsichtbar werden.|Impostor: can turn invisible for a while.",
            VanillaSetting.Float(FloatOptionNames.PhantomCooldown, "Abklingzeit|Cooldown", 0, 60, 2.5f, "s"),
            VanillaSetting.Float(FloatOptionNames.PhantomDuration, "Dauer|Duration", 0, 60, 2.5f, "s")),
        new(RoleTypes.Viper, "Viper", true, "Impostor: die Leichen ihrer Opfer lösen sich auf.|Impostor: her victims' bodies dissolve.",
            VanillaSetting.Float(FloatOptionNames.ViperDissolveTime, "Auflöse-Zeit|Dissolve time", 0, 60, 2.5f, "s")),
    };
}

/// <summary>Die normalen Spiel-Einstellungen (Reiter "Spiel").</summary>
internal static class VanillaGameSettings
{
    public static readonly List<ISetting> All = new()
    {
        VanillaSetting.Int(Int32OptionNames.NumImpostors, "Impostor|Impostors", 1, 3),
        VanillaSetting.Float(FloatOptionNames.KillCooldown, "Kill-Abklingzeit|Kill cooldown", 10, 60, 2.5f, "s"),
        VanillaSetting.Int(Int32OptionNames.KillDistance, "Kill-Reichweite|Kill distance", 0, 2, 1, "", new[] { "Kurz|Short", "Mittel|Medium", "Lang|Long" }),
        VanillaSetting.Float(FloatOptionNames.PlayerSpeedMod, "Tempo|Speed", 0.5f, 3f, 0.25f, "x"),
        VanillaSetting.Float(FloatOptionNames.CrewLightMod, "Sicht Crew|Crew vision", 0.25f, 5f, 0.25f, "x"),
        VanillaSetting.Float(FloatOptionNames.ImpostorLightMod, "Sicht Impostor|Impostor vision", 0.25f, 5f, 0.25f, "x"),
        VanillaSetting.Int(Int32OptionNames.NumEmergencyMeetings, "Notfall-Meetings|Emergency meetings", 0, 9),
        VanillaSetting.Int(Int32OptionNames.EmergencyCooldown, "Notfall-Abklingzeit|Emergency cooldown", 0, 60, 5, "s"),
        VanillaSetting.Int(Int32OptionNames.DiscussionTime, "Diskussionszeit|Discussion time", 0, 120, 15, "s"),
        VanillaSetting.Int(Int32OptionNames.VotingTime, "Abstimmzeit (0 = ewig)|Voting time (0 = endless)", 0, 300, 15, "s"),
        VanillaSetting.Toggle(BoolOptionNames.ConfirmImpostor, "Rauswurf bestätigen|Confirm ejects"),
        VanillaSetting.Toggle(BoolOptionNames.AnonymousVotes, "Anonyme Stimmen|Anonymous votes"),
        VanillaSetting.Int(Int32OptionNames.NumCommonTasks, "Gemeinsame Aufgaben|Common tasks", 0, 4),
        VanillaSetting.Int(Int32OptionNames.NumShortTasks, "Kurze Aufgaben|Short tasks", 0, 5),
        VanillaSetting.Int(Int32OptionNames.NumLongTasks, "Lange Aufgaben|Long tasks", 0, 3),
        VanillaSetting.Int(Int32OptionNames.TaskBarMode, "Aufgabenbalken|Task bar", 0, 2, 1, "", new[] { "Immer|Always", "Bei Meetings|Meetings", "Nie|Never" }),
        VanillaSetting.Toggle(BoolOptionNames.VisualTasks, "Sichtbare Aufgaben|Visual tasks"),
        VanillaSetting.Toggle(BoolOptionNames.GhostsDoTasks, "Geister machen Aufgaben|Ghosts do tasks"),
    };
}
