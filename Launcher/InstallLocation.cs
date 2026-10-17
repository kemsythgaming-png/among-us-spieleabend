using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Spieleabend.Launcher;

/// <summary>
/// Wohin die Mod-Kopie kommt. Beim ersten Start fragt der Launcher wie ein Installer nach dem Ordner.
/// Vorschlag: C:\Spieleabend\Among Us Spieleabend - ohne Umlaute/Benutzernamen (damit hat BepInEx Probleme:
/// "Failed to start BepInEx") und ohne Admin-Rechte (Ordner direkt auf C:\ darf jeder Benutzer anlegen).
/// Die Wahl wird gemerkt (Registry unter HKEY_CURRENT_USER, ebenfalls ohne Admin-Rechte).
/// </summary>
internal static class InstallLocation
{
    private const string RegKey = @"Software\AmongUsSpieleabend";
    private const string RegValue = "InstallDir";
    private const long NeededBytes = 1_500_000_000; // Spielkopie ca. 1 GB + Puffer

    /// <summary>
    /// Standard-Vorschlag: C:\Spieleabend\Among Us Spieleabend. Gibt es kein C: (oder ist es kein normales Laufwerk),
    /// dann der oeffentliche Benutzerordner C:\Users\Public - der hat ebenfalls keinen Benutzernamen im Pfad.
    /// </summary>
    public static string DefaultFolder => defaultFolder ??= FindWritableDefault();
    private static string defaultFolder;

