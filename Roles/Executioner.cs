using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Neutral: bekommt einen Crewmate als Ziel. Wird genau dieses Ziel rausgevotet, gewinnt er allein.
/// Stirbt das Ziel auf andere Weise, wird er (einstellbar) zum Jester.
/// </summary>
public sealed class Executioner : CustomRole
{
    public override string Name =>
        Lang.T("Executioner", "Executioner");
    public override Color Color => new Color32(140, 64, 5, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override string IntroText =>
        Lang.T("Bring dein Ziel an den Galgen", "Get your target ejected");
    public override string Description
    {
        get
        {
            string name = LocalTargetName();
            return name != null
                ? Lang.T($"Lass dein Ziel rausvoten: {name}\nDann gewinnst du allein.", $"Get your target voted out: {name}\nThen you win alone.")
                : Lang.T("Lass dein Ziel rausvoten,\ndann gewinnst du allein.", "Get your target voted out,\nto win alone.");
        }
    }
    public override string LongDescription =>
        Lang.T("Du bist neutral und hast ein Ziel - es steht oben links bei deiner Rolle.\n\nWird genau dieses Ziel rausgevotet, gewinnst du allein. Stirbt es anders, wirst du (einstellbar) zum Jester.", "You are neutral and have a target - it's shown with your role (top left).\n\nIf exactly this target is voted out, you win alone. If it dies another way, you become a Jester (configurable).");

    /// <summary>Name des eigenen Ziels (nur fuer den Executioner selbst), oder null.</summary>
    private string LocalTargetName()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !RoleRegistry.Has(local, this) || !targets.TryGetValue(local.PlayerId, out var t)) return null;
        var target = FindPlayer(t);
        return target && target.Data != null ? target.Data.PlayerName : null;
    }

    private const byte ActionSetTarget = 0;

    private RoleOption becomesJester;

    /// <summary>Executioner -> Ziel (bei allen Clients bekannt, sichtbar nur fuer den Executioner).</summary>
    private readonly Dictionary<byte, byte> targets = new();

    protected override void Setup()
    {
        becomesJester = AddToggle("BecomesJester", "Wird Jester, wenn das Ziel anders stirbt|Becomes Jester if target dies otherwise", true);
    }

    public override void ResetState() => targets.Clear();

    public override void OnAssigned(PlayerControl player)
    {
        if (!AmHost) return;

        // Ziel: ein lebender Crewmate ohne neutrale Rolle
        var candidates = PlayerControl.AllPlayerControls.ToArray()
            .Where(p => IsAlive(p) && p != player && !IsImpostor(p) && !IsNeutral(p))
            .ToList();
        if (candidates.Count == 0) return;

        var target = candidates[Random.Range(0, candidates.Count)];
        SendToAll(ActionSetTarget, player.PlayerId, target.PlayerId);
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionSetTarget && data.Length >= 2) targets[data[0]] = data[1];
    }

    public override void OnAnyExiled(PlayerControl exiled)
    {
        if (!AmHost) return;
        var winners = targets.Where(t => t.Value == exiled.PlayerId)
            .Select(t => FindPlayer(t.Key))
            .Where(p => IsAlive(p) && RoleRegistry.Has(p, this))
            .ToList();
        if (winners.Count > 0) CustomWin.Trigger(this, winners);
    }

    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        if (!AmHost || cause == DeathCause.Exile || !becomesJester.Bool) return;

        var jester = RoleRegistry.Roles.OfType<Jester>().FirstOrDefault();
        foreach (var pair in targets.Where(t => t.Value == victim.PlayerId).ToList())
        {
            var exe = FindPlayer(pair.Key);
            if (IsAlive(exe) && RoleRegistry.Has(exe, this) && jester != null)
                RoleRegistry.ChangeRole(exe, jester);
        }
    }

    // Kein Zeichen mehr ueber dem Kopf des Ziels - der Zielname steht nur oben links bei der Rolle (siehe Description).
}
