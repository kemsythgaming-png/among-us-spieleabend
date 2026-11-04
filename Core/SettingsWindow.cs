using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Unsichtbares Unity-Objekt, das jedes Frame unsere Fenster zeichnet (IMGUI).
/// Hinweis: Diese Klasse wird als Il2Cpp-Klasse registriert - hier nur einfache Felder/Parameter verwenden.
/// Die eigentliche Logik liegt in SettingsPanel, RoleGuide usw.
/// </summary>
public class SettingsWindow : MonoBehaviour
{
    public SettingsWindow(IntPtr ptr) : base(ptr) { }

    /// <summary>Fenster darf angezeigt werden: in der Lobby oder im Freeplay.</summary>
    public static bool Available => LobbyBehaviour.Instance || (FreeplayTest.IsFreeplay && ShipStatus.Instance);

    /// <summary>Werte duerfen geaendert werden: Host in der Lobby, oder im Freeplay.</summary>
    public static bool CanEdit
    {
        get
        {
            if (FreeplayTest.IsFreeplay && ShipStatus.Instance) return true;
            return LobbyBehaviour.Instance && AmongUsClient.Instance && AmongUsClient.Instance.AmHost;
        }
    }

    private void Update()
    {
        // Pflicht laut Among Us Mod Policy: Mod-Stempel oben rechts immer anzeigen
        if (ModManager.InstanceExists) ModManager.Instance.ShowModStamp();

        if (Input.GetKeyDown(KeyCode.F2) && !Ui.IsTyping) RoleGuide.Toggle();
        if (Input.GetKeyDown(KeyCode.F3) && !Ui.IsTyping) FreeplayPanel.Toggle();
        if (Input.GetKeyDown(KeyCode.F4) && !Ui.IsTyping) UpdateNotes.Toggle();
        if (Input.GetKeyDown(KeyCode.T) && !Ui.IsTyping) Emotes.ToggleWheel();
        SettingsPanel.UpdateInput();
        try { SurveillanceFeeds.Tick(); } catch { }

        // Laeuft immer (auch waehrend der Rollen-Enthuellung, wenn der HUD noch nicht da ist):
        // im Intro die echte Mod-Rolle schreiben statt "Crewmate/Impostor"
        if (IntroCutscene.Instance) RoleUI.ApplyIntroOverride(IntroCutscene.Instance);
    }

    private void OnGUI()
    {
        if (RoleRegistry.Roles.Count == 0) return;
        Ui.Begin();

        // Spiel-Fenster der eigenen Rollen (z. B. Guesser im Meeting)
        var local = PlayerControl.LocalPlayer;
        if (local && local.Data != null)
        {
            foreach (var role in RoleRegistry.GetAll(local).ToList())
            {
                try { role.DrawGUI(); }
                catch (Exception e) { Plugin.Logger.LogError($"{role.Name}.DrawGUI: {e.Message}"); }
            }
        }

        // Jedes Fenster einzeln absichern: ein Fehler in einem darf die anderen nicht mitnehmen
        Safe("Kameras", SurveillanceFeeds.Draw); // Pfeile der Überwachung (für jeden Zuschauer)
        Safe("BOO-Kleckse", Spieleabend.Roles.BooOverlay.Draw); // zuerst - liegt hinter den Fenstern
        Safe("Hinweise", Notifier.Draw);
        Safe("Sieg", CustomWin.DrawWinBanner);
        Safe("Rollen-Info", RoleGuide.Draw);
        Safe("Einstellungen", SettingsPanel.Draw);
        Safe("Freeplay", FreeplayPanel.Draw);
        Safe("Update-Notes", UpdateNotes.Draw);
        Safe("Emotes", Emotes.Draw);
    }

    private static readonly System.Collections.Generic.HashSet<string> reported = new();

    private static void Safe(string what, Action draw)
    {
        try { draw(); }
        catch (Exception e)
        {
            if (reported.Add(what + e.Message)) Plugin.Logger.LogError($"Fenster {what}: {e}");
        }
    }
}

/// <summary>
/// Einstellungsfenster (Taste F1), aufgebaut wie die Einstellungen im echten Spiel. Reiter:
/// Allgemein (Mod-Regeln) / Spiel (normale Spiel-Einstellungen) / Spiel-Rollen (Engineer, Shapeshifter ...) /
/// Crew / Impostor / Neutral / Anpassungen (unsere Rollen).
/// Nur in der Lobby und im Freeplay. In der Lobby kann nur der Host aendern.
/// </summary>
internal static class SettingsPanel
{
    private const float W = 1160f, H = 800f, Row = 50f, ControlWidth = 280f, ValueWidth = 170f;

    private enum TabKind { General, Game, Roles, Sets }

    private static readonly (string Label, TabKind Kind, RoleTeam Team)[] Tabs =
    {
        ("Allgemein|General", TabKind.General, default),
        ("Spiel|Game", TabKind.Game, default),
        ("Crew", TabKind.Roles, RoleTeam.Crewmate),
        ("Impostor", TabKind.Roles, RoleTeam.Impostor),
        ("Neutral", TabKind.Roles, RoleTeam.Neutral),
        ("Anpassungen|Modifiers", TabKind.Roles, RoleTeam.Modifier),
    };

    private static bool open;
    private static int tab;
    private static int selected;
    private static string search = "";
    private static Vector2 listScroll, detailScroll;

    // Schnell-Einstellung (Reiter "Allgemein"): Zielwerte fuer "alle Chancen"/"alle Anzahl"
    private static int bulkChance = 50, bulkCount = 1;

    /// <summary>Ein Eintrag in der Rollen-Liste: Mod-Rolle, Rolle aus dem Spiel, Zwischenueberschrift oder (bei der Suche) eine einzelne Einstellung.</summary>
    private sealed class Entry
    {
        public string Label;
        public string Name;
        public Color Color = Color.white;
        public string Description = "";
        public List<ISetting> Settings = new();
        public bool IsHeader;
    }

    /// <summary>Fenster auf/zu (Knopf oben rechts oder F1).</summary>
    public static void Toggle()
    {
        if (!SettingsWindow.Available) return;
        open = !open;
        if (open) RoleGuide.Close(); // nie zwei Fenster uebereinander
        else Ui.StopTyping();
    }

