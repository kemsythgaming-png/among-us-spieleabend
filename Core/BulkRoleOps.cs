using System;
using AmongUs.GameOptions;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Sammel-Aktionen fuer alle Rollen auf einmal (Schnell-Einstellung im Reiter "Allgemein"):
/// alle Chancen zufaellig, alle Chancen auf denselben Wert, alle Anzahlen auf denselben Wert.
/// Wirkt auf die Mod-Rollen UND die Rollen aus dem Spiel (Engineer, Shapeshifter ...). Nur Host/Freeplay.
/// </summary>
internal static class BulkRoleOps
{
    /// <summary>Jeder Rolle eine zufaellige Chance geben.</summary>
    public static void RandomizeChances()
    {
        if (!SettingsWindow.CanEdit) return;
        foreach (var r in RoleRegistry.Roles)
        {
            var o = r.Chance;
            int steps = Mathf.Max(1, Mathf.RoundToInt((o.Max - o.Min) / o.Step));
            o.SetValue(o.Min + UnityEngine.Random.Range(0, steps + 1) * o.Step);
        }
        ForVanilla((role, opts) => opts.RoleOptions.SetRoleRate(role, opts.RoleOptions.GetNumPerGame(role), UnityEngine.Random.Range(0, 11) * 10));
        ChatNote.Message(Lang.T("Alle Chancen: zufällig", "All chances: random"));
    }

    /// <summary>Alle Rollen auf dieselbe Chance setzen.</summary>
    public static void SetAllChances(int percent)
    {
        if (!SettingsWindow.CanEdit) return;
        foreach (var r in RoleRegistry.Roles) r.Chance.SetValue(percent);
        ForVanilla((role, opts) => opts.RoleOptions.SetRoleRate(role, opts.RoleOptions.GetNumPerGame(role), percent));
        ChatNote.Message(Lang.T($"Alle Chancen: {percent}%", $"All chances: {percent}%"));
    }

    /// <summary>Alle Rollen auf dieselbe Anzahl setzen.</summary>
    public static void SetAllCounts(int count)
    {
        if (!SettingsWindow.CanEdit) return;
        foreach (var r in RoleRegistry.Roles) r.Count.SetValue(count);
        ForVanilla((role, opts) => opts.RoleOptions.SetRoleRate(role, count, opts.RoleOptions.GetChancePerGame(role)));
        ChatNote.Message(Lang.T($"Alle Anzahl: {count}", $"All counts: {count}"));
    }

    /// <summary>Dieselbe Aktion auf alle Spiel-Rollen anwenden und danach an alle synchronisieren.</summary>
    private static void ForVanilla(Action<RoleTypes, IGameOptions> apply)
    {
        var o = GameOptionsManager.Instance?.CurrentGameOptions;
        if (o == null) return;
        try
        {
            foreach (var v in VanillaRoleEntry.All) apply(v.Role, o);
            if (GameManager.Instance && GameManager.Instance.LogicOptions != null) GameManager.Instance.LogicOptions.SyncOptions();
        }
        catch (Exception e) { Plugin.Logger.LogWarning($"Sammel-Einstellung (Spiel-Rollen): {e.Message}"); }
    }
}
