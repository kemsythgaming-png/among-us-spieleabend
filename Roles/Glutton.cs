using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Neutral: Verschluckt Spieler und haelt sie im Magen gefangen (unsichtbar, bewegungslos, keine Aufgaben, nicht meldbar).
/// Bei Meeting/Report sterben die Gefangenen; stirbt der Vielfrass, werden sie freigelassen. Sieg als letzter Lebender.
/// </summary>
public sealed class Glutton : CustomRole
{
    public override string ConfigSection => "Vielfrass";
    public override string Name => Lang.T("Vielfraß", "Glutton");
    public override Color Color => new Color32(150, 110, 70, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override string IntroText => Lang.T("Friss sie alle", "Devour them all");
    public override string Description => Lang.T("Verschluck Spieler (F).\nGewinne als Letzter.", "Swallow players (F).\nWin as the last one.");
    public override string LongDescription => Lang.T(
        "Neutral: Mit dem Knopf verschluckst du einen Spieler - er verschwindet in deinem Magen (unsichtbar, kann sich nicht bewegen, keine Aufgaben, nicht meldbar). Bei einem Meeting oder Report sterben alle in deinem Magen. Stirbst du vorher, werden sie wieder ausgespuckt. Du gewinnst, wenn du als Letzter übrig bist.",
        "Neutral: the button swallows a player - they vanish into your stomach (invisible, can't move, no tasks, can't be reported). On a meeting or report everyone in your stomach dies. If you die first, they are spat back out. You win if you are the last one standing.");

    internal static Glutton Instance { get; private set; }

    private const byte ActionSwallow = 0;
    private RoleOption cooldown;

    // VielfrassId -> verschluckte Spieler
    private readonly Dictionary<byte, HashSet<byte>> stomach = new();

    protected override void Setup()
    {
        Instance = this;
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 30f, 10f, 90f, 2.5f, "s");

        AddButton(new RoleButton
        {
            Label = "Verschlucken|Swallow",
            Icon = "eat",
            NeedsTarget = true,
            TargetFilter = p => !RoleRegistry.Has(p, this), // keine anderen Vielfraesse schlucken
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            OnClick = target => { if (target) SendToAll(ActionSwallow, target.PlayerId); },
        });
    }

    public override void ResetState()
    {
        foreach (var set in stomach.Values)
            foreach (var id in set) { var p = FindPlayer(id); if (p) p.Visible = true; }
        stomach.Clear();
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionSwallow || data.Length < 1 || !RoleRegistry.Has(sender, this)) return;
        if (!stomach.TryGetValue(sender.PlayerId, out var set)) stomach[sender.PlayerId] = set = new HashSet<byte>();
        set.Add(data[0]);
        if (sender == PlayerControl.LocalPlayer) Sfx.Play(Sfx.Kill, 0.6f);
    }

    internal bool IsSwallowed(PlayerControl p) => p && stomach.Values.Any(s => s.Contains(p.PlayerId));

    public override float SpeedMultiplier(PlayerControl local) => IsSwallowed(local) && !InMeeting ? 0f : 1f;

    public override void Update()
    {
        // Verschluckte unsichtbar halten und zum Vielfrass ziehen
        if (!InMeeting)
        {
            foreach (var pair in stomach)
            {
                var belly = FindPlayer(pair.Key);
                foreach (var id in pair.Value)
                {
                    var p = FindPlayer(id);
                    if (!p) continue;
                    if (p.Visible) p.Visible = false;
                    if (belly) p.transform.position = belly.transform.position;
                }
            }
        }

        // Host: Sieg, wenn nur noch Vielfraesse leben (Verschluckte zaehlen nicht als Gegner)
        if (AmHost && RoleRegistry.HasAssignments && !InMeeting)
        {
            var contenders = PlayerControl.AllPlayerControls.ToArray()
                .Where(p => IsAlive(p) && !RoleRegistry.Has(p, this) && !IsSwallowed(p)).ToList();
            var gluttons = PlayerControl.AllPlayerControls.ToArray().Where(p => IsAlive(p) && RoleRegistry.Has(p, this)).ToList();
            if (contenders.Count == 0 && gluttons.Count > 0) CustomWin.Trigger(this, gluttons);
        }
    }

    public override void OnMeetingStart()
    {
        // Verschluckte sterben beim Meeting/Report (nur Host loest den Tod aus)
        if (!AmHost) return;
        foreach (var set in stomach.Values.ToList())
            foreach (var id in set.ToList())
            {
                var p = FindPlayer(id);
                if (IsAlive(p)) KillSystem.KillNoBody(p);
            }
        foreach (var set in stomach.Values) set.Clear();
    }

    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        // Stirbt ein Vielfrass -> seine Gefangenen werden freigelassen
        if (!RoleRegistry.Has(victim, this) || !stomach.TryGetValue(victim.PlayerId, out var set)) return;
        foreach (var id in set) { var p = FindPlayer(id); if (p) p.Visible = true; }
        set.Clear();
    }
}
