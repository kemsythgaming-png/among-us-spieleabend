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

    /// <summary>
    /// Haengt sich ins BepInEx-Log und schreibt nur Warnung/Fehler/Fatal der MODS mit.
    /// Das Spiel selbst (Unity/Among Us) gibt staendig harmlose Warnungen aus (fehlende Uebersetzungen, Epic-Online-Services,
    /// Controller-Menues ...) - die gehoeren NICHT ins Fehler-Protokoll, sonst gehen echte Mod-Fehler darin unter.
    /// </summary>
    private sealed class Listener : ILogListener
    {
        public LogLevel LogLevelFilter => LogLevel.Warning | LogLevel.Error | LogLevel.Fatal;

        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            if ((eventArgs.Level & LogLevelFilter) == 0) return;
            if (IsGameNoise(eventArgs.Source)) return; // das Spiel selbst nicht mitschreiben
            Append(eventArgs.Level, eventArgs.Source, eventArgs.Data);
        }

        /// <summary>Kommt die Meldung vom Spiel selbst (Unity) und nicht von einem Mod?</summary>
        private static bool IsGameNoise(object source)
        {
            string name = source?.GetType().Name ?? "";
            string src = source is ManualLogSource m ? m.SourceName ?? "" : "";
            return name.Contains("Unity") || src.Equals("Unity", StringComparison.OrdinalIgnoreCase) || src.StartsWith("IL2CPP", StringComparison.OrdinalIgnoreCase);
        }

        public void Dispose() { }
    }
}
