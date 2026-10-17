using System;
using System.IO;
using System.Linq;

namespace Spieleabend.Launcher;

/// <summary>
/// Protokoll des Launchers. Bis der Spielordner feststeht in %TEMP%\AmongUsSpieleabend-Launcher.log,
/// danach zusaetzlich in &lt;Spielordner&gt;\Logs\Launcher.log - so liegen bei jedem Spieler alle Protokolle an einer Stelle.
/// </summary>
internal static class Log
{
    public static readonly string TempPath = Path.Combine(Path.GetTempPath(), "AmongUsSpieleabend-Launcher.log");

    private static string folderPath;

    /// <summary>Wo man nachschauen soll (fuer Fehlermeldungen).</summary>
    public static string FilePath => folderPath ?? TempPath;

    /// <summary>Ab jetzt auch in den Logs-Ordner des Spiels schreiben (bisheriges Protokoll dieses Starts wird mitgenommen).</summary>
    public static void UseGameFolder(string gameFolder)
    {
        try
        {
            string dir = LogsFolder(gameFolder);
            Directory.CreateDirectory(dir);
            folderPath = Path.Combine(dir, "Launcher.log");
            Trim(folderPath, 2_000_000);
            Write("----- Launcher-Start, Spielordner: " + gameFolder);
        }
        catch { folderPath = null; }
    }

    public static string LogsFolder(string gameFolder) => Path.Combine(gameFolder, "Logs");

    public static void Write(string text)
    {
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}{Environment.NewLine}";
        try { File.AppendAllText(TempPath, line); }
        catch { }
        if (folderPath == null) return;
        try { File.AppendAllText(folderPath, line); }
        catch { }
    }

    /// <summary>
    /// Das Protokoll des letzten Spiels sichern, bevor BepInEx es beim naechsten Start ueberschreibt
    /// (wichtig nach Abstuerzen). Es bleiben die letzten 10.
    /// </summary>
    public static void KeepLastGameLog(string gameFolder)
    {
        try
        {
            string bep = Path.Combine(gameFolder, "BepInEx", "LogOutput.log");
            if (!File.Exists(bep)) return;
            string dir = LogsFolder(gameFolder);
            Directory.CreateDirectory(dir);
            string stamp = File.GetLastWriteTime(bep).ToString("yyyy-MM-dd_HH-mm-ss");
            string dest = Path.Combine(dir, $"Spiel_{stamp}.log");
            if (!File.Exists(dest)) File.Copy(bep, dest);
            foreach (var old in Directory.GetFiles(dir, "Spiel_*.log").OrderByDescending(f => f).Skip(10))
                File.Delete(old);
        }
        catch (Exception e) { Write("Spiel-Protokoll sichern: " + e.Message); }
    }

    private static void Trim(string file, long maxBytes)
    {
        if (File.Exists(file) && new FileInfo(file).Length > maxBytes) File.Delete(file);
    }
}
