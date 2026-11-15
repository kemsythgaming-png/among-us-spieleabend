using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Neutral (Kometenschnüffler): hat Aufgaben wie ein Crewmate. Sind alle erledigt, bekommt er in Weapons die echte
/// "Asteroiden abschießen"-Aufgabe - aber er muss insgesamt 200 statt 20 schießen (die Aufgabe kommt dafür mehrfach).
/// Schafft er das, gewinnt er allein. Gibt es die Asteroiden-Aufgabe auf der Map nicht, fällt er auf einen Knopf zurück.
/// </summary>
public sealed class CometSniffer : CustomRole
{
    public override string ConfigSection => "Kometenschnueffler"; // Schlüssel bleibt (Einstellungen), Anzeigename geändert
    public override string Name => Lang.T("Astronaut", "Astronaut");
    public override Color Color => new Color32(90, 160, 220, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override bool KeepsTasks => true; // bekommt echte Crew-Aufgaben
    public override string IntroText => Lang.T("Erst Tasks, dann Kometen", "Tasks first, then comets");
    public override string Description => Lang.T("Mach deine Aufgaben, dann\nschieß 200 Kometen in Weapons.", "Do your tasks, then\nshoot 200 comets in Weapons.");
    public override string LongDescription => Lang.T(
        "Du bist neutral und hast ganz normale Aufgaben. Hast du ALLE erledigt, kommt in Weapons die echte Asteroiden-Aufgabe - du musst aber insgesamt 200 Kometen abschießen (die Aufgabe kommt dafür immer wieder). Schaffst du das, gewinnst du allein.",
        "You are neutral with normal tasks. Once you've done them ALL, the real asteroid task appears in Weapons - but you must shoot 200 comets in total (the task keeps coming back). If you make it, you win alone.");

    private const byte ActionWin = 0;
    private const int PerTask = 20; // Vanilla: eine Asteroiden-Aufgabe = 20 Schuss

    private RoleOption needed;
    private int shot;
    private bool reached;              // alle normalen Aufgaben fertig?
    private int mode;                  // 0 = unbekannt, 1 = echte Aufgabe, 2 = Knopf-Fallback
    private NormalPlayerTask activeTask;

    protected override void Setup()
    {
        needed = AddOption("Comets", "Kometen zum Sieg|Comets to win", 200f, 50f, 500f, 10f);

        AddButton(new RoleButton
        {
            Label = "Komet schießen|Shoot comet",
            Icon = "shoot",
            NeedsTarget = false,
            Cooldown = () => 0.25f,
            StartCooldown = 0f,
            Visible = () => mode == 2 && reached && InWeapons(),
            CanUse = () => mode == 2 && reached && InWeapons() && !InMeeting,
            OnClick = _ =>
            {
                shot++;
                Sfx.Play(Sfx.TaskUpdate, 0.4f);
                if (shot >= Need) SendToHost(ActionWin, PlayerControl.LocalPlayer.PlayerId);
            },
        });
    }

    private int Need => Mathf.RoundToInt(needed.Value);

    public override void ResetState() { shot = 0; reached = false; mode = 0; activeTask = null; }

    public override void Update()
    {
        var local = PlayerControl.LocalPlayer;
        if (!RoleRegistry.Has(local, this) || !IsAlive(local) || InMeeting || !ShipStatus.Instance) return;

        if (!reached)
        {
            StripAsteroidTasks(local); // Asteroiden-Aufgabe erst nach allen anderen -> vorher NICHT in der Liste
            try { if (local.AllTasksCompleted()) reached = true; } catch { }
            if (!reached) return;
            mode = FindAsteroidProto() ? 1 : 2; // echte Aufgabe vorhanden? sonst Knopf
            if (mode == 2) Notifier.Show($"<color={ColorHex}>{Lang.T("Schieß in Weapons Kometen (Knopf)!", "Shoot comets in Weapons (button)!")}</color>", 4f);
        }

        if (mode != 1) return; // Knopf-Modus läuft über den Button

        // Echte-Aufgabe-Modus: immer wieder die Asteroiden-Aufgabe geben, Abschlüsse × 20 bis 200.
        if (activeTask == null)
        {
            if (shot < Need) activeTask = AddAsteroidTask(local);
        }
        else if (activeTask.IsComplete)
        {
            // Erledigte Asteroiden-Aufgabe sofort entfernen, damit immer nur EINE in der Liste steht (nicht 10 Stück).
            try { if (local.myTasks != null) local.myTasks.Remove(activeTask); } catch { }
            try { Object.Destroy(activeTask.gameObject); } catch { }
            shot += PerTask;
            activeTask = null;
            Sfx.Play(Sfx.TaskUpdate, 0.5f);
            if (shot >= Need) SendToHost(ActionWin, local.PlayerId);
        }
    }

    /// <summary>Alle (natürlichen) Asteroiden-Aufgaben aus der Liste entfernen - solange die anderen noch nicht fertig sind.</summary>
    private static void StripAsteroidTasks(PlayerControl local)
    {
        try
        {
            if (local.myTasks == null) return;
            for (int i = local.myTasks.Count - 1; i >= 0; i--)
            {
                var t = local.myTasks[i];
                var npt = t ? t.TryCast<NormalPlayerTask>() : null;
                if (npt != null && npt.TaskType == TaskTypes.ClearAsteroids)
                {
                    local.myTasks.RemoveAt(i);
                    try { Object.Destroy(npt.gameObject); } catch { }
                }
            }
        }
        catch { }
    }

    private static NormalPlayerTask FindAsteroidProto()
    {
        var ship = ShipStatus.Instance;
        if (!ship) return null;
        foreach (var arr in new[] { ship.ShortTasks, ship.LongTasks, ship.CommonTasks })
        {
            if (arr == null) continue;
            foreach (var t in arr)
                if (t && t.TaskType == TaskTypes.ClearAsteroids) return t;
        }
        return null;
    }

    private static NormalPlayerTask AddAsteroidTask(PlayerControl local)
    {
        try
        {
            var proto = FindAsteroidProto();
            if (!proto || local.myTasks == null) return null;
            var task = Object.Instantiate(proto, local.transform);
            task.Id = (uint)(230 + UnityEngine.Random.Range(0, 60000));
            task.Owner = local;
            task.Initialize();
            local.myTasks.Add(task);
            return task;
        }
        catch (System.Exception e) { Plugin.Logger.LogWarning($"[Kometenschnüffler] Asteroiden-Aufgabe: {e.Message}"); return null; }
    }

    private static bool InWeapons()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !ShipStatus.Instance) return false;
        Vector2 me = local.GetTruePosition();
        bool hasWeapons = false;
        foreach (var c in Object.FindObjectsOfType<Console>())
        {
            if (!c) continue;
            if (c.Room == SystemTypes.Weapons)
            {
                hasWeapons = true;
                if (Vector2.Distance(me, c.transform.position) <= 2.0f) return true;
            }
        }
        return !hasWeapons; // auf Maps ohne Weapons überall erlaubt
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionWin || !AmHost || data.Length < 1 || !RoleRegistry.Has(sender, this)) return;
        if (IsAlive(sender)) CustomWin.Trigger(this, new[] { sender });
    }

    public override string GetTaskNote(PlayerControl local)
    {
        if (!RoleRegistry.Has(local, this)) return null;
        if (!reached)
            return Lang.T($"<color={ColorHex}>Erst alle Aufgaben erledigen</color>, dann {Need} Kometen in Weapons.", $"<color={ColorHex}>Finish all tasks first</color>, then {Need} comets in Weapons.");
        return Lang.T($"<color={ColorHex}>Kometen: {shot}/{Need}</color>\nIn Weapons Asteroiden abschießen!", $"<color={ColorHex}>Comets: {shot}/{Need}</color>\nShoot asteroids in Weapons!");
    }
}
