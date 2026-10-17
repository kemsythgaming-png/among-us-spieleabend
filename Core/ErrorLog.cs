using System;
using System.IO;
using System.Reflection;
using BepInEx.Logging;

namespace Spieleabend.Core;

/// <summary>
/// Schreibt Warnungen und Fehler aller Mods zusaetzlich in &lt;Spielordner&gt;\Logs\Fehler.log.
/// So liegen bei jedem Spieler die Fehler an einer festen Stelle (neben Launcher.log und den Spiel-Protokollen),
/// auch wenn das grosse BepInEx-Protokoll beim naechsten Start ueberschrieben wird.
/// </summary>
internal static class ErrorLog
{
    private static string file;
    private static Listener listener;

    public static string Folder { get; private set; }

    public static void Init()
    {
        try
        {
            // Der Mod liegt in <Spielordner>\BepInEx\plugins - zwei Ebenen hoch ist der Spielordner
            string dll = Assembly.GetExecutingAssembly().Location;
            string gameFolder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(dll), "..", ".."));
            Folder = Path.Combine(gameFolder, "Logs");
            Directory.CreateDirectory(Folder);
            file = Path.Combine(Folder, "Fehler.log");

            // Bei jedem Start frisch beginnen, aber die letzte Datei als Fehler.vorher.log behalten
            if (File.Exists(file))
            {
                string prev = Path.Combine(Folder, "Fehler.vorher.log");
                try { File.Copy(file, prev, true); } catch { }
            }
            File.WriteAllText(file, $"===== {Plugin.Name} {Plugin.Version} - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ====={Environment.NewLine}");

            listener = new Listener();
            BepInEx.Logging.Logger.Listeners.Add(listener);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"Fehler-Protokoll konnte nicht angelegt werden: {e.Message}");
        }
    }

    private static void Append(LogLevel level, object source, object message)
    {
        if (file == null) return;
        try
        {
            string src = source is ManualLogSource m ? m.SourceName : source?.ToString() ?? "?";
            File.AppendAllText(file, $"[{DateTime.Now:HH:mm:ss}] [{level}] [{src}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    /// <summary>Haengt sich ins BepInEx-Log und schreibt nur Warnung/Fehler/Fatal mit.</summary>
    private sealed class Listener : ILogListener
    {
        public LogLevel LogLevelFilter => LogLevel.Warning | LogLevel.Error | LogLevel.Fatal;

        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            if ((eventArgs.Level & LogLevelFilter) != 0) Append(eventArgs.Level, eventArgs.Source, eventArgs.Data);
        }

        public void Dispose() { }
    }
}
