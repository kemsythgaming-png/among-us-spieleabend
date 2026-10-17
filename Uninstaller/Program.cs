using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Spieleabend.Uninstaller;

/// <summary>
/// Entfernt Among Us Spieleabend komplett:
/// - jede Mod-Kopie (gemerkter Ordner, C:\Spieleabend, neben dem Steam-Spiel, alte Standard-Orte),
/// - die Spieldaten des Mods (AppData\LocalLow\Innersloth\Among Us Spieleabend),
/// - Desktop-Verknuepfungen, den gemerkten Ordner in der Registry, das Launcher-Protokoll.
/// Das normale Among Us aus Steam und dessen Spieldaten werden NIE angefasst.
/// </summary>
internal static class Program
{
    private const string Title = "Among Us Spieleabend - Deinstallieren";
    private const string GameFolderName = "Among Us Spieleabend";
    private const string RegKey = @"Software\AmongUsSpieleabend";
    private const string TempArg = "--aus-temp";

    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        try
        {
            // Liegt diese EXE selbst in einem Ordner, der geloescht wird? Dann aus %TEMP% weitermachen.
            string self = Application.ExecutablePath;
            var targets = FindTargets();
            if (!args.Contains(TempArg) && targets.Folders.Any(f => IsInside(self, f)))
            {
                string copy = Path.Combine(Path.GetTempPath(), "AmongUsSpieleabend-Deinstallieren.exe");
                File.Copy(self, copy, true);
                Process.Start(new ProcessStartInfo(copy, TempArg) { UseShellExecute = false });
                return;
            }

            if (targets.Count == 0)
            {
                MessageBox.Show("Es wurde nichts von Among Us Spieleabend gefunden - es ist schon alles weg.\n\nDein normales Among Us bleibt wie es ist.",
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string list = string.Join("\n", targets.Describe().Select(l => "  - " + l));
            var answer = MessageBox.Show(
                "Folgendes wird komplett gelöscht:\n\n" + list +
                "\n\nDein normales Among Us aus Steam (und dessen Name/Einstellungen) bleibt unberührt.\n\nJetzt alles löschen?",
                Title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;

            if (!CloseRunningGame(targets.Folders)) return;

            var (problems, leftovers) = targets.DeleteAll();
            string downloadHint = "\n\nDie heruntergeladene Launcher-Datei (falls noch irgendwo, z. B. unter Downloads) kannst du selbst löschen.";

            if (problems.Count == 0 && leftovers.Count == 0)
                MessageBox.Show("Among Us Spieleabend wurde vollständig entfernt." + downloadHint,
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            else if (problems.Count == 0)
                MessageBox.Show("Among Us Spieleabend ist entfernt - alle Dateien sind weg.\n\nNur ein leerer Ordner ist noch übrig (er war wohl im Explorer geöffnet):\n" +
                                string.Join("\n", leftovers.Select(p => "  - " + p)) +
                                "\n\nSchließe das Explorer-Fenster - der leere Ordner verschwindet dann von selbst (sonst beim nächsten Neustart)." + downloadHint,
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show("Fast alles wurde entfernt. Das hier ging nicht (evtl. noch geöffnet):\n\n" + string.Join("\n", problems.Select(p => "  - " + p)) +
                                "\n\nEinfach den PC neu starten und die Deinstallation noch einmal ausführen.",
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception e)
        {
            MessageBox.Show("Fehler beim Deinstallieren:\n" + e.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (args.Contains(TempArg)) DeleteSelfLater();
        }
    }

    // ---------- Was gehoert zum Mod? ----------

    private sealed class Targets
    {
        public readonly List<string> Folders = new();      // Mod-Kopien
        public readonly List<string> DataFolders = new();  // Spieldaten des Mods
        public readonly List<string> Files = new();        // Verknuepfungen, Protokolle
        public bool Registry;
        public readonly List<string> EmptyParents = new(); // z. B. C:\Spieleabend, wenn danach leer

        public int Count => Folders.Count + DataFolders.Count + Files.Count + (Registry ? 1 : 0);

        public IEnumerable<string> Describe()
        {
            foreach (var f in Folders) yield return "Mod-Kopie: " + f;
            foreach (var f in DataFolders) yield return "Spieldaten des Mods: " + f;
            foreach (var f in Files) yield return Path.GetFileName(f) + "  (" + Path.GetDirectoryName(f) + ")";
            if (Registry) yield return "Gemerkter Installationsordner (Registry)";
        }

        /// <summary>Loescht alles. problems = echte Fehler, leftovers = nur noch leere Ordner (werden im Hintergrund entfernt).</summary>
        public (List<string> Problems, List<string> Leftovers) DeleteAll()
        {
            var problems = new List<string>();
            var leftovers = new List<string>();
            foreach (var f in Folders.Concat(DataFolders))
                switch (DeleteFolder(f))
                {
                    case DeleteResult.EmptyLeftover: leftovers.Add(f); break;
                    case DeleteResult.Failed: problems.Add(f); break;
                }
            foreach (var f in Files)
                if (!TryDeleteFile(f)) problems.Add(f);
            if (Registry)
            {
                try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(RegKey, false); }
                catch { problems.Add(@"HKEY_CURRENT_USER\" + RegKey); }
            }
            foreach (var p in EmptyParents)
            {
                try
                {
                    if (Directory.Exists(p) && !Directory.EnumerateFileSystemEntries(p).Any()) Directory.Delete(p);
                }
                catch { }
            }
            return (problems, leftovers);
        }
    }

    private static Targets FindTargets()
    {
        var t = new Targets();
        var steamGames = SteamGameFolders();

        void AddFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            string full;
            try { full = Path.GetFullPath(folder).TrimEnd('\\'); }
            catch { return; }
            if (!IsOurInstall(full)) return;
            // Sicherheitsnetz: nie das Original-Spiel
            if (steamGames.Any(g => full.Equals(g, StringComparison.OrdinalIgnoreCase))) return;
            if (full.EndsWith(@"\common\Among Us", StringComparison.OrdinalIgnoreCase)) return;
            if (!t.Folders.Contains(full, StringComparer.OrdinalIgnoreCase)) t.Folders.Add(full);
        }

        // 1) gemerkter Ordner
        string saved = Registry.GetValue(@"HKEY_CURRENT_USER\" + RegKey, "InstallDir", null) as string;
        AddFolder(saved);
        t.Registry = Registry.CurrentUser.OpenSubKey(RegKey) != null;

        // 2) bekannte Standard-Orte (auch von aelteren Launcher-Versionen)
        AddFolder(Path.Combine(@"C:\Spieleabend", GameFolderName));
        string pub = Environment.GetEnvironmentVariable("PUBLIC");
        if (!string.IsNullOrEmpty(pub)) AddFolder(Path.Combine(pub, "Spieleabend", GameFolderName));
        AddFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", GameFolderName));
        foreach (var game in steamGames) AddFolder(Path.Combine(Path.GetDirectoryName(game), GameFolderName));

        // 3) Ziel der Desktop-Verknuepfung (falls woanders installiert)
        foreach (var lnk in ShortcutFiles())
        {
            string target = ShortcutTarget(lnk);
            if (!string.IsNullOrEmpty(target)) AddFolder(Path.GetDirectoryName(target));
            if (!t.Files.Contains(lnk, StringComparer.OrdinalIgnoreCase)) t.Files.Add(lnk);
        }

        foreach (var f in t.Folders)
        {
            string parent = Path.GetDirectoryName(f);
            if (parent != null && Path.GetFileName(parent).Equals("Spieleabend", StringComparison.OrdinalIgnoreCase)) t.EmptyParents.Add(parent);
        }

        // 4) Spieldaten des Mods (eigener Ordner - NICHT der vom normalen Among Us)
        string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "Innersloth", GameFolderName);
        if (Directory.Exists(data)) t.DataFolders.Add(data);

        // 5) Protokolle
        foreach (var name in new[] { "AmongUsSpieleabend-Launcher.log" })
        {
            string f = Path.Combine(Path.GetTempPath(), name);
            if (File.Exists(f)) t.Files.Add(f);
        }
        return t;
    }

    /// <summary>Nur Ordner, die eindeutig eine Mod-Kopie sind (Spiel + BepInEx + unser Mod oder Launcher).</summary>
    private static bool IsOurInstall(string folder) =>
        Directory.Exists(folder)
        && File.Exists(Path.Combine(folder, "Among Us.exe"))
        && Directory.Exists(Path.Combine(folder, "BepInEx"))
        && (File.Exists(Path.Combine(folder, "BepInEx", "plugins", "AmongUsSpieleabend.dll"))
            || Directory.GetFiles(folder, "*Spieleabend*Launcher*.exe").Length > 0);

    private static IEnumerable<string> ShortcutFiles()
    {
        foreach (var desktop in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                     Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                 })
        {
            if (string.IsNullOrEmpty(desktop)) continue;
            string lnk = Path.Combine(desktop, GameFolderName + ".lnk");
            if (File.Exists(lnk)) yield return lnk;
        }
    }

    private static string ShortcutTarget(string lnk)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic shortcut = shell.CreateShortcut(lnk);
            return shortcut.TargetPath as string;
        }
        catch { return null; }
    }

    /// <summary>Alle Steam-Bibliotheken: Pfade des ORIGINAL-Spiels (die werden nie geloescht).</summary>
    private static List<string> SteamGameFolders()
    {
        var result = new List<string>();
        var libraries = new List<string>();
        string steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
        if (!string.IsNullOrEmpty(steam))
        {
            steam = steam.Replace('/', '\\');
            libraries.Add(steam);
            string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"(.+?)\""))
                    libraries.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
        }
        foreach (var lib in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string game = Path.Combine(lib, "steamapps", "common", "Among Us");
            if (Directory.Exists(game)) result.Add(Path.GetFullPath(game).TrimEnd('\\'));
        }
        return result;
    }

    // ---------- Loeschen ----------

    private static bool CloseRunningGame(List<string> folders)
    {
        var running = Process.GetProcesses().Where(p =>
        {
            try { return folders.Any(f => IsInside(p.MainModule?.FileName, f)); }
            catch { return false; }
        }).ToList();
        if (running.Count == 0) return true;

        var answer = MessageBox.Show("Among Us Spieleabend läuft noch. Soll es jetzt beendet werden?", Title,
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return false;
        foreach (var p in running)
        {
            try { p.Kill(); p.WaitForExit(5000); }
            catch { }
        }
        Thread.Sleep(1000);
        return true;
    }

    internal enum DeleteResult { Ok, EmptyLeftover, Failed }

    /// <summary>
    /// Loescht erst allen Inhalt (Datei fuer Datei, auch schreibgeschuetzte), dann den Ordner selbst.
    /// Haengt nur noch die leere Huelle (z. B. weil der Ordner gerade im Explorer offen ist), wird sie fuer
    /// spaeter zum Loeschen vorgemerkt - der Inhalt ist dann weg, nur ein leerer Ordner bleibt kurz uebrig.
    /// </summary>
    private static DeleteResult DeleteFolder(string folder)
    {
        if (!Directory.Exists(folder)) return DeleteResult.Ok;

        // 1) Inhalt restlos entfernen
        for (int i = 0; i < 3; i++)
        {
            try
            {
                foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); File.Delete(file); } catch { }
                }
                foreach (var dir in Directory.GetDirectories(folder, "*", SearchOption.AllDirectories)
                             .OrderByDescending(d => d.Length)) // tiefste zuerst
                {
                    try { Directory.Delete(dir, false); } catch { }
                }
                break;
            }
            catch { }
            Thread.Sleep(600);
        }

