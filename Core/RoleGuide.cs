using System.Linq;
using System.Text;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Rollen-Info (Taste F2 oder Button): erklaert jede Rolle - in der Lobby, waehrend der Runde und im Meeting.
/// Zeigt Beschreibung, Faehigkeiten mit Tasten und die aktuellen Einstellungen des Hosts.
/// Verraet nicht, wer welche Rolle hat.
/// </summary>
internal static class RoleGuide
{
    private const float W = 1500f, H = 900f, ListWidth = 430f, RowH = 50f;

    /// <summary>Reiter oben: wie im Einstellungsfenster, Geisterrollen bekommen einen eigenen.</summary>
    private static readonly (string Label, System.Func<CustomRole, bool> Filter)[] Tabs =
    {
        ("Aktuell aktiv|Active now", r => r.Count.Int > 0), // alles, was eingeschaltet ist (verraet nicht, wer was hat)
        ("Crew", r => r.Team == RoleTeam.Crewmate && !r.IsGhostRole),
        ("Impostor", r => r.Team == RoleTeam.Impostor && !r.IsGhostRole),
        ("Neutral", r => r.Team == RoleTeam.Neutral && !r.IsGhostRole),
        ("Geister|Ghosts", r => r.IsGhostRole),
        ("Anpassungen|Modifiers", r => r.Team == RoleTeam.Modifier),
    };

    private static bool open;
    private static int tab;
    private static int selected = -1;
    private static Vector2 scroll;
    private static VanillaRoleEntry selectedVanilla; // im Reiter "Diese Runde" bzw. in der Suche: gewaehlte Rolle aus dem Spiel
    private static string search = "";
    private static GUIStyle body;

    private static bool Available =>
        PlayerControl.LocalPlayer && RoleRegistry.Roles.Count > 0 && !IntroCutscene.Instance && !ExileController.Instance;

    public static void Toggle()
    {
        if (!Available) return;
        open = !open;
        if (!open) return;
        SettingsPanel.Close(); // nie zwei Fenster uebereinander
        SelectOwnRole();
    }

    public static void Close()
    {
        if (open) Ui.StopTyping();
        open = false;
    }

    private static void SelectOwnRole()
    {
        var own = RoleRegistry.GetDisplayed(PlayerControl.LocalPlayer).FirstOrDefault();
        if (own == null) return;
        selected = RoleRegistry.Roles.IndexOf(own);
        tab = 0; // "Diese Runde" zuerst - dort steht die eigene Rolle auch
        for (int i = 0; i < Tabs.Length; i++)
            if (Tabs[i].Filter(own)) { tab = i; break; }
        selectedVanilla = null;
        scroll = Vector2.zero;
    }

