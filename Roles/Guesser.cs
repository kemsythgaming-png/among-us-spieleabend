using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>Crew-Guesser: raet im Meeting Impostor, Neutrale (und optional Crew-Rollen).</summary>
public sealed class NiceGuesser : GuesserBase
{
    public override string ConfigSection => "Guter Guesser"; // fest, damit Einstellungen erhalten bleiben
    public override string Name =>
        Lang.T("Guter Guesser", "Nice Guesser");
    public override Color Color => new Color32(60, 200, 230, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText =>
        Lang.T("Errate die Impostor", "Guess the impostors");
    public override string Description =>
        Lang.T("Im Meeting Rollen raten.\nFalsch geraten = du stirbst.", "Guess roles in meetings.\nWrong guess = you die.");
    public override string LongDescription =>
        Lang.T("Du bist Crew. Im Meeting erscheint unten rechts \"Raten\".\n\nWähle einen Spieler und rate seine Rolle. Richtig: er stirbt sofort. Falsch: du stirbst.\n\nRatbar sind Impostor-Rollen, Neutrale und je nach Einstellung Crew-Rollen.", "You are crew. In meetings a \"Guess\" button appears bottom right.\n\nPick a player and guess their role. Right: they die instantly. Wrong: you die.\n\nYou can guess impostor roles, neutrals and - depending on the settings - crew roles.");

    protected override bool IsEvil => false;
}

/// <summary>Impostor-Guesser: hat seinen normalen Kill und raet im Meeting Crew-Rollen und Neutrale.</summary>
public sealed class EvilGuesser : GuesserBase
{
    public override string ConfigSection => "Böser Guesser"; // fest, damit Einstellungen erhalten bleiben
    public override string Name =>
        Lang.T("Böser Guesser", "Evil Guesser");
    public override Color Color => new Color32(190, 30, 70, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText =>
        Lang.T("Töte auch im Meeting", "Kill during meetings too");
    public override string Description =>
        Lang.T("Impostor: rate im Meeting Rollen.\nFalsch geraten = du stirbst.", "Impostor: guess roles in meetings.\nWrong guess = you die.");
    public override string LongDescription =>
        Lang.T("Du bist Impostor mit normalem Kill und kannst zusätzlich im Meeting raten (Button \"Raten\" unten rechts).\n\nRichtig geraten: der Spieler stirbt. Falsch: du stirbst. Andere Impostor kannst du nicht wählen.", "You are an impostor with a normal kill and can also guess in meetings (\"Guess\" button bottom right).\n\nRight guess: the player dies. Wrong: you die. You cannot pick other impostors.");

    protected override bool IsEvil => true;
}

/// <summary>
/// Gemeinsame Logik: im Meeting die Rolle eines Spielers raten. Richtig -> er stirbt sofort (ohne Leiche),
/// falsch -> der Guesser stirbt selbst. Der Host entscheidet.
/// </summary>
public abstract class GuesserBase : CustomRole
{
    protected abstract bool IsEvil { get; }

    private const byte ActionGuess = 0, ActionAnnounce = 1;
    private const byte KindMod = 0, KindVanilla = 1;
    private const byte None = byte.MaxValue;

    private static readonly RoleTypes[] VanillaImpostorRoles = { RoleTypes.Impostor, RoleTypes.Shapeshifter, RoleTypes.Phantom, RoleTypes.Viper };
    private static readonly RoleTypes[] VanillaCrewRoles = { RoleTypes.Engineer, RoleTypes.Scientist, RoleTypes.Noisemaker, RoleTypes.Tracker, RoleTypes.Detective, RoleTypes.Judge };

    private RoleOption shots, multiPerMeeting, canGuessCrewRoles, canGuessCrewmate;

    // Lokal (beim Guesser)
    private int shotsLeft;
    private bool shotThisMeeting;
    private int guessesThisMeeting;
    private bool open;
    private byte selectedTarget = None;

    // Beim Host: verbrauchte Schuesse je Guesser
    private readonly Dictionary<byte, int> shotsUsed = new();

