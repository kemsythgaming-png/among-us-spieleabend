using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Crew (Geheimagent): ein ganz normaler Crewmate mit Aufgaben - ABER den Impostoren wird er als zusätzlicher
/// (dritter) Impostor angezeigt (roter Name). Er selbst sieht NICHT, wer die echten Impostor sind. So wissen die
/// Impostor nicht, wer ihr echter Partner ist, und der Agent nicht, mit wem er es zu tun hat. Er gewinnt mit der Crew.
/// </summary>
public sealed class SecretAgent : CustomRole
{
    public override string ConfigSection => "Geheimagent";
    public override string Name => Lang.T("Geheimagent", "Secret Agent");
    public override Color Color => new Color32(200, 60, 60, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Die Impostor halten dich für einen von ihnen", "The impostors think you're one of them");
    public override string Description => Lang.T("Du bist Crew mit Aufgaben,\ndoch Impostor sehen dich als Impostor.", "You're crew with tasks,\nbut impostors see you as an impostor.");
    public override string LongDescription => Lang.T(
        "Du bist in Wahrheit ein Crewmate mit ganz normalen Aufgaben und gewinnst mit der Crew. Für die Impostor wirst du aber als zusätzlicher (dritter) Impostor angezeigt - dein Name ist für sie rot. Du selbst siehst NICHT, wer die echten Impostor sind. Dadurch wissen die Impostor nicht, wer ihr echter Partner ist. Mach deine Aufgaben und nutze das Misstrauen.",
        "You are really a crewmate with normal tasks and win with the crew. But the impostors see you as an extra (third) impostor - your name is red to them. You yourself do NOT see who the real impostors are. So the impostors can't tell which teammate is real. Do your tasks and use the confusion.");

    protected override void Setup() { }

    /// <summary>Impostor sehen den Agenten als Impostor (roten Namen) - sonst niemand.</summary>
    public override Color? GetNameColor(PlayerControl viewer, PlayerControl target)
    {
        if (!viewer || !target || viewer == target) return null;
        if (RoleRegistry.Has(target, this) && IsImpostor(viewer)) return ImpostorRed;
        return null;
    }
}

/// <summary>Im Impostor-Intro wird der Geheimagent als zusätzliches Teammitglied angezeigt (nur Impostor sehen dieses Intro).</summary>
[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginImpostor))]
internal static class SecretAgentIntroPatch
{
    public static void Prefix(Il2CppSystem.Collections.Generic.List<PlayerControl> __0)
    {
        try
        {
            if (__0 == null) return;
            foreach (var p in PlayerControl.AllPlayerControls)
                if (p && RoleRegistry.GetRole(p) is SecretAgent && !__0.Contains(p)) __0.Add(p);
        }
        catch { }
    }
}
