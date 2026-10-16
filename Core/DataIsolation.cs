using System;
using System.IO;
using HarmonyLib;
using Innersloth.IO;

namespace Spieleabend.Core;

/// <summary>
/// Leitet den Spieldaten-Ordner der Mod-Kopie auf einen eigenen Ordner um.
/// Ohne das teilen sich Mod-Kopie (alte Version) und Steam-Spiel (neue Version)
/// "AppData\LocalLow\Innersloth\Among Us", und die alte Version ueberschreibt
/// z. B. settings.amogus in einem aelteren Format.
/// </summary>
internal static class DataIsolation
{
    public static readonly string RootPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData", "LocalLow", "Innersloth", "Among Us Spieleabend");

    private static readonly string VanillaPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData", "LocalLow", "Innersloth", "Among Us");

    public static void Init()
    {
        bool firstRun = !Directory.Exists(RootPath);
        Directory.CreateDirectory(RootPath);

        // Nur beim ersten Start: Name und Kosmetik einmalig uebernehmen (nur lesen, Original bleibt unberuehrt)
        if (firstRun)
        {
            foreach (var file in new[] { "player.amogus", "regionInfo.json" })
            {
                var src = Path.Combine(VanillaPath, file);
                if (File.Exists(src)) File.Copy(src, Path.Combine(RootPath, file));
            }
        }

        ServerSetup.Init(RootPath);
        Plugin.Logger.LogInfo($"Spieldaten-Ordner: {RootPath}");
    }
}

[HarmonyPatch(typeof(PlatformPaths), nameof(PlatformPaths.persistentDataPath), MethodType.Getter)]
internal static class PersistentDataPathPatch
{
    public static bool Prefix(ref string __result)
    {
        __result = DataIsolation.RootPath;
        return false;
    }
}

[HarmonyPatch(typeof(FileIO), nameof(FileIO.GetRootDataPath))]
internal static class FileIORootPatch
{
    public static bool Prefix(ref string __result)
    {
        __result = DataIsolation.RootPath;
        return false;
    }
}

[HarmonyPatch(typeof(DefaultFileIO), nameof(DefaultFileIO.GetRootDataPath))]
internal static class DefaultFileIORootPatch
{
    public static bool Prefix(ref string __result)
    {
        __result = DataIsolation.RootPath;
        return false;
    }
}
