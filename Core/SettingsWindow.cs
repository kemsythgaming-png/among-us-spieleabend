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
        if (SettingsWindow.Available) open = !open;
    }

    public static void UpdateInput()
    {
        if (!SettingsWindow.Available)
        {
            open = false;
            return;
        }
        if (Input.GetKeyDown(KeyCode.F1)) open = !open;
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
                if (Ui.Button(openRect, Lang.T("Rollen-Einstellungen (F1)", "Role settings (F1)"))) open = true;
            }
            return;
        }

        var win = new Rect((Ui.ScreenWidth - W) / 2f, (1080 - H) / 2f, W, H);
        GUI.Box(win, new GUIContent(""), Ui.Panel);
        Ui.Block(win);
        GUI.Label(new Rect(win.x + 30, win.y + 16, W - 120, 44), $"{Plugin.Name} - {Lang.T("Einstellungen", "Settings")}", Ui.Title);
        if (Ui.Button(new Rect(win.xMax - 70, win.y + 16, 50, 50), "X")) open = false;
        if (!SettingsWindow.CanEdit)
            GUI.Label(new Rect(win.x + 30, win.y + 58, W - 60, 30), Lang.T("Nur der Host kann die Einstellungen ändern.", "Only the host can change the settings."), Ui.Small);

        float tx = win.x + 30;
        for (int i = 0; i < Tabs.Length; i++)
        {
            if (Ui.Button(new Rect(tx, win.y + 92, 150, 46), Lang.Pick(Tabs[i].Label), i == tab))
            {
                tab = i;
                selected = 0;
            }
            tx += 157;
        }

        var area = new Rect(win.x + 30, win.y + 160, W - 60, H - 180);
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

        GUI.Label(new Rect(area.x + 10, y + 10, area.width - 20, 60),
            Lang.T("Mod-Rollen gehen nur an normale Crewmates/Impostor - die Rollen aus dem Spiel (Reiter \"Spiel-Rollen\") bleiben unverändert.",
                   "Mod roles only go to plain crewmates/impostors - the game's own roles (tab \"Game roles\") stay unchanged."), Ui.Small);

        if (FreeplayTest.IsFreeplay) DrawFreeplay(new Rect(area.x + 10, area.yMax - 120, 600, 120));
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
        DrawDetailHeader(new Rect(area.x + 370, area.y, area.width - 370, 110), role.Name, role.Color, role.Description);
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
        GUI.Label(new Rect(area.x + 50, y + 4, 240, 36), Lang.T("Testrolle: ", "Test role: ") + roleName, Ui.Small);
        if (Ui.Button(new Rect(area.x + 300, y, 40, 38), ">")) FreeplayTest.CycleRole(1);
        y += 44;

        if (Ui.Button(new Rect(area.x, y, 40, 38), "-")) FreeplayTest.ChangeDummyImpostors(-1);
        GUI.Label(new Rect(area.x + 50, y + 4, 240, 36), $"{FreeplayTest.DummyImpostors} " + Lang.T("Dummy-Impostor", "dummy impostors"), Ui.Small);
        if (Ui.Button(new Rect(area.x + 300, y, 40, 38), "+")) FreeplayTest.ChangeDummyImpostors(1);
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

    private static void InitStyles()
    {
        if (Panel != null && panelTex) return;

        panelTex = MakeTex(new Color(0.08f, 0.09f, 0.13f, 0.96f));
        buttonTex = MakeTex(new Color(0.22f, 0.24f, 0.32f, 1f));
        selectedTex = MakeTex(new Color(0.30f, 0.45f, 0.75f, 1f));

        Panel = new GUIStyle(GUI.skin.box);
        Panel.normal.background = panelTex;

        Title = MakeLabel(32, FontStyle.Bold);
        Header = MakeLabel(26, FontStyle.Bold);
        Text = MakeLabel(24, FontStyle.Normal);
        Small = MakeLabel(20, FontStyle.Normal);
        Small.wordWrap = true;
        Small.normal.textColor = new Color(0.8f, 0.8f, 0.85f);

        ButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 21, richText = true };
        ButtonStyle.normal.background = buttonTex;
        ButtonStyle.hover.background = selectedTex;
        ButtonStyle.active.background = selectedTex;
        ButtonStyle.normal.textColor = Color.white;
        ButtonStyle.hover.textColor = Color.white;

        Selected = new GUIStyle(ButtonStyle);
        Selected.normal.background = selectedTex;
    }

    private static GUIStyle MakeLabel(int size, FontStyle fontStyle)
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fontStyle, richText = true };
        style.normal.textColor = Color.white;
        return style;
    }

    private static Texture2D MakeTex(Color color)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        return tex;
    }
}
