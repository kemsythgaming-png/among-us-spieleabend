using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace Spieleabend.Launcher;

/// <summary>
/// Ablauf: (Ordner waehlt InstallLocation) -> Mod-Kopie anlegen/aktualisieren -> BepInEx + Mod einspielen ->
/// Update von GitHub holen -> Desktop-Verknuepfung -> Steam + Spiel starten.
/// Das Original-Spiel im Steam-Ordner wird nie veraendert (nur gelesen).
///
/// Zum Testen (Umgebungsvariablen): SPIELEABEND_TARGET = anderer Zielordner,
/// SPIELEABEND_NOSTART = 1 (Spiel nicht starten), SPIELEABEND_NOSHORTCUT = 1 (keine Verknuepfung).
/// </summary>
internal sealed class Installer
{
    private readonly Action<string> status;
    private readonly Action<int> progress;

    public Installer(Action<string> status, Action<int> progress)
    {
        this.status = status;
        this.progress = progress;
    }

    /// <summary>true = Spiel wurde gestartet. steamGame und target bestimmt das Fenster vorher (siehe InstallLocation).</summary>
    public bool Run(string steamGame, string target)
    {
        if (Path.GetFullPath(target).TrimEnd('\\').Equals(Path.GetFullPath(steamGame).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            throw new Exception("Zielordner darf nicht das Original-Spiel sein.");

        if (IsRunning(target))
        {
            status("Among Us Spieleabend läuft bereits.");
            Thread.Sleep(1500);
            return false;
        }

        if (!Is64Bit(Path.Combine(steamGame, "GameAssembly.dll")))
            throw new Exception("Diese Among-Us-Version ist nicht 64-Bit - der Mod passt nicht dazu.");

        SyncGame(steamGame, target);
        InstallModPackage(target);
        UpdateFromGitHub(target);
        InstallLauncherAndShortcut(target);

        if (Environment.GetEnvironmentVariable("SPIELEABEND_NOSTART") == "1") return false;
        StartGame(target);
        return true;
    }

    // ---------- Spiel kopieren / aktuell halten ----------

    /// <summary>Kopiert das Steam-Spiel in die Mod-Kopie - beim ersten Mal komplett, danach nur geaenderte Dateien (Steam-Update).</summary>
    private void SyncGame(string source, string target)
    {
        var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
        var changed = files.Where(f => NeedsCopy(f, Path.Combine(target, f.Substring(source.Length + 1)))).ToList();
        if (changed.Count == 0) return;

        bool firstInstall = !File.Exists(Path.Combine(target, "Among Us.exe"));
        long total = changed.Sum(f => new FileInfo(f).Length), done = 0;
        status(firstInstall
            ? "Erste Installation: kopiere Among Us (ca. 1 GB) ..."
            : "Among Us wurde aktualisiert - übernehme das Update ...");
        progress(0);

        foreach (var file in changed)
        {
            string dest = Path.Combine(target, file.Substring(source.Length + 1));
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Copy(file, dest, true);
            File.SetLastWriteTimeUtc(dest, File.GetLastWriteTimeUtc(file));
            done += new FileInfo(file).Length;
            progress(total == 0 ? 100 : (int)(done * 100 / total));
        }
        progress(-1);
    }

    private static bool NeedsCopy(string src, string dest)
    {
        if (!File.Exists(dest)) return true;
        var a = new FileInfo(src);
        var b = new FileInfo(dest);
        return a.Length != b.Length || Math.Abs((a.LastWriteTimeUtc - b.LastWriteTimeUtc).TotalSeconds) > 2;
    }

    // ---------- BepInEx + Mod aus der EXE ----------

    /// <summary>Spielt BepInEx und den Mod aus dem eingebetteten Paket ein, wenn sie fehlen oder das Paket neuer ist.</summary>
    private void InstallModPackage(string target)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ModPackage.zip");
        if (stream == null)
        {
            if (!File.Exists(Path.Combine(target, "winhttp.dll")))
                throw new Exception("Diese Launcher-Datei enthält keinen Mod. Bitte die richtige Datei verwenden.");
            return;
        }

        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        string plugin = Path.Combine(target, "BepInEx", "plugins", Config.ModAssetName);
        bool bepinexMissing = !File.Exists(Path.Combine(target, "winhttp.dll")) || !Directory.Exists(Path.Combine(target, "BepInEx", "core"));

        var packagedDll = zip.Entries.FirstOrDefault(e => e.FullName.Replace('\\', '/').EndsWith("BepInEx/plugins/" + Config.ModAssetName));
        Version packaged = packagedDll != null ? ReadVersion(packagedDll) : null;
        Version installed = ModVersion(plugin);

        bool modOutdated = packaged != null && (installed == null || packaged > installed);

        // Fehlende Dateien (z. B. vom Virenscanner geloescht) immer wiederherstellen - die Konfiguration bleibt
        var missing = zip.Entries.Where(e => !string.IsNullOrEmpty(e.Name) && !File.Exists(DestOf(target, e))).ToList();
        if (!bepinexMissing && !modOutdated && missing.Count == 0) return;

        status(bepinexMissing ? "Installiere BepInEx und den Mod ..."
            : modOutdated ? $"Aktualisiere den Mod auf {packaged} ..."
            : $"Repariere {missing.Count} fehlende Datei(en) ...");
        if (missing.Count > 0 && !bepinexMissing) Log.Write("Fehlende Dateien: " + string.Join(", ", missing.Select(e => e.FullName)));
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // Ordner
            // Bei bestehender Installation nur den Mod selbst und fehlende Dateien, BepInEx-Konfiguration bleibt
            bool isMod = entry.FullName.Replace('\\', '/').EndsWith(Config.ModAssetName);
            if (!bepinexMissing && !(isMod && modOutdated) && !missing.Contains(entry)) continue;
            string dest = DestOf(target, entry);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            entry.ExtractToFile(dest, true);
        }

        // Loescht ein Virenscanner die Dateien gleich wieder, startet BepInEx nicht ("Failed to start BepInEx")
        Thread.Sleep(1500);
        var gone = zip.Entries.Where(e => !string.IsNullOrEmpty(e.Name) && !File.Exists(DestOf(target, e))).Select(e => e.FullName).ToList();
        if (gone.Count > 0)
        {
            Log.Write("Nach dem Entpacken wieder verschwunden: " + string.Join(", ", gone));
            throw new Exception(
                "Dein Virenscanner hat Dateien des Mods entfernt (z. B. " + gone[0] + ").\n\n" +
                "Bitte im Virenscanner den Ordner\n" + target + "\nals Ausnahme freigeben (bzw. die Dateien aus der Quarantäne wiederherstellen) " +
                "und den Launcher danach noch einmal starten - er repariert die fehlenden Dateien selbst.");
        }
    }

