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
    private const float W = 1500f, H = 900f, ListColumn = 250f;

    private static readonly (string Label, RoleTeam Team)[] Groups =
    {
        ("Crew", RoleTeam.Crewmate),
        ("Impostor", RoleTeam.Impostor),
        ("Neutral", RoleTeam.Neutral),
        ("Anpassungen|Modifiers", RoleTeam.Modifier),
    };

    private static bool open;
    private static int selected = -1;
    private static GUIStyle body;

    private static bool Available =>
        PlayerControl.LocalPlayer && RoleRegistry.Roles.Count > 0 && !IntroCutscene.Instance && !ExileController.Instance;

    public static void Toggle()
    {
        if (!Available) return;
        open = !open;
        if (open) SelectOwnRole();
    }

    private static void SelectOwnRole()
    {
        var own = RoleRegistry.GetAll(PlayerControl.LocalPlayer).FirstOrDefault();
        selected = own != null ? RoleRegistry.Roles.IndexOf(own) : Mathf.Max(selected, 0);
    }

    internal static void Draw()
    {
        if (!Available)
        {
            open = false;
            return;
        }

        // Button: im Meeting links unter dem Aufgabenbalken, in der Lobby neben "Rollen-Einstellungen", sonst unten links
        Rect buttonRect;
        if (MeetingHud.Instance) buttonRect = new Rect(20, 100, 260, 52); // unter dem Aufgabenbalken
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
        if (Ui.Button(new Rect(win.xMax - 70, win.y + 18, 50, 50), "X")) open = false;

        // Links: alle Rollen nach Team, in zwei Spalten (bricht um, wenn eine Spalte voll ist)
        float top = win.y + 80, bottom = win.yMax - 20;
        float x = win.x + 30, y = top;
        var local = PlayerControl.LocalPlayer;
        foreach (var (label, team) in Groups)
        {
            var roles = RoleRegistry.Roles.Where(r => r.Team == team).ToList();
            if (roles.Count == 0) continue;
            if (y + 30 + 40 > bottom) { x += ListColumn + 10; y = top; }
            GUI.Label(new Rect(x, y, ListColumn, 30), Lang.Pick(label), Ui.Small);
            y += 30;
            foreach (var role in roles)
            {
                if (y + 36 > bottom) { x += ListColumn + 10; y = top; }
                int index = RoleRegistry.Roles.IndexOf(role);
                string text = role.ColoredName;
                if (RoleRegistry.Has(local, role)) text += Lang.T(" (du)", " (you)");
                else if (role.Count.Int == 0) text = $"<color=#777777>{role.Name}</color>";
                if (GUI.Button(new Rect(x, y, ListColumn, 34), new GUIContent(text), index == selected ? Ui.Selected : Ui.ButtonStyle))
                    selected = index;
                y += 38;
            }
            y += 6;
        }

        if (selected < 0 || selected >= RoleRegistry.Roles.Count) selected = 0;
        float detailX = win.x + 30 + 2 * (ListColumn + 10) + 20;
        DrawDetails(RoleRegistry.Roles[selected], new Rect(detailX, win.y + 80, win.xMax - detailX - 30, H - 100));
    }

    private static void DrawDetails(CustomRole role, Rect area)
    {
        Ui.Header.normal.textColor = role.Color;
        GUI.Label(new Rect(area.x, area.y, area.width, 44), role.Name, Ui.Header);
        Ui.Header.normal.textColor = Color.white;

        string team = role.Team switch
        {
            RoleTeam.Crewmate => "<color=#8CFFFF>Crew</color>",
            RoleTeam.Impostor => "<color=#FF1919>Impostor</color>",
            RoleTeam.Neutral => Lang.T("<color=#AAAAAA>Neutral (eigener Sieg)</color>", "<color=#AAAAAA>Neutral (own win)</color>"),
            _ => Lang.T("<color=#FFB366>Anpassung (kommt zu jeder Rolle dazu)</color>", "<color=#FFB366>Modifier (added to any role)</color>"),
        };
        string active = role.Count.Int > 0
            ? $"<color=#5BE35B>{Lang.T("Eingeschaltet", "Enabled")}</color> ({role.Count.Int}x, {role.Chance.Int}% {Lang.T("Chance", "chance")})"
            : $"<color=#999999>{Lang.T("Ausgeschaltet", "Disabled")}</color>";
        GUI.Label(new Rect(area.x, area.y + 44, area.width, 30), $"Team: {team}     {active}", Ui.Small);

        float y = area.y + 90;
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
