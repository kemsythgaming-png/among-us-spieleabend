using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Vergibt Geisterrollen beim Tod (nur der Host): Anzahl und Chance wie bei jeder Rolle, jede Rolle entscheidet mit
/// CanHaunt selbst, wer sie bekommen darf. Hoechstens eine Geisterrolle pro Spieler.
/// </summary>
internal static class GhostRoles
{
    private static readonly Dictionary<byte, int> used = new();

    public static void Reset() => used.Clear();

    private static float nextCheck;

    /// <summary>
    /// Rollen, die nach dem Tod KEIN normaler Geist werden: eigene Geisterrollen (Spukgestalt/Waechtergeist/Poltergeist)
    /// sowie Survivor und Untoter, solange sie wieder aufstehen. Diese bekommen keine Spiel-Geisterrolle und keinen
    /// Haunt-Knopf. Normale Tote (auch Guardian Angel / Spirit Guide) bleiben unberuehrt.
    /// </summary>
    internal static bool PlainGhostOnly(PlayerControl p)
    {
        if (p == null || p.Data == null) return false;
        if (RoleRegistry.GetGhostRole(p) != null) return true;
        if (Roles.Modifiers.SecondLife.Instance?.WillRise(p) ?? false) return true;
        if (Roles.Halloween.Undead.Instance?.WillRise(p) ?? false) return true;
        return false;
    }

    /// <summary>Host, regelmaessig: betroffene Rollen behalten keine Spiel-Geisterrolle (falls das Spiel sie vergibt).</summary>
    internal static void Update()
    {
        if (!CustomRole.AmHost || Time.time < nextCheck) return;
        nextCheck = Time.time + 0.5f;
        foreach (var p in PlayerControl.AllPlayerControls)
            if (p && p.Data != null && p.Data.IsDead && !p.Data.Disconnected && PlainGhostOnly(p)) EnsurePlainGhost(p);
    }

    /// <summary>
    /// Den Vanilla-"Haunt"-Knopf (Geister-Faehigkeit, die jeder normale Geist hat) fuer den lokalen Spieler ausblenden,
    /// wenn er eine Rolle hat, die kein normaler Geist wird (Spukgestalt, Survivor ...). Laeuft bei jedem Client.
    /// </summary>
    internal static void SuppressHauntButton()
    {
        var local = PlayerControl.LocalPlayer;
        var hud = HudManager.Instance;
        if (!local || local.Data == null || !hud || !hud.AbilityButton) return;
        if (local.Data.IsDead && PlainGhostOnly(local)) hud.AbilityButton.gameObject.SetActive(false);
    }

    /// <summary>Keine Spiel-Geisterrolle (Guardian Angel, Spirit Guide ...) - diese Rolle ist nur ein reiner Geist.</summary>
    private static void EnsurePlainGhost(PlayerControl p)
    {
        try
        {
            if (!p.Data.Role) return;
            var plain = p.Data.Role.IsImpostor ? RoleTypes.ImpostorGhost : RoleTypes.CrewmateGhost;
            if (p.Data.Role.Role != plain) p.RpcSetRole(plain, true);
        }
        catch (System.Exception e) { Plugin.Logger.LogWarning($"Geisterrolle: {e.Message}"); }
    }

    internal static void OnDeath(PlayerControl victim)
    {
        if (!CustomRole.AmHost || !victim || victim.Data == null || victim.Data.Disconnected) return;
        if (RoleRegistry.GetModifiers(victim).Any(r => r.IsGhostRole)) return;
        if ((Roles.Halloween.Undead.Instance?.WillRise(victim) ?? false) || (Roles.Modifiers.SecondLife.Instance?.WillRise(victim) ?? false)) return; // steht wieder auf
        // Survivor und Untoter bekommen nie eine Geisterrolle (sonst haetten sie nach dem endgueltigen Tod einen Geister-Knopf)
        if (Roles.Modifiers.SecondLife.Instance != null && RoleRegistry.Has(victim, Roles.Modifiers.SecondLife.Instance)) return;
        if (Roles.Halloween.Undead.Instance != null && RoleRegistry.Has(victim, Roles.Halloween.Undead.Instance)) return;

        foreach (var role in RoleRegistry.Roles.Where(r => r.IsGhostRole && r.Count.Int > 0).OrderBy(_ => Random.value))
        {
            used.TryGetValue(role.Id, out int n);
            if (n >= role.Count.Int || !role.CanHaunt(victim)) continue;
            if (Random.Range(0, 100) >= role.Chance.Int) continue;

            used[role.Id] = n + 1;
            RoleRegistry.AddRole(victim, role);

            EnsurePlainGhost(victim);
            return;
        }
    }
}