    // ---------- Haken für Varianten (z. B. Orakel) ----------
    protected virtual float DefaultShots => 2f;
    protected virtual float MaxShots => 5f;
    protected virtual bool HasMultiToggle => true;
    protected virtual bool AllowMultiPerMeeting => multiPerMeeting != null && multiPerMeeting.Bool;
    protected virtual int PerMeetingCap => int.MaxValue;
    /// <summary>Orakel: erst Crew/Impostor/Neutral wählen, dann nur diese Rollen (A-Z). Andere Rater: flache Liste.</summary>
    protected virtual bool UseCategoryMenu => false;
    protected virtual void SetupExtra() { }
    /// <summary>Gesamtzahl der Schüsse (für Unterklassen).</summary>
    protected int TotalShots => shots != null ? shots.Int : 0;
    /// <summary>Verbleibende Schüsse/Versuche des lokalen Guessers (für Unterklassen).</summary>
    protected int ShotsLeftValue => shotsLeft;
    protected void AnnounceDeath(PlayerControl who) { if (who) SendToAll(ActionAnnounce, who.PlayerId, 1); }
    protected void AnnounceMiss(PlayerControl who) { if (who) SendToAll(ActionAnnounce, who.PlayerId, 2); }
    /// <summary>Hinweistext im Rate-Fenster. Standard: falsch = du stirbst.</summary>
    protected virtual string GuessHint() =>
        Lang.T($"Schüsse übrig: {shotsLeft}  -  falsch geraten = du stirbst!", $"Shots left: {shotsLeft}  -  wrong guess = you die!");

    protected override void Setup()
    {
        shots = AddOption("Shots", "Schüsse pro Spiel|Shots per game", DefaultShots, 1f, MaxShots, 1f);
        if (HasMultiToggle)
            multiPerMeeting = AddToggle("MultiPerMeeting", "Mehrere Schüsse pro Meeting|Multiple shots per meeting", false);
        canGuessCrewRoles = IsEvil
            ? AddToggle("CanGuessCrewRoles", "Spiel-Crewrollen ratbar|Game crew roles guessable", true)
            : AddToggle("CanGuessCrewRoles", "Crew-Rollen ratbar|Crew roles guessable", true);
        if (IsEvil) canGuessCrewmate = AddToggle("CanGuessCrewmate", "\"Crewmate\" ratbar (ohne Rolle)|\"Crewmate\" guessable (no role)", true);
        SetupExtra();
    }

    public override void ResetState()
    {
        shotsUsed.Clear();
        open = false;
        selectedTarget = None;
    }

    public override void OnAssigned(PlayerControl player)
    {
        if (player == PlayerControl.LocalPlayer) shotsLeft = shots.Int;
    }

    public override void OnMeetingStart()
    {
        shotThisMeeting = false;
        guessesThisMeeting = 0;
        open = false;
        selectedTarget = None;
    }

    /// <summary>Wen darf man anvisieren? Der Boese Guesser keine echten/Mod-Impostor (Fake-Impostor-Dummys im Freeplay schon).</summary>
    private bool CanTarget(PlayerControl p, PlayerControl local) =>
        IsAlive(p) && p != local && !(IsEvil && IsRealImpostorTeam(p));

    private static bool IsRealImpostorTeam(PlayerControl p)
    {
        if (!p || p.Data == null) return false;
        if (p.Data.Role && p.Data.Role.IsImpostor) return true;     // echter Impostor (ohne Fake)
        var r = RoleRegistry.GetRole(p);
        return r != null && r.Team == RoleTeam.Impostor;            // Mod-Impostor-Rolle
    }

    // ---------- Meeting: Knopf auf jeder Spielerkarte + Rollen-Menue ----------

    private bool CanShoot
    {
        get
        {
            var meeting = MeetingHud.Instance;
            return meeting && IsAlive(PlayerControl.LocalPlayer) && shotsLeft > 0
                   && (!shotThisMeeting || AllowMultiPerMeeting) && guessesThisMeeting < PerMeetingCap
                   && meeting.state != MeetingHud.MeetingStates.Results && meeting.state != MeetingHud.MeetingStates.Proceeding;
        }
    }

    private const float SlideTime = 0.18f;
    private readonly Dictionary<System.IntPtr, float> slideStart = new();

