using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Spieleabend.Launcher;

/// <summary>Kleines Fenster mit Status und Fortschrittsbalken; die Arbeit macht der Installer im Hintergrund.</summary>
internal sealed class LauncherForm : Form
{
    private readonly Label status;
    private readonly ProgressBar progress;

    public LauncherForm()
    {
        Text = "Among Us Spieleabend";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(480, 170);
        BackColor = Color.FromArgb(24, 22, 34);
        ForeColor = Color.White;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        var title = new Label
        {
            Text = "Among Us Spieleabend",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 160, 60),
            AutoSize = true,
            Location = new Point(20, 18),
        };

        // Version des Launchers oben rechts - so sieht man, ob man die neueste EXE hat
        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        Text = $"Among Us Spieleabend {ver.Major}.{ver.Minor}.{ver.Build}";
        var versionLabel = new Label
        {
            Text = $"v{ver.Major}.{ver.Minor}.{ver.Build}",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(150, 150, 165),
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(ClientSize.Width - 70, 26),
        };
        status = new Label
        {
            Text = "Starte ...",
            Font = new Font("Segoe UI", 10f),
            AutoSize = false,
            Size = new Size(440, 44),
            Location = new Point(20, 64),
        };
        progress = new ProgressBar
        {
            Location = new Point(20, 118),
            Size = new Size(440, 22),
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
        };
        Controls.Add(title);
        Controls.Add(versionLabel);
        Controls.Add(status);
        Controls.Add(progress);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        var installer = new Installer(SetStatus, SetProgress);
        try
        {
            SetStatus("Suche Among Us (Steam) ...");
            string steamGame = SteamLocator.FindAmongUs();
            if (steamGame == null)
                throw new Exception("Among Us (Steam) wurde nicht gefunden.\nIst Among Us über Steam installiert?");

            // Beim ersten Mal: Speicherort wie bei einer Installation bestaetigen lassen
            string target = InstallLocation.Resolve(this, steamGame);
            if (target == null)
            {
                Close();
                return;
            }
            Log.Write("Zielordner: " + target);
            Log.UseGameFolder(target);

            bool started = await Task.Run(() => installer.Run(steamGame, target));
            if (started)
            {
                SetStatus("Viel Spaß!");
                await Task.Delay(1500);
            }
        }
        catch (Exception ex)
        {
            Log.Write("FEHLER: " + ex);
            if (Environment.GetEnvironmentVariable("SPIELEABEND_NOUI") != "1")
                MessageBox.Show(this, ex.Message + "\n\nDetails: " + Log.FilePath, "Among Us Spieleabend", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        Close();
    }

    private void SetStatus(string text)
    {
        Log.Write(text);
        if (InvokeRequired) { BeginInvoke(new Action(() => SetStatus(text))); return; }
        status.Text = text;
    }

    /// <summary>percent &lt; 0 = unbestimmt (laufender Balken).</summary>
    private void SetProgress(int percent)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => SetProgress(percent))); return; }
        if (percent < 0)
        {
            progress.Style = ProgressBarStyle.Marquee;
            return;
        }
        progress.Style = ProgressBarStyle.Continuous;
        progress.Value = Math.Max(0, Math.Min(100, percent));
    }
}
