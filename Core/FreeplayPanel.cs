using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Grosses Freeplay-Test-Fenster (nur im Uebungsmodus): links die (nach Team gefilterte) Rollenliste, rechts die
/// Dummys. Rolle anklicken, dann "Fuer dich" oder einen bestimmten Dummy anklicken - nur der bekommt die Rolle.
/// Dazu Dummy-Impostor-Zahl, selbst sterben und "Dummies folgen der Stimme". Dritter HUD-Knopf oder F3.
/// </summary>
internal static class FreeplayPanel
{
    private const float H = 1000f;

    private static bool open;
    private static string selected = "";
    private static int filter;
    private static Vector2 roleScroll, dummyScroll;

    private static readonly (string Label, Func<CustomRole, bool> Match)[] Filters =
    {
        ("Alle|All", _ => true),
        ("Crew", r => r.Team == RoleTeam.Crewmate && !r.IsGhostRole),
        ("Impostor", r => r.Team == RoleTeam.Impostor && !r.IsGhostRole),
        ("Neutral", r => r.Team == RoleTeam.Neutral && !r.IsGhostRole),
        ("Anpassungen|Mods", r => r.Team == RoleTeam.Modifier),
        ("Geister|Ghosts", r => r.IsGhostRole),
    };

    public static bool Available => FreeplayTest.IsFreeplay && ShipStatus.Instance;

    public static void Toggle()
    {
        if (!Available) return;
        open = !open;
        if (open) { SettingsPanel.Close(); RoleGuide.Close(); }
    }

    public static void Close() => open = false;

    public static void Draw()
    {
        if (!Available) { open = false; return; }

        if (!open)
        {
            if (!HudButtons.FreeplayVisible)
            {
                var r = new Rect(660, 1080 - 80, 260, 56);
                Ui.Block(r);
                if (Ui.Button(r, Lang.T("Freeplay-Test (F3)", "Freeplay test (F3)"))) Toggle();
            }
            return;
        }

        float W = Mathf.Min(Ui.ScreenWidth - 50f, 1680f);
        var win = new Rect((Ui.ScreenWidth - W) / 2f, (1080 - H) / 2f, W, H);
        GUI.Box(win, new GUIContent(""), Ui.Panel);
        Ui.Block(win);
        GUI.Label(new Rect(win.x + 36, win.y + 18, W - 120, 46), Lang.T("Freeplay-Test", "Freeplay test"), Ui.Title);
        if (GUI.Button(new Rect(win.xMax - 76, win.y + 18, 54, 54), new GUIContent("X"), Ui.CloseButton)) { Close(); return; }

        string myRole = string.IsNullOrEmpty(FreeplayTest.TestRoleName) ? Lang.T("(keine)", "(none)") : FreeplayTest.TestRoleDisplayName;
        string selName = SelectedName();
        GUI.Label(new Rect(win.x + 36, win.y + 72, W - 72, 30),
            Lang.T($"Deine Rolle: <b>{myRole}</b>        Gewählt zum Vergeben: <b>{selName}</b>",
                   $"Your role: <b>{myRole}</b>        Selected to assign: <b>{selName}</b>"), Ui.Text);

        float contentY = win.y + 116, contentBottom = win.yMax - 28;
        float leftW = (W - 96) * 0.5f;
        float lx = win.x + 36;
        float rx = lx + leftW + 36;
        float rightW = win.xMax - 36 - rx;

        DrawRolePicker(lx, contentY, leftW, contentBottom);
        DrawDummies(rx, contentY, rightW, contentBottom);
    }

    private static string SelectedName()
    {
        if (string.IsNullOrEmpty(selected)) return Lang.T("(keine)", "(none)");
        var r = RoleRegistry.Roles.FirstOrDefault(x => x.ConfigSection == selected);
        return r != null ? r.Name : selected;
    }

