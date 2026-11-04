using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Crew: Am Admin-Tisch sieht der Administrator die Punkte in den echten Spielerfarben -
/// so erkennt er, WER gerade in welchem Raum ist (statt nur wie viele).
/// </summary>
public sealed class Administrator : CustomRole
{
    public override string Name => Lang.T("Administrator", "Administrator");
    public override Color Color => new Color32(70, 150, 235, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Behalte den Überblick", "Keep the overview");
    public override string Description => Lang.T("Am Admin-Tisch siehst du\ndie Farben der Spieler.", "At admin you see\nthe players' colors.");
    public override string LongDescription => Lang.T(
        "Du bist Crew. Am Admin-Tisch zeigen die Punkte die echten Farben der Spieler - du erkennst also, WER gerade in welchem Raum ist. Außerdem kannst du mit dem Knopf (F) von ÜBERALL die Admin-Ansicht kurz einschalten (Standard: 10 s, alle 40 s).",
        "You are crew. At the admin table the dots show the players' real colors - so you see WHO is in which room. Also, the button (F) lets you open the admin view from ANYWHERE for a short time (default: 10 s, every 40 s).");

    internal static Administrator Instance { get; private set; }

    private RoleOption cooldown, duration;
    private float adminUntil;
    private bool seenOpen; // true, sobald die Karte wirklich offen war (damit wir sie nicht im 1. Frame gleich schließen)

    protected override void Setup()
    {
        Instance = this;
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 40f, 10f, 120f, 5f, "s");
        duration = AddOption("Duration", "Dauer der Ansicht|View duration", 10f, 3f, 30f, 1f, "s");

        AddButton(new RoleButton
        {
            Label = "Admin-Ansicht|Admin view",
            Icon = "admin",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && adminUntil <= 0f,
            OnClick = _ => OpenAdmin(),
        });
    }

    public override void ResetState() { Instance = this; adminUntil = 0f; seenOpen = false; }

    public override void OnMeetingStart() => CloseAdmin();

    public override void Update()
    {
        if (!IsLocalRole || adminUntil <= 0f) return;
        var local = PlayerControl.LocalPlayer;

        bool mapOpen = MapBehaviour.Instance && MapBehaviour.Instance.isActiveAndEnabled;
        if (mapOpen)
        {
            seenOpen = true;
            // Solange die Ansicht offen ist, wirklich stehen bleiben (Map-Flag allein reicht hier nicht).
            if (local)
            {
                local.moveable = false;
                if (local.MyPhysics && local.MyPhysics.body) local.MyPhysics.body.velocity = Vector2.zero;
            }
        }

        // Ende, sobald die Zeit um ist ODER die Karte (vom Spieler) geschlossen wurde -> Bewegung sofort wieder frei.
        if (Time.time >= adminUntil || (seenOpen && !mapOpen)) CloseAdmin();
    }

    private int lastWhole = -1;
    private float popAt;
    private static GUIStyle timerStyle;

    /// <summary>Countdown in der Bildschirmmitte: große Sekundenzahl, hellblau -> orange -> rot, mit Aufpopp-Effekt.</summary>
    public override void DrawGUI()
    {
        if (!IsLocalRole || adminUntil <= 0f) return;
        float left = Mathf.Max(0f, adminUntil - Time.time);
        int whole = Mathf.CeilToInt(left);
        if (whole <= 0) return;
        if (whole != lastWhole) { lastWhole = whole; popAt = Time.time; } // neuer Sekundenwert -> aufpoppen

        if (timerStyle == null && Ui.ValueBig != null) timerStyle = new GUIStyle(Ui.ValueBig);
        if (timerStyle == null) return;

        Color col = whole <= 1 ? new Color(1f, 0.3f, 0.25f)     // rot
                  : whole <= 3 ? new Color(1f, 0.6f, 0.2f)       // orange
                  : new Color(0.45f, 0.85f, 1f);                 // hellblau

        float t = Mathf.Clamp01((Time.time - popAt) / 0.25f);
        float scale = 1f + 0.6f * (1f - t);                      // 1.6 -> 1.0 (Aufpopp)
        timerStyle.fontSize = Mathf.RoundToInt(130f * scale);

        float cx = Ui.ScreenWidth / 2f, cy = 540f;
        var rect = new Rect(cx - 170f, cy - 130f, 340f, 260f);
        Ui.Block(rect);

        string txt = whole.ToString();
        timerStyle.normal.textColor = new Color(0f, 0f, 0f, 0.55f);               // Schatten
        GUI.Label(new Rect(rect.x + 5f, rect.y + 5f, rect.width, rect.height), txt, timerStyle);
        timerStyle.normal.textColor = col;
        GUI.Label(rect, txt, timerStyle);
    }

