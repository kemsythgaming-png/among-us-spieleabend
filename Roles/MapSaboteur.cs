using System.Linq;
using System.Reflection;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor (Stoersender): loest per Knopf die echte Kommunikations-Sabotage aus (kein Admin, keine Cams, keine
/// Karte/Tasks fuer die Crew). Sie laesst sich 10-20 Sekunden lang NICHT reparieren (auch nicht vom Feuerwehrmann),
/// zeigt keine Pfeile und geht danach von selbst wieder weg. Normaler Kill bleibt.
/// </summary>
public sealed class MapSaboteur : CustomRole
{
    public override string ConfigSection => "Stoersender";
    public override string Name => Lang.T("Störsender", "Jammer");
    public override Color Color => new Color32(220, 70, 60, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Schalt die Karte ab", "Shut down the map");
    public override string Description => Lang.T("Löse die Kommunikations-\nStörung aus (F).", "Trigger the comms\njam (F).");
    public override string LongDescription => Lang.T(
        "Du bist Impostor. Mit Taste F löst du die echte Kommunikations-Störung aus: Für die Crew gibt es 10-20 Sekunden lang keinen Admin, keine Kameras, keine Karte und keine Aufgaben-Anzeige. Sie lässt sich NICHT reparieren - auch der Feuerwehrmann kann sie nicht beheben - und endet von selbst. Deinen normalen Kill hast du zusätzlich.",
        "You are impostor. Press F to trigger the real comms jam: for 10-20 seconds the crew has no admin, no cameras, no map and no task display. It can NOT be repaired - not even by the Firefighter - and ends on its own. You also keep your normal kill.");

    internal static MapSaboteur Instance { get; private set; }

    private const byte ActionJam = 0;

    private RoleOption cooldown;
    private float jamUntil;
    private bool wasJamming;

    protected override void Setup()
    {
        Instance = this;
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 30f, 10f, 120f, 5f, "s");

        AddButton(new RoleButton
        {
            Label = "Karte stören|Jam map",
            Icon = "radar",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer) && Time.time >= jamUntil && !AnySabotage(),
            OnClick = _ => SendToAll(ActionJam, (byte)UnityEngine.Random.Range(10, 21)),
        });
    }

    public override void ResetState() { Instance = this; jamUntil = 0f; wasJamming = false; }
    public override void OnMeetingStart() => jamUntil = 0f;

    /// <summary>Freeplay-Test: ein Störsender-Dummy löst die Comms-Störung aus.</summary>
    public override bool FreeplayAbility(PlayerControl attacker, PlayerControl target)
    {
        jamUntil = Time.time + UnityEngine.Random.Range(10f, 20f);
        TriggerComms();
        return true;
    }

    /// <summary>Läuft die Störung dieser Rolle gerade? (Repair-Sperre + Feuerwehrmann.)</summary>
    internal bool JamActive => Time.time < jamUntil;

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionJam || data.Length < 1 || !RoleRegistry.Has(sender, this)) return;
        jamUntil = Time.time + data[0]; // 10-20 s, auf allen Clients gleich
        if (AmHost) TriggerComms();
    }

    public override void Update()
    {
        if (!AmHost) { wasJamming = JamActive; return; }
        // Fenster vorbei -> Störung von selbst beheben (während des Fensters ist Reparieren per Patch gesperrt)
        if (!JamActive && wasJamming) RepairComms();
        wasJamming = JamActive;
    }

    private static bool AnySabotage()
    {
        var ship = ShipStatus.Instance;
        if (!ship || ship.Systems == null) return false;
        try
        {
            return ship.Systems.ContainsKey(SystemTypes.Sabotage)
                && ship.Systems[SystemTypes.Sabotage].TryCast<SabotageSystemType>() is { AnyActive: true };
        }
        catch { return false; }
    }

    private static void TriggerComms()
    {
        try { if (ShipStatus.Instance) ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Sabotage, (byte)SystemTypes.Comms); }
        catch { }
    }

    private static void RepairComms()
    {
        try { if (ShipStatus.Instance && ShipStatus.Instance.Systems != null && ShipStatus.Instance.Systems.ContainsKey(SystemTypes.Comms)) ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Comms, 0); }
        catch { }
    }
}

/// <summary>
/// Während der Störsender-Störung lässt sich die Kommunikation NICHT reparieren: jede Comms-System-Aktualisierung
/// (Konsolen-Reparatur, auch vom Feuerwehrmann) wird verschluckt. Nur das automatische Ende (jamUntil vorbei) geht durch.
/// </summary>
[HarmonyPatch]
internal static class JamCommsBlockPatch
{
    public static MethodBase TargetMethod() =>
        typeof(ShipStatus).GetMethods()
            .FirstOrDefault(m => m.Name == "UpdateSystem" && m.GetParameters().Length == 3
                && m.GetParameters()[2].ParameterType.Name == "MessageReader");

    public static bool Prefix(SystemTypes __0) =>
        !(__0 == SystemTypes.Comms && (MapSaboteur.Instance?.JamActive ?? false));
}