    /// <summary>
    /// "?"-Knopf pro Spielerkarte:
    /// - noch nicht abgestimmt: faehrt beim Antippen der Karte zusammen mit Haekchen/X aus (Kopie des Judge-Knopfs),
    /// - schon abgestimmt (Karten lassen sich nicht mehr aufklappen): sitzt direkt auf der Karte.
    /// </summary>
    public override void OnMeetingUpdate(MeetingHud meeting)
    {
        var local = PlayerControl.LocalPlayer;
        bool canShoot = CanShoot;
        bool voted = meeting.playerStates.Any(s => s && (byte)s.PlayerId == local.PlayerId && s.DidVote);

        foreach (var state in meeting.playerStates)
        {
            if (!state) continue;
            var target = FindPlayer((byte)state.PlayerId);
            bool can = canShoot && target && CanTarget(target, local);

            // 1) Ausfahrender Knopf bei den Abstimm-Knoepfen
            if (state.Buttons)
            {
                var slide = state.Buttons.transform.Find("Spieleabend_GuessSlide");
                if (!slide && can) slide = CreateSlideButton(state, target.PlayerId);
                if (slide)
                {
                    slide.gameObject.SetActive(can && !voted);
                    AnimateSlide(state, slide);
                }
            }

            // 2) Knopf direkt auf der Karte (nach dem Abstimmen)
            var card = state.transform.Find("Spieleabend_GuessButton");
            if (!card && can && voted && state.CancelButton) card = CreateCardButton(state, target.PlayerId);
            if (card) card.gameObject.SetActive(can && voted);
        }
    }

    private Transform CreateSlideButton(PlayerVoteArea state, byte targetId)
    {
        // Basis: Judge-Knopf (gleiches Design wie die Abstimm-Knoepfe), sonst der X-Knopf
        var source = state.JudgeOverruleButton ? state.JudgeOverruleButton.gameObject : state.CancelButton ? state.CancelButton.gameObject : null;
        if (!source) return null;

        var go = Object.Instantiate(source, state.Buttons.transform);
        go.name = "Spieleabend_GuessSlide";
        foreach (var gavel in go.GetComponentsInChildren<JudgeGavel>(true)) Object.Destroy(gavel); // Judge-Logik entfernen
        foreach (var child in go.GetComponentsInChildren<Transform>(true))
            if (child != go.transform && child.name.ToLower().Contains("comms")) child.gameObject.SetActive(false);

        // Zielposition: neben Haekchen/X, eine Knopfbreite weiter aussen
        Vector3 target;
        if (state.JudgeOverruleButton && source == state.JudgeOverruleButton.gameObject)
            target = state.Buttons.transform.InverseTransformPoint(state.JudgeOverruleButton.transform.position);
        else
        {
            var confirm = state.Buttons.transform.InverseTransformPoint(state.ConfirmButton.transform.position);
            var cancel = state.Buttons.transform.InverseTransformPoint(state.CancelButton.transform.position);
            target = cancel + (cancel - confirm);
        }
        go.transform.localPosition = target;
        SetupGuessButton(go, targetId);
        slideTargets[go.transform.Pointer] = target;
        return go.transform;
    }

    private readonly Dictionary<System.IntPtr, Vector3> slideTargets = new();

    /// <summary>Faehrt beim Aufklappen der Karte vom X-Knopf aus nach aussen.</summary>
    private void AnimateSlide(PlayerVoteArea state, Transform slide)
    {
        if (!slideTargets.TryGetValue(slide.Pointer, out var target)) return;
        bool open = state.Buttons.activeInHierarchy;
        if (!open)
        {
            slideStart.Remove(state.Pointer);
            return;
        }
        if (!slideStart.ContainsKey(state.Pointer)) slideStart[state.Pointer] = Time.time;
        float t = Mathf.Clamp01((Time.time - slideStart[state.Pointer]) / SlideTime);
        var from = state.CancelButton ? state.Buttons.transform.InverseTransformPoint(state.CancelButton.transform.position) : target;
        slide.localPosition = Vector3.Lerp(from, target, 1f - (1f - t) * (1f - t));
    }

