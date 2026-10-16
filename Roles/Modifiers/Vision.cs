using AmongUs.GameOptions;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Modifiers;

/// <summary>Anpassung: sieht weniger weit.</summary>
public sealed class Nearsighted : CustomRole
{
    public override string ConfigSection => "Kurzsichtig";
    public override string Name => Lang.T("Kurzsichtig", "Nearsighted");
    public override Color Color => new Color32(130, 130, 170, 255);
    public override RoleTeam Team => RoleTeam.Modifier;
    public override string IntroText => Lang.T("Wo ist meine Brille?", "Where are my glasses?");
    public override string Description => Lang.T("Du siehst weniger weit.", "You can't see as far.");
    public override string LongDescription => Lang.T(
        "Anpassung: Deine Sicht ist kleiner als die der anderen. Deine Rolle bleibt.",
        "Modifier: your vision is smaller than everyone else's. Your role stays.");

    internal static Nearsighted Instance { get; private set; }
    internal RoleOption Factor { get; private set; }

    protected override void Setup()
    {
        Instance = this;
        Factor = AddOption("Factor", "Sicht|Vision", 0.6f, 0.3f, 0.9f, 0.05f, "x");
    }
}

/// <summary>Anpassung: sieht weiter.</summary>
public sealed class EagleEye : CustomRole
{
    public override string ConfigSection => "Adlerauge";
    public override string Name => Lang.T("Adlerauge", "Eagle Eye");
    public override Color Color => new Color32(220, 180, 90, 255);
    public override RoleTeam Team => RoleTeam.Modifier;
    public override string IntroText => Lang.T("Dir entgeht nichts", "Nothing escapes you");
    public override string Description => Lang.T("Du siehst weiter.", "You can see further.");
    public override string LongDescription => Lang.T(
        "Anpassung: Deine Sicht ist größer als die der anderen. Deine Rolle bleibt.",
        "Modifier: your vision is bigger than everyone else's. Your role stays.");

    internal static EagleEye Instance { get; private set; }
    internal RoleOption Factor { get; private set; }

    protected override void Setup()
    {
        Instance = this;
        Factor = AddOption("Factor", "Sicht|Vision", 1.5f, 1.1f, 2.5f, 0.1f, "x");
    }
}

/// <summary>Anpassung (Crew): sieht auch bei der Licht-Sabotage normal.</summary>
public sealed class Torch : CustomRole
{
    public override string ConfigSection => "Fackel";
    public override string Name => Lang.T("Fackel", "Torch");
    public override Color Color => new Color32(255, 170, 60, 255);
    public override RoleTeam Team => RoleTeam.Modifier;
    public override bool ModifierAllowsImpostors => false;
    public override string IntroText => Lang.T("Dir geht nie das Licht aus", "Your light never goes out");
    public override string Description => Lang.T("Licht-Sabotage stört dich nicht.", "Light sabotage doesn't affect you.");
    public override string LongDescription => Lang.T(
        "Anpassung (nur Crew): Wenn die Impostor das Licht sabotieren, siehst du trotzdem normal weit. Deine Rolle bleibt.",
        "Modifier (crew only): when the impostors sabotage the lights, you still see normally. Your role stays.");

    internal static Torch Instance { get; private set; }

    protected override void Setup() => Instance = this;
}

/// <summary>Sichtweite fuer Kurzsichtig, Adlerauge und Fackel anpassen.</summary>
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CalculateLightRadius))]
internal static class VisionPatch
{
    public static void Postfix(ShipStatus __instance, NetworkedPlayerInfo player, ref float __result)
    {
        if (!RoleRegistry.HasAssignments || player == null || player.IsDead) return;
        var pc = player.Object;
        if (!pc) return;

        // Fackel: so weit wie bei vollem Licht
        if (Torch.Instance != null && RoleRegistry.Has(pc, Torch.Instance) && player.Role && !player.Role.IsImpostor)
        {
            float crewLight = GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.CrewLightMod);
            __result = Mathf.Max(__result, __instance.MaxLightRadius * crewLight);
        }

        if (Nearsighted.Instance != null && RoleRegistry.Has(pc, Nearsighted.Instance)) __result *= Nearsighted.Instance.Factor.Value;
        if (EagleEye.Instance != null && RoleRegistry.Has(pc, EagleEye.Instance)) __result *= EagleEye.Instance.Factor.Value;
    }
}
