using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor (Grinch): markiert einen Spieler. Nach dem naechsten Meeting oder Report verliert dieser Spieler seine
/// Sonderrolle und ist nur noch ein normaler Crewmate/Impostor. Normaler Kill bleibt.
/// </summary>
public sealed class RoleDisabler : CustomRole
{
    public override string ConfigSection => "Grinch";
    public override string Name => Lang.T("Grinch", "Grinch");
    public override Color Color => new Color32(60, 160, 70, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Keine Geschenke für dich", "No gifts for you");
    public override string Description => Lang.T("Markiere jemanden (F) - verliert\nnach dem Meeting die Rolle.", "Mark someone (F) - loses the\nrole after the meeting.");
    public override string LongDescription => Lang.T(
        "Impostor: Mit Taste F markierst du einen Spieler in der Nähe. Nach dem nächsten Meeting oder Report verliert er seine Sonderrolle und ist nur noch ein normaler Spieler - seine Knöpfe und Fähigkeiten sind weg. Deinen normalen Kill hast du zusätzlich.",
        "Impostor: press F to mark a nearby player. After the next meeting or report they lose their special role and become a plain player - their buttons and abilities are gone. You also keep your normal kill.");

    private const byte ActionMark = 0;
    private RoleOption range;
    private readonly HashSet<byte> pending = new(); // Host: Ziele, die nach dem Meeting ihre Rolle verlieren

    protected override void Setup()
    {
        range = AddOption("Range", "Reichweite|Range", 1.5f, 0.8f, 4f, 0.2f);

        AddButton(new RoleButton
        {
            Label = "Rolle rauben|Steal role",
            Icon = "curse",
            NeedsTarget = false,
            Cooldown = () => 15f,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && Target() != null,
            OnClick = _ => { var t = Target(); if (t) SendToHost(ActionMark, t.PlayerId); },
        });
    }

    public override void ResetState() => pending.Clear();

    private PlayerControl Target()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !IsAlive(local) || local.inVent) return null;
        Vector2 me = local.GetTruePosition();
        return PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p && p != local && IsAlive(p) && !p.inVent && RoleRegistry.GetRole(p) != null && Vector2.Distance(me, p.GetTruePosition()) <= range.Value)
            .OrderBy(p => Vector2.Distance(me, p.GetTruePosition())).FirstOrDefault();
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionMark || !AmHost || data.Length < 1 || !RoleRegistry.Has(sender, this)) return;
        pending.Add(data[0]);
        if (sender == PlayerControl.LocalPlayer) Notifier.Show($"<color={ColorHex}>{Lang.T("Markiert - verliert nach dem Meeting die Rolle.", "Marked - loses the role after the meeting.")}</color>", 3f);
    }

    public override void OnMeetingEnd(PlayerControl exiled)
    {
        if (!AmHost) return;
        foreach (var id in pending.ToList())
        {
            var p = FindPlayer(id);
            if (p && RoleRegistry.GetRole(p) != null) RoleRegistry.ChangeRole(p, null);
        }
        pending.Clear();
    }
}