    public static void Close()
    {
        if (open) Ui.StopTyping();
        open = false;
    }

    public static void UpdateInput()
    {
        if (!SettingsWindow.Available)
        {
            open = false;
            return;
        }
        if (Input.GetKeyDown(KeyCode.F1) && !Ui.IsTyping) Toggle();
    }

    public static void Draw()
    {
        if (!SettingsWindow.Available) return;

        if (!open)
        {
            // Ersatz-Knopf unten links, falls der Knopf oben rechts fehlt
            if (!HudButtons.SettingsVisible)
            {
                var openRect = new Rect(20, 1080 - 80, 300, 56);
                Ui.Block(openRect);
                if (Ui.Button(openRect, Lang.T("Rollen-Einstellungen (F1)", "Role settings (F1)"))) Toggle();
            }
            return;
        }

        var win = new Rect((Ui.ScreenWidth - W) / 2f, (1080 - H) / 2f, W, H);
        GUI.Box(win, new GUIContent(""), Ui.Panel);
        Ui.Block(win);
        GUI.Label(new Rect(win.x + 30, win.y + 16, W - 160, 56), $"{Plugin.Name} - {Lang.T("Einstellungen", "Settings")}", Ui.Title);
        if (GUI.Button(new Rect(win.xMax - 74, win.y + 18, 52, 52), new GUIContent("X"), Ui.CloseButton))
        {
            Close();
            return;
        }

        if (!SettingsWindow.CanEdit)
            GUI.Label(new Rect(win.x + 30, win.y + 60, W - 520, 30), Lang.T("Nur der Host kann die Einstellungen ändern.", "Only the host can change the settings."), Ui.Small);

        float tx = win.x + 30;
        float tabW = (W - 60f - 6f * (Tabs.Length - 1)) / Tabs.Length; // Reiter gleichmäßig auf die Breite verteilen
        for (int i = 0; i < Tabs.Length; i++)
        {
            bool active = i == tab && search.Length == 0;
            if (GUI.Button(new Rect(tx, win.y + 92, tabW, 46), new GUIContent(Lang.Pick(Tabs[i].Label)), active ? Ui.TabSelected : Ui.Tab))
            {
                tab = i;
                selected = 0;
                search = "";
                Ui.StopTyping();
                listScroll = detailScroll = generalScroll = gameScroll = Vector2.zero;
            }
            tx += tabW + 6f;
        }

        GUI.Box(new Rect(win.x + 20, win.y + 150, W - 40, H - 170), new GUIContent(""), Ui.Inner);
        var area = new Rect(win.x + 30, win.y + 165, W - 60, H - 195);

        if (search.Length > 0)
        {
            DrawEntries(area, SearchResults(search.Trim()));
            return;
        }
        switch (Tabs[tab].Kind)
        {
            case TabKind.General: DrawGeneral(area); break;
            case TabKind.Game: DrawGame(area); break;
            default:
                var team = Tabs[tab].Team;
                DrawTeamBulk(new Rect(area.x, area.y, area.width, 52), team);
                DrawEntries(new Rect(area.x, area.y + 60, area.width, area.height - 60), TeamEntries(team));
                break;
        }
    }

    /// <summary>Schmale Leiste oben in einem Rollen-Reiter: Chance bzw. Anzahl NUR für dieses Team setzen.</summary>
    private static void DrawTeamBulk(Rect r, RoleTeam team)
    {
        bool canEdit = SettingsWindow.CanEdit;
        bool guiBefore = GUI.enabled;
        float x = r.x, y = r.y;

        GUI.Label(new Rect(x, y + 8, 220, 36), Lang.T("<b>Nur dieses Team setzen:</b>", "<b>Set this team only:</b>"), Ui.Small);
        x += 230;

        GUI.Label(new Rect(x, y + 8, 70, 36), Lang.T("Chance", "Chance"), Ui.Small); x += 72;
        GUI.enabled = canEdit;
        if (Ui.Button(new Rect(x, y, 40, 44), "-") && canEdit) bulkChance = Mathf.Clamp(bulkChance - 10, 0, 100);
        GUI.enabled = guiBefore;
        GUI.Label(new Rect(x + 44, y, 60, 44), $"{bulkChance}%", Ui.ValueMid);
        GUI.enabled = canEdit;
        if (Ui.Button(new Rect(x + 108, y, 40, 44), "+") && canEdit) bulkChance = Mathf.Clamp(bulkChance + 10, 0, 100);
        if (Ui.Button(new Rect(x + 154, y, 90, 44), Lang.T("Setzen", "Apply")) && canEdit) BulkRoleOps.SetTeamChances(team, bulkChance);
        x += 260;

        GUI.enabled = guiBefore;
        GUI.Label(new Rect(x, y + 8, 70, 36), Lang.T("Anzahl", "Count"), Ui.Small); x += 72;
        GUI.enabled = canEdit;
        if (Ui.Button(new Rect(x, y, 40, 44), "-") && canEdit) bulkCount = Mathf.Clamp(bulkCount - 1, 0, 15);
        GUI.enabled = guiBefore;
        GUI.Label(new Rect(x + 44, y, 50, 44), $"{bulkCount}", Ui.ValueMid);
        GUI.enabled = canEdit;
        if (Ui.Button(new Rect(x + 98, y, 40, 44), "+") && canEdit) bulkCount = Mathf.Clamp(bulkCount + 1, 0, 15);
        if (Ui.Button(new Rect(x + 144, y, 90, 44), Lang.T("Setzen", "Apply")) && canEdit) BulkRoleOps.SetTeamCounts(team, bulkCount);

        GUI.enabled = guiBefore;
    }

    // ---------- Reiter "Sets" ----------
    private static string setName = "";
    private static float setsScroll;
    private static bool testModeTested = true; // Standard: die GETESTETEN sind an