    private Transform CreateCardButton(PlayerVoteArea state, byte targetId)
    {
        var go = Object.Instantiate(state.CancelButton.gameObject, state.transform);
        go.name = "Spieleabend_GuessButton";
        go.transform.localPosition = new Vector3(1.05f, 0.03f, -1.3f);
        go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
        SetupGuessButton(go, targetId);
        return go.transform;
    }

    private void SetupGuessButton(GameObject go, byte targetId)
    {
        go.SetActive(true);
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!sr.sprite) continue;
            var sprite = ButtonIcons.Get("guess", sr.sprite);
            if (sprite) sr.sprite = sprite;
        }

        var button = go.GetComponent<PassiveButton>();
        if (!button) return;
        button.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        button.OnClick.AddListener((System.Action)(() =>
        {
            selectedTarget = targetId;
            open = true;
            guessScroll = 0;   // Liste oben beginnen
            guessCategory = -1; // Orakel: wieder mit der Kategorie-Auswahl starten
            Sfx.Play(Sfx.Investigate);
        }));
    }

    private GUIStyle meetingStatusStyle;

    /// <summary>Wie viele Schüsse in DIESEM Meeting noch möglich sind (Pro-Meeting-Limit UND Gesamtschüsse berücksichtigt).</summary>
    protected int ShotsPossibleThisMeeting
    {
        get
        {
            int perMeeting = AllowMultiPerMeeting ? PerMeetingCap : 1;
            int leftThisMeeting = Mathf.Max(0, perMeeting - guessesThisMeeting);
            return Mathf.Min(leftThisMeeting, shotsLeft);
        }
    }

    /// <summary>Dauer-Anzeige im Meeting (Standard: mögliche Schüsse). Guter/Böser Guesser zeigen nur das.</summary>
    protected virtual string MeetingStatusText() =>
        Lang.T($"Schüsse möglich: {ShotsPossibleThisMeeting}", $"Shots available: {ShotsPossibleThisMeeting}");

