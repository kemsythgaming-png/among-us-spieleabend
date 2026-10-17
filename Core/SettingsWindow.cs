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

        if (Input.GetKeyDown(KeyCode.F2)) RoleGuide.Toggle();
        SettingsPanel.UpdateInput();
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

        Notifier.Draw();
        RoleGuide.Draw();
        SettingsPanel.Draw();
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
    private const float W = 1160f, H = 800f, Row = 50f;

    private enum TabKind { General, Game, GameRoles, ModRoles }

    private static readonly (string Label, TabKind Kind, RoleTeam Team)[] Tabs =
    {
        ("Allgemein|General", TabKind.General, default),
        ("Spiel|Game", TabKind.Game, default),
        ("Spiel-Rollen|Game roles", TabKind.GameRoles, default),
        ("Crew", TabKind.ModRoles, RoleTeam.Crewmate),
        ("Impostor", TabKind.ModRoles, RoleTeam.Impostor),
        ("Neutral", TabKind.ModRoles, RoleTeam.Neutral),
        ("Anpassungen|Modifiers", TabKind.ModRoles, RoleTeam.Modifier),
    };

    private static bool open;
    private static int tab;
    private static int selected;

    /// <summary>Fenster auf/zu (Knopf oben rechts oder F1).</summary>
    public static void Toggle()
    {
        if (!SettingsWindow.Available) return;
        open = !open;
        if (open) RoleGuide.Close(); // nie zwei Fenster uebereinander
    }

    public static void Close() => open = false;

    public static void UpdateInput()
    {
        if (!SettingsWindow.Available)
        {
            open = false;
            return;
        }
        if (Input.GetKeyDown(KeyCode.F1)) Toggle();
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
        GUI.Label(new Rect(win.x + 30, win.y + 18, W - 120, 44), $"{Plugin.Name} - {Lang.T("Einstellungen", "Settings")}", Ui.Title);
        if (GUI.Button(new Rect(win.xMax - 74, win.y + 18, 52, 52), new GUIContent("X"), Ui.CloseButton)) open = false;
        if (!SettingsWindow.CanEdit)
            GUI.Label(new Rect(win.x + 30, win.y + 58, W - 60, 30), Lang.T("Nur der Host kann die Einstellungen ändern.", "Only the host can change the settings."), Ui.Small);

        float tx = win.x + 30;
        for (int i = 0; i < Tabs.Length; i++)
        {
            if (GUI.Button(new Rect(tx, win.y + 92, 150, 46), new GUIContent(Lang.Pick(Tabs[i].Label)), i == tab ? Ui.TabSelected : Ui.Tab))
            {
                tab = i;
                selected = 0;
            }
            tx += 157;
        }

        GUI.Box(new Rect(win.x + 20, win.y + 150, W - 40, H - 170), new GUIContent(""), Ui.Inner);
        var area = new Rect(win.x + 30, win.y + 165, W - 60, H - 195);
        switch (Tabs[tab].Kind)
        {
            case TabKind.General: DrawGeneral(area); break;
            case TabKind.Game: DrawGame(area); break;
            case TabKind.GameRoles: DrawGameRoles(area); break;
            default: DrawModRoles(area, Tabs[tab].Team); break;
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
            DrawSetting(option, area.x + 10, y, 440);
            y += Row;
        }

        // Rechts daneben: Hinweis und (im Freeplay) der Test-Bereich - nie ueber den Einstellungen
        var side = new Rect(area.x + 720, area.y, area.width - 730, area.height);
        GUI.Label(new Rect(side.x, side.y, side.width, 90),
            Lang.T("Mod-Rollen gehen nur an normale Crewmates/Impostor - die Rollen aus dem Spiel (Reiter \"Spiel-Rollen\") bleiben unverändert.",
                   "Mod roles only go to plain crewmates/impostors - the game's own roles (tab \"Game roles\") stay unchanged."), Ui.Small);

        if (FreeplayTest.IsFreeplay)
        {
            var box = new Rect(side.x - 10, side.y + 110, side.width + 10, 150);
            GUI.Box(box, new GUIContent(""), Ui.Panel);
            DrawFreeplay(new Rect(box.x + 18, box.y + 12, box.width - 36, 130));
        }
    }

    private static void DrawGame(Rect area)
    {
        // Zwei Spalten
        var settings = VanillaGameSettings.All;
        int perColumn = (settings.Count + 1) / 2;
        for (int i = 0; i < settings.Count; i++)
        {
            float x = area.x + (i < perColumn ? 0 : area.width / 2f + 10);
            float y = area.y + (i % perColumn) * Row;
            DrawSetting(settings[i], x, y, 270);
        }
    }

    private static void DrawGameRoles(Rect area)
    {
        var roles = VanillaRoleEntry.All;
        selected = Mathf.Clamp(selected, 0, roles.Count - 1);

        float y = area.y;
        for (int i = 0; i < roles.Count; i++)
        {
            var r = roles[i];
            string label = $"<color=#{ColorUtility.ToHtmlStringRGB(r.Color)}>{r.Name}</color>  ({r.Summary})";
            if (Ui.Button(new Rect(area.x, y, 340, 44), label, i == selected)) selected = i;
            y += 49;
        }

        var role = roles[selected];
        DrawDetailHeader(new Rect(area.x + 370, area.y, area.width - 370, 110), role.Name, role.Color, role.Description);
        y = area.y + 110;
        foreach (var setting in role.Settings)
        {
            DrawSetting(setting, area.x + 370, y, 340);
            y += Row;
        }
    }

    private static void DrawModRoles(Rect area, RoleTeam team)
    {
        var roles = RoleRegistry.Roles.Where(r => r.Team == team).ToList();
        if (roles.Count == 0)
        {
            GUI.Label(new Rect(area.x, area.y, 600, 40), Lang.T("Keine Rollen in diesem Team.", "No roles in this team."), Ui.Text);
            return;
        }
        selected = Mathf.Clamp(selected, 0, roles.Count - 1);

        // Bei vielen Rollen (z. B. Anpassungen) werden die Zeilen schmaler, damit alles ins Fenster passt
        float rowH = Mathf.Min(49f, area.height / roles.Count);
        float y = area.y;
        for (int i = 0; i < roles.Count; i++)
        {
            var r = roles[i];
            string label = $"{r.ColoredName}  ({r.Count.Int}x, {r.Chance.Int}%)";
            if (Ui.Button(new Rect(area.x, y, 340, rowH - 5f), label, i == selected)) selected = i;
            y += rowH;
        }

        var role = roles[selected];
        DrawDetailHeader(new Rect(area.x + 370, area.y, area.width - 370, 110), role.Name, role.NameColor, role.Description);
        y = area.y + 110;
        foreach (var option in role.Options)
        {
            DrawSetting(option, area.x + 370, y, 340);
            y += Row;
        }
    }

    // ---------- Bausteine ----------

    private static void DrawDetailHeader(Rect area, string name, Color color, string description)
    {
        Ui.Header.normal.textColor = color;
        GUI.Label(new Rect(area.x, area.y, area.width, 40), name, Ui.Header);
        Ui.Header.normal.textColor = Color.white;
        GUI.Label(new Rect(area.x, area.y + 42, area.width, 60), description, Ui.Small);
    }

    private static void DrawSetting(ISetting setting, float x, float y, float labelWidth)
    {
        GUI.Label(new Rect(x, y + 7, labelWidth, 40), setting.Name, Ui.Text);
        float cx = x + labelWidth + 10;
        bool canEdit = SettingsWindow.CanEdit || setting.AlwaysEditable;

        if (setting.IsToggle)
        {
            if (Ui.Button(new Rect(cx, y, 230, 44), setting.Display, setting.Bool) && canEdit) setting.Change(1);
            return;
        }

        if (Ui.Button(new Rect(cx, y, 50, 44), "-") && canEdit) setting.Change(-1);
        GUI.Label(new Rect(cx + 55, y + 7, 120, 40), setting.Display, Ui.Header);
        if (Ui.Button(new Rect(cx + 180, y, 50, 44), "+") && canEdit) setting.Change(1);
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
    public static GUIStyle Inner, Tab, TabSelected, CloseButton;
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
