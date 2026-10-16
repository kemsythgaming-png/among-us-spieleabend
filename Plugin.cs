using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Spieleabend.Core;

namespace Spieleabend;

[BepInPlugin(Id, Name, Version)]
[BepInProcess("Among Us.exe")]
public class Plugin : BasePlugin
{
    public const string Id = "de.spieleabend.amongus";
    public const string Name = "Among Us Spieleabend";
    public const string Version = "4.5.0";

    internal static ManualLogSource Logger;

    public Harmony Harmony { get; } = new(Id);

    public override void Load()
    {
        Logger = Log;

        DataIsolation.Init();
        Lang.Bind(Config);
        FreeplayTest.BindConfig(Config);
        GeneralOptions.Init(Config);
        RoleRegistry.DiscoverRoles(Config);

        Harmony.PatchAll();
        AddComponent<SettingsWindow>();

        Logger.LogInfo($"{Name} {Version} geladen, {RoleRegistry.Roles.Count} Rolle(n): {string.Join(", ", RoleRegistry.Roles.ConvertAll(r => r.Name))}");
    }
}
