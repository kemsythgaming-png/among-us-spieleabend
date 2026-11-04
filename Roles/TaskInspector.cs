using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>Zaehlt je Spieler die erledigten Aufgaben (seit dem letzten Meeting - wird bei Meeting-Start zurueckgesetzt). Mehrteilige Tasks zaehlen erst, wenn ganz fertig. Fuer Impostor zaehlt stattdessen das Tueren-Schliessen (sie haben keine Aufgaben).</summary>
internal static class TaskTracker
{
    private static readonly Dictionary<byte, int> done = new();

    public static void Reset() => done.Clear();
    public static void Add(byte playerId) { done.TryGetValue(playerId, out int n); done[playerId] = n + 1; }
    public static int Count(byte playerId) => done.TryGetValue(playerId, out int n) ? n : 0;
}

/// <summary>Jede abgeschlossene Aufgabe (auch die letzte Teilaufgabe eines mehrteiligen Tasks) zaehlt +1. Impostor zaehlen nicht.</summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask), typeof(uint))]
internal static class CompleteTaskPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (!__instance || __instance.Data == null) return;
        if (__instance.Data.Role && __instance.Data.Role.IsImpostor) return; // Impostor haben keine echten Aufgaben
        TaskTracker.Add(__instance.PlayerId);
    }
}

/// <summary>
/// Schliesst ein Impostor Tueren (Sabotage), zaehlt das wie eine erledigte Aufgabe - so steht beim Aufgabenpruefer
/// nicht mehr dauerhaft 0 ueber jedem Impostor. Laeuft bei allen Clients (player = der schliessende Impostor).
/// </summary>
[HarmonyPatch]
internal static class DoorCloseCountPatch
{
    public static MethodBase TargetMethod() =>
        typeof(ShipStatus).GetMethods()
            .FirstOrDefault(m => m.Name == "UpdateSystem" && m.GetParameters().Length == 3
                && m.GetParameters()[2].ParameterType.Name == "MessageReader");

    public static void Postfix(SystemTypes __0, PlayerControl __1)
    {
        if (__0 != SystemTypes.Doors || !__1 || __1.Data == null) return;
        if (__1.Data.Role && __1.Data.Role.IsImpostor) TaskTracker.Add(__1.PlayerId);
    }
}

/// <summary>Crew: prueft bei einem Spieler, wie viele Aufgaben er bisher erledigt hat (ohne Maximalwert/Rolle).</summary>
public sealed class TaskInspector : CustomRole
{
    public override string ConfigSection => "Aufgabenpruefer";
    public override string Name => Lang.T("Aufgabenprüfer", "Task Inspector");
    public override Color Color => new Color32(90, 170, 220, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Wer hat wirklich gearbeitet?", "Who actually worked?");
    public override string Description => Lang.T("Prüf, wie viele Aufgaben jemand\nerledigt hat (F).", "Check how many tasks someone\nhas done (F).");
    public override string LongDescription => Lang.T(
        "Du bist Crew. Mit dem Knopf prüfst du einen Spieler in der Nähe: Du siehst, wie viele Aufgaben er seit dem letzten Meeting vollständig erledigt hat (ohne Maximalwert, ohne Rollenhinweis). Nach jedem Meeting fängt die Zählung wieder bei 0 an. Mehrteilige Aufgaben zählen erst, wenn sie ganz fertig sind.",
        "You are crew. The button checks a nearby player: you see how many tasks they have fully completed since the last meeting (no maximum, no role hint). After every meeting the count restarts at 0. Multi-part tasks only count when fully done.");

    private RoleOption cooldown;
    private int uses = 1;

    // Welchen Spieler zeige ich gerade (nur fuer mich, blinkt kurz ueber dem Kopf)?
    private byte shownId = byte.MaxValue;
    private float shownUntil;
    private int shownCount;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 35f, 10f, 90f, 2.5f, "s");

        AddButton(new RoleButton
        {
            Label = "Prüfen|Inspect",
            GameIcon = _ => ClipboardSprite(), // eigenes Klemmbrett-Symbol
            Icon = "boards",
            NeedsTarget = true,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            UsesLeft = () => uses, // nur einmal pro Runde
            CanUse = () => uses > 0,
            OnClick = target =>
            {
                if (!target || target.Data == null || uses <= 0) return;
                shownId = target.PlayerId;
                shownCount = TaskTracker.Count(target.PlayerId);
                shownUntil = Time.time + 4f;
                uses = 0;
                Sfx.Play(Sfx.Investigate);
            },
        });
    }

    public override void ResetState() { shownId = byte.MaxValue; uses = 1; }
    public override void OnMeetingEnd(PlayerControl exiled) { uses = 1; } // nach jedem Meeting wieder ein Versuch

    /// <summary>Die Zahl taucht kurz groß und türkisblau in der Bildschirmmitte auf - nur der Prüfer sieht sie.</summary>
    public override void DrawGUI()
    {
        if (!IsLocalRole || shownId == byte.MaxValue) return;
        float remain = shownUntil - Time.time;
        if (remain <= 0f) return;

        float a = Mathf.Min(Mathf.Clamp01((4f - remain) / 0.3f), Mathf.Clamp01(remain / 0.8f)); // auftauchen + ausblenden
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 150,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
        };
        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, a * 0.5f);
        GUI.Label(new Rect(Ui.ScreenWidth / 2f - 298f, 540f - 108f, 600f, 220f), shownCount.ToString(), style); // Schatten
        GUI.color = new Color(0.1f, 0.9f, 1f, a); // tuerkisblau
        GUI.Label(new Rect(Ui.ScreenWidth / 2f - 300f, 540f - 110f, 600f, 220f), shownCount.ToString(), style);
        GUI.color = prev;
    }

    // ---------- Eigenes Klemmbrett-Symbol (Brett + Klemme + Häkchen-Zeilen) ----------
    private static Sprite clipboardSprite;
    private static Sprite ClipboardSprite()
    {
        if (clipboardSprite) return clipboardSprite;
        const int S = 96;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) tex.SetPixel(x, y, Color.clear);
        var board = new Color(0.95f, 0.95f, 0.98f, 1f);
        var clip = new Color(0.6f, 0.62f, 0.7f, 1f);
        var check = new Color(0.25f, 0.8f, 0.4f, 1f);

        // Brett (abgerundetes Rechteck)
        for (int y = 10; y < 86; y++)
        for (int x = 20; x < 76; x++)
        {
            bool corner = (x < 25 || x > 70) && (y < 15 || y > 80);
            if (!corner) tex.SetPixel(x, y, board);
        }
        // Klemme oben
        for (int y = 80; y < 90; y++)
        for (int x = 40; x < 56; x++)
            tex.SetPixel(x, y, clip);

        // drei Häkchen-Zeilen (Haken + Linie)
        void Row(int yy)
        {
            // Haken
            for (int i = 0; i < 6; i++) { int px = 28 + i, py = yy - 3 + i; if (py < yy) tex.SetPixel(px, py, check); }
            for (int i = 0; i < 9; i++) { int px = 33 + i, py = yy + 2 - i; if (py > yy - 4) tex.SetPixel(px, py, check); }
            // Linie
            for (int x = 46; x < 70; x++) { tex.SetPixel(x, yy, clip); tex.SetPixel(x, yy + 1, clip); }
        }
        Row(64); Row(48); Row(32);

        tex.Apply();
        clipboardSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        clipboardSprite.hideFlags = HideFlags.HideAndDontSave;
        return clipboardSprite;
    }
}
