using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Neutral (Wintergeist): markiert heimlich Spieler. Stirbt ein markierter Spieler, bekommt der Wintergeist eine
/// Schneeflocke. Bei genug Schneeflocken gewinnt er allein.
/// </summary>
public sealed class WinterGhost : CustomRole
{
    public override string ConfigSection => "Wintergeist";
    public override string Name => Lang.T("Wintergeist", "Winter Ghost");
    public override Color Color => new Color32(180, 220, 255, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override string IntroText => Lang.T("Sammle Schneeflocken", "Collect snowflakes");
    public override string Description => Lang.T("Markiere heimlich (F).\nStirbt das Ziel: Schneeflocke.", "Secretly mark (F).\nTarget dies: a snowflake.");
    public override string LongDescription => Lang.T(
        "Du bist neutral. Mit Taste F markierst du heimlich den nächsten Spieler (niemand merkt es). Stirbt ein von dir markierter Spieler - egal wie - bekommst du eine Schneeflocke. Bei 3 Schneeflocken gewinnst du allein.",
        "You are neutral. Press F to secretly mark the nearest player (nobody notices). If a player you marked dies - no matter how - you get a snowflake. At 3 snowflakes you win alone.");

    private const byte ActionMark = 0, ActionFlake = 1;

    private RoleOption cooldown, range, toWin;
    // Host: Geist-Id -> markierte Spieler
    private readonly Dictionary<byte, HashSet<byte>> marks = new();
    private readonly Dictionary<byte, int> flakes = new();

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 30f, 5f, 90f, 5f, "s");
        range = AddOption("Range", "Reichweite|Range", 2f, 1f, 6f, 0.5f);
        toWin = AddOption("ToWin", "Schneeflocken zum Sieg|Snowflakes to win", 3f, 1f, 6f, 1f);

        AddButton(new RoleButton
        {
            Label = "Markieren|Mark",
            Icon = "frost",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && Target() != null,
            OnClick = _ => { var t = Target(); if (t) SendToHost(ActionMark, t.PlayerId); },
        });
    }

    public override void ResetState() { marks.Clear(); flakes.Clear(); }

    private PlayerControl Target()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !IsAlive(local) || local.inVent) return null;
        Vector2 me = local.GetTruePosition();
        return PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p && p != local && IsAlive(p) && !p.inVent && Vector2.Distance(me, p.GetTruePosition()) <= range.Value)
            .OrderBy(p => Vector2.Distance(me, p.GetTruePosition())).FirstOrDefault();
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionMark && AmHost && data.Length >= 1 && RoleRegistry.Has(sender, this))
        {
            if (!marks.TryGetValue(sender.PlayerId, out var set)) marks[sender.PlayerId] = set = new HashSet<byte>();
            set.Clear();        // nur EINE Markierung gleichzeitig - die neue ersetzt die alte
            set.Add(data[0]);
            if (sender == PlayerControl.LocalPlayer) Sfx.Play(Sfx.TaskUpdate, 0.5f);
            else SendToAll(ActionFlake, sender.PlayerId, 255); // stiller Ping: nur fuer das lokale Markier-Feedback
        }
        else if (action == ActionFlake && data.Length >= 2)
        {
            if (data[1] == 255) { if (FindPlayer(data[0]) == PlayerControl.LocalPlayer) Sfx.Play(Sfx.TaskUpdate, 0.5f); return; }
            if (FindPlayer(data[0]) == PlayerControl.LocalPlayer)
                Notifier.Show($"<color={ColorHex}>{Lang.T("Schneeflocke!", "Snowflake!")} {data[1]}/{Mathf.RoundToInt(toWin.Value)}</color>", 3f);
        }
    }

    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        if (!AmHost || !victim) return;
        foreach (var pair in marks.ToList())
        {
            if (!pair.Value.Contains(victim.PlayerId)) continue;
            pair.Value.Remove(victim.PlayerId);
            flakes.TryGetValue(pair.Key, out int n);
            flakes[pair.Key] = ++n;
            SendToAll(ActionFlake, pair.Key, (byte)Mathf.Min(255, n));
            var ghost = FindPlayer(pair.Key);
            if (n >= Mathf.RoundToInt(toWin.Value) && ghost) CustomWin.Trigger(this, new[] { ghost });
        }
    }
}
