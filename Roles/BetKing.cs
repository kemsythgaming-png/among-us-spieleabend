using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Neutral (Wettkoenig): waehlt zu Beginn jeder Runde eine geheime Wette (z. B. "diese Runde stirbt niemand" oder
/// "es wird jemand ausgeworfen"). Liegt er zweimal richtig, gewinnt er allein. Die Wette steht in einem kleinen Fenster.
/// </summary>
public sealed class BetKing : CustomRole
{
    public override string ConfigSection => "Wettkoenig";
    public override string Name => Lang.T("Wettkönig", "Bet King");
    public override Color Color => new Color32(220, 190, 60, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override string IntroText => Lang.T("Setz auf den Ausgang", "Bet on the outcome");
    public override string Description => Lang.T("Wette jede Runde.\n2x richtig = Sieg.", "Bet each round.\n2x right = win.");
    public override string LongDescription => Lang.T(
        "Du bist neutral. Zu Beginn jeder Runde wählst du im kleinen Wett-Fenster eine geheime Wette - etwa dass niemand stirbt oder dass jemand ausgeworfen wird. Am Ende der Runde wird abgerechnet. Liegst du zweimal richtig, gewinnst du allein.",
        "You are neutral. At the start of each round you pick a secret bet in the small bet window - e.g. that nobody dies or that someone gets ejected. It is settled at the end of the round. If you are right twice, you win alone.");

    private const byte ActionBet = 0, ActionResult = 1;

    private RoleOption toWin;

    // Host
    private readonly Dictionary<byte, int> betByKing = new();
    private readonly Dictionary<byte, int> scoreByKing = new();
    private int deathsThisRound;

    // lokal (der Koenig)
    private int myBet = -1, myScore;

    private static readonly (string De, string En)[] Bets =
    {
        ("Niemand wird ausgeworfen", "Nobody is ejected"),
        ("Jemand wird ausgeworfen", "Someone is ejected"),
        ("Diese Runde stirbt jemand", "Someone dies this round"),
        ("Diese Runde stirbt niemand", "Nobody dies this round"),
    };

    protected override void Setup()
    {
        toWin = AddOption("ToWin", "Treffer zum Sieg|Correct to win", 2f, 1f, 5f, 1f);
    }

    public override void ResetState() { betByKing.Clear(); scoreByKing.Clear(); deathsThisRound = 0; myBet = -1; myScore = 0; }

    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        if (AmHost && cause != DeathCause.Exile) deathsThisRound++;
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionBet && AmHost && data.Length >= 1 && RoleRegistry.Has(sender, this))
        {
            betByKing[sender.PlayerId] = data[0];
        }
        else if (action == ActionResult && data.Length >= 2)
        {
            if (FindPlayer(data[0]) == PlayerControl.LocalPlayer)
            {
                myScore = data[1];
                myBet = -1;
                Notifier.Show($"<color={ColorHex}>{Lang.T("Wette abgerechnet.", "Bet settled.")} {myScore}/{Mathf.RoundToInt(toWin.Value)}</color>", 4f);
            }
        }
    }

    public override void OnMeetingEnd(PlayerControl exiled)
    {
        if (!AmHost) return;
        bool ejected = exiled != null;
        bool someoneDied = deathsThisRound > 0;
        foreach (var pair in new List<KeyValuePair<byte, int>>(betByKing))
        {
            bool correct = pair.Value switch
            {
                0 => !ejected,
                1 => ejected,
                2 => someoneDied,
                3 => !someoneDied,
                _ => false,
            };
            scoreByKing.TryGetValue(pair.Key, out int s);
            if (correct) s++;
            scoreByKing[pair.Key] = s;
            SendToAll(ActionResult, pair.Key, (byte)Mathf.Min(255, s));
            var king = FindPlayer(pair.Key);
            if (s >= Mathf.RoundToInt(toWin.Value) && king) CustomWin.Trigger(this, new[] { king });
        }
        betByKing.Clear();
        deathsThisRound = 0;
    }

    public override void DrawGUI()
    {
        if (!IsLocalRole || !IsAlive(PlayerControl.LocalPlayer) || InMeeting) return;

        float w = 520f;
        float h = myBet < 0 ? 150f + Bets.Length * 60f : 190f;
        var panel = new Rect((Ui.ScreenWidth - w) / 2f, 170f, w, h);
        Ui.Block(panel);
        GUI.Box(panel, GUIContent.none, Ui.Panel);

        GUI.Label(new Rect(panel.x + 24f, panel.y + 18f, w - 48f, 44f),
            $"{Lang.T("Wettkönig", "Bet King")}", Ui.Header);
        GUI.Label(new Rect(panel.xMax - 160f, panel.y + 22f, 136f, 40f),
            $"{myScore}/{Mathf.RoundToInt(toWin.Value)}", Ui.ValueBig);

        if (myBet >= 0)
        {
            GUI.Label(new Rect(panel.x + 24f, panel.y + 80f, w - 48f, 90f),
                Lang.T("Deine Wette läuft:\n", "Your bet is running:\n") + Lang.T(Bets[myBet].De, Bets[myBet].En), Ui.Text);
            return;
        }

        GUI.Label(new Rect(panel.x + 24f, panel.y + 74f, w - 48f, 34f), Lang.T("Wähle deine Wette:", "Choose your bet:"), Ui.Text);
        for (int i = 0; i < Bets.Length; i++)
        {
            if (Ui.Button(new Rect(panel.x + 24f, panel.y + 116f + i * 60f, w - 48f, 52f), Lang.T(Bets[i].De, Bets[i].En)))
            {
                myBet = i;
                SendToHost(ActionBet, (byte)i);
            }
        }
    }
}
