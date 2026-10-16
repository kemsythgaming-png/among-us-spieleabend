using System.Collections.Generic;
using System.Linq;
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

    internal static void OnDeath(PlayerControl victim)
    {
        if (!CustomRole.AmHost || !victim || victim.Data == null || victim.Data.Disconnected) return;
        if (RoleRegistry.GetModifiers(victim).Any(r => r.IsGhostRole)) return;
        if (Roles.Halloween.Undead.Instance?.WillRise(victim) ?? false) return; // steht wieder auf

        foreach (var role in RoleRegistry.Roles.Where(r => r.IsGhostRole && r.Count.Int > 0).OrderBy(_ => Random.value))
        {
            used.TryGetValue(role.Id, out int n);
            if (n >= role.Count.Int || !role.CanHaunt(victim)) continue;
            if (Random.Range(0, 100) >= role.Chance.Int) continue;

            used[role.Id] = n + 1;
            RoleRegistry.AddRole(victim, role);
            return;
        }
    }
}
