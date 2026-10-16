using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using BepInEx;

namespace Spieleabend.Core;

/// <summary>
/// Das schwarze Konsolen-Fenster von BepInEx soll beim Spielen nicht aufgehen:
/// sofort verstecken und in der BepInEx-Einstellung fuer die naechsten Starts abschalten.
/// Das Protokoll (BepInEx/LogOutput.log) wird davon nicht beruehrt und weiter geschrieben.
/// </summary>
internal static class ConsoleHider
{
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SwHide = 0;

    public static void Apply()
    {
        try
        {
            var window = GetConsoleWindow();
            if (window != IntPtr.Zero) ShowWindow(window, SwHide);
        }
        catch (Exception e) { Plugin.Logger.LogWarning($"Konsole verstecken: {e.Message}"); }

        try
        {
            string cfg = Path.Combine(Paths.ConfigPath, "BepInEx.cfg");
            if (!File.Exists(cfg)) return;
            string text = File.ReadAllText(cfg);
            // Nur im Abschnitt [Logging.Console] "Enabled = true" -> false
            var fixedText = Regex.Replace(text, @"(\[Logging\.Console\][^\[]*?Enabled\s*=\s*)true", "${1}false");
            if (fixedText != text) File.WriteAllText(cfg, fixedText);
        }
        catch (Exception e) { Plugin.Logger.LogWarning($"Konsole abschalten: {e.Message}"); }
    }
}