        // 2) Ordner selbst
        for (int i = 0; i < 3; i++)
        {
            try { Directory.Delete(folder, true); return DeleteResult.Ok; }
            catch { Thread.Sleep(600); }
        }
        if (!Directory.Exists(folder)) return DeleteResult.Ok;

        // 3) Nur noch leer uebrig (meist: gerade im Explorer offen) -> fuer spaeter vormerken
        bool empty = !Directory.EnumerateFileSystemEntries(folder).Any();
        if (empty)
        {
            ScheduleEmptyFolderRemoval(folder);
            return DeleteResult.EmptyLeftover;
        }
        return DeleteResult.Failed;
    }

    /// <summary>Loescht den leeren Ordner im Hintergrund noch mehrmals (sobald z. B. das Explorer-Fenster zu ist) - ohne Admin.</summary>
    private static void ScheduleEmptyFolderRemoval(string folder)
    {
        try
        {
            // wartet und versucht es ein paar Mal; auch der uebergeordnete leere "Spieleabend"-Ordner wird mitgenommen
            string parent = Path.GetDirectoryName(folder);
            bool parentToo = parent != null && Path.GetFileName(parent).Equals("Spieleabend", StringComparison.OrdinalIgnoreCase);
            string cmd = $"for /l %i in (1,1,6) do (rd /q \"{folder}\" 2>nul & ping 127.0.0.1 -n 4 >nul)";
            if (parentToo) cmd += $" & rd /q \"{parent}\" 2>nul";
            Process.Start(new ProcessStartInfo("cmd.exe", "/c " + cmd)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            });
        }
        catch { }
    }

    private static bool TryDeleteFile(string file)
    {
        try
        {
            if (File.Exists(file)) File.Delete(file);
            return true;
        }
        catch { return false; }
    }

    private static bool IsInside(string path, string folder) =>
        !string.IsNullOrEmpty(path) && Path.GetFullPath(path).StartsWith(Path.GetFullPath(folder).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);

    /// <summary>Die Kopie in %TEMP% loescht sich nach dem Beenden selbst.</summary>
    private static void DeleteSelfLater()
    {
        try
        {
            string self = Application.ExecutablePath;
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c ping 127.0.0.1 -n 3 > nul & del /f /q \"{self}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            });
        }
        catch { }
    }
}