    private void OpenAdmin()
    {
        // MapBehaviour.Instance ist beim 1. Mal noch null (Karte nie geöffnet) -> ToggleMapVisible erzeugt sie und
        // zeigt direkt die Admin-Zählansicht (CountOverlay).
        if (InMeeting || !HudManager.InstanceExists) return;
        try
        {
            HudManager.Instance.ToggleMapVisible(new MapOptions { Mode = MapOptions.Modes.CountOverlay, AllowMovementWhileMapOpen = false });
            adminUntil = Time.time + duration.Value;
            seenOpen = false;
            lastWhole = -1;
            var local = PlayerControl.LocalPlayer;
            if (local)
            {
                local.moveable = false; // sofort stehen bleiben
                if (local.MyPhysics && local.MyPhysics.body) local.MyPhysics.body.velocity = Vector2.zero;
            }
            Sfx.Play(Sfx.TaskUpdate, 0.6f);
        }
        catch (System.Exception e) { Plugin.Logger.LogWarning($"Admin-Ansicht: {e.Message}"); }
    }

    private void CloseAdmin()
    {
        adminUntil = 0f;
        seenOpen = false;
        var local = PlayerControl.LocalPlayer;
        if (local) local.moveable = true; // Bewegung wieder freigeben
        try { if (MapBehaviour.Instance && MapBehaviour.Instance.isActiveAndEnabled) MapBehaviour.Instance.Close(); }
        catch { }
    }

    /// <summary>Farben der lebenden Spieler in einem Raum (nach PlayerId sortiert, damit es ruhig bleibt).</summary>
    internal static List<Color> ColorsInRoom(SystemTypes room)
    {
        var result = new List<Color>();
        var ship = ShipStatus.Instance;
        if (!ship || ship.AllRooms == null) return result;
        PlainShipRoom psr = null;
        foreach (var r in ship.AllRooms)
            if (r && r.RoomId == room) { psr = r; break; }
        if (psr == null || !psr.roomArea) return result;

        foreach (var p in PlayerControl.AllPlayerControls.ToArray().OrderBy(p => p ? p.PlayerId : byte.MaxValue))
        {
            if (!p || p.Data == null || p.Data.IsDead || p.Data.Disconnected) continue;
            if (psr.roomArea.OverlapPoint(p.GetTruePosition()))
                result.Add(Palette.PlayerColors[p.Data.DefaultOutfit.ColorId % Palette.PlayerColors.Length]);
        }
        return result;
    }
}

/// <summary>Beim Administrator: die Punkte am Admin-Tisch in den echten Spielerfarben einfärben.</summary>
[HarmonyPatch(typeof(CounterArea), nameof(CounterArea.UpdateCount))]
internal static class AdminColorPatch
{
    public static void Postfix(CounterArea __instance, int cnt)
    {
        var admin = Administrator.Instance;
        if (admin == null || !admin.IsLocalRole || !__instance) return;

        var colors = Administrator.ColorsInRoom(__instance.RoomType);
        var icons = __instance.myIcons;
        if (icons == null) return;

        for (int i = 0; i < cnt && i < icons.Count; i++)
        {
            var icon = icons[i];
            if (!icon) continue;
            var sr = icon.GetComponentInChildren<SpriteRenderer>();
            if (sr) sr.color = i < colors.Count ? colors[i] : Color.white;
        }
    }
}