    public override void DrawGUI()
    {
        var localPlayer = PlayerControl.LocalPlayer;

        // Anzeige erst, wenn die Meeting-Animation vorbei ist und die Besprechung/Abstimmung läuft (nicht während Animating/Results).
        var mh = MeetingHud.Instance;
        bool meetingReady = mh && (mh.state == MeetingHud.MeetingStates.Discussion
                                   || mh.state == MeetingHud.MeetingStates.NotVoted
                                   || mh.state == MeetingHud.MeetingStates.Voted);
        if (IsLocalRole && meetingReady && localPlayer && IsAlive(localPlayer))
        {
            meetingStatusStyle ??= new GUIStyle(Ui.Header) { alignment = TextAnchor.MiddleCenter, fontSize = 30, fontStyle = FontStyle.Bold };
            // auf Höhe der "Voting Ends In"-Zeit (unten).
            GUI.Label(new Rect(Ui.ScreenWidth / 2f - 420f, 892f, 840f, 44f),
                $"<color={NameHex}>{MeetingStatusText()}</color>", meetingStatusStyle);
        }

        if (!CanShoot || !open || selectedTarget == None)
        {
            if (!CanShoot) open = false;
            return;
        }

        var local = PlayerControl.LocalPlayer;
        var target = FindPlayer(selectedTarget);
        if (!CanTarget(target, local))
        {
            open = false;
            selectedTarget = None;
            return;
        }

        const int columns = 3;
        const float rowH = 52f, headerH = 100f, footerH = 70f, margin = 50f;

        // Orakel: erst Crew/Impostor/Neutral wählen; dann nur diese Rollen (A-Z).
        List<(string Label, byte Kind, byte Id)> options;
        if (UseCategoryMenu)
        {
            var all = GuessablesCat().ToList();
            if (guessCategory < 0)
            {
                DrawCategoryPicker(target, all);
                return;
            }
            options = all.Where(o => o.Cat == guessCategory)
                         .OrderBy(o => PlainName(o.Label), System.StringComparer.CurrentCultureIgnoreCase)
                         .Select(o => (o.Label, o.Kind, o.Id)).ToList();
        }
        else options = Guessables().ToList();
        int rows = (options.Count + columns - 1) / columns;
        float w = 3 * 230 + 60;

        // Wunschhöhe; aber nie über den Bildschirm hinaus (Rand oben/unten), Rest wird scrollbar.
        float wantH = headerH + rows * rowH + footerH;
        float maxH = 1080f - 2f * margin;
        bool scroll = wantH > maxH;
        float h = scroll ? maxH : wantH;
        var panel = new Rect((Ui.ScreenWidth - w) / 2f, (1080f - h) / 2f, w, h);
        Ui.Block(panel);
        GUI.Box(panel, new GUIContent(""), Ui.Panel);

        GUI.Label(new Rect(panel.x + 30, panel.y + 18, w - 120, 40), target.Data.PlayerName + Lang.T(" ist ...", " is ..."), Ui.Header);
        GUI.Label(new Rect(panel.x + 30, panel.y + 58, w - 60, 30), GuessHint(), Ui.Small);
        if (Ui.Button(new Rect(panel.xMax - 70, panel.y + 15, 50, 50), "X"))
        {
            open = false;
            selectedTarget = None;
            return;
        }

        // Sichtbarer Listenbereich zwischen Kopf und Fußzeile.
        float viewTop = panel.y + headerH;
        float viewBottom = panel.yMax - footerH;
        float viewH = viewBottom - viewTop;
        int rowsVisible = Mathf.Max(1, Mathf.FloorToInt(viewH / rowH));
        int maxScroll = Mathf.Max(0, rows - rowsVisible);

        // Mausrad über dem Fenster scrollt die Liste.
        var ev = Event.current;
        if (scroll && ev != null && ev.type == EventType.ScrollWheel && panel.Contains(ev.mousePosition))
        {
            guessScroll += ev.delta.y > 0f ? 1 : -1;
            ev.Use();
        }
        guessScroll = Mathf.Clamp(guessScroll, 0, maxScroll);

        int startRow = guessScroll;
        int startIndex = startRow * columns;
        int endIndex = Mathf.Min(options.Count, (startRow + rowsVisible) * columns);
        for (int i = startIndex; i < endIndex; i++)
        {
            var (label, kind, id) = options[i];
            int displayRow = (i / columns) - startRow;
            var rect = new Rect(panel.x + 30 + (i % columns) * 230, viewTop + displayRow * rowH, 220, 46);
            if (GUI.Button(rect, new GUIContent(label), Ui.ButtonStyle))
            {
                SendToHost(ActionGuess, target.PlayerId, kind, id);
                shotsLeft--;
                shotThisMeeting = true;
                guessesThisMeeting++;
                open = false;
                selectedTarget = None;
                return;
            }
        }

        // Scroll-Hinweis (mehr Rollen ober-/unterhalb).
        if (scroll)
        {
            scrollHintStyle ??= new GUIStyle(Ui.Small) { alignment = TextAnchor.MiddleCenter };
            if (startRow > 0)
                GUI.Label(new Rect(panel.xMax - 190, viewTop - 26, 160, 24), "▲ " + Lang.T("mehr", "more"), scrollHintStyle);
            if (startRow < maxScroll)
                GUI.Label(new Rect(panel.xMax - 190, viewBottom + 2, 160, 24), "▼ " + Lang.T("mehr", "more"), scrollHintStyle);
        }

        if (Ui.Button(new Rect(panel.x + 30, panel.yMax - 62, 220, 46), Lang.T("Abbrechen", "Cancel")))
        {
            open = false;
            selectedTarget = None;
        }

        // Orakel: zurück zur Kategorie-Auswahl (Menü wechseln)
        if (UseCategoryMenu && Ui.Button(new Rect(panel.x + 30 + 240, panel.yMax - 62, 260, 46), "◀ " + Lang.T("Kategorie", "Category")))
        {
            guessCategory = -1;
            guessScroll = 0;
        }
    }

