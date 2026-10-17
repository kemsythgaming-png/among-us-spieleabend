using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Sammel-Aktionen fuer alle Mod-Rollen auf einmal (Schnell-Einstellung im Reiter "Allgemein"):
/// alle Chancen zufaellig, alle Chancen auf denselben Wert, alle Anzahlen auf denselben Wert.
/// Wirkt nur auf die Mod-Rollen; die Rollen aus dem Spiel bleiben unberuehrt. Nur Host/Freeplay.
/// </summary>
internal static class BulkRoleOps
{
    /// <summary>Jeder Mod-Rolle eine zufaellige Chance geben (in Schritten ihrer Einstellung).</summary>
    public static void RandomizeChances()
    {
        if (!SettingsWindow.CanEdit) return;
        foreach (var r in RoleRegistry.Roles)
        {
            var o = r.Chance;
            int steps = Mathf.Max(1, Mathf.RoundToInt((o.Max - o.Min) / o.Step));
            o.SetValue(o.Min + Random.Range(0, steps + 1) * o.Step);
        }
    }

    /// <summary>Alle Mod-Rollen auf dieselbe Chance setzen.</summary>
    public static void SetAllChances(int percent)
    {
        if (!SettingsWindow.CanEdit) return;
        foreach (var r in RoleRegistry.Roles) r.Chance.SetValue(percent);
    }

    /// <summary>Alle Mod-Rollen auf dieselbe Anzahl setzen.</summary>
    public static void SetAllCounts(int count)
    {
        if (!SettingsWindow.CanEdit) return;
        foreach (var r in RoleRegistry.Roles) r.Count.SetValue(count);
    }
}
