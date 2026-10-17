using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace Spieleabend.Launcher;

/// <summary>
/// Versucht, den Zielordner bei Windows Defender als Ausnahme einzutragen, damit der Scanner die Mod-Dateien
/// (vor allem BepInEx' winhttp.dll) nicht in Quarantaene schiebt.
///
/// Das geht NUR bei Windows Defender und NUR mit Administrator-Rechten (es erscheint ein "Ja"-Fenster von Windows).
/// Mit aktivem Manipulationsschutz ("Tamper Protection") kann es trotzdem scheitern - dann zeigen wir die Anleitung.
/// Fuer andere Virenscanner (Avast, Kaspersky ...) gibt es keinen solchen Weg; dort hilft nur die Anleitung.
/// </summary>
internal static class Defender
{
    /// <summary>
    /// Pruefung (langsam, darf im Hintergrund laufen): Lohnt es sich, die Defender-Ausnahme anzubieten?
    /// Nur wenn Defender der aktive Scanner ist, der Ordner noch keine Ausnahme ist und (noch) kein Mod drinliegt.
    /// </summary>
    public static bool ShouldOffer(string folder)
    {
        try
        {
            if (Environment.GetEnvironmentVariable("SPIELEABEND_NOUI") == "1") return false;
            if (File.Exists(Path.Combine(folder, "winhttp.dll"))) return false; // laeuft schon - nicht nerven
            if (!DefenderIsActive()) return false;                              // anderer Scanner oder Defender aus
            if (IsExcluded(folder)) return false;                               // schon freigegeben
            return true;
        }
        catch (Exception e) { Log.Write("Defender-Pruefung: " + e.Message); return false; }
    }

    /// <summary>Fragt (UI-Thread) und traegt die Ausnahme ein; bei Misserfolg Anleitung. VOR dem Kopieren aufrufen.</summary>
    public static void RunExclusionFlow(IWin32Window owner, string folder)
    {
        try
        {
            var answer = MessageBox.Show(owner,
                "Windows Defender (der Virenschutz) könnte gleich Mod-Dateien blockieren und löschen.\n\n" +
                "Sollen wir den Ordner\n" + folder + "\neinmalig als Ausnahme eintragen, damit das nicht passiert?\n\n" +
                "Gleich erscheint ein Windows-Fenster \"Möchtest du zulassen, dass diese App Änderungen vornimmt?\" - " +
                "dort bitte auf \"Ja\" klicken.\n\n" +
                "Nur dieser eine Ordner wird ausgenommen. Dein normales Among Us ist nicht betroffen.",
                "Among Us Spieleabend", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (answer != DialogResult.Yes) return;

            bool ok = TryAddExclusion(folder);
            Log.Write("Defender-Ausnahme fuer " + folder + ": " + (ok ? "gesetzt" : "nicht moeglich"));
            if (ok) return;

            MessageBox.Show(owner,
                "Die automatische Ausnahme hat nicht geklappt (oft wegen des \"Manipulationsschutzes\" von Windows).\n\n" +
                "So geht es von Hand - das muss nur einmal gemacht werden:\n\n" +
                "1. \"Windows-Sicherheit\" öffnen → \"Viren- & Bedrohungsschutz\".\n" +
                "2. Unter \"Einstellungen für Viren- & Bedrohungsschutz\" auf \"Einstellungen verwalten\".\n" +
                "3. Ganz unten bei \"Ausschlüsse\" auf \"Ausschlüsse hinzufügen oder entfernen\".\n" +
                "4. \"Ausschluss hinzufügen\" → \"Ordner\" → diesen Ordner wählen:\n" + folder + "\n\n" +
                "Danach den Launcher noch einmal starten.",
                "Among Us Spieleabend", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception e) { Log.Write("Defender-Ausnahme: " + e.Message); }
    }

    /// <summary>Ist Windows Defender der aktive Echtzeit-Scanner (nicht nur im Passiv-Modus neben einem anderen)?</summary>
    private static bool DefenderIsActive()
    {
        // AMRunningMode: "Normal" = Defender ist der Hauptscanner; "Passive"/"SxS Passive Mode"/"EDR Block Mode" = anderer AV aktiv
        string result = RunPowerShell(
            "$s = Get-MpComputerStatus; if ($s.RealTimeProtectionEnabled -and $s.AMRunningMode -eq 'Normal') { 'JA' } else { 'NEIN' }");
        return result != null && result.Contains("JA");
    }

    private static bool IsExcluded(string folder)
    {
        string f = folder.TrimEnd('\\').Replace("'", "''");
        string result = RunPowerShell(
            "$p = (Get-MpPreference).ExclusionPath; if ($p -and ($p -contains '" + f + "' -or $p -contains '" + f + "\\')) { 'JA' } else { 'NEIN' }");
        return result != null && result.Contains("JA");
    }

    /// <summary>Fuegt die Ausnahme per erhoehter PowerShell hinzu (UAC-"Ja") und prueft danach, ob sie wirklich da ist.</summary>
    private static bool TryAddExclusion(string folder)
    {
        string f = folder.Replace("'", "''");
        var psi = new ProcessStartInfo("powershell.exe",
            "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"Add-MpPreference -ExclusionPath '" + f + "'\"")
        {
            UseShellExecute = true,
            Verb = "runas", // loest das UAC-"Ja"-Fenster aus
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
        };
        try
        {
            var p = Process.Start(psi);
            p.WaitForExit(20000);
        }
        catch (System.ComponentModel.Win32Exception) { return false; } // "Ja" nicht geklickt / abgebrochen
        return IsExcluded(folder);
    }

    private static string RunPowerShell(string command)
    {
        try
        {
            var psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"" + command.Replace("\"", "\\\"") + "\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            using var p = Process.Start(psi);
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(15000);
            return output;
        }
        catch { return null; }
    }
}
