using System;
using System.IO;

namespace Spieleabend.Launcher;

/// <summary>Einfaches Protokoll in %TEMP%\AmongUsSpieleabend-Launcher.log (fuer die Fehlersuche).</summary>
internal static class Log
{
    public static readonly string FilePath = Path.Combine(Path.GetTempPath(), "AmongUsSpieleabend-Launcher.log");

    public static void Write(string text)
    {
        try { File.AppendAllText(FilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}{Environment.NewLine}"); }
        catch { }
    }
}
