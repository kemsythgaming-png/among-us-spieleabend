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
        ("Diese Runde|This game", r => r.Count.Int > 0), // alles, was eingeschaltet ist (verraet nicht, wer was hat)
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
    private static VanillaRoleEntry selectedVanilla; // im Reiter "Diese Runde": gewaehlte Rolle aus dem Spiel
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

    public static void Close() => open = false;

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
        GUI.Label(new Rect(win.x + 30, win.y + 18, W - 120, 44), Lang.T("Rollen-Info", "Role info"), Ui.Title);
        if (GUI.Button(new Rect(win.xMax - 74, win.y + 18, 52, 52), new GUIContent("X"), Ui.CloseButton)) open = false;

        // Reiter
        float tx = win.x + 30;
        for (int i = 0; i < Tabs.Length; i++)
        {
            if (GUI.Button(new Rect(tx, win.y + 88, 200, 46), new GUIContent(Lang.Pick(Tabs[i].Label)), i == tab ? Ui.TabSelected : Ui.Tab))
            {
                tab = i;
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
        bool thisGame = tab == 0;
        var roles = RoleRegistry.Roles.Where(Tabs[tab].Filter).ToList();
        var vanilla = thisGame ? VanillaRoleEntry.All.Where(v => v.Count > 0).ToList() : new System.Collections.Generic.List<VanillaRoleEntry>();
        int rows = roles.Count + vanilla.Count + (thisGame ? 1 : 0);
        var view = new Rect(listBox.x + 10, listBox.y + 12, listBox.width - 20, listBox.height - 24);
        var content = new Rect(0, 0, view.width - 22, Mathf.Max(view.height, rows * RowH));
        scroll = GUI.BeginScrollView(view, scroll, content);
        var local = PlayerControl.LocalPlayer;
        float ly = 0;
        if (thisGame)
        {
            GUI.Label(new Rect(4, ly + 10, content.width, 34), Lang.T("<b>Aktiv in dieser Runde:</b>", "<b>Active this game:</b>"), Ui.Small);
            ly += RowH;
        }
        foreach (var role in roles)
        {
            int index = RoleRegistry.Roles.IndexOf(role);
            string text = role.Count.Int == 0 ? $"<color=#8A8F9C>{role.Name}</color>" : role.ColoredName;
            if (thisGame) text += $"  <color=#9AA0AE>{role.Count.Int}x · {role.Chance.Int}%</color>";
            if (RoleRegistry.Has(local, role) && !role.IsSecret(local)) text += Lang.T("  <color=#FFFFFF>(du)</color>", "  <color=#FFFFFF>(you)</color>");
            if (GUI.Button(new Rect(0, ly, content.width, RowH - 6), new GUIContent(text), index == selected && selectedVanilla == null ? Ui.Selected : Ui.ButtonStyle))
            {
                selected = index;
                selectedVanilla = null;
            }
            ly += RowH;
        }
        foreach (var v in vanilla)
        {
            string text = $"<color=#{ColorUtility.ToHtmlStringRGB(v.Color)}>{v.Name}</color>  <color=#9AA0AE>{v.Summary.Replace(", ", " · ")}</color>";
            if (GUI.Button(new Rect(0, ly, content.width, RowH - 6), new GUIContent(text), v == selectedVanilla ? Ui.Selected : Ui.ButtonStyle))
                selectedVanilla = v;
            ly += RowH;
        }
        if (thisGame && rows == 1)
            GUI.Label(new Rect(4, ly, content.width, 40), Lang.T("Keine Sonderrollen eingeschaltet.", "No special roles enabled."), Ui.Small);
        GUI.EndScrollView();

        // Rechts: Details
        var detailBox = new Rect(listBox.xMax + 14, listBox.y, win.xMax - listBox.xMax - 34, listBox.height);
        GUI.Box(detailBox, new GUIContent(""), Ui.Inner);
        if (selectedVanilla != null && thisGame)
        {
            DrawVanillaDetails(selectedVanilla, new Rect(detailBox.x + 24, detailBox.y + 18, detailBox.width - 48, detailBox.height - 36));
            return;
        }
        if (selected < 0 || selected >= RoleRegistry.Roles.Count || !Tabs[tab].Filter(RoleRegistry.Roles[selected]))
        {
            GUI.Label(new Rect(detailBox.x + 24, detailBox.y + 20, detailBox.width - 48, 40),
                roles.Count == 0 ? Lang.T("Keine Rollen in diesem Reiter.", "No roles in this tab.") : Lang.T("Wähle links eine Rolle.", "Pick a role on the left."), Ui.Text);
            return;
        }
        DrawDetails(RoleRegistry.Roles[selected], new Rect(detailBox.x + 24, detailBox.y + 18, detailBox.width - 48, detailBox.height - 36));
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
