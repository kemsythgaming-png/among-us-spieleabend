using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor (Ausbilder): gibt einem Crewmitglied in der Nähe eine zusätzliche zufällige Aufgabe - mehr Arbeit,
/// die es erledigen muss. Die bisherigen Aufgaben bleiben erhalten.
/// </summary>
public sealed class Instructor : CustomRole
{
    public override string ConfigSection => "Ausbilder";
    public override string Name => Lang.T("Ausbilder", "Taskmaster");
    public override Color Color => new Color32(90, 140, 210, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Mehr Arbeit!", "More work!");
    public override string Description => Lang.T("Gib einem Crewmitglied\neine neue Aufgabe (F).", "Give a crewmate\na new task (F).");
    public override string LongDescription => Lang.T(
        "Du bist Impostor. Mit Taste F gibst du einem Crewmitglied in deiner Nähe eine zusätzliche zufällige Aufgabe - es hat dann mehr zu tun. Seine bisherigen Aufgaben bleiben. Deinen normalen Kill hast du zusätzlich.",
        "You are impostor. Press F to give a nearby crewmate an extra random task - more work for them. Their existing tasks stay. You also keep your normal kill.");

    private const byte ActionAssign = 0, ActionAdd = 1;

    private RoleOption cooldown;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 25f, 10f, 90f, 5f, "s");

        AddButton(new RoleButton
        {
            Label = "Aufgabe geben|Give task",
            Icon = "boards",
            GameIcon = _ => ProcIcons.Graduation(),
            NeedsTarget = true,
            TargetFilter = p => IsAlive(p) && !IsImpostor(p) && !IsNeutral(p),
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer),
            OnClick = target => { if (target) SendToHost(ActionAssign, target.PlayerId); },
        });
    }

    /// <summary>Freeplay-Test: ein Dummy-Ausbilder gibt DIR eine zusätzliche Aufgabe.</summary>
    public override bool FreeplayAbility(PlayerControl attacker, PlayerControl target)
    {
        var me = PlayerControl.LocalPlayer;
        if (!me) return false;
        byte idx = PickTaskIndex();
        if (idx != 255) AddTask(me, idx);
        return true;
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionAssign && AmHost && data.Length >= 1 && RoleRegistry.Has(sender, this))
        {
            byte idx = PickTaskIndex();
            if (idx != 255) SendToAll(ActionAdd, data[0], idx);
        }
        else if (action == ActionAdd && data.Length >= 2)
        {
            AddTask(FindPlayer(data[0]), data[1]);
        }
    }

    /// <summary>Host: eine zufällige Kurz-/Lang-Aufgabe der Map auswählen (deren Index).</summary>
    private static byte PickTaskIndex()
    {
        try
        {
            var ship = ShipStatus.Instance;
            if (!ship) return 255;
            var pool = new System.Collections.Generic.List<NormalPlayerTask>();
            foreach (var t in ship.ShortTasks) if (t) pool.Add(t);
            foreach (var t in ship.LongTasks) if (t) pool.Add(t);
            if (pool.Count == 0) return 255;
            return (byte)pool[UnityEngine.Random.Range(0, pool.Count)].Index;
        }
        catch { return 255; }
    }

    /// <summary>Dem Spieler die gewählte Aufgabe zusätzlich geben (ohne die bisherigen zu löschen).</summary>
    private static void AddTask(PlayerControl p, byte index)
    {
        try
        {
            var ship = ShipStatus.Instance;
            if (!p || !ship || p.myTasks == null) return;
            NormalPlayerTask proto = null;
            foreach (var t in ship.ShortTasks) if (t && t.Index == index) { proto = t; break; }
            if (!proto) foreach (var t in ship.LongTasks) if (t && t.Index == index) { proto = t; break; }
            if (!proto) return;

            var task = Object.Instantiate(proto, p.transform);
            task.Id = (uint)(250 + UnityEngine.Random.Range(0, 60000));
            task.Owner = p;
            task.Initialize();
            p.myTasks.Add(task);

            if (p == PlayerControl.LocalPlayer)
                Notifier.Show(Lang.T("Du hast eine neue Aufgabe bekommen!", "You got a new task!"), 4f);
        }
        catch (System.Exception e) { Plugin.Logger.LogWarning($"[Ausbilder] Aufgabe geben: {e.Message}"); }
    }
}