    private static void DrawSets(Rect area)
    {
        bool canEdit = SettingsWindow.CanEdit;
        float x = area.x, y = area.y, w = area.width;

        // Test-Umschalter: mit einem Klick alle ungetesteten bzw. alle getesteten Rollen einschalten
        GUI.Label(new Rect(x, y, w, 30), Lang.T(
            $"<b>Test-Umschalter</b>  <color=#9AA0AE>({TestStatus.UntestedCount} {Lang.T("ungetestet", "untested")})</color>",
            $"<b>Test toggle</b>  <color=#9AA0AE>({TestStatus.UntestedCount} untested)</color>"), Ui.Text);
        y += 36;
        GUI.enabled = canEdit;
        string toggleLbl = testModeTested
            ? Lang.T("Aktiv: GETESTETE Rollen  -  klicken: ungetestete einschalten", "Active: TESTED roles  -  click: enable untested")
            : Lang.T("Aktiv: UNGETESTETE Rollen  -  klicken: getestete einschalten", "Active: UNTESTED roles  -  click: enable tested");
        if (Ui.Button(new Rect(x, y, 935, 46), toggleLbl) && canEdit) { testModeTested = !testModeTested; TestStatus.Apply(testModeTested); }
        GUI.enabled = true;
        y += 64;

        // Name eingeben + aktuelle Einstellung als Set speichern
        GUI.Label(new Rect(x, y + 6, 170, 36), Lang.T("Set-Name:", "Set name:"), Ui.Text);
        setName = Ui.SearchField(new Rect(x + 150, y, 340, 44), setName, Lang.T("z. B. Halloween", "e.g. Halloween"));
        bool nameOk = setName.Trim().Length > 0;
        GUI.enabled = canEdit && nameOk;
        if (Ui.Button(new Rect(x + 505, y, 430, 44), Lang.T("Aktuelle Einstellung als Set speichern", "Save current config as set")) && canEdit && nameOk)
            RoleSets.SaveCurrent(setName);
        GUI.enabled = true;
        y += 58;

        GUI.Label(new Rect(x, y, w, 30), Lang.T("Set wählen und Aktivieren = nur diese Rollen an, der Rest aus.", "Pick a set and Activate = only these roles on, the rest off."), Ui.Small);
        y += 40;

        var sets = RoleSets.Sets;
        if (sets.Count == 0)
        {
            GUI.Label(new Rect(x, y, w, 36), Lang.T("Noch keine Sets. Stell die Rollen ein und speichere oben ein Set.", "No sets yet. Configure the roles and save a set above."), Ui.Text);
            return;
        }

        var view = new Rect(x, y, w, area.yMax - y);
        const float rowH = 56f;
        setsScroll = Ui.Scroll(view, sets.Count * rowH, setsScroll);
        float ry = 0;
        foreach (var s in sets.ToList())
        {
            if (Ui.RowVisible(view, ry, rowH - 6, setsScroll))
            {
                float sy = view.y + ry - setsScroll;
                int roleCount = s.Roles.Count(kv => kv.Value.Count > 0);
                GUI.Label(new Rect(x + 10, sy + 12, 340, 36), $"<b>{s.Name}</b>  <color=#9AA0AE>({roleCount} {Lang.T("Rollen", "roles")})</color>", Ui.Text);

                GUI.enabled = canEdit;
                if (Ui.Button(new Rect(x + 380, sy + 6, 170, 46), Lang.T("Aktivieren", "Activate")) && canEdit) RoleSets.Apply(s.Name);
                bool canRename = canEdit && setName.Trim().Length > 0;
                GUI.enabled = canRename;
                if (Ui.Button(new Rect(x + 560, sy + 6, 230, 46), Lang.T("Auf Namen umbenennen", "Rename to name")) && canRename) RoleSets.Rename(s.Name, setName);
                GUI.enabled = canEdit;
                if (Ui.Button(new Rect(x + 800, sy + 6, 130, 46), Lang.T("Löschen", "Delete")) && canEdit) RoleSets.Delete(s.Name);
                GUI.enabled = true;
            }
            ry += rowH;
        }
    }

    // ---------- Reiter ----------

    private static Vector2 generalScroll, gameScroll;

    /// <summary>Eine einspaltige, scrollbare Liste aus Zeilen (Kategorie-Überschriften + Einstellungen + Sonder-Widgets).</summary>
    private static void DrawColumn(Rect area, List<(float H, System.Action<float, float> Draw)> rows, ref Vector2 scroll)
    {
        float total = 0f;
        foreach (var r in rows) total += r.H;
        scroll.y = Ui.Scroll(area, total, scroll.y);
        float y = 0f;
        foreach (var r in rows)
        {
            if (Ui.RowVisible(area, y, r.H - 4f, scroll.y)) r.Draw(area.y + y - scroll.y, area.width - 16f);
            y += r.H;
        }
    }

    /// <summary>Schnell-Einstellung in EINER Zeile: Beschriftung, [-] Wert [+] und ein "Setzen"-Knopf.</summary>
    private static void DrawBulkLine(float x, float y, float width, string label, string value, System.Action minus, System.Action plus, System.Action apply)
    {
        bool canEdit = SettingsWindow.CanEdit;
        bool guiBefore = GUI.enabled;
        GUI.Label(new Rect(x, y + 8, 150, 32), label + ":", Ui.Small);
        float bx = x + 160;
        GUI.enabled = canEdit;
        if (Ui.Button(new Rect(bx, y, 44, 46), "-") && canEdit) minus();
        GUI.enabled = guiBefore;
        GUI.Label(new Rect(bx + 48, y, 70, 46), value, Ui.ValueMid);
        GUI.enabled = canEdit;
        if (Ui.Button(new Rect(bx + 122, y, 44, 46), "+") && canEdit) plus();
        if (Ui.Button(new Rect(bx + 176, y, Mathf.Max(120f, width - 176f - 160f), 46), Lang.T("Setzen", "Apply")) && canEdit) apply();
        GUI.enabled = guiBefore;
    }

