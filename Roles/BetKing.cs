using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Neutral (Wettkönig): sagt verschiedene Dinge voraus (wer stirbt, wer rausgevotet wird, Doppelkill, Skip, wer das
/// nächste Meeting ruft/die Leiche findet, wer zuerst alle Aufgaben hat). Pro Knopfdruck gibt es eine Auswahl aus
/// 2 ZUFÄLLIGEN Wetten; klickt man den Knopf erneut, kommen wieder 2 neue - die alte Wette zählt dann nicht mehr.
/// Trifft die Vorhersage zu, gibt es einen Punkt. Genug Treffer = Sieg. Kein Kill, kein Meeting.
/// </summary>
public sealed class BetKing : CustomRole
{
    public override string ConfigSection => "Wettkoenig";
    public override string Name => Lang.T("Wettkönig", "Bet King");
    public override Color Color => new Color32(220, 190, 60, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override string IntroText => Lang.T("Sage das nächste Ereignis voraus", "Predict the next event");
    public override string Description => Lang.T("Wähle 1 von 2 Wetten (Knopf).\nTriff sie für einen Punkt.", "Pick 1 of 2 bets (button).\nHit it for a point.");
    public override string LongDescription => Lang.T(
        "Du bist neutral, ohne Kill und ohne Meeting. Mit dem Wett-Knopf (alle 15 s) bekommst du eine Auswahl aus 2 zufälligen Wetten. Manche brauchen einen Spieler (wer stirbt / rausgevotet wird / die Leiche findet / das Meeting ruft / zuerst alle Aufgaben hat), andere nicht (Doppelkill, Skip). Klickst du den Knopf erneut, kommen 2 neue Wetten und die alte zählt nicht mehr. Trifft deine Wette bis zum nächsten Meeting zu, gibt es einen Punkt. Genug Treffer = du gewinnst allein.",
        "You are neutral, no kill and no meeting. The bet button (every 15 s) offers 2 random bets. Some need a player (who dies / is voted out / finds the body / calls the meeting / finishes all tasks first), others don't (double kill, skip). Click again for 2 new bets - the old one no longer counts. If your bet comes true by the next meeting, you get a point. Enough points = you win alone.");

    private const byte ActionBet = 0, ActionResult = 1, ActionMeeting = 2;
    private const byte None = 255;

    // Wett-Arten
    private const byte BetDeath = 0, BetExile = 1, BetReporter = 2, BetCaller = 3, BetFirstTasks = 4, BetDoubleKill = 5, BetSkip = 6;
    private static readonly byte[] AllBets = { BetDeath, BetExile, BetReporter, BetCaller, BetFirstTasks, BetDoubleKill, BetSkip };
    private static bool IsTargeted(byte t) => t <= BetFirstTasks; // 0..4 brauchen einen Spieler

    private RoleOption toWin;

    // Host
    private readonly Dictionary<byte, byte> betByKing = new();
    private readonly Dictionary<byte, byte> betTypeByKing = new();
    private readonly Dictionary<byte, int> scoreByKing = new();
    private readonly HashSet<byte> deathsThisRound = new();
    private byte lastCaller = None;      // wer hat das aktuelle Meeting gerufen
    private bool lastWasReport;          // war es eine Leichenmeldung?
    private byte firstFinisher = None;   // wer war als Erster mit allen Aufgaben fertig

    // lokal (der König)
    private byte myBet = None;
    private byte myBetType = BetDeath;
    private int myScore;
    private bool choosing;               // Auswahl aus 2 Wetten offen?
    private byte[] offered = { BetDeath, BetExile };
    private ShapeshifterMinigame menu;

    protected override void Setup()
    {
        toWin = AddOption("ToWin", "Treffer zum Sieg|Correct to win", 2f, 1f, 5f, 1f);

        AddButton(new RoleButton
        {
            Label = "Wetten|Bet",
            Icon = "guess",
            NeedsTarget = false,
            Cooldown = () => 15f,
            StartCooldown = 0f,
            CanUse = () => !InMeeting && !Minigame.Instance && IsAlive(PlayerControl.LocalPlayer) && !choosing,
            OnClick = _ => OpenChooser(),
        });
    }

    public override void ResetState()
    {
        betByKing.Clear(); betTypeByKing.Clear(); scoreByKing.Clear(); deathsThisRound.Clear();
        lastCaller = None; lastWasReport = false; firstFinisher = None;
        myBet = None; myBetType = BetDeath; myScore = 0; choosing = false; placedGlobal = false;
        CloseMenu();
    }

    public override void OnMeetingStart() => CloseMenu();

    private void CloseMenu() { if (menu) { try { menu.Close(); } catch { } } menu = null; }

    // ---------- Auswahl aus 2 Wetten ----------
    private void OpenChooser()
    {
        CloseMenu();
        // 2 verschiedene zufällige Wett-Arten anbieten.
        var pool = AllBets.OrderBy(_ => UnityEngine.Random.value).Take(2).ToArray();
        offered = pool;
        choosing = true;
    }

    private void ChooseType(byte type)
    {
        choosing = false;
        myBetType = type;
        if (IsTargeted(type)) { OpenMenu(); return; } // Spieler wählen
        // Globale Wette (Doppelkill/Skip): sofort setzen, kein Spieler.
        myBet = None;
        placedGlobal = true;
        SendToHost(ActionBet, None, type);
        ChatNote.Display(PlayerControl.LocalPlayer, $"<color={ColorHex}>{Lang.T("Wette: ", "Bet: ")}{TypeName(type)}</color>");
    }

    // ---------- Menü-Titel + Wett-Auswahl zeichnen ----------
    private static GUIStyle titleStyle, subStyle;
    public override void DrawGUI()
    {
        MenuSfx.Suppress = menu != null; // solange das Spieler-Menü offen ist: Hover-/Auswahl-Geräusche unterdrücken
        float sw = Ui.ScreenWidth;
        if (titleStyle == null) titleStyle = new GUIStyle(Ui.Text) { fontSize = 34, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
        if (subStyle == null) subStyle = new GUIStyle(Ui.Text) { fontSize = 24, alignment = TextAnchor.MiddleCenter, wordWrap = true };

        if (choosing)
        {
            // Zwei Wett-Knöpfe mittig.
            var box = new Rect(sw / 2f - 430f, 300f, 860f, 320f);
            GUI.Box(box, GUIContent.none, Ui.Panel);
            Ui.Block(box);
            GUI.Label(new Rect(box.x, box.y + 16f, box.width, 50f), $"<color={ColorHex}>{Lang.T("Wähle deine Wette:", "Pick your bet:")}</color>", titleStyle);
            for (int i = 0; i < offered.Length; i++)
            {
                var r = new Rect(box.x + 40f, box.y + 90f + i * 100f, box.width - 80f, 84f);
                if (Ui.Button(r, TypeName(offered[i]))) ChooseType(offered[i]);
                Ui.Block(r);
            }
            return;
        }

        if (menu)
        {
            // Über dem Spieler-Menü steht, worauf gewettet wird.
            GUI.Label(new Rect(0, 110, sw, 60), $"<color={ColorHex}>{TypeName(myBetType)}</color>", titleStyle);
        }
    }

    private static string TypeName(byte t) => t switch
    {
        BetDeath => Lang.T("Wer stirbt als Nächstes?", "Who dies next?"),
        BetExile => Lang.T("Wer wird als Nächstes rausgevotet?", "Who is voted out next?"),
        BetReporter => Lang.T("Wer findet die nächste Leiche?", "Who finds the next body?"),
        BetCaller => Lang.T("Wer ruft das nächste Meeting?", "Who calls the next meeting?"),
        BetFirstTasks => Lang.T("Wer ist zuerst mit allen Aufgaben fertig?", "Who finishes all tasks first?"),
        BetDoubleKill => Lang.T("Doppelkill: mind. 2 Tote bis zum Meeting", "Double kill: 2+ deaths before the meeting"),
        BetSkip => Lang.T("Nächstes Meeting endet mit Skip/Gleichstand", "Next meeting ends in skip/tie"),
        _ => "?",
    };

    public override string GetTaskNote(PlayerControl local)
    {
        if (!RoleRegistry.Has(local, this)) return null;
        string bet;
        if (choosing) bet = Lang.T("wähle gerade …", "choosing …");
        else if (IsTargeted(myBetType)) bet = myBet == None ? Lang.T("noch keine", "none yet") : $"{TypeName(myBetType)} → {FindPlayer(myBet)?.Data?.PlayerName ?? "?"}";
        else bet = placedGlobal ? TypeName(myBetType) : Lang.T("noch keine", "none yet");
        return Lang.T(
            $"<color={ColorHex}>Treffer: {myScore}/{Mathf.RoundToInt(toWin.Value)}</color>\nWett-Knopf: 2 Wetten zur Auswahl (alle 15 s).\nDeine Wette: {bet}",
            $"<color={ColorHex}>Hits: {myScore}/{Mathf.RoundToInt(toWin.Value)}</color>\nBet button: pick 1 of 2 (every 15 s).\nYour bet: {bet}");
    }

    private bool placedGlobal;

    // ---------- Spieler-Menü (Shapeshifter-Stil) ----------
    private static ShapeshifterMinigame MenuPrefab()
    {
        if (!RoleManager.Instance) return null;
        foreach (var role in RoleManager.Instance.AllRoles)
        {
            var ss = role ? role.TryCast<ShapeshifterRole>() : null;
            if (ss && ss.ShapeshifterMenu) return ss.ShapeshifterMenu;
        }
        return null;
    }

    private void OpenMenu()
    {
        try
        {
            var prefab = MenuPrefab();
            var local = PlayerControl.LocalPlayer;
            if (!prefab || !Camera.main || Minigame.Instance || !IsAlive(local) || InMeeting) return;

            menu = Object.Instantiate(prefab, Camera.main.transform, false);
            menu.transform.localPosition = new Vector3(0f, 0f, -50f);
            menu.Begin(null);
            try { foreach (var a in menu.GetComponentsInChildren<AudioSource>(true)) if (a) { a.mute = true; a.Stop(); } } catch { }

            var targets = PlayerControl.AllPlayerControls.ToArray()
                .Where(p => p && IsAlive(p) && p != local && p.Data != null).ToList();
            for (int i = 0; i < targets.Count; i++)
            {
                var p = targets[i];
                var panel = Object.Instantiate(menu.PanelPrefab, menu.transform);
                panel.transform.localPosition = new Vector3(menu.XStart + i % 3 * menu.XOffset, menu.YStart + i / 3 * menu.YOffset, -1f);
                panel.SetPlayer(i, p.Data, (System.Action)(() => Pick(p.PlayerId)));
                menu.potentialVictims.Add(panel);
            }
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogWarning($"Wettkönig-Menü: {e.Message}");
            CloseMenu();
        }
    }

    private void Pick(byte id)
    {
        CloseMenu();
        var local = PlayerControl.LocalPlayer;
        if (!IsAlive(local) || InMeeting) return;
        myBet = id;
        placedGlobal = false;
        SendToHost(ActionBet, id, myBetType);
        ChatNote.Display(local, $"<color={ColorHex}>{Lang.T("Wette: ", "Bet: ")}{TypeName(myBetType)} → {FindPlayer(id)?.Data?.PlayerName ?? "?"}</color>");
    }

    // ---------- Ereignisse ----------
    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        if (AmHost && victim) deathsThisRound.Add(victim.PlayerId);
    }

    /// <summary>Läuft beim Melder selbst: meldet dem Host, wer das Meeting gerufen hat (und ob es eine Leichenmeldung war).</summary>
    public void LocalCalledMeeting(bool report)
    {
        var local = PlayerControl.LocalPlayer;
        if (!local) return;
        SendToHost(ActionMeeting, local.PlayerId, (byte)(report ? 1 : 0));
    }

    public override void Update()
    {
        if (!AmHost) return;
        // Wer war als Erster mit allen Aufgaben fertig (einmal festhalten).
        if (firstFinisher == None && !InMeeting)
        {
            foreach (var p in PlayerControl.AllPlayerControls)
                if (p && IsAlive(p) && AllTasksDone(p)) { firstFinisher = p.PlayerId; break; }
        }
    }

    private static bool AllTasksDone(PlayerControl p)
    {
        try
        {
            var tasks = p.Data != null ? p.Data.Tasks : null;
            if (tasks == null || tasks.Count == 0) return false;
            foreach (var t in tasks) if (t == null || !t.Complete) return false;
            return true;
        }
        catch { return false; }
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionBet && AmHost && data.Length >= 2 && RoleRegistry.Has(sender, this))
        {
            betByKing[sender.PlayerId] = data[0];
            betTypeByKing[sender.PlayerId] = data[1];
        }
        else if (action == ActionMeeting && AmHost && data.Length >= 2)
        {
            lastCaller = data[0];
            lastWasReport = data[1] != 0;
        }
        else if (action == ActionResult && data.Length >= 2)
        {
            if (FindPlayer(data[0]) == PlayerControl.LocalPlayer)
            {
                myScore = data[1];
                myBet = None;
                placedGlobal = false;
                ChatNote.Display(PlayerControl.LocalPlayer, $"<color={ColorHex}>{Lang.T("Wette abgerechnet.", "Bet settled.")} {myScore}/{Mathf.RoundToInt(toWin.Value)}</color>");
            }
        }
    }

    public override void OnMeetingEnd(PlayerControl exiled)
    {
        if (!AmHost) return;
        foreach (var pair in new List<KeyValuePair<byte, byte>>(betByKing))
        {
            byte type = betTypeByKing.TryGetValue(pair.Key, out var bt) ? bt : BetDeath;
            byte target = pair.Value;
            bool correct = type switch
            {
                BetDeath => deathsThisRound.Contains(target),
                BetExile => exiled && exiled.PlayerId == target,
                BetReporter => lastWasReport && lastCaller == target,
                BetCaller => lastCaller == target,
                BetFirstTasks => firstFinisher != None && firstFinisher == target,
                BetDoubleKill => deathsThisRound.Count == 2, // genau 2 (bei 3 zählt es nicht)
                BetSkip => !exiled,
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
        betTypeByKing.Clear();
        deathsThisRound.Clear();
        lastCaller = None;
        lastWasReport = false;
    }
}

/// <summary>Schalter: solange ein Rollen-Auswahlmenü offen ist, werden neue Sounds (z. B. das Dauer-"Ausgewählt") unterdrückt.</summary>
internal static class MenuSfx
{
    public static bool Suppress;
}

/// <summary>Unterdrückt neue Sounds, während ein Auswahlmenü offen ist (laufende Musik bleibt - sie wird nicht neu gestartet).</summary>
[HarmonyPatch(typeof(SoundManager), nameof(SoundManager.PlaySound))]
internal static class MenuSfxPlaySoundPatch
{
    public static bool Prefix(ref AudioSource __result)
    {
        if (MenuSfx.Suppress) { __result = null; return false; }
        return true;
    }
}

[HarmonyPatch(typeof(SoundManager), nameof(SoundManager.PlayNamedSound))]
internal static class MenuSfxNamedSoundPatch
{
    public static bool Prefix(ref AudioSource __result)
    {
        if (MenuSfx.Suppress) { __result = null; return false; }
        return true;
    }
}

/// <summary>Der Wettkönig kann keine Leiche melden und kein Notfall-Meeting auslösen.</summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdReportDeadBody))]
internal static class BetKingNoMeetingPatch
{
    public static bool Prefix(PlayerControl __instance)
    {
        var local = PlayerControl.LocalPlayer;
        if (__instance == local && CustomRole.IsAlive(local) && RoleRegistry.GetRole(local) is BetKing)
        {
            ChatNote.Display(local, Lang.T("Als Wettkönig kannst du kein Meeting rufen.", "As Bet King you can't call a meeting."));
            return false;
        }
        return true;
    }
}

/// <summary>Meldet dem Wettkönig(-Host), wer das Meeting gerufen hat (für die Wetten "Melder"/"Meeting-Rufer").</summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdReportDeadBody))]
internal static class BetKingReportPatch
{
    public static void Postfix(PlayerControl __instance, NetworkedPlayerInfo target)
    {
        var local = PlayerControl.LocalPlayer;
        if (__instance != local || !local) return;
        if (RoleRegistry.GetRole(local) is BetKing) return; // der König ruft eh nicht (geblockt)
        try { (RoleRegistry.Roles.FirstOrDefault(r => r is BetKing) as BetKing)?.LocalCalledMeeting(target != null); } catch { }
    }
}