    /// <summary>
    /// Erster Ort, an dem man wirklich schreiben darf (manche PCs sperren C:\ oder wollen nur auf D: speichern):
    /// C:\Spieleabend, dann \Spieleabend auf den anderen festen Laufwerken (D:, E: ...), dann der oeffentliche Ordner.
    /// Getestet wird ohne Spuren: angelegte Testordner werden wieder geloescht.
    /// </summary>
    private static string FindWritableDefault()
    {
        var candidates = new System.Collections.Generic.List<string>();
        try
        {
            foreach (var d in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                         .OrderBy(d => d.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(d => d.Name))
                if (d.AvailableFreeSpace >= NeededBytes) candidates.Add(Path.Combine(d.Name, "Spieleabend", Config.GameFolderName));
        }
        catch { }
        string pub = Environment.GetEnvironmentVariable("PUBLIC");
        if (!string.IsNullOrEmpty(pub) && IsPlainPath(pub)) candidates.Add(Path.Combine(pub, "Spieleabend", Config.GameFolderName));

        foreach (var c in candidates)
            if (CanWriteWithoutTrace(c)) return c;
        return candidates.FirstOrDefault() ?? Path.Combine(@"C:\Spieleabend", Config.GameFolderName);
    }

    /// <summary>Darf man hier Ordner anlegen, Dateien schreiben, umbenennen und loeschen? Hinterlaesst nichts.</summary>
    private static bool CanWriteWithoutTrace(string folder)
    {
        string firstCreated = null;
        try
        {
            // ersten noch nicht vorhandenen Ordner merken, um ihn danach wieder zu entfernen
            string probe = folder;
            while (!string.IsNullOrEmpty(probe) && !Directory.Exists(probe))
            {
                firstCreated = probe;
                probe = Path.GetDirectoryName(probe);
            }
            return RightsError(folder) == null;
        }
        catch { return false; }
        finally
        {
            try { if (firstCreated != null && Directory.Exists(firstCreated)) Directory.Delete(firstCreated, true); }
            catch { }
        }
    }

    /// <summary>Alle Rechte, die der Launcher spaeter braucht: Ordner anlegen, schreiben, umbenennen (Updates), loeschen.</summary>
    private static string RightsError(string folder)
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(folder, "Logs"));
            string a = Path.Combine(folder, ".rechtetest");
            string b = a + "2";
            File.WriteAllText(a, "ok");
            if (File.ReadAllText(a) != "ok") return "Dateien lassen sich dort nicht richtig lesen.";
            if (File.Exists(b)) File.Delete(b);
            File.Move(a, b);
            File.Delete(b);
            return null;
        }
        catch (UnauthorizedAccessException) { return "keine Schreibrechte"; }
        catch (Exception e) { return e.Message; }
    }

    /// <summary>Nur einfache Zeichen (kein Umlaut, kein Sonderzeichen) - sonst startet BepInEx womoeglich nicht.</summary>
    public static bool IsPlainPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        foreach (char ch in path)
            if (ch > 126) return false;
        return true;
    }

    /// <summary>
    /// Zielordner bestimmen. null = abgebrochen.
    /// Reihenfolge: Test-Umgebungsvariable, gemerkter Ordner, Launcher liegt schon in einer Installation,
    /// alte Installation neben dem Steam-Spiel, sonst nachfragen.
    /// </summary>
    public static string Resolve(IWin32Window owner, string steamGame)
    {
        string env = Environment.GetEnvironmentVariable("SPIELEABEND_TARGET");
        if (!string.IsNullOrEmpty(env)) return env;

        string saved = Registry.GetValue(@"HKEY_CURRENT_USER\" + RegKey, RegValue, null) as string;

        // Shift beim Start gedrueckt: Ordner neu waehlen (z. B. auf ein anderes Laufwerk umziehen)
        if ((Control.ModifierKeys & Keys.Shift) != 0 && Environment.GetEnvironmentVariable("SPIELEABEND_NOUI") != "1")
        {
            using var move = new LocationDialog(IsPlainPath(saved) ? saved : DefaultFolder, steamGame);
            return move.ShowDialog(owner) == DialogResult.OK ? Remember(move.Folder) : null;
        }
        if (IsInstallation(saved)) return saved;

        string self = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        if (IsInstallation(self)) return Remember(self);

        // Fruehere Launcher-Versionen haben neben das Steam-Spiel installiert - die Installation weiter nutzen
        if (steamGame != null)
        {
            string old = Path.Combine(Path.GetDirectoryName(steamGame), Config.GameFolderName);
            if (IsInstallation(old)) return Remember(old);
        }

        if (Environment.GetEnvironmentVariable("SPIELEABEND_NOUI") == "1") return Remember(DefaultFolder);

        using var dialog = new LocationDialog(IsPlainPath(saved) ? saved : DefaultFolder, steamGame);
        return dialog.ShowDialog(owner) == DialogResult.OK ? Remember(dialog.Folder) : null;
    }

    private static bool IsInstallation(string folder) =>
        !string.IsNullOrEmpty(folder) && IsPlainPath(folder)
        && File.Exists(Path.Combine(folder, "Among Us.exe")) && Directory.Exists(Path.Combine(folder, "BepInEx"));

    private static string Remember(string folder)
    {
        try { Registry.SetValue(@"HKEY_CURRENT_USER\" + RegKey, RegValue, folder); }
        catch (Exception e) { Log.Write("Ordner merken: " + e.Message); }
        return folder;
    }

    /// <summary>Fehlertext, wenn der Ordner nicht geht - sonst null.</summary>
    public static string Check(string folder, string steamGame)
    {
        if (string.IsNullOrWhiteSpace(folder)) return "Bitte einen Ordner wählen.";
        if (!IsPlainPath(folder))
            return "Der Pfad enthält Umlaute oder Sonderzeichen (z. B. ä, ö, ü, ß).\nDamit startet der Mod-Lader nicht. Bitte einen einfachen Pfad wählen, z. B. " + DefaultFolder;
        string full;
        try { full = Path.GetFullPath(folder).TrimEnd('\\'); }
        catch { return "Das ist kein gültiger Ordner."; }

        if (steamGame != null)
        {
            string game = Path.GetFullPath(steamGame).TrimEnd('\\');
            if (full.Equals(game, StringComparison.OrdinalIgnoreCase) || full.StartsWith(game + "\\", StringComparison.OrdinalIgnoreCase))
                return "Bitte nicht in den Ordner des normalen Among Us installieren - das bleibt unverändert.";
        }

        // Alle Rechte, die spaeter gebraucht werden (anlegen, schreiben, umbenennen fuer Updates, loeschen) - ohne Admin
        string rights = RightsError(full);
        if (rights != null)
            return "In diesem Ordner fehlen Rechte (" + rights + ").\nBitte einen anderen Ordner wählen - z. B. " + DefaultFolder +
                   "\n(Auf manchen PCs ist C:\\ gesperrt, dann z. B. D:\\Spieleabend.)";

        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(full));
            if (drive.IsReady && drive.AvailableFreeSpace < NeededBytes && !File.Exists(Path.Combine(full, "Among Us.exe")))
                return $"Auf Laufwerk {drive.Name} ist zu wenig Platz frei (gebraucht: ca. 1,5 GB).";
        }
        catch { }
        return null;
    }

    /// <summary>Fenster wie bei einer Installation: Ordner anzeigen, "Durchsuchen ...", "Installieren".</summary>
    private sealed class LocationDialog : Form
    {
        private readonly TextBox path;
        private readonly string steamGame;

        public string Folder => path.Text.Trim();

        public LocationDialog(string suggestion, string steamGame)
        {
            this.steamGame = steamGame;
            Text = "Among Us Spieleabend - Installation";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 256);
            BackColor = Color.FromArgb(24, 22, 34);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10f);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var title = new Label
            {
                Text = "Wohin soll Among Us Spieleabend installiert werden?",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 160, 60),
                AutoSize = true,
                Location = new Point(20, 16),
            };
            var info = new Label
            {
                Text = "Hier landet eine Kopie deines Steam-Among-Us mit dem Mod (ca. 1 GB).\nDein normales Among Us bleibt unverändert. Bitte einen Pfad ohne Umlaute wählen - der Vorschlag passt und braucht keine Administrator-Rechte.\nSpäter ändern: beim Start des Launchers Shift gedrückt halten.",
                AutoSize = false,
                Size = new Size(520, 88),
                Location = new Point(20, 50),
                ForeColor = Color.Gainsboro,
            };
            path = new TextBox
            {
                Text = suggestion,
                Location = new Point(20, 148),
                Size = new Size(400, 26),
            };
            var browse = new Button { Text = "Durchsuchen ...", Location = new Point(428, 146), Size = new Size(112, 30), ForeColor = Color.Black, BackColor = Color.Gainsboro };
            browse.Click += (_, _) => Browse();

            var install = new Button { Text = "Installieren", Location = new Point(320, 206), Size = new Size(110, 34), ForeColor = Color.Black, BackColor = Color.FromArgb(255, 160, 60) };
            install.Click += (_, _) => Confirm();
            var cancel = new Button { Text = "Abbrechen", Location = new Point(440, 206), Size = new Size(100, 34), ForeColor = Color.Black, BackColor = Color.Gainsboro, DialogResult = DialogResult.Cancel };

            path.SelectionStart = path.Text.Length; // Ende des Pfads sichtbar, nichts markiert
            AcceptButton = install;
            CancelButton = cancel;
            Controls.AddRange(new Control[] { title, info, path, browse, install, cancel });
        }

        private void Browse()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Ordner für Among Us Spieleabend wählen (ein Unterordner \"" + Config.GameFolderName + "\" wird angelegt)",
                ShowNewFolderButton = true,
            };
            try
            {
                string start = Folder;
                while (!string.IsNullOrEmpty(start) && !Directory.Exists(start)) start = Path.GetDirectoryName(start);
                if (!string.IsNullOrEmpty(start)) dialog.SelectedPath = start;
            }
            catch { }

            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            string chosen = dialog.SelectedPath;
            // Wie bei Installern: eigenen Unterordner anhaengen, ausser er heisst schon so
            if (!Path.GetFileName(chosen.TrimEnd('\\')).Equals(Config.GameFolderName, StringComparison.OrdinalIgnoreCase))
                chosen = Path.Combine(chosen, Config.GameFolderName);
            path.Text = chosen;
        }

        private void Confirm()
        {
            string error = Check(Folder, steamGame);
            if (error != null)
            {
                MessageBox.Show(this, error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
