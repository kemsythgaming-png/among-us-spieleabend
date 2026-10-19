using System.Collections.Generic;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>Zaehlt je Spieler die erledigten Aufgaben (seit Rundenbeginn). Mehrteilige Tasks zaehlen erst, wenn ganz fertig.</summary>
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
        "Du bist Crew. Mit dem Knopf prüfst du einen Spieler in der Nähe: Du siehst, wie viele Aufgaben er seit Rundenbeginn vollständig erledigt hat (ohne Maximalwert, ohne Rollenhinweis). Mehrteilige Aufgaben zählen erst, wenn sie ganz fertig sind.",
        "You are crew. The button checks a nearby player: you see how many tasks they have fully completed since the round started (no maximum, no role hint). Multi-part tasks only count when fully done.");

    private RoleOption cooldown;

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
            Icon = "check",
            NeedsTarget = true,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            OnClick = target =>
            {
                if (!target || target.Data == null) return;
                shownId = target.PlayerId;
                shownCount = TaskTracker.Count(target.PlayerId);
                shownUntil = Time.time + 4f;
                Sfx.Play(Sfx.Investigate);
            },
        });
    }

    public override void ResetState() => shownId = byte.MaxValue;

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
}
