using UnityEngine;

namespace Spieleabend.Core;

/// <summary>Kurze Einblendung oben in der Mitte (z. B. "Dein Schild wurde angegriffen!"). Nur lokal.</summary>
public static class Notifier
{
    private static string message;
    private static float until;
    private static GUIStyle style;

    public static void Show(string text, float seconds = 4f)
    {
        message = text;
        until = Time.time + seconds;
        Plugin.Logger.LogInfo("Hinweis: " + text);
    }

    internal static void Draw()
    {
        if (message == null || Time.time > until) return;
        style ??= new GUIStyle(Ui.Title) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        var rect = new Rect(Ui.ScreenWidth / 2f - 500f, 120f, 1000f, 90f);
        GUI.Box(rect, new GUIContent(""), Ui.Panel);
        GUI.Label(rect, message, style);
    }
}
