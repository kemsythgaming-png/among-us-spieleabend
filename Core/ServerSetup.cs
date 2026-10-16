using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Spieleabend.Core;

/// <summary>
/// Traegt beim Start die Modded-Server in die Serverliste (regionInfo.json im eigenen Datenordner) ein,
/// bevor das Spiel sie liest. Beim allerersten Start wird "Modded EU" auch gleich ausgewaehlt.
/// Danach bleibt die Auswahl des Spielers erhalten.
/// </summary>
internal static class ServerSetup
{
    private static readonly (string Name, string Url)[] ModdedServers =
    {
        ("Modded EU (MEU)", "https://au-eu.duikbo.at"),
        ("Modded NA (MNA)", "https://aumods.org"),
        ("Modded Asia (MAS)", "https://au-as.duikbo.at"),
    };

    private const string DefaultServer = "Modded EU (MEU)";

    private static readonly (string Name, string Host, int TranslateName)[] OfficialServers =
    {
        ("North America", "matchmaker.among.us", 289),
        ("Europe", "matchmaker-eu.among.us", 290),
        ("Asia", "matchmaker-as.among.us", 291),
    };

    public static void Init(string dataPath)
    {
        try
        {
            var file = Path.Combine(dataPath, "regionInfo.json");
            var firstRunFlag = Path.Combine(dataPath, "spieleabend-server-gewaehlt.txt");

            JsonObject root = File.Exists(file)
                ? JsonNode.Parse(File.ReadAllText(file))?.AsObject()
                : null;

            if (root == null || root["Regions"] is not JsonArray)
            {
                root = new JsonObject { ["CurrentRegionIdx"] = 1, ["Regions"] = new JsonArray() };
                var official = root["Regions"].AsArray();
                foreach (var s in OfficialServers)
                    official.Add(MakeRegion(s.Name, s.Host, "https://" + s.Host, s.TranslateName));
            }

            var regions = root["Regions"].AsArray();
            int noTranslation = (int)StringNames.NoTranslation;
            int added = 0;

            foreach (var server in ModdedServers)
            {
                bool exists = regions.Any(r =>
                    (string)r?["Name"] == server.Name ||
                    (r?["Servers"] as JsonArray)?.Any(s => (string)s?["Ip"] == server.Url) == true);
                if (exists) continue;

                regions.Add(MakeRegion(server.Name, server.Url, server.Url, noTranslation));
                added++;
            }

            if (!File.Exists(firstRunFlag))
            {
                int index = regions.Select((r, i) => ((string)r?["Name"], i)).FirstOrDefault(x => x.Item1 == DefaultServer).i;
                root["CurrentRegionIdx"] = index;
                File.WriteAllText(firstRunFlag, $"Beim ersten Start wurde '{DefaultServer}' als Server ausgewaehlt. Diese Datei loeschen, um das erneut zu tun.");
                Plugin.Logger.LogInfo($"Server '{DefaultServer}' vorausgewaehlt.");
            }

            File.WriteAllText(file, root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }), new UTF8Encoding(false));
            if (added > 0) Plugin.Logger.LogInfo($"{added} Modded-Server in die Serverliste eingetragen.");
        }
        catch (Exception e)
        {
            // Serverliste ist nicht lebenswichtig - Fehler nur protokollieren
            Plugin.Logger.LogWarning($"Serverliste konnte nicht angepasst werden: {e.Message}");
        }
    }

    private static JsonObject MakeRegion(string name, string pingServer, string url, int translateName) => new()
    {
        // "$type" muss als erstes stehen, sonst erkennt das Spiel den Eintrag nicht
        ["$type"] = "StaticHttpRegionInfo, Assembly-CSharp",
        ["Name"] = name,
        ["PingServer"] = pingServer,
        ["Servers"] = new JsonArray(new JsonObject
        {
            ["Name"] = "Http-1",
            ["Ip"] = url,
            ["Port"] = 443,
            ["UseDtls"] = false,
            ["Players"] = 0,
            ["ConnectionFailures"] = 0,
        }),
        ["TargetServer"] = null,
        ["TranslateName"] = translateName,
    };
}