    private static void DrawGeneral(Rect area)
    {
        var rows = new List<(float H, System.Action<float, float> Draw)>();
        void Head(string t) => rows.Add((58f, (yy, w) => GUI.Label(new Rect(area.x + 4, yy + 20, w, 34), $"<b>{t}</b>", Ui.Header)));
        void Set(ISetting s) => rows.Add((Row, (yy, w) => DrawSetting(s, area.x + 10, yy, w - ControlWidth - 30f)));

        Head(Lang.T("Sprache & Anzeige", "Language & display"));
        Set(Lang.LanguageSetting);
        Set(GeneralOptions.DeadSeeRoles);
        Set(GeneralOptions.ShowOwnRole);
        Set(GeneralOptions.Halloween);
        Set(GeneralOptions.CameraShake);

        Head(Lang.T("Rollen-Verteilung", "Role distribution"));
        Set(GeneralOptions.ModRolesChance);
        Set(GeneralOptions.MaxCrewRoles);
        Set(GeneralOptions.MaxImpostorRoles);
        Set(GeneralOptions.MaxNeutralRoles);
        Set(GeneralOptions.MaxModifiers);
        Set(GeneralOptions.TaskSheriffChance);

        Head(Lang.T("Spielregeln", "Game rules"));
        Set(GeneralOptions.FirstDeathShield);

        Head(Lang.T("Schnell einstellen (alle Mod-Rollen)", "Quick set (all mod roles)"));
        rows.Add((56f, (yy, w) =>
        {
            bool guiBefore = GUI.enabled; GUI.enabled = SettingsWindow.CanEdit;
            if (Ui.Button(new Rect(area.x + 10, yy, Mathf.Min(560f, w), 46), Lang.T("Alle Chancen zufällig", "Randomize all chances")) && SettingsWindow.CanEdit) BulkRoleOps.RandomizeChances();
            GUI.enabled = guiBefore;
        }));
        rows.Add((62f, (yy, w) => DrawBulkLine(area.x + 10, yy, Mathf.Min(560f, w), Lang.T("Alle Chancen", "All chances"), $"{bulkChance}%",
            () => bulkChance = Mathf.Clamp(bulkChance - 10, 0, 100), () => bulkChance = Mathf.Clamp(bulkChance + 10, 0, 100), () => BulkRoleOps.SetAllChances(bulkChance))));
        rows.Add((62f, (yy, w) => DrawBulkLine(area.x + 10, yy, Mathf.Min(560f, w), Lang.T("Alle Anzahl", "All counts"), $"{bulkCount}",
            () => bulkCount = Mathf.Clamp(bulkCount - 1, 0, 15), () => bulkCount = Mathf.Clamp(bulkCount + 1, 0, 15), () => BulkRoleOps.SetAllCounts(bulkCount))));

        DrawColumn(area, rows, ref generalScroll);
    }

    /// <summary>Schnell-Einstellung: alle Mod-Rollen-Chancen/-Anzahlen auf einmal setzen (nur Host/Freeplay).</summary>
    private static void DrawBulk(float x, float y, float width)
    {
        bool canEdit = SettingsWindow.CanEdit;
        bool guiBefore = GUI.enabled;

        // Ueberschrift
        GUI.Label(new Rect(x, y, width, 30), Lang.T("<b>Schnell einstellen</b>", "<b>Quick set</b>"), Ui.Text);
        y += 46;

        // Alle Chancen zufaellig
        GUI.enabled = canEdit;
        if (Ui.Button(new Rect(x, y, width, 44), Lang.T("Alle Chancen zufällig", "Randomize all chances")) && canEdit)
            BulkRoleOps.RandomizeChances();
        y += 66;

        // Alle Chancen auf denselben Wert
        y = DrawBulkRow(x, y, width, Lang.T("Alle Chancen", "All chances"), $"{bulkChance}%",
            () => bulkChance = Mathf.Clamp(bulkChance - 10, 0, 100),
            () => bulkChance = Mathf.Clamp(bulkChance + 10, 0, 100),
            () => BulkRoleOps.SetAllChances(bulkChance), canEdit, guiBefore);

        // Alle Anzahlen auf denselben Wert
        y = DrawBulkRow(x, y, width, Lang.T("Alle Anzahl", "All counts"), $"{bulkCount}",
            () => bulkCount = Mathf.Clamp(bulkCount - 1, 0, 15),
            () => bulkCount = Mathf.Clamp(bulkCount + 1, 0, 15),
            () => BulkRoleOps.SetAllCounts(bulkCount), canEdit, guiBefore);

        GUI.enabled = guiBefore;
    }

    /// <summary>Eine Zeile der Schnell-Einstellung: Beschriftung, darunter [-] Wert [+] und ein "Setzen"-Knopf.</summary>
    private static float DrawBulkRow(float x, float y, float width, string label, string value,
        Action minus, Action plus, Action apply, bool canEdit, bool guiBefore)
    {
        GUI.enabled = guiBefore;
        GUI.Label(new Rect(x, y, width, 26), label + ":", Ui.Small);
        y += 34;
        GUI.enabled = canEdit;
        if (Ui.Button(new Rect(x, y, 44, 44), "-") && canEdit) minus();
        GUI.enabled = guiBefore;
        GUI.Label(new Rect(x + 48, y, 66, 44), value, Ui.ValueMid);
        GUI.enabled = canEdit;
        if (Ui.Button(new Rect(x + 118, y, 44, 44), "+") && canEdit) plus();
        if (Ui.Button(new Rect(x + 170, y, width - 170, 44), Lang.T("Setzen", "Apply")) && canEdit) apply();
        return y + 60;
    }

