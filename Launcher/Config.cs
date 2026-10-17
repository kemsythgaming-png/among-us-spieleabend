namespace Spieleabend.Launcher;

/// <summary>Einstellungen des Launchers.</summary>
internal static class Config
{
    /// <summary>
    /// GitHub-Projekt fuer automatische Updates, z. B. "deinname/among-us-spieleabend".
    /// Leer = keine Update-Pruefung (der Launcher installiert und startet nur).
    /// </summary>
    public const string GitHubRepo = "kemsythgaming-png/among-us-spieleabend";

    /// <summary>Name der Mod-Datei in den GitHub-Releases.</summary>
    public const string ModAssetName = "AmongUsSpieleabend.dll";

    public const string GameFolderName = "Among Us Spieleabend";
    public const string ShortcutName = "Among Us Spieleabend";
    public const string InstalledLauncherName = "AmongUsSpieleabend-Launcher.exe";
}