    private static string DestOf(string target, ZipArchiveEntry entry) =>
        Path.Combine(target, entry.FullName.Replace('/', Path.DirectorySeparatorChar));

    private static Version ReadVersion(ZipArchiveEntry entry)
    {
        string temp = Path.Combine(Path.GetTempPath(), "spieleabend_" + Guid.NewGuid().ToString("N") + ".dll");
        try
        {
            entry.ExtractToFile(temp, true);
            return ModVersion(temp);
        }
        finally
        {
            try { File.Delete(temp); } catch { }
        }
    }

    /// <summary>Version der Mod-DLL (Datei-Version), oder null.</summary>
    internal static Version ModVersion(string dll)
    {
        if (!File.Exists(dll)) return null;
        try
        {
            var info = FileVersionInfo.GetVersionInfo(dll);
            return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart);
        }
        catch { return null; }
    }

    // ---------- Update von GitHub ----------

    private void UpdateFromGitHub(string target)
    {
        if (string.IsNullOrEmpty(Config.GitHubRepo)) return;
        status("Suche nach Updates ...");
        string plugin = Path.Combine(target, "BepInEx", "plugins", Config.ModAssetName);
        try
        {
            var update = GitHubUpdater.CheckForUpdate(Config.GitHubRepo, ModVersion(plugin));
            if (update == null) return;

            status($"Lade Update {update.Version} ...");
            string temp = plugin + ".download";
            GitHubUpdater.Download(update.DownloadUrl, temp);
            File.Copy(temp, plugin, true);
            File.Delete(temp);
            status($"Mod auf Version {update.Version} aktualisiert.");
            Thread.Sleep(800);
        }
        catch (Exception e)
        {
            // Kein Internet / GitHub nicht erreichbar: mit der vorhandenen Version weiterspielen
            status("Update-Prüfung nicht möglich - starte mit der vorhandenen Version.");
            Debug.WriteLine(e);
            Thread.Sleep(1200);
        }
    }

    // ---------- Launcher + Desktop-Verknuepfung ----------

    private void InstallLauncherAndShortcut(string target)
    {
        string self = Assembly.GetExecutingAssembly().Location;
        string installed = Path.Combine(target, Config.InstalledLauncherName);

        // Launcher in den Spielordner kopieren, damit die Verknuepfung immer funktioniert (auch wenn die Download-Datei geloescht wird)
        if (!self.Equals(installed, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Copy(self, installed, true); }
            catch { installed = self; }
        }

        if (Environment.GetEnvironmentVariable("SPIELEABEND_NOSHORTCUT") == "1") return;
        string desktop = Environment.GetEnvironmentVariable("SPIELEABEND_SHORTCUTDIR");
        if (string.IsNullOrEmpty(desktop)) desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string link = Path.Combine(desktop, Config.ShortcutName + ".lnk");

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        dynamic shell = Activator.CreateInstance(shellType);
        dynamic shortcut = shell.CreateShortcut(link);
        shortcut.TargetPath = installed;
        shortcut.WorkingDirectory = target;
        shortcut.IconLocation = installed + ",0";
        shortcut.Description = "Among Us mit dem Spieleabend-Mod";
        shortcut.Save();
    }

    // ---------- Starten ----------

    private void StartGame(string target)
    {
        if (!Process.GetProcessesByName("steam").Any())
        {
            status("Starte Steam ...");
            Process.Start(new ProcessStartInfo("steam://open/main") { UseShellExecute = true });
            for (int i = 0; i < 30 && !Process.GetProcessesByName("steam").Any(); i++) Thread.Sleep(1000);
            Thread.Sleep(5000); // Steam braucht kurz zum Anmelden
        }

        status("Starte Among Us Spieleabend ...");
        Process.Start(new ProcessStartInfo(Path.Combine(target, "Among Us.exe"))
        {
            WorkingDirectory = target,
            UseShellExecute = true,
        });
    }

    private static bool IsRunning(string target)
    {
        string exe = Path.Combine(target, "Among Us.exe");
        foreach (var p in Process.GetProcessesByName("Among Us"))
        {
            try
            {
                if (string.Equals(p.MainModule?.FileName, exe, StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch { }
        }
        return false;
    }

    private static bool Is64Bit(string dll)
    {
        using var fs = File.OpenRead(dll);
        using var br = new BinaryReader(fs);
        fs.Seek(0x3C, SeekOrigin.Begin);
        int pe = br.ReadInt32();
        fs.Seek(pe + 4, SeekOrigin.Begin);
        return br.ReadUInt16() == 0x8664;
    }
}

/// <summary>Findet den Steam-Ordner von Among Us (Registry + alle Steam-Bibliotheken).</summary>
internal static class SteamLocator
{
    public static string FindAmongUs()
    {
        var libraries = new List<string>();
        string steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
        if (!string.IsNullOrEmpty(steam))
        {
            steam = steam.Replace('/', '\\');
            libraries.Add(steam);
            string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"(.+?)\""))
                    libraries.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
            }
        }

        foreach (var lib in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string game = Path.Combine(lib, "steamapps", "common", "Among Us");
            if (File.Exists(Path.Combine(game, "Among Us.exe"))) return game;
        }
        return null;
    }
}