    private static void DrawGame(Rect area)
    {
        var rows = new List<(float H, System.Action<float, float> Draw)>();
        void Head(string t) => rows.Add((58f, (yy, w) => GUI.Label(new Rect(area.x + 4, yy + 20, w, 34), $"<b>{t}</b>", Ui.Header)));
        void Set(ISetting s) => rows.Add((Row, (yy, w) => DrawSetting(s, area.x + 10, yy, w - ControlWidth - 30f)));

        Head(Lang.T("Spiel-Einstellungen", "Game settings"));
        foreach (var s in VanillaGameSettings.All) Set(s);

        Head(Lang.T("Mod-Zusätze fürs Spiel", "Mod game extras"));
        foreach (var o in GeneralOptions.GameTab) Set(o);

        // ----- Rollen-Sets & Test-Umschalter (früher eigener Reiter) -----
        Head(Lang.T("Rollen-Sets & Test-Umschalter", "Role sets & test toggle"));

        rows.Add((52f, (yy, w) =>
        {
            bool ce = SettingsWindow.CanEdit;
            GUI.Label(new Rect(area.x + 10, yy + 8, 360, 32), Lang.T($"Test-Umschalter  ({TestStatus.UntestedCount} ungetestet)", $"Test toggle  ({TestStatus.UntestedCount} untested)"), Ui.Small);
            GUI.enabled = ce;
            string lbl = testModeTested
                ? Lang.T("Aktiv: GETESTETE  –  klicken: ungetestete", "Active: TESTED  –  click: untested")
                : Lang.T("Aktiv: UNGETESTETE  –  klicken: getestete", "Active: UNTESTED  –  click: tested");
            if (Ui.Button(new Rect(area.x + 380, yy, Mathf.Min(620f, w - 380f), 46), lbl) && ce) { testModeTested = !testModeTested; TestStatus.Apply(testModeTested); }
            GUI.enabled = true;
        }));

        rows.Add((52f, (yy, w) =>
        {
            GUI.Label(new Rect(area.x + 10, yy + 6, 110, 36), Lang.T("Set-Name:", "Set name:"), Ui.Text);
            setName = Ui.SearchField(new Rect(area.x + 120, yy, 300, 46), setName, Lang.T("z. B. Halloween", "e.g. Halloween"));
            bool ok = SettingsWindow.CanEdit && setName.Trim().Length > 0;
            GUI.enabled = ok;
            if (Ui.Button(new Rect(area.x + 430, yy, Mathf.Min(460f, w - 430f), 46), Lang.T("Aktuelle Einstellung als Set speichern", "Save current config as set")) && ok) RoleSets.SaveCurrent(setName);
            GUI.enabled = true;
        }));

        rows.Add((34f, (yy, w) => GUI.Label(new Rect(area.x + 10, yy, w, 28), Lang.T("Set wählen und Aktivieren = nur diese Rollen an, der Rest aus.", "Pick a set and Activate = only these roles on, the rest off."), Ui.Small)));

        if (RoleSets.Sets.Count == 0)
            rows.Add((40f, (yy, w) => GUI.Label(new Rect(area.x + 10, yy, w, 32), Lang.T("Noch keine Sets gespeichert.", "No sets saved yet."), Ui.Small)));
        else
            foreach (var setItem in RoleSets.Sets.ToList())
            {
                var s = setItem;
                rows.Add((56f, (yy, w) =>
                {
                    bool ce = SettingsWindow.CanEdit;
                    int rc = s.Roles.Count(kv => kv.Value.Count > 0);
                    GUI.Label(new Rect(area.x + 10, yy + 10, 320, 36), $"<b>{s.Name}</b>  <color=#9AA0AE>({rc} {Lang.T("Rollen", "roles")})</color>", Ui.Text);
                    GUI.enabled = ce;
                    if (Ui.Button(new Rect(area.x + 340, yy + 4, 160, 46), Lang.T("Aktivieren", "Activate")) && ce) RoleSets.Apply(s.Name);
                    bool canRename = ce && setName.Trim().Length > 0;
                    GUI.enabled = canRename;
                    if (Ui.Button(new Rect(area.x + 510, yy + 4, 230, 46), Lang.T("Auf Namen umbenennen", "Rename to name")) && canRename) RoleSets.Rename(s.Name, setName);
                    GUI.enabled = ce;
                    if (Ui.Button(new Rect(area.x + 750, yy + 4, 130, 46), Lang.T("Löschen", "Delete")) && ce) RoleSets.Delete(s.Name);
                    GUI.enabled = true;
                }));
            }

        DrawColumn(area, rows, ref gameScroll);
    }

    /// <summary>Rollen eines Teams: zuerst die Mod-Rollen, darunter die Rollen aus dem Spiel.</summary>
    private static List<Entry> TeamEntries(RoleTeam team)
    {
        var list = RoleRegistry.Roles.Where(r => r.Team == team).Select(FromRole).ToList();
        if (team is RoleTeam.Crewmate or RoleTeam.Impostor)
        {
            var vanilla = VanillaRoleEntry.All.Where(v => v.IsImpostor == (team == RoleTeam.Impostor)).ToList();
            if (vanilla.Count > 0)
            {
                list.Add(new Entry { IsHeader = true, Label = Lang.T("Rollen aus dem Spiel", "Roles from the game") });
                list.AddRange(vanilla.Select(FromVanilla));
            }
        }
        return list;
    }

    /// <summary>Suche ueber alle Rollen (Mod und Spiel) und alle allgemeinen/Spiel-Einstellungen.</summary>
    private static List<Entry> SearchResults(string text)
    {
        bool Match(string s) => !string.IsNullOrEmpty(s) && s.IndexOf(text, System.StringComparison.OrdinalIgnoreCase) >= 0;
        var list = new List<Entry>();
        list.AddRange(RoleRegistry.Roles.Where(r => Match(r.Name) || Match(r.Description)).Select(FromRole));
        list.AddRange(VanillaRoleEntry.All.Where(v => Match(v.Name) || Match(v.Description)).Select(FromVanilla));

        var settings = new List<(ISetting Setting, string Where)> { (Lang.LanguageSetting, Lang.T("Allgemein", "General")) };
        settings.AddRange(GeneralOptions.Options.Select(o => ((ISetting)o, Lang.T("Allgemein", "General"))));
        settings.AddRange(VanillaGameSettings.All.Select(o => (o, Lang.T("Spiel", "Game"))));
        foreach (var (setting, where) in settings.Where(s => Match(s.Setting.Name)))
            list.Add(new Entry
            {
                Label = $"{setting.Name}  <color=#9AA0AE>({where})</color>",
                Name = setting.Name,
                Description = Lang.T($"Einstellung im Reiter \"{where}\".", $"Setting in the \"{where}\" tab."),
                Settings = { setting },
            });
        return list;
    }

    private static Entry FromRole(CustomRole r) => new()
    {
        Label = $"{r.ColoredName}  <color=#9AA0AE>({r.Count.Int}x, {r.Chance.Int}%)</color>",
        Name = r.Name,
        Color = r.NameColor,
        Description = r.Description,
        Settings = r.Options.Cast<ISetting>().ToList(),
    };

