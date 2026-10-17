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

    /// <summary>Host, jede Sekunde: Spieler mit unserer Geisterrolle behalten keine Spiel-Geisterrolle (falls das Spiel sie spaeter vergibt).</summary>
    internal static void Update()
    {
        if (!CustomRole.AmHost || Time.time < nextCheck) return;
        nextCheck = Time.time + 1f;
        foreach (var p in PlayerControl.AllPlayerControls)
            if (p && p.Data != null && p.Data.IsDead && RoleRegistry.GetGhostRole(p) != null) EnsurePlainGhost(p);
    }

    /// <summary>Keine Spiel-Geisterrolle (Guardian Angel ...) zusaetzlich - unsere ersetzt sie.</summary>
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