    internal static void Draw()
    {
        if (!Available)
        {
            open = false;
            return;
        }

        // Ersatz-Knopf, falls der Knopf oben rechts fehlt: im Meeting links unter dem Aufgabenbalken, in der Lobby neben "Rollen-Einstellungen", sonst unten links
        Rect buttonRect;
        if (MeetingHud.Instance) buttonRect = new Rect(20, 100, 260, 52);
        else if (SettingsWindow.Available) buttonRect = new Rect(340, 1080 - 80, 260, 56);
        else buttonRect = new Rect(20, 1080 - 80, 260, 56);

        if (!HudButtons.InfoVisible)
        {
            if (Ui.Button(buttonRect, Lang.T("Rollen-Info (F2)", "Role info (F2)"), open)) Toggle();
            Ui.Block(buttonRect);
        }

        if (!open) return;
        body ??= new GUIStyle(Ui.Text) { wordWrap = true, fontSize = 22 };

        var win = new Rect((Ui.ScreenWidth - W) / 2f, (1080 - H) / 2f, W, H);
        GUI.Box(win, new GUIContent(""), Ui.Panel);
        Ui.Block(win);
        GUI.Label(new Rect(win.x + 30, win.y + 18, W - 520, 44), Lang.T("Rollen-Info", "Role info"), Ui.Title);

        // Suchfeld oben rechts: sucht in allen Reitern (Name und Beschreibung)
        string before = search;
        search = Ui.SearchField(new Rect(win.xMax - 74 - 380, win.y + 22, 366, 46), search, Lang.T("Rolle suchen ...", "Search role ..."));
        if (search != before)
        {
            scroll = Vector2.zero;
            selected = -1;
            selectedVanilla = null;
        }
        bool searching = search.Trim().Length > 0;
        if (GUI.Button(new Rect(win.xMax - 74, win.y + 18, 52, 52), new GUIContent("X"), Ui.CloseButton)) open = false;

        // Reiter
        float tx = win.x + 30;
        for (int i = 0; i < Tabs.Length; i++)
        {
            if (GUI.Button(new Rect(tx, win.y + 88, 200, 46), new GUIContent(Lang.Pick(Tabs[i].Label)), i == tab && !searching ? Ui.TabSelected : Ui.Tab))
            {
                tab = i;
                search = "";
                Ui.StopTyping();
                scroll = Vector2.zero;
                selectedVanilla = null;
                var first = RoleRegistry.Roles.FirstOrDefault(Tabs[i].Filter);
                selected = first != null ? RoleRegistry.Roles.IndexOf(first) : -1;
            }
            tx += 208;
        }

        // Links: Liste der Rollen dieses Reiters (scrollt, wenn es viele sind)
        var listBox = new Rect(win.x + 20, win.y + 150, ListWidth + 20, H - 170);
        GUI.Box(listBox, new GUIContent(""), Ui.Inner);
        bool thisGame = tab == 0 && !searching;
        string q = search.Trim();
        bool Match(string s) => !string.IsNullOrEmpty(s) && s.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0;
        var roles = searching
            ? RoleRegistry.Roles.Where(r => Match(r.Name) || Match(r.Description) || Match(r.LongDescription)).ToList()
            : RoleRegistry.Roles.Where(Tabs[tab].Filter).ToList();
        var vanilla = searching ? VanillaRoleEntry.All.Where(v => Match(v.Name) || Match(v.Description)).ToList()
            : thisGame ? VanillaRoleEntry.All.Where(v => v.Count > 0).ToList()
            : new System.Collections.Generic.List<VanillaRoleEntry>();
        if (searching && selected < 0 && selectedVanilla == null)
        {
            if (roles.Count > 0) selected = RoleRegistry.Roles.IndexOf(roles[0]);
            else if (vanilla.Count > 0) selectedVanilla = vanilla[0];
        }
        int rows = roles.Count + vanilla.Count + (thisGame ? 1 : 0);
        var view = new Rect(listBox.x + 10, listBox.y + 12, listBox.width - 20, listBox.height - 24);
        float rowWidth = view.width - 16;
        scroll.y = Ui.Scroll(view, rows * RowH, scroll.y);
        Rect Row(float y, float h) => new(view.x, view.y + y - scroll.y, rowWidth, h);
        bool Shown(float y, float h) => Ui.RowVisible(view, y, h, scroll.y);
        var local = PlayerControl.LocalPlayer;
        float ly = 0;
        if (thisGame)
        {
            // Spaltenkoepfe: Anzahl und Chance stehen sauber untereinander
            if (Shown(ly, 44))
            {
                var head = Row(ly + 8, 34);
                GUI.Label(new Rect(head.x + 16, head.y, head.width - 200, head.height), Lang.T("<b>Rolle</b>", "<b>Role</b>"), ColumnLeft);
                GUI.Label(new Rect(head.xMax - 190, head.y, 80, head.height), Lang.T("<b>Anzahl</b>", "<b>Count</b>"), ColumnRight);
                GUI.Label(new Rect(head.xMax - 100, head.y, 84, head.height), Lang.T("<b>Chance</b>", "<b>Chance</b>"), ColumnRight);
            }
            ly += RowH;
        }
        foreach (var role in roles)
        {
            int index = RoleRegistry.Roles.IndexOf(role);
            string text = role.Count.Int == 0 ? $"<color=#8A8F9C>{role.Name}</color>" : role.ColoredName;
            if (RoleRegistry.Has(local, role) && !role.IsSecret(local)) text += Lang.T("  <color=#FFFFFF>(du)</color>", "  <color=#FFFFFF>(you)</color>");
            if (Shown(ly, RowH - 6))
            {
                var rect = Row(ly, RowH - 6);
                bool clicked = thisGame
                    ? ColumnRow(rect, text, $"{role.Count.Int}x", $"{role.Chance.Int}%", index == selected && selectedVanilla == null)
                    : GUI.Button(rect, new GUIContent(text), index == selected && selectedVanilla == null ? Ui.Selected : Ui.ButtonStyle);
                if (clicked)
                {
                    selected = index;
                    selectedVanilla = null;
                }
            }
            ly += RowH;
        }
        foreach (var v in vanilla)
        {
            string name = $"<color=#{ColorUtility.ToHtmlStringRGB(v.Color)}>{v.Name}</color>";
            if (Shown(ly, RowH - 6))
            {
                var rect = Row(ly, RowH - 6);
                var parts = v.Summary.Split(new[] { ", " }, System.StringSplitOptions.None);
                bool clicked = thisGame && parts.Length == 2
                    ? ColumnRow(rect, name, parts[0], parts[1], v == selectedVanilla)
                    : GUI.Button(rect, new GUIContent(name), v == selectedVanilla ? Ui.Selected : Ui.ButtonStyle);
                if (clicked) selectedVanilla = v;
            }
            ly += RowH;
        }
        if (thisGame && rows == 1)
            GUI.Label(Row(ly, 40), Lang.T("Keine Sonderrollen eingeschaltet.", "No special roles enabled."), Ui.Small);

        // Rechts: Details
        var detailBox = new Rect(listBox.xMax + 14, listBox.y, win.xMax - listBox.xMax - 34, listBox.height);
        GUI.Box(detailBox, new GUIContent(""), Ui.Inner);
        if (selectedVanilla != null && (thisGame || searching))
        {
            DrawVanillaDetails(selectedVanilla, new Rect(detailBox.x + 24, detailBox.y + 18, detailBox.width - 48, detailBox.height - 36));
            return;
        }
        if (selected < 0 || selected >= RoleRegistry.Roles.Count || (!searching && !Tabs[tab].Filter(RoleRegistry.Roles[selected])))
        {
            GUI.Label(new Rect(detailBox.x + 24, detailBox.y + 20, detailBox.width - 48, 40),
                searching && roles.Count + vanilla.Count == 0 ? Lang.T("Nichts gefunden.", "Nothing found.") : roles.Count == 0 ? Lang.T("Keine Rollen in diesem Reiter.", "No roles in this tab.") : Lang.T("Wähle links eine Rolle.", "Pick a role on the left."), Ui.Text);
            return;
        }
        DrawDetails(RoleRegistry.Roles[selected], new Rect(detailBox.x + 24, detailBox.y + 18, detailBox.width - 48, detailBox.height - 36));
    }

