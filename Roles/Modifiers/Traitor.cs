using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Modifiers;

/// <summary>
/// Anpassung (nur Crew): Sterben alle Impostor, wird der Verraeter (mit einstellbarer Chance) zum neuen Impostor -
/// das Spiel geht weiter, statt dass die Crew gewinnt.
/// </summary>
public sealed class Traitor : CustomRole
{
    public override string ConfigSection => "Verraeter";
    public override string Name => Lang.T("Verräter", "Traitor");
    public override Color Color => new Color32(170, 40, 40, 255);
    public override RoleTeam Team => RoleTeam.Modifier;
    public override bool ModifierAllowsImpostors => false;

    /// <summary>Nur Crew - keine Impostor und keine Neutralen (Jester, Zombie ...).</summary>
    public override bool CanReceiveModifier(PlayerControl player, CustomRole mainRole) =>
        !IsImpostor(player) && mainRole?.Team != RoleTeam.Neutral;
    public override string IntroText => Lang.T("Dein Moment kommt noch ...", "Your time will come ...");
    public override string Description => Lang.T("Sterben alle Impostor,\nwirst du der neue Impostor.", "If all impostors die,\nyou become the new impostor.");
    public override string LongDescription => Lang.T(
        "Anpassung (nur Crew): Du spielst erst ganz normal in der Crew. Sterben aber alle Impostor, wirst du (mit einstellbarer Chance) zum neuen Impostor - mit Kill-Button - und das Spiel geht weiter.\n\nBis dahin hilfst du der Crew ganz normal. Nur du weißt, dass du ein Verräter bist.",
        "Modifier (crew only): you start as a normal crewmate. But if all impostors die, you become the new impostor (configurable chance) - with a kill button - and the game goes on.\n\nUntil then you help the crew as usual. Only you know you are a traitor.");

    internal static Traitor Instance { get; private set; }

    private RoleOption turnChance;
    private bool decided;
    private float activatedAt = -10f;

    protected override void Setup()
    {
        Instance = this;
        turnChance = AddOption("TurnChance", "Chance, Impostor zu werden|Chance to turn impostor", 100f, 10f, 100f, 10f, "%");
    }

    public override void ResetState()
    {
        decided = false;
        activatedAt = -10f;
    }

    /// <summary>
    /// Host: Wenn kein Impostor mehr lebt, einen lebenden Verraeter zum Impostor machen.
    /// true = es wurde gerade jemand verwandelt (dann darf das Spiel in diesem Moment nicht enden).
    /// </summary>
    internal bool TryActivate()
    {
        // Kurz nach der Verwandlung das Spielende weiter anhalten, bis die neue Rolle ueberall angekommen ist
        if (Time.time - activatedAt < 1.5f) return true;
        if (decided || !AmHost || !RoleRegistry.HasAssignments || FreeplayTest.IsFreeplay) return false;

        var alive = PlayerControl.AllPlayerControls.ToArray().Where(IsAlive).ToList();
        if (alive.Any(IsImpostor)) return false;

        // Nur Verraeter, die (noch) Crew sind - z. B. kein inzwischen infizierter Zombie
        var traitor = alive.Where(p => RoleRegistry.Has(p, this) && !IsNeutral(p)).OrderBy(_ => Random.value).FirstOrDefault();
        if (!traitor) return false;

        decided = true; // nur einmal pro Spiel wuerfeln
        if (Random.Range(0, 100) >= turnChance.Int) return false;

        // Crew-Faehigkeit (Sheriff, Medic ...) weg - ab jetzt ein normaler Impostor
        if (RoleRegistry.GetRole(traitor) != null) RoleRegistry.ChangeRole(traitor, null);
        traitor.RpcSetRole(RoleTypes.Impostor, true);
        activatedAt = Time.time;
        SendToAll(0, traitor.PlayerId);
        Plugin.Logger.LogInfo($"Verraeter {traitor.Data.PlayerName} ist jetzt Impostor.");
        return true;
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != 0 || data.Length < 1) return;
        var local = PlayerControl.LocalPlayer;
        if (!local || local.PlayerId != data[0]) return;
        Notifier.Show($"<color={ColorHex}>{Lang.T("Alle Impostor sind tot - jetzt bist DU der Impostor!", "All impostors are dead - now YOU are the impostor!")}</color>", 7f);
        Sfx.Play(Sfx.Creepy);
    }
}

/// <summary>
/// Stirbt der letzte Impostor, wuerde das Spiel sofort als Crew-Sieg enden.
/// Gibt es einen Verraeter, wird er vorher zum Impostor und das Spiel laeuft weiter.
/// </summary>
[HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
internal static class TraitorEndCriteriaPatch
{
    public static bool Prefix() => !(Traitor.Instance != null && Traitor.Instance.TryActivate());
}