    /// <summary>Orakel: kleines Fenster zur Wahl von Crew / Impostor / Neutral (nur vorhandene Kategorien).</summary>
    private void DrawCategoryPicker(PlayerControl target, List<(string Label, byte Kind, byte Id, int Cat)> all)
    {
        var cats = new List<(int Cat, string Label, string Hex)>();
        if (all.Any(o => o.Cat == CatCrew)) cats.Add((CatCrew, Lang.T("Crew", "Crew"), "#8CFFFF"));
        if (all.Any(o => o.Cat == CatImpostor)) cats.Add((CatImpostor, "Impostor", "#FF1919"));
        if (all.Any(o => o.Cat == CatNeutral)) cats.Add((CatNeutral, Lang.T("Neutral", "Neutral"), "#C8A0FF"));

        float w = 3 * 230 + 60;
        float h = 120f + cats.Count * 64f + 30f;
        var panel = new Rect((Ui.ScreenWidth - w) / 2f, (1080f - h) / 2f, w, h);
        Ui.Block(panel);
        GUI.Box(panel, new GUIContent(""), Ui.Panel);

        GUI.Label(new Rect(panel.x + 30, panel.y + 18, w - 120, 40), target.Data.PlayerName + Lang.T(" ist ...", " is ..."), Ui.Header);
        GUI.Label(new Rect(panel.x + 30, panel.y + 58, w - 60, 30), Lang.T("Welche Art Rolle? (dann A-Z)", "Which kind of role? (then A-Z)"), Ui.Small);
        if (Ui.Button(new Rect(panel.xMax - 70, panel.y + 15, 50, 50), "X"))
        {
            open = false;
            selectedTarget = None;
            return;
        }

        for (int i = 0; i < cats.Count; i++)
        {
            var (cat, label, hex) = cats[i];
            var rect = new Rect(panel.x + 30, panel.y + 100 + i * 64, w - 60, 54);
            if (GUI.Button(rect, new GUIContent($"<color={hex}>{label}</color>"), Ui.ButtonStyle))
            {
                guessCategory = cat;
                guessScroll = 0;
                return;
            }
        }
    }

    private int guessScroll;
    private GUIStyle scrollHintStyle;

    private IEnumerable<(string Label, byte Kind, byte Id)> Guessables()
    {
        // Mod-Rollen: nur was AKTIV ist (wie "Aktuell aktiv" = Count > 0), ohne Anpassungen/Geister (IsExtra).
        foreach (var role in RoleRegistry.Roles.Where(r => !r.IsExtra && r.Count.Int > 0))
        {
            if (IsEvil && role.Team == RoleTeam.Impostor) continue;                         // keine eigenen Leute
            if (!IsEvil && role.Team == RoleTeam.Crewmate && !canGuessCrewRoles.Bool) continue;
            yield return (role.ColoredName, KindMod, role.Id);
        }

        // Spiel-Rollen nur, wenn sie in den Einstellungen wirklich aktiviert sind (Impostor-Basis immer möglich).
        if (!IsEvil)
            foreach (var type in VanillaImpostorRoles)
                if (type == RoleTypes.Impostor || VanillaEnabled(type))
                    yield return ($"<color=#FF1919>{type}</color>", KindVanilla, (byte)type);

        if (canGuessCrewRoles.Bool)
            foreach (var type in VanillaCrewRoles)
                if (VanillaEnabled(type))
                    yield return ($"<color=#8CFFFF>{type}</color>", KindVanilla, (byte)type);

        // Boeser Guesser: auch "Crewmate" - richtig nur, wenn der Spieler wirklich keine Rolle hat
        if (IsEvil && canGuessCrewmate != null && canGuessCrewmate.Bool)
            yield return ("<color=#FFFFFF>Crewmate</color>", KindVanilla, (byte)RoleTypes.Crewmate);
    }

    // Kategorien fürs Orakel-Menü
    protected const int CatCrew = 0, CatImpostor = 1, CatNeutral = 2;