    private static Entry FromVanilla(VanillaRoleEntry v) => new()
    {
        Label = $"<color=#{ColorUtility.ToHtmlStringRGB(v.Color)}>{v.Name}</color>  <color=#9AA0AE>({v.Summary})</color>",
        Name = v.Name,
        Color = v.Color,
        Description = v.Description,
        Settings = v.Settings,
    };

    /// <summary>Links die Liste (scrollt), rechts Name, Beschreibung und Einstellungen des gewaehlten Eintrags.</summary>
    private static void DrawEntries(Rect area, List<Entry> entries)
    {
        if (entries.Count(e => !e.IsHeader) == 0)
        {
            GUI.Label(new Rect(area.x, area.y, 700, 40), search.Length > 0 ? Lang.T("Nichts gefunden.", "Nothing found.") : Lang.T("Keine Rollen in diesem Team.", "No roles in this team."), Ui.Text);
            return;
        }
        var clickable = entries.Where(e => !e.IsHeader).ToList();
        selected = Mathf.Clamp(selected, 0, clickable.Count - 1);

        const float listWidth = 380f, rowH = 50f;
        var view = new Rect(area.x, area.y, listWidth, area.height);
        float listWidthInner = listWidth - 16;
        listScroll.y = Ui.Scroll(view, entries.Count * rowH, listScroll.y);
        float y = 0;
        int index = 0;
        foreach (var e in entries)
        {
            bool visible = Ui.RowVisible(view, y, rowH - 6, listScroll.y);
            float sy0 = view.y + y - listScroll.y;
            if (e.IsHeader)
            {
                if (visible) GUI.Label(new Rect(view.x + 4, sy0 + 12, listWidthInner, 34), $"<b>{e.Label}</b>", Ui.Small);
                y += rowH;
                continue;
            }
            int my = index++;
            if (visible && GUI.Button(new Rect(view.x, sy0, listWidthInner, rowH - 6), new GUIContent(e.Label), my == selected ? Ui.Selected : Ui.ButtonStyle))
            {
                selected = my;
                detailScroll = Vector2.zero;
            }
            y += rowH;
        }

        var entry = clickable[selected];
        float dx = area.x + listWidth + 24, dw = area.xMax - dx;
        DrawDetailHeader(new Rect(dx, area.y, dw, 110), entry.Name, entry.Color, entry.Description);

        var detailView = new Rect(dx, area.y + 112, dw, area.height - 112);
        detailScroll.y = Ui.Scroll(detailView, entry.Settings.Count * Row, detailScroll.y);
        float sy = 0;
        foreach (var setting in entry.Settings)
        {
            if (Ui.RowVisible(detailView, sy, 44, detailScroll.y))
                DrawSetting(setting, detailView.x, detailView.y + sy - detailScroll.y, dw - 16 - ControlWidth - 20);
            sy += Row;
        }
    }

    // ---------- Bausteine ----------

    private static void DrawDetailHeader(Rect area, string name, Color color, string description)
    {
        Ui.Header.normal.textColor = color;
        GUI.Label(new Rect(area.x, area.y, area.width, 40), name, Ui.Header);
        Ui.Header.normal.textColor = Color.white;
        GUI.Label(new Rect(area.x, area.y + 42, area.width, 66), description, Ui.Small);
    }

    /// <summary>Eine Einstellung: Name links (bei langen Namen kleiner und umgebrochen), rechts -/Wert/+ oder ein An/Aus-Knopf.</summary>
    private static void DrawSetting(ISetting setting, float x, float y, float labelWidth)
    {
        var name = new GUIContent(setting.Name);
        if (Ui.Width(Ui.Text, name) <= labelWidth) GUI.Label(new Rect(x, y + 7, labelWidth, 40), name, Ui.Text);
        else GUI.Label(new Rect(x, y - 2, labelWidth, 50), name, Ui.LabelWrap);

        float cx = x + labelWidth + 10;
        bool canEdit = SettingsWindow.CanEdit || setting.AlwaysEditable;

        // Gaeste (nicht Host) sehen die Werte, koennen sie aber nicht aendern -> Knoepfe ausgegraut
        bool guiBefore = GUI.enabled;
        GUI.enabled = canEdit;

        if (setting.IsToggle)
        {
            if (Ui.Button(new Rect(cx, y, ControlWidth, 44), setting.Display, setting.Bool) && canEdit) setting.Change(1);
            GUI.enabled = guiBefore;
            return;
        }

        if (Ui.Button(new Rect(cx, y, 50, 44), "-") && canEdit) setting.Change(-1);
        GUI.enabled = true; // der Wert selbst bleibt immer gut lesbar
        // Wert: so gross wie moeglich, ohne abgeschnitten zu werden
        var value = new GUIContent(setting.Display);
        var style = Ui.Width(Ui.ValueBig, value) <= ValueWidth ? Ui.ValueBig
                  : Ui.Width(Ui.ValueMid, value) <= ValueWidth ? Ui.ValueMid : Ui.ValueSmall;
        GUI.Label(new Rect(cx + 55, y, ValueWidth, 44), value, style);
        GUI.enabled = canEdit;
        if (Ui.Button(new Rect(cx + 55 + ValueWidth + 5, y, 50, 44), "+") && canEdit) setting.Change(1);
        GUI.enabled = guiBefore;
    }

}

/// <summary>Gemeinsame IMGUI-Styles. Koordinaten immer fuer 1080 Pixel Hoehe (wird skaliert).</summary>
public static class Ui
{
    public static GUIStyle Panel, Title, Header, Text, Small, ButtonStyle, Selected;
    public static GUIStyle ListButton, ListSelected; // groessere Knoepfe fuer lange Listen (gut lesbar)
    private static Texture2D panelTex, buttonTex, selectedTex;

    public static float ScreenWidth => Screen.width / (Screen.height / 1080f);

    // Bereiche, ueber denen Klicks NICHT ans Spiel gehen (sonst klickt man z. B. im Meeting durch das Fenster auf eine Spielerkarte)
    private static readonly List<Rect> blocked = new();
    private static readonly List<Rect> building = new();