    private static GUIStyle columnLeft, columnRight;
    private static GUIStyle ColumnLeft => columnLeft ??= new GUIStyle(Ui.Text) { fontSize = 22, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
    private static GUIStyle ColumnRight => columnRight ??= new GUIStyle(Ui.Text) { fontSize = 22, alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(0.75f, 0.78f, 0.85f) } };

    /// <summary>Zeile wie ein Knopf, aber mit Spalten: Name links, Anzahl und Chance rechts untereinander ausgerichtet.</summary>
    private static bool ColumnRow(Rect rect, string name, string count, string chance, bool isSelected)
    {
        bool clicked = GUI.Button(rect, new GUIContent(""), isSelected ? Ui.Selected : Ui.ButtonStyle);
        GUI.Label(new Rect(rect.x + 16, rect.y, rect.width - 210, rect.height), name, ColumnLeft);
        GUI.Label(new Rect(rect.xMax - 190, rect.y, 80, rect.height), count, ColumnRight);
        GUI.Label(new Rect(rect.xMax - 100, rect.y, 84, rect.height), chance, ColumnRight);
        return clicked;
    }

    private static void DrawVanillaDetails(VanillaRoleEntry v, Rect area)
    {
        Ui.Header.normal.textColor = v.Color;
        GUI.Label(new Rect(area.x, area.y, area.width, 44), v.Name, Ui.Header);
        Ui.Header.normal.textColor = Color.white;
        string team = v.IsImpostor ? "<color=#FF1919>Impostor</color>" : "<color=#8CFFFF>Crew</color>";
        GUI.Label(new Rect(area.x, area.y + 46, area.width, 30), $"Team: {team}  <color=#9AA0AE>{Lang.T("(Rolle aus dem Spiel)", "(role from the game)")}</color>", Ui.Small);
        GUI.Label(new Rect(area.x, area.y + 76, area.width, 30), $"<color=#5BE35B>{Lang.T("Eingeschaltet", "Enabled")}</color> ({v.Summary})", Ui.Small);
        float h = body.CalcHeight(new GUIContent(v.Description), area.width);
        GUI.Label(new Rect(area.x, area.y + 120, area.width, h), v.Description, body);
        float y = area.y + 140 + h;
        foreach (var setting in v.Settings.Skip(2)) // Anzahl/Chance stehen schon oben
        {
            GUI.Label(new Rect(area.x + 20, y, area.width - 20, 30), $"{setting.Name}: <b>{setting.Display}</b>", Ui.Small);
            y += 30;
        }
    }

