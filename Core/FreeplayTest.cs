using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using BepInEx.Configuration;
using InnerNet;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Solo-Test im Freeplay (Uebungsmodus): Du bekommst die eingestellte Testrolle,
/// und die ersten Dummies gelten als Impostor (Name rot), damit man Treffer testen kann.
/// Die Laptop-Funktion "Alle wiederbeleben" funktioniert weiterhin.
/// </summary>
internal static class FreeplayTest
{
    private static ConfigEntry<string> testRole;
    private static ConfigEntry<int> dummyImpostors;

    private static ShipStatus appliedFor;
    private static readonly HashSet<byte> fakeImpostors = new();

    public static bool IsFreeplay => AmongUsClient.Instance && AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay;

    public static string TestRoleName => testRole.Value;

    public static string TestRoleDisplayName =>
        RoleRegistry.Roles.FirstOrDefault(r => r.ConfigSection == testRole.Value || r.Name == testRole.Value)?.Name ?? testRole.Value;
    public static int DummyImpostors => dummyImpostors.Value;

    public static void BindConfig(ConfigFile config)
    {
        testRole = config.Bind("Freeplay-Test", "Rolle", "Sheriff", "Rolle, die du im Freeplay bekommst (Name der Rolle, leer = keine).");
        dummyImpostors = config.Bind("Freeplay-Test", "DummyImpostors", 2, "So viele Dummies gelten im Freeplay als Impostor.");
    }

    public static bool IsFakeImpostor(PlayerControl player) => IsFreeplay && player && fakeImpostors.Contains(player.PlayerId);

    /// <summary>Test-Impostor-Dummy wird zum normalen Dummy (z. B. vom Zombie infiziert).</summary>
    public static void RemoveFakeImpostor(PlayerControl player)
    {
        if (player) fakeImpostors.Remove(player.PlayerId);
    }

    public static void Reset()
    {
        appliedFor = null;
        fakeImpostors.Clear();
    }

    public static void Update()
    {
        if (!IsFreeplay || !ShipStatus.Instance || appliedFor == ShipStatus.Instance) return;
        var local = PlayerControl.LocalPlayer;
        if (!local || local.Data == null || !local.Data.Role) return;
        Apply();
    }

    /// <summary>Aus dem Einstellungsfenster: andere Testrolle waehlen.</summary>
    public static void CycleRole(int direction)
    {
        var names = new List<string> { "" };
        names.AddRange(RoleRegistry.Roles.Select(r => r.ConfigSection));
        int index = Mathf.Max(0, names.IndexOf(testRole.Value));
        testRole.Value = names[(index + direction + names.Count) % names.Count];
        if (IsFreeplay && ShipStatus.Instance) Apply();
    }

    public static void ChangeDummyImpostors(int direction)
    {
        dummyImpostors.Value = Mathf.Clamp(dummyImpostors.Value + direction, 0, 14);
        if (IsFreeplay && ShipStatus.Instance) Apply();
    }

    private static void Apply()
    {
        appliedFor = ShipStatus.Instance;
        var local = PlayerControl.LocalPlayer;
        var role = RoleRegistry.Roles.FirstOrDefault(r => r.ConfigSection == testRole.Value || r.Name == testRole.Value);

        // Vanilla-Grundrolle passend zum Team der Testrolle setzen
        if (role != null)
        {
            bool wantImpostor = role.Team == RoleTeam.Impostor;
            if (local.Data.Role.IsImpostor != wantImpostor)
            {
                RoleManager.Instance.SetRole(local, wantImpostor ? RoleTypes.Impostor : RoleTypes.Crewmate);
                // Impostor haben keine echten Aufgaben - die alten Crew-Aufgaben aus dem Practice entfernen
                if (wantImpostor) local.ClearTasks();
            }
        }

        fakeImpostors.Clear();
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (fakeImpostors.Count >= dummyImpostors.Value) break;
            if (p && p.isDummy) fakeImpostors.Add(p.PlayerId);
        }

        RoleUI.Reset();
        var pairs = new List<(byte, byte)>();
        if (role != null)
        {
            pairs.Add((local.PlayerId, role.Id));

            // Rollen fuer zwei Spieler (Lover): ein normaler Dummy wird Partner
            for (int i = 1; i < role.PlayersPerCount; i++)
            {
                var partner = PlayerControl.AllPlayerControls.ToArray()
                    .FirstOrDefault(p => p && p.isDummy && !fakeImpostors.Contains(p.PlayerId) && pairs.All(x => x.Item1 != p.PlayerId));
                if (partner) pairs.Add((partner.PlayerId, role.Id));
            }
        }
        RoleRegistry.SetAssignments(pairs);

        Plugin.Logger.LogInfo($"Freeplay-Test: Rolle '{testRole.Value}', {fakeImpostors.Count} Dummy-Impostor(s)");
    }

    public static void ColorFakeImpostors()
    {
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p && p.cosmetics && fakeImpostors.Contains(p.PlayerId))
                p.cosmetics.nameText.color = Palette.ImpostorRed;
        }
    }
}
