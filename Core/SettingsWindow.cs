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
        SettingsPanel.UpdateInput();

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
        Safe("Hinweise", Notifier.Draw);
        Safe("Rollen-Info", RoleGuide.Draw);
        Safe("Einstellungen", SettingsPanel.Draw);
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

    private enum TabKind { General, Game, Roles }

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
        GUI.Label(new Rect(win.x + 30, win.y + 18, W - 520, 44), $"{Plugin.Name} - {Lang.T("Einstellungen", "Settings")}", Ui.Title);
        if (GUI.Button(new Rect(win.xMax - 74, win.y + 18, 52, 52), new GUIContent("X"), Ui.CloseButton))
        {
            Close();
            return;
        }

        if (!SettingsWindow.CanEdit)
            GUI.Label(new Rect(win.x + 30, win.y + 60, W - 520, 30), Lang.T("Nur der Host kann die Einstellungen ändern.", "Only the host can change the settings."), Ui.Small);

        float tx = win.x + 30;
        for (int i = 0; i < Tabs.Length; i++)
        {
            bool active = i == tab && search.Length == 0;
            if (GUI.Button(new Rect(tx, win.y + 92, 176, 46), new GUIContent(Lang.Pick(Tabs[i].Label)), active ? Ui.TabSelected : Ui.Tab))
            {
                tab = i;
                selected = 0;
                search = "";
                Ui.StopTyping();
                listScroll = detailScroll = Vector2.zero;
            }
            tx += 182;
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
            default: DrawEntries(area, TeamEntries(Tabs[tab].Team)); break;
        }
    }

    // ---------- Reiter ----------

    private static void DrawGeneral(Rect area)
    {
        float y = area.y;
        DrawSetting(Lang.LanguageSetting, area.x + 10, y, 440);
        y += Row;
        foreach (var option in GeneralOptions.Options)
        {
            if (GeneralOptions.GameTab.Contains(option)) continue; // steht im Reiter "Spiel"
            DrawSetting(option, area.x + 10, y, 440);
            y += Row;
        }

        // Rechts daneben: Hinweis und (im Freeplay) der Test-Bereich - nie ueber den Einstellungen
        var side = new Rect(area.x + 760, area.y, area.width - 770, area.height);
        GUI.Label(new Rect(side.x, side.y, side.width, 110),
            Lang.T("Mod-Rollen gehen nur an normale Crewmates/Impostor - die Rollen aus dem Spiel (in den Reitern Crew/Impostor unten) bleiben unverändert.",
                   "Mod roles only go to plain crewmates/impostors - the game's own roles (bottom of the Crew/Impostor tabs) stay unchanged."), Ui.Small);

        if (FreeplayTest.IsFreeplay)
        {
            var box = new Rect(side.x - 10, side.y + 130, side.width + 10, 150);
            GUI.Box(box, new GUIContent(""), Ui.Panel);
            DrawFreeplay(new Rect(box.x + 18, box.y + 12, box.width - 36, 130));
        }
    }

    private static void DrawGame(Rect area)
    {
        // Zwei Spalten: Spiel-Einstellungen, danach die Mod-Hilfen fuer das Spiel (z. B. Karte durchziehen)
        var settings = new List<ISetting>(VanillaGameSettings.All);
        settings.AddRange(GeneralOptions.GameTab);
        int perColumn = (settings.Count + 1) / 2;
        float columnWidth = area.width / 2f;
        for (int i = 0; i < settings.Count; i++)
        {
            float x = area.x + (i < perColumn ? 0 : columnWidth + 10);
            float y = area.y + (i % perColumn) * Row;
            DrawSetting(settings[i], x, y, columnWidth - ControlWidth - 30);
        }
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

    private static void DrawFreeplay(Rect area)
    {
        GUI.Label(new Rect(area.x, area.y, 320, 36), Lang.T("Freeplay-Test", "Freeplay test"), Ui.Text);
        float y = area.y + 38;

        string roleName = string.IsNullOrEmpty(FreeplayTest.TestRoleName) ? Lang.T("(keine)", "(none)") : FreeplayTest.TestRoleDisplayName;
        if (Ui.Button(new Rect(area.x, y, 40, 38), "<")) FreeplayTest.CycleRole(-1);
        GUI.Label(new Rect(area.x + 50, y + 4, area.width - 100, 36), Lang.T("Testrolle: ", "Test role: ") + roleName, Ui.Small);
        if (Ui.Button(new Rect(area.xMax - 40, y, 40, 38), ">")) FreeplayTest.CycleRole(1);
        y += 44;

        if (Ui.Button(new Rect(area.x, y, 40, 38), "-")) FreeplayTest.ChangeDummyImpostors(-1);
        GUI.Label(new Rect(area.x + 50, y + 4, area.width - 100, 36), $"{FreeplayTest.DummyImpostors} " + Lang.T("Dummy-Impostor", "dummy impostors"), Ui.Small);
        if (Ui.Button(new Rect(area.xMax - 40, y, 40, 38), "+")) FreeplayTest.ChangeDummyImpostors(1);
    }
}

/// <summary>Gemeinsame IMGUI-Styles. Koordinaten immer fuer 1080 Pixel Hoehe (wird skaliert).</summary>
public static class Ui
{
    public static GUIStyle Panel, Title, Header, Text, Small, ButtonStyle, Selected;
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

        Title = MakeLabel(34, FontStyle.Bold);
        Header = MakeLabel(28, FontStyle.Bold);
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