    private static void DrawDetails(CustomRole role, Rect area)
    {
        Ui.Header.normal.textColor = role.NameColor;
        GUI.Label(new Rect(area.x, area.y, area.width, 44), role.Name, Ui.Header);
        Ui.Header.normal.textColor = Color.white;

        string team = role.Team switch
        {
            RoleTeam.Crewmate => "<color=#8CFFFF>Crew</color>",
            RoleTeam.Impostor => "<color=#FF1919>Impostor</color>",
            RoleTeam.Neutral => Lang.T("<color=#AAAAAA>Neutral (eigener Sieg)</color>", "<color=#AAAAAA>Neutral (own win)</color>"),
            _ => Lang.T("<color=#FFB366>Anpassung (kommt zu jeder Rolle dazu)</color>", "<color=#FFB366>Modifier (added to any role)</color>"),
        };
        if (role.IsGhostRole) team += Lang.T("  <color=#C8B4FF>(Geisterrolle - kommt beim Tod)</color>", "  <color=#C8B4FF>(ghost role - given on death)</color>");
        string active = role.Count.Int > 0
            ? $"<color=#5BE35B>{Lang.T("Eingeschaltet", "Enabled")}</color> ({role.Count.Int}x, {role.Chance.Int}% {Lang.T("Chance", "chance")})"
            : $"<color=#999999>{Lang.T("Ausgeschaltet", "Disabled")}</color>";
        GUI.Label(new Rect(area.x, area.y + 46, area.width, 30), $"Team: {team}", Ui.Small);
        GUI.Label(new Rect(area.x, area.y + 76, area.width, 30), active, Ui.Small);

        float y = area.y + 120;
        float descHeight = body.CalcHeight(new GUIContent(role.LongDescription), area.width);
        GUI.Label(new Rect(area.x, y, area.width, descHeight), role.LongDescription, body);
        y += descHeight + 20;

        if (role.Buttons.Count > 0)
        {
            var sb = new StringBuilder(Lang.T("<b>Fähigkeiten:</b>  ", "<b>Abilities:</b>  "));
            sb.Append(string.Join(",  ", role.Buttons.Select(b => b.Hotkey == KeyCode.None ? Lang.Pick(b.Label) : $"{Lang.Pick(b.Label)} [{b.Hotkey}]")));
            GUI.Label(new Rect(area.x, y, area.width, 34), sb.ToString(), Ui.Small);
            y += 40;
        }

        var options = role.Options.Where(o => o != role.Count && o != role.Chance).ToList();
        if (options.Count > 0)
        {
            GUI.Label(new Rect(area.x, y, area.width, 30), Lang.T("<b>Aktuelle Einstellungen:</b>", "<b>Current settings:</b>"), Ui.Small);
            y += 32;
            foreach (var option in options)
            {
                GUI.Label(new Rect(area.x + 20, y, area.width - 20, 30), $"{option.Name}: <b>{option.Display}</b>", Ui.Small);
                y += 30;
            }
        }
    }
}