    internal static void Begin()
    {
        InitStyles();
        float scale = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        if (Event.current.type == EventType.Layout)
        {
            blocked.Clear();
            blocked.AddRange(building);
            building.Clear();
        }
    }

    /// <summary>Diesen Bereich (1080p-Koordinaten) fuer Spiel-Klicks sperren - fuer jedes Fenster/jeden Button aufrufen.</summary>
    public static void Block(Rect rect)
    {
        if (Event.current != null && Event.current.type == EventType.Layout) building.Add(rect);
    }

    public static bool IsMouseBlocked()
    {
        if (blocked.Count == 0) return false;
        float scale = Screen.height / 1080f;
        var mouse = Input.mousePosition;
        var point = new Vector2(mouse.x / scale, (Screen.height - mouse.y) / scale);
        foreach (var rect in blocked)
            if (rect.Contains(point)) return true;
        return false;
    }

    public static bool Button(Rect rect, string label) => GUI.Button(rect, new GUIContent(label), ButtonStyle);

    public static bool Button(Rect rect, string label, bool selected) => GUI.Button(rect, new GUIContent(label), selected ? Selected : ButtonStyle);

    /// <summary>Innenflaeche (z. B. Liste oder Detailbereich) im selben Stil, etwas heller.</summary>
    public static GUIStyle Inner, Tab, TabSelected, CloseButton, LabelWrap, ValueBig, ValueMid, ValueSmall, SearchStyle;

    /// <summary>Tippt gerade jemand in ein Textfeld (Suche)? Dann gehen Tasten nicht ans Spiel.</summary>
    // GUI.TextField gibt es in diesem Spiel nicht (vom Spiel entfernt) - darum ein eigenes, einfaches Suchfeld
    private static bool typing;

    /// <summary>Tippt gerade jemand ins Suchfeld? Dann gehen Tasten nicht ans Spiel.</summary>
    public static bool IsTyping => typing;

    public static void StopTyping() => typing = false;

    /// <summary>
    /// Suchfeld: anklicken zum Tippen (Tasten kommen aus Input.inputString), Rueck-Taste loescht,
    /// Enter/Esc oder ein Klick daneben beendet das Tippen.
    /// </summary>
    public static string SearchField(Rect rect, string text, string placeholder)
    {
        text ??= "";
        var e = Event.current;
        if (e != null && e.type == EventType.MouseDown) typing = rect.Contains(e.mousePosition);

        GUI.Box(rect, new GUIContent(""), typing ? SearchFocused : SearchStyle);

        // Tasten nur einmal pro Bild verarbeiten (OnGUI laeuft mehrmals pro Bild)
        if (typing && e != null && e.type == EventType.Repaint)
        {
            string typed;
            try { typed = Input.inputString ?? ""; }
            catch { typed = ""; }
            foreach (char c in typed)
            {
                if (c == '\b') { if (text.Length > 0) text = text.Substring(0, text.Length - 1); }
                else if (c == '\n' || c == '\r') typing = false;
                else if (!char.IsControl(c) && text.Length < 40) text += c;
            }
            if (Input.GetKeyDown(KeyCode.Escape)) typing = false;
        }

        var inner = new Rect(rect.x + 16, rect.y + 4, rect.width - 24, rect.height - 8);
        if (text.Length == 0 && !typing) GUI.Label(inner, $"<color=#8A90A0>{placeholder}</color>", SearchText);
        else
        {
            bool caret = typing && Time.realtimeSinceStartup % 1f < 0.55f;
            GUI.Label(inner, text + (caret ? "|" : ""), SearchText);
        }
        return text;
    }

    public static GUIStyle SearchFocused, SearchText, ScrollThumb;

    /// <summary>
    /// Eigenes Scrollen (GUI.BeginScrollView ist im Spiel evtl. entfernt): Mausrad ueber dem Bereich verschiebt den Inhalt,
    /// rechts zeigt ein schmaler Balken die Position. Gibt den neuen Versatz zurueck.
    /// </summary>
    public static float Scroll(Rect view, float contentHeight, float offset)
    {
        float max = Mathf.Max(0f, contentHeight - view.height);
        var e = Event.current;
        if (e != null && e.type == EventType.ScrollWheel && view.Contains(e.mousePosition)) offset += e.delta.y * 25f;
        offset = Mathf.Clamp(offset, 0f, max);
        if (max > 0f)
        {
            float h = Mathf.Max(30f, view.height * view.height / contentHeight);
            float y = view.y + (view.height - h) * (offset / max);
            GUI.Box(new Rect(view.xMax - 8f, y, 6f, h), new GUIContent(""), ScrollThumb);
        }
        return offset;
    }

    private static bool measureBroken;

    /// <summary>Textbreite - falls das Messen im Spiel fehlt, grob geschaetzt (halbe Schriftgroesse pro Zeichen).</summary>
    public static float Width(GUIStyle style, GUIContent content)
    {
        if (!measureBroken)
        {
            try { return style.CalcSize(content).x; }
            catch (System.Exception e)
            {
                measureBroken = true;
                Plugin.Logger.LogWarning($"Textbreite messen geht nicht, schaetze sie: {e.Message}");
            }
        }
        return (content.text ?? "").Length * style.fontSize * 0.52f;
    }

    /// <summary>Liegt eine Zeile (y relativ zum Inhalt) ganz im sichtbaren Bereich?</summary>
    public static bool RowVisible(Rect view, float y, float h, float offset) => y - offset >= -0.5f && y - offset + h <= view.height + 0.5f;
    private static Font gameFont;

