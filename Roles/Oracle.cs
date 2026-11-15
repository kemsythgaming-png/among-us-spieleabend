using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Neutral: rät im Meeting Rollen wie der Guesser, verfolgt aber ein eigenes Ziel - er gewinnt, sobald er genug
/// Spieler RICHTIG geraten hat (Standard 3). Insgesamt hat er 5 Versuche, pro Meeting höchstens 3. Ein richtiger
/// Tipp tötet das Ziel; ein falscher tötet ihn NICHT - er stirbt erst, wenn er die nötigen Treffer nicht mehr schaffen kann.
/// </summary>
public sealed class Oracle : GuesserBase
{
    public override string ConfigSection => "Orakel";
    public override string Name => Lang.T("Orakel", "Oracle");
    public override Color Color => new Color32(150, 90, 220, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override string IntroText => Lang.T("Sprich das Schicksal aus", "Speak the fate");
    public override string Description => Lang.T("Rate im Meeting Rollen richtig -\n3 Treffer und du gewinnst.", "Guess roles right in meetings -\n3 hits and you win.");
    public override string LongDescription => Lang.T(
        "Du bist neutral. Im Meeting rätst du Rollen (Knopf \"Raten\" unten rechts). Rätst du genug Spieler RICHTIG (Standard 3), gewinnst du allein - jeder richtig geratene Spieler stirbt dabei.\n\nDu hast insgesamt 5 Versuche, pro Meeting höchstens 3. Ein falscher Tipp tötet dich NICHT; du stirbst erst, wenn du die nötigen Treffer nicht mehr erreichen kannst (z. B. nach zu vielen Fehlversuchen).",
        "You are neutral. In meetings you guess roles (\"Guess\" button bottom right). Guess enough players RIGHT (default 3) to win alone - each correctly guessed player dies.\n\nYou have 5 attempts total, at most 3 per meeting. A wrong guess does NOT kill you; you only die once you can no longer reach the needed hits (e.g. after too many misses).");

    protected override bool IsEvil => false;

    // Guesser-Verhalten anpassen: 5 Schüsse, kein Einzelschuss-Limit, aber max. N pro Meeting.
    protected override float DefaultShots => 5f;
    protected override bool HasMultiToggle => false;
    protected override bool AllowMultiPerMeeting => true;
    protected override bool UseCategoryMenu => true; // erst Kategorie wählen, dann A-Z-Liste
    protected override int PerMeetingCap => perMeeting != null ? perMeeting.Int : 3;

    private const byte ActionStatus = 2;
    private RoleOption neededCorrect, perMeeting;
    private readonly Dictionary<byte, int> correctByPlayer = new(); // Host: richtige Treffer je Orakel
    private readonly Dictionary<byte, HashSet<byte>> guessedHost = new(); // Host: auf wen hat welches Orakel schon geraten
    private readonly HashSet<byte> localGuessed = new();            // lokal: auf wen habe ich schon geraten (jede Person nur 1x)
    private int localCorrect;                                       // lokal: eigene richtige Treffer (für die Anzeige)

    protected override void SetupExtra()
    {
        neededCorrect = AddOption("NeededCorrect", "Richtige für den Sieg|Correct for the win", 3f, 1f, 5f, 1f);
        perMeeting = AddOption("PerMeeting", "Max. Versuche pro Meeting|Max attempts per meeting", 3f, 1f, 5f, 1f);
    }

    public override void ResetState()
    {
        base.ResetState();
        correctByPlayer.Clear();
        guessedHost.Clear();
        localGuessed.Clear();
        localCorrect = 0;
    }

    private int Need => neededCorrect != null ? neededCorrect.Int : 3;

    // ---------- Jede Person nur 1x ratbar ----------

    /// <summary>Client: bereits geratene Spieler verschwinden als Ziel (Knopf/Button weg).</summary>
    protected override bool CanTargetExtra(PlayerControl p) => p && !localGuessed.Contains(p.PlayerId);

    /// <summary>Lokal merken, auf wen schon geraten wurde.</summary>
    protected override void OnGuessSent(byte targetId) => localGuessed.Add(targetId);

    /// <summary>Host: lehnt einen 2. Tipp auf dieselbe Person ab; sonst vermerken.</summary>
    protected override bool AllowGuessHost(PlayerControl guesser, PlayerControl target)
    {
        if (!guessedHost.TryGetValue(guesser.PlayerId, out var set)) guessedHost[guesser.PlayerId] = set = new HashSet<byte>();
        if (set.Contains(target.PlayerId)) return false; // schon auf diese Person geraten
        set.Add(target.PlayerId);
        return true;
    }

    /// <summary>
    /// Host: solange ein lebendes Orakel seine nötigen Treffer noch erreichen kann (genug lebende, noch nicht
    /// geratene Ziele), läuft das Spiel weiter - auch wenn die Impostor schon rausgevotet sind.
    /// </summary>
    public override bool KeepsGameGoing()
    {
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!IsAlive(p) || !RoleRegistry.Has(p, this)) continue;
            correctByPlayer.TryGetValue(p.PlayerId, out int c);
            int remaining = Need - c;
            if (remaining <= 0) continue; // hätte eigentlich schon gewonnen
            guessedHost.TryGetValue(p.PlayerId, out var set);
            int targets = PlayerControl.AllPlayerControls.ToArray()
                .Count(t => IsAlive(t) && t != p && (set == null || !set.Contains(t.PlayerId)));
            if (targets >= remaining) return true; // kann die nötigen Treffer noch schaffen
        }
        return false;
    }

    protected override string GuessHint() =>
        Lang.T($"Richtig für den Sieg: {Need}. Ein falscher Tipp tötet dich NICHT.",
               $"Correct to win: {Need}. A wrong guess does NOT kill you.");

    /// <summary>Im Meeting: diesmal mögliche Schüsse (sinken pro Schuss) und Treffer hoch bis zum Ziel (0/3 → 3/3).</summary>
    protected override string MeetingStatusText() =>
        Lang.T($"Schüsse möglich: {ShotsPossibleThisMeeting}  -  Richtig {localCorrect}/{Need}",
               $"Shots available: {ShotsPossibleThisMeeting}  -  Correct {localCorrect}/{Need}");

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        base.OnAction(sender, action, data);
        if (action == ActionStatus && data.Length >= 2)
        {
            var local = PlayerControl.LocalPlayer;
            if (local && local.PlayerId == data[0]) localCorrect = data[1]; // Host meldet den aktuellen Trefferstand
        }
    }

    protected override void ResolveGuess(PlayerControl guesser, PlayerControl target, bool correct, int usedTotal)
    {
        if (correct)
        {
            correctByPlayer.TryGetValue(guesser.PlayerId, out int c);
            c++;
            correctByPlayer[guesser.PlayerId] = c;
            AnnounceDeath(target);
            KillSystem.KillNoBody(target, guesser);
            SendToAll(ActionStatus, guesser.PlayerId, (byte)c);                 // Trefferstand zum Guesser syncen
            if (c >= Need) { CustomWin.Trigger(this, new List<PlayerControl> { guesser }); return; }
        }
        else
        {
            AnnounceMiss(guesser);
            correctByPlayer.TryGetValue(guesser.PlayerId, out int c0);
            SendToAll(ActionStatus, guesser.PlayerId, (byte)c0);
        }

        // Stirbt erst, wenn die nötigen Treffer rechnerisch nicht mehr erreichbar sind.
        correctByPlayer.TryGetValue(guesser.PlayerId, out int now);
        if (now + (TotalShots - usedTotal) < Need)
            KillSystem.KillNoBody(guesser, null);
    }
}