    /// <summary>Wie Guessables(), aber jede Rolle mit ihrer Kategorie (Crew/Impostor/Neutral) - fürs Orakel-Menü.</summary>
    private IEnumerable<(string Label, byte Kind, byte Id, int Cat)> GuessablesCat()
    {
        foreach (var role in RoleRegistry.Roles.Where(r => !r.IsExtra && r.Count.Int > 0))
        {
            if (IsEvil && role.Team == RoleTeam.Impostor) continue;
            if (!IsEvil && role.Team == RoleTeam.Crewmate && !canGuessCrewRoles.Bool) continue;
            int cat = role.Team == RoleTeam.Impostor ? CatImpostor : role.Team == RoleTeam.Neutral ? CatNeutral : CatCrew;
            yield return (role.ColoredName, KindMod, role.Id, cat);
        }

        if (!IsEvil)
            foreach (var type in VanillaImpostorRoles)
                if (type == RoleTypes.Impostor || VanillaEnabled(type))
                    yield return ($"<color=#FF1919>{type}</color>", KindVanilla, (byte)type, CatImpostor);

        if (canGuessCrewRoles.Bool)
            foreach (var type in VanillaCrewRoles)
                if (VanillaEnabled(type))
                    yield return ($"<color=#8CFFFF>{type}</color>", KindVanilla, (byte)type, CatCrew);

        if (IsEvil && canGuessCrewmate != null && canGuessCrewmate.Bool)
            yield return ("<color=#FFFFFF>Crewmate</color>", KindVanilla, (byte)RoleTypes.Crewmate, CatCrew);
    }

    /// <summary>Farbcodes/Namen zum Sortieren entfernen (&lt;color&gt;...&lt;/color&gt; raus).</summary>
    private static string PlainName(string label) =>
        System.Text.RegularExpressions.Regex.Replace(label ?? "", "<.*?>", "").Trim();

    private int guessCategory = -1; // -1 = Kategorie-Auswahl, sonst CatCrew/CatImpostor/CatNeutral

    /// <summary>Ist eine Spiel-Rolle (Shapeshifter, Engineer ...) in den Einstellungen aktiviert?</summary>
    private static bool VanillaEnabled(RoleTypes type)
    {
        try
        {
            var ro = GameOptionsManager.Instance?.CurrentGameOptions?.RoleOptions;
            return ro != null && ro.GetNumPerGame(type) > 0 && ro.GetChancePerGame(type) > 0;
        }
        catch { return false; }
    }

    // ---------- Host-Entscheidung ----------

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionAnnounce && data.Length >= 2)
        {
            // data[1]: 0/1 = jemand ist gestorben (Kill-Sound), 2 = Fehlschuss ohne Tod (Fail-Sound)
            Sfx.Play(data[1] == 2 ? Sfx.Fail : Sfx.Kill);
            return;
        }

        if (action != ActionGuess || data.Length < 3 || !AmHost) return;
        if (!RoleRegistry.Has(sender, this) || !IsAlive(sender) || !MeetingHud.Instance) return;

        var target = FindPlayer(data[0]);
        if (!CanTarget(target, sender)) return;

        shotsUsed.TryGetValue(sender.PlayerId, out int used);
        if (used >= shots.Int) return;
        shotsUsed[sender.PlayerId] = used + 1;

        bool correct = Matches(target, data[1], data[2]);
        ResolveGuess(sender, target, correct, used + 1);
    }

    /// <summary>Host: entscheidet, was ein Treffer/Fehlschuss bewirkt. Standard: richtig -> Ziel stirbt, falsch -> Guesser stirbt.</summary>
    protected virtual void ResolveGuess(PlayerControl guesser, PlayerControl target, bool correct, int usedTotal)
    {
        var dying = correct ? target : guesser;
        SendToAll(ActionAnnounce, dying.PlayerId, (byte)(correct ? 1 : 0));
        KillSystem.KillNoBody(dying, guesser);
    }

    private static bool Matches(PlayerControl target, byte kind, byte id)
    {
        var main = RoleRegistry.GetRole(target);
        // Die Rolle muss genau stimmen: "Impostor" nur bei einem Impostor ohne Rolle
        if (kind == KindMod) return main != null && main.Id == id;
        if (main != null) return false;
        if (FreeplayTest.IsFakeImpostor(target)) return (RoleTypes)id == RoleTypes.Impostor;
        return target.Data.Role && target.Data.Role.Role == (RoleTypes)id;
    }
}
