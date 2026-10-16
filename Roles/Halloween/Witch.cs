using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Halloween;

/// <summary>
/// Impostor: verflucht Spieler. Wird die Hexe im naechsten Meeting NICHT rausgevotet,
/// sterben danach alle Verfluchten.
/// </summary>
public sealed class Witch : CustomRole
{
    public override string ConfigSection => "Hexe"; // fest, damit Einstellungen erhalten bleiben
    public override string Name =>
        Lang.T("Hexe", "Witch");
    public override Color Color => new Color32(140, 70, 190, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText =>
        Lang.T("Verfluche sie alle", "Curse them all");
    public override string Description =>
        Lang.T("Verfluche Spieler (F).\nÜberlebst du das Meeting, sterben sie.", "Curse players (F).\nSurvive the meeting and they die.");
    public override string LongDescription =>
        Lang.T("Halloween-Impostor. Mit Taste F verfluchst du Spieler.\n\nWirst du im nächsten Meeting nicht rausgevotet, sterben danach alle Verfluchten. Fluch und normaler Kill teilen sich die Abklingzeit.", "Halloween impostor. Press F to curse players.\n\nIf you are not voted out in the next meeting, all cursed players die afterwards. Curse and normal kill share the cooldown.");

    private const byte ActionCurse = 0;

    private RoleOption cooldown, everyoneSeesCurse;

    /// <summary>Hexe -> verfluchte Spieler (bei allen Clients bekannt).</summary>
    private readonly Dictionary<byte, HashSet<byte>> curses = new();

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Fluch-Abklingzeit|Curse cooldown", 20f, 5f, 60f, 2.5f, "s");
        everyoneSeesCurse = AddToggle("EveryoneSees", "Alle sehen Verfluchte im Meeting|Everyone sees cursed in meetings", true);

        AddButton(new RoleButton
        {
            Label = "Verfluchen|Curse",
            Icon = "curse",
            SharesKillCooldown = true, // kein Doppelkill mit dem normalen Kill-Button
            Cooldown = () => cooldown.Value,
            Hotkey = KeyCode.F,
            TargetFilter = p => !IsImpostor(p) && !IsCursed(p),
            OnClick = target => SendToAll(ActionCurse, target.PlayerId),
        });
    }

    public override void ResetState() => curses.Clear();

    private bool IsCursed(PlayerControl p) => curses.Values.Any(set => set.Contains(p.PlayerId));

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionCurse || data.Length < 1 || !RoleRegistry.Has(sender, this)) return;
        if (!curses.TryGetValue(sender.PlayerId, out var set)) curses[sender.PlayerId] = set = new HashSet<byte>();
        set.Add(data[0]);
        if (sender == PlayerControl.LocalPlayer) Sfx.Play(Sfx.Vanish);
    }

    public override void OnMeetingEnd(PlayerControl exiled)
    {
        if (AmHost)
        {
            foreach (var pair in curses)
            {
                var witch = FindPlayer(pair.Key);
                bool witchSurvives = IsAlive(witch) && witch != exiled;
                if (!witchSurvives) continue; // Hexe rausgevotet/tot -> Fluch gebrochen

                foreach (var id in pair.Value)
                {
                    var victim = FindPlayer(id);
                    if (IsAlive(victim) && victim != exiled) KillSystem.KillNoBody(victim, witch);
                }
            }
        }
        curses.Clear();
    }

    /// <summary>Im Meeting: Totenkopf hinter dem Namen (Hexe immer, alle anderen je nach Einstellung).</summary>
    public override string GetMeetingIcon(PlayerControl viewer, PlayerControl target) =>
        IsCursed(target) && (RoleRegistry.Has(viewer, this) || everyoneSeesCurse.Bool) ? "cursed" : null;

    /// <summary>Auf der Map sieht nur die Hexe, wen sie verflucht hat.</summary>
    public override string GetNameIcon(PlayerControl viewer, PlayerControl target) =>
        IsCursed(target) && RoleRegistry.Has(viewer, this) ? "cursed" : null;
}
