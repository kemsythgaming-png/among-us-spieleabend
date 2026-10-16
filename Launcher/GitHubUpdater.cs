using System;
using System.Net;
using System.Text.RegularExpressions;

namespace Spieleabend.Launcher;

/// <summary>
/// Fragt das neueste Release auf GitHub ab (github.com/OWNER/REPO/releases) und laedt die Mod-DLL herunter.
/// Release-Name/Tag z. B. "v4.6.0", als Datei angehaengt: AmongUsSpieleabend.dll.
/// </summary>
internal static class GitHubUpdater
{
    internal sealed class Update
    {
        public Version Version;
        public string DownloadUrl;
    }

    /// <summary>Neueres Release als installed, oder null.</summary>
    public static Update CheckForUpdate(string repo, Version installed)
    {
        string json;
        using (var client = NewClient())
            json = client.DownloadString($"https://api.github.com/repos/{repo}/releases/latest");

        var tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([0-9]+(\\.[0-9]+){1,3})\"");
        if (!tag.Success) return null;
        var latest = Normalize(new Version(tag.Groups[1].Value));
        if (installed != null && latest <= Normalize(installed)) return null;

        var asset = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+/" + Regex.Escape(Config.ModAssetName) + ")\"");
        if (!asset.Success) return null;
        return new Update { Version = latest, DownloadUrl = asset.Groups[1].Value };
    }

    public static void Download(string url, string file)
    {
        using var client = NewClient();
        client.DownloadFile(url, file);
    }

    private static WebClient NewClient()
    {
        var client = new WebClient();
        client.Headers[HttpRequestHeader.UserAgent] = "AmongUsSpieleabend-Launcher";
        return client;
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));
}