    private static void InitStyles()
    {
        if (Panel != null && panelTex) return;

        // Among-Us-Stil: dunkle Flaechen mit dicker, heller, runder Kontur; Auswahl leuchtet gruen
        var white = new Color(0.93f, 0.95f, 0.97f, 1f);
        var green = new Color(0.36f, 0.94f, 0.33f, 1f);
        panelTex = MakeRounded(new Color(0.10f, 0.11f, 0.16f, 0.98f), white, 24, 5);
        var innerTex = MakeRounded(new Color(0.15f, 0.16f, 0.22f, 1f), new Color(0.45f, 0.48f, 0.56f, 1f), 16, 3);
        buttonTex = MakeRounded(new Color(0.20f, 0.22f, 0.29f, 1f), white, 12, 3);
        var hoverTex = MakeRounded(new Color(0.27f, 0.30f, 0.38f, 1f), white, 12, 3);
        selectedTex = MakeRounded(new Color(0.16f, 0.32f, 0.17f, 1f), green, 12, 4);
        var closeTex = MakeRounded(new Color(0.75f, 0.15f, 0.15f, 1f), white, 24, 4);
        var closeHoverTex = MakeRounded(new Color(0.9f, 0.25f, 0.25f, 1f), white, 24, 4);

        gameFont = FindGameFont();

        Panel = new GUIStyle(GUI.skin.box) { border = Offset(26), padding = Offset(0) };
        Panel.normal.background = panelTex;
        Inner = new GUIStyle(GUI.skin.box) { border = Offset(18), padding = Offset(0) };
        Inner.normal.background = innerTex;

        Title = MakeLabel(32, FontStyle.Bold);
        Title.alignment = TextAnchor.MiddleLeft;   // vertikal zentriert -> Oberkante wird nicht abgeschnitten
        Header = MakeLabel(26, FontStyle.Bold);
        Header.alignment = TextAnchor.MiddleLeft;
        Text = MakeLabel(24, FontStyle.Normal);
        Small = MakeLabel(20, FontStyle.Normal);
        Small.wordWrap = true;
        Small.normal.textColor = new Color(0.82f, 0.84f, 0.9f);

        ButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 21, richText = true, border = Offset(14), padding = Offset(6) };
        if (gameFont) ButtonStyle.font = gameFont;
        ButtonStyle.normal.background = buttonTex;
        ButtonStyle.hover.background = hoverTex;
        ButtonStyle.active.background = selectedTex;
        ButtonStyle.normal.textColor = Color.white;
        ButtonStyle.hover.textColor = Color.white;
        ButtonStyle.active.textColor = Color.white;

        Selected = new GUIStyle(ButtonStyle);
        Selected.normal.background = selectedTex;
        Selected.hover.background = selectedTex;

        ListButton = new GUIStyle(ButtonStyle) { fontSize = 28 };
        ListSelected = new GUIStyle(Selected) { fontSize = 28 };

        Tab = new GUIStyle(ButtonStyle) { fontSize = 23, fontStyle = FontStyle.Bold };
        TabSelected = new GUIStyle(Tab);
        TabSelected.normal.background = selectedTex;
        TabSelected.hover.background = selectedTex;

        LabelWrap = MakeLabel(19, FontStyle.Normal);
        LabelWrap.wordWrap = true;
        LabelWrap.alignment = TextAnchor.MiddleLeft;
        ValueBig = MakeLabel(28, FontStyle.Bold);
        ValueMid = MakeLabel(23, FontStyle.Bold);
        ValueSmall = MakeLabel(18, FontStyle.Bold);
        ValueSmall.wordWrap = true;
        foreach (var v in new[] { ValueBig, ValueMid, ValueSmall }) v.alignment = TextAnchor.MiddleCenter;

        SearchStyle = new GUIStyle(GUI.skin.box) { border = Offset(18), padding = Offset(0) };
        SearchStyle.normal.background = innerTex;
        SearchFocused = new GUIStyle(SearchStyle);
        SearchFocused.normal.background = selectedTex;
        ScrollThumb = new GUIStyle(GUI.skin.box) { border = Offset(3), padding = Offset(0) };
        ScrollThumb.normal.background = MakeRounded(new Color(0.75f, 0.78f, 0.85f, 0.9f), new Color(0.75f, 0.78f, 0.85f, 0.9f), 3, 0);
        SearchText = MakeLabel(24, FontStyle.Normal);
        SearchText.alignment = TextAnchor.MiddleLeft;
        SearchText.clipping = TextClipping.Clip;

        CloseButton = new GUIStyle(ButtonStyle) { fontSize = 26, fontStyle = FontStyle.Bold, border = Offset(24) };
        CloseButton.normal.background = closeTex;
        CloseButton.hover.background = closeHoverTex;
        CloseButton.active.background = closeHoverTex;
    }

    private static RectOffset Offset(int v)
    {
        var o = new RectOffset();
        o.left = o.right = o.top = o.bottom = v;
        return o;
    }

    private static GUIStyle MakeLabel(int size, FontStyle fontStyle)
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fontStyle, richText = true };
        if (gameFont) style.font = gameFont;
        style.normal.textColor = Color.white;
        return style;
    }

    /// <summary>
    /// Schrift des Spiels fuer die Fenster, falls die Schriftdatei im Spiel steckt (sonst bleibt die Standardschrift).
    /// </summary>
    private static Font FindGameFont()
    {
        try
        {
            Font best = null;
            foreach (var asset in Resources.FindObjectsOfTypeAll<TMPro.TMP_FontAsset>())
            {
                if (!asset || !asset.sourceFontFile) continue;
                string n = asset.name.ToLowerInvariant();
                if (n.Contains("brook") || n.Contains("amongus") || n.Contains("among us"))
                {
                    best = asset.sourceFontFile;
                    break;
                }
                best ??= asset.sourceFontFile;
            }
            if (best) Plugin.Logger.LogInfo($"Fenster-Schrift: {best.name}");
            return best;
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogWarning($"Fenster-Schrift: {e.Message}");
            return null;
        }
    }

    /// <summary>Abgerundetes Rechteck mit Kontur (wird per 9-Slice auf jede Groesse gestreckt).</summary>
    private static Texture2D MakeRounded(Color fill, Color outline, int radius, int outlineWidth)
    {
        int size = radius * 2 + 8;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        float half = size / 2f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // Abstand zum Rand des abgerundeten Rechtecks (negativ = innen)
            float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
            float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
            float alpha = Mathf.Clamp01(0.5f - outside);
            float edge = Mathf.Clamp01(outside + outlineWidth + 0.5f);
            var c = Color.Lerp(fill, outline, edge);
            c.a *= alpha;
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }
}
