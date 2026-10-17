using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Spieleabend.Launcher;

/// <summary>
/// Wohin die Mod-Kopie kommt. Beim ersten Start fragt der Launcher wie ein Installer nach dem Ordner
/// (Vorschlag: ein Ordner im eigenen Benutzerprofil - dafuer braucht es keine Admin-Rechte).
/// Die Wahl wird gemerkt (Registry unter HKEY_CURRENT_USER, ebenfalls ohne Admin-Rechte).
/// </summary>
internal static class InstallLocation
{
    private const string RegKey = @"Software\AmongUsSpieleabend";
    private const string RegValue = "InstallDir";
    private const long NeededBytes = 1_500_000_000; // Spielkopie ca. 1 GB + Puffer

    /// <summary>Standard-Vorschlag: %LOCALAPPDATA%\Programs\Among Us Spieleabend (wie andere Programme ohne Admin-Installation).</summary>
    public static string DefaultFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", Config.GameFolderName);

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

        using var dialog = new LocationDialog(string.IsNullOrEmpty(saved) ? DefaultFolder : saved, steamGame);
        return dialog.ShowDialog(owner) == DialogResult.OK ? Remember(dialog.Folder) : null;
    }

    private static bool IsInstallation(string folder) =>
        !string.IsNullOrEmpty(folder) && File.Exists(Path.Combine(folder, "Among Us.exe")) && Directory.Exists(Path.Combine(folder, "BepInEx"));

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
        string full;
        try { full = Path.GetFullPath(folder).TrimEnd('\\'); }
        catch { return "Das ist kein gültiger Ordner."; }

        if (steamGame != null)
        {
            string game = Path.GetFullPath(steamGame).TrimEnd('\\');
            if (full.Equals(game, StringComparison.OrdinalIgnoreCase) || full.StartsWith(game + "\\", StringComparison.OrdinalIgnoreCase))
                return "Bitte nicht in den Ordner des normalen Among Us installieren - das bleibt unverändert.";
        }

        // Darf man dort schreiben? (ohne Admin-Rechte)
        try
        {
            Directory.CreateDirectory(full);
            string probe = Path.Combine(full, ".schreibtest");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
        }
        catch
        {
            return "In diesen Ordner darf ohne Administrator-Rechte nicht geschrieben werden.\nBitte einen anderen Ordner wählen (z. B. den Vorschlag).";
        }

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
            ClientSize = new Size(560, 236);
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
                Text = "Hier landet eine Kopie deines Steam-Among-Us mit dem Mod (ca. 1 GB).\nDein normales Among Us bleibt unverändert. Der Vorschlag braucht keine Administrator-Rechte.",
                AutoSize = false,
                Size = new Size(520, 66),
                Location = new Point(20, 50),
                ForeColor = Color.Gainsboro,
            };
            path = new TextBox
            {
                Text = suggestion,
                Location = new Point(20, 128),
                Size = new Size(400, 26),
            };
            var browse = new Button { Text = "Durchsuchen ...", Location = new Point(428, 126), Size = new Size(112, 30), ForeColor = Color.Black, BackColor = Color.Gainsboro };
            browse.Click += (_, _) => Browse();

            var install = new Button { Text = "Installieren", Location = new Point(320, 186), Size = new Size(110, 34), ForeColor = Color.Black, BackColor = Color.FromArgb(255, 160, 60) };
            install.Click += (_, _) => Confirm();
            var cancel = new Button { Text = "Abbrechen", Location = new Point(440, 186), Size = new Size(100, 34), ForeColor = Color.Black, BackColor = Color.Gainsboro, DialogResult = DialogResult.Cancel };

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
