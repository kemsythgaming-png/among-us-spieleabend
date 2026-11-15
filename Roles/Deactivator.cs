using System.Collections.Generic;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor (Deaktivator): klickt auf der Karte einen Raum an und sperrt dort eine Weile die normalen Aufgaben-Konsolen
/// (Standard 60 s). Sabotagen, Lichter/Komm. und Vents bleiben nutzbar - es werden NUR die Aufgaben blockiert (über
/// einen Patch auf Console.CanUse, es wird nichts aus der Welt entfernt).
/// </summary>
public sealed class Deactivator : CustomRole
{
    public override string ConfigSection => "Deaktivator";
    public override string Name => Lang.T("Deaktivator", "Deactivator");
    public override Color Color => new Color32(200, 150, 50, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Raum aus", "Room off");
    public override string Description => Lang.T("Sperre die Aufgaben eines\nRaums per Karte (F).", "Jam a room's tasks\nvia the map (F).");
    public override string LongDescription => Lang.T(
        "Du bist Impostor. Mit Taste F geht die Karte auf - klick einen Raum an. Dort kann die Crew für eine Weile (Standard 60 s) die normalen Aufgaben nicht erledigen. Sabotagen, Lichter/Kommunikation und Vents bleiben nutzbar - es werden nur die Aufgaben gesperrt. Deinen normalen Kill hast du zusätzlich.",
        "You are impostor. Press F to open the map - click a room. There the crew can't do normal tasks for a while (default 60 s). Sabotages, lights/comms and vents stay usable - only tasks are jammed. You also keep your normal kill.");

    private const byte ActionDisable = 0;

    private RoleOption cooldown, duration;
    private readonly MapPicker picker = new();
    private SystemTypes lastRoom = SystemTypes.Hallway; // zuletzt gesperrter Raum (nicht zweimal hintereinander erlaubt)
    private bool frozen;
    private RoleButton button;

    /// <summary>Raum -> bis wann gesperrt (auf allen Clients gespiegelt; der CanUse-Patch liest das).</summary>
    internal static readonly Dictionary<SystemTypes, float> Jammed = new();

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 40f, 15f, 120f, 5f, "s");
        duration = AddOption("Duration", "Dauer|Duration", 60f, 15f, 120f, 5f, "s");