    // ---------- Links: Rollen waehlen ----------
    private static void DrawRolePicker(float x, float y, float w, float bottom)
    {
        // Filter-Reiter
        float tw = (w - 5 * 6) / 6f;
        for (int i = 0; i < Filters.Length; i++)
            if (GUI.Button(new Rect(x + i * (tw + 6), y, tw, 46), new GUIContent(Lang.Pick(Filters[i].Label)), i == filter ? Ui.TabSelected : Ui.Tab))
            {
                filter = i;
                roleScroll = Vector2.zero;
            }

        float listTop = y + 56, listBottom = bottom - 64;
        var roles = new List<(string Section, string Label)>();
        if (filter == 0) roles.Add(("", Lang.T("(keine Rolle)", "(no role)")));
        roles.AddRange(RoleRegistry.Roles.Where(Filters[filter].Match).Select(r => (r.ConfigSection, r.ColoredName)));

        const float rowH = 52f;
        var view = new Rect(x, listTop, w, listBottom - listTop);
        GUI.Box(view, GUIContent.none, Ui.Inner);
        roleScroll.y = Ui.Scroll(view, roles.Count * rowH, roleScroll.y);
        float ry = 0;
        foreach (var (section, label) in roles)
        {
            if (Ui.RowVisible(view, ry, rowH - 6, roleScroll.y))
            {
                float sy = view.y + ry - roleScroll.y;
                bool sel = selected == section;
                if (GUI.Button(new Rect(view.x + 8, sy + 3, w - 26, rowH - 7), new GUIContent(label), sel ? Ui.ListSelected : Ui.ListButton))
                    selected = section;
            }
            ry += rowH;
        }

        // "Fuer dich" - gewaehlte Rolle an den eigenen Spieler
        if (GUI.Button(new Rect(x, bottom - 56, w, 56), new GUIContent(Lang.T("Für dich", "For you")), Ui.ListButton))
            FreeplayTest.SetTestRole(selected);
    }

    // ---------- Rechts: Dummys + Steuerung ----------
    private static void DrawDummies(float x, float y, float w, float bottom)
    {
        GUI.Label(new Rect(x, y, w, 30), Lang.T("Dummy anklicken → er bekommt die gewählte Rolle:", "Click a dummy → it gets the selected role:"), Ui.Small);

        float listTop = y + 36, listBottom = bottom - 228;
        var dummies = PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p && p.isDummy && p != PlayerControl.LocalPlayer && p.Data != null).ToList();

        const float rowH = 52f;
        var view = new Rect(x, listTop, w, listBottom - listTop);
        GUI.Box(view, GUIContent.none, Ui.Inner);
        dummyScroll.y = Ui.Scroll(view, Mathf.Max(1, dummies.Count) * rowH, dummyScroll.y);
        if (dummies.Count == 0)
            GUI.Label(new Rect(view.x + 12, view.y + 12, w - 24, 40), Lang.T("Keine Dummys da - spawn welche über den Laptop.", "No dummies - spawn some via the laptop."), Ui.Small);

        float ry = 0;
        foreach (var d in dummies)
        {
            if (Ui.RowVisible(view, ry, rowH - 6, dummyScroll.y))
            {
                float sy = view.y + ry - dummyScroll.y;
                var role = FreeplayTest.DummyRole(d.PlayerId);
                string label = $"{d.Data.PlayerName}  —  {(role != null ? role.ColoredName : "<color=#9AA0AE>-</color>")}";
                if (GUI.Button(new Rect(view.x + 8, sy + 3, w - 26, rowH - 7), new GUIContent(label), Ui.ListButton))
                    FreeplayTest.SetDummyRole(d.PlayerId, selected);
            }
            ry += rowH;
        }

        // Steuerung unten
        float cy = bottom - 184;
        if (GUI.Button(new Rect(x, cy, 56, 52), new GUIContent("-"), Ui.ListButton)) FreeplayTest.ChangeDummyImpostors(-1);
        GUI.Label(new Rect(x + 66, cy + 10, w - 240, 32), $"{FreeplayTest.DummyImpostors} " + Lang.T("Fake-Impostor (zum Killen)", "fake impostors (to kill)"), Ui.Small);
        if (GUI.Button(new Rect(x + 172, cy, 56, 52), new GUIContent("+"), Ui.ListButton)) FreeplayTest.ChangeDummyImpostors(1);
        if (GUI.Button(new Rect(x + w - 240, cy, 240, 52), new GUIContent(Lang.T("Dummy-Rollen zurücksetzen", "Reset dummy roles")), Ui.ListButton)) FreeplayTest.ClearDummyRoles();
        cy += 62;

        if (GUI.Button(new Rect(x, cy, w, 52), new GUIContent(Lang.T("Selbst sterben", "Kill yourself")), Ui.ListButton)) FreeplayTest.SelfKill();
        cy += 62;

        GUI.Label(new Rect(x, cy + 12, w - 150, 32), Lang.T("Dummies folgen deiner Stimme:", "Dummies follow your vote:"), Ui.Small);
        if (GUI.Button(new Rect(x + w - 140, cy, 140, 52), new GUIContent(FreeplayTest.DummiesFollowVote ? Lang.T("An", "On") : Lang.T("Aus", "Off")), FreeplayTest.DummiesFollowVote ? Ui.ListSelected : Ui.ListButton))
            FreeplayTest.ToggleDummiesFollowVote();
    }
}