        button = AddButton(new RoleButton
        {
            Label = "Raum stören|Jam room",
            Icon = "radar",
            GameIcon = _ => ProcIcons.Power(),
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer) && !picker.Active,
            OnClick = _ => { if (picker.Open()) Sfx.Play(Sfx.Investigate, 0.5f); },
        });
    }

    private static readonly Dictionary<Console, bool> dimmed = new();

    public override void ResetState() { picker.Cancel(); Jammed.Clear(); RestoreTints(); lastRoom = SystemTypes.Hallway; Unfreeze(); }
    public override void OnMeetingStart() { picker.Cancel(); Jammed.Clear(); RestoreTints(); lastRoom = SystemTypes.Hallway; Unfreeze(); }

    private void Unfreeze() { if (frozen && PlayerControl.LocalPlayer) PlayerControl.LocalPlayer.moveable = true; frozen = false; }

    private static void RestoreTints()
    {
        foreach (var kv in dimmed) if (kv.Key && kv.Key.Image) kv.Key.Image.color = Color.white;
        dimmed.Clear();
    }

    private static SystemTypes RoomAt(Vector2 p)
    {
        var ship = ShipStatus.Instance;
        if (ship && ship.AllRooms != null)
            foreach (var r in ship.AllRooms)
                if (r && r.roomArea && r.roomArea.OverlapPoint(p)) return r.RoomId;
        PlainShipRoom best = null; float bd = float.MaxValue;
        if (ship && ship.AllRooms != null)
            foreach (var r in ship.AllRooms)
            {
                if (!r || !r.roomArea) continue;
                float d = ((Vector2)r.roomArea.bounds.center - p).sqrMagnitude;
                if (d < bd) { bd = d; best = r; }
            }
        return best ? best.RoomId : SystemTypes.Hallway;
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionDisable || data.Length < 1 || !RoleRegistry.Has(sender, this)) return;
        var room = (SystemTypes)data[0];
        Jammed[room] = Time.time + duration.Value; // auf jedem Client -> der CanUse-Patch sperrt dort die Aufgaben
        if (sender == PlayerControl.LocalPlayer) Sfx.Play(Sfx.Fail);
    }

    public override void Update()
    {
        // Beim Zielen (Karte offen) stehen bleiben, nicht mit dem letzten Input weiterlaufen.
        var lp = PlayerControl.LocalPlayer;
        if (picker.Active)
        {
            if (lp) { lp.moveable = false; if (lp.MyPhysics && lp.MyPhysics.body) lp.MyPhysics.body.velocity = Vector2.zero; }
            frozen = true;
        }
        else if (frozen) Unfreeze();

        if (picker.Active && picker.Poll(out var world))
        {
            var room = RoomAt(world);
            if (room == SystemTypes.Hallway) { if (button != null) button.Timer = 0f; }
            else if (room == lastRoom)
            {
                // Derselbe Raum ist nicht erlaubt -> Abklingzeit zurückgeben, damit man sofort einen anderen wählen kann.
                if (button != null) button.Timer = 0f;
                ChatNote.Display(lp, Lang.T("Nicht denselben Raum - wähle einen anderen.", "Not the same room - pick a different one."));
            }
            else { lastRoom = room; SendToAll(ActionDisable, (byte)room); }
        }

        float t = Time.time;
        if (Jammed.Count > 0)
        {
            var expired = new List<SystemTypes>();
            foreach (var kv in Jammed) if (t >= kv.Value) expired.Add(kv.Key);
            foreach (var r in expired) Jammed.Remove(r);
        }

        // Visuelles Feedback: gesperrte Aufgaben-Konsolen rot abdunkeln, sonst normal (nur wenn gerade etwas gesperrt/getönt ist).
        if (Jammed.Count > 0 || dimmed.Count > 0)
        {
            foreach (var c in Object.FindObjectsOfType<Console>())
            {
                if (!c || !c.Image) continue;
                bool jam = IsJammed(c.Room) && IsTaskConsole(c);
                bool isDim = dimmed.ContainsKey(c);
                if (jam && !isDim) { dimmed[c] = true; c.Image.color = new Color(1f, 0.35f, 0.35f, 0.55f); }
                else if (!jam && isDim) { dimmed.Remove(c); c.Image.color = Color.white; }
            }
        }
    }

    /// <summary>Ist dieser Raum gerade gesperrt?</summary>
    internal static bool IsJammed(SystemTypes room) => Jammed.TryGetValue(room, out var until) && Time.time < until;

    // Sabotage-/Reparatur-Aufgabentypen - diese Konsolen (Licht, Reaktor, O2, Komm. ...) NICHT sperren/färben.
    private static readonly HashSet<TaskTypes> SabotageTasks = new()
    {
        TaskTypes.ResetReactor, TaskTypes.FixLights, TaskTypes.FixComms, TaskTypes.RestoreOxy,
        TaskTypes.StabilizeSteering, TaskTypes.MushroomMixupSabotage,
    };

    /// <summary>
    /// Crew-Aufgaben-Konsole? Alles sperren AUSSER reinen Sabotage/Reparatur-Konsolen (Licht, Reaktor, O2, Komm.).
    /// Hat eine Konsole keine TaskTypes hinterlegt, gilt sie als normale Aufgabe (sonst würden z. B. Cafeteria-Tasks durchrutschen).
    /// Vents sind kein Console und werden daher NIE angefasst.
    /// </summary>
    internal static bool IsTaskConsole(Console c)
    {
        if (!c) return false;
        var tts = c.TaskTypes;
        if (tts == null || tts.Length == 0) return true; // keine Angabe -> als Crew-Aufgabe behandeln
        foreach (var tt in tts)
            if (tt != TaskTypes.None && !SabotageTasks.Contains(tt)) return true; // hat eine normale Aufgabe
        return false; // nur Sabotage-Typen (z. B. Licht) -> nicht sperren
    }
}

/// <summary>Aufgaben-Konsolen in einem gesperrten Raum sind nicht benutzbar (NUR Aufgaben - Sabotage/Vents sind andere Klassen).</summary>
[HarmonyPatch(typeof(Console), nameof(Console.CanUse))]
internal static class DeactivatorConsolePatch
{
    public static bool Prefix(Console __instance, ref bool canUse, ref bool couldUse, ref float __result)
    {
        // NUR echte Aufgaben-Konsolen sperren (TaskTypes != leer). Licht-Reparatur/Sabotage haben keine TaskTypes
        // und bleiben nutzbar; Vents sind sowieso kein Console.
        if (__instance && Deactivator.IsJammed(__instance.Room) && Deactivator.IsTaskConsole(__instance))
        {
            canUse = false;
            couldUse = false;
            __result = float.MaxValue;
            return false; // Original nicht ausführen -> Konsole gilt als nicht benutzbar
        }
        return true;
    }
}
