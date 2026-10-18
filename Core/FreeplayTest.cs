using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using BepInEx.Configuration;
using HarmonyLib;
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
    private static ConfigEntry<string> dummyRole;
    private static ConfigEntry<int> dummyImpostors;
    private static ConfigEntry<bool> dummiesFollowVote;

    private static ShipStatus appliedFor;
    private static readonly HashSet<byte> fakeImpostors = new();

    /// <summary>Dummy-PlayerId -> zugewiesene Testrolle.</summary>
    private static readonly Dictionary<byte, byte> dummyRoles = new();

    public static bool IsFreeplay => AmongUsClient.Instance && AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay;

    public static string TestRoleName => testRole.Value;

    public static string TestRoleDisplayName =>
        RoleRegistry.Roles.FirstOrDefault(r => r.ConfigSection == testRole.Value || r.Name == testRole.Value)?.Name ?? testRole.Value;
    public static int DummyImpostors => dummyImpostors.Value;

    public static string DummyRoleName => dummyRole.Value;
    public static string DummyRoleDisplay =>
        RoleRegistry.Roles.FirstOrDefault(r => r.ConfigSection == dummyRole.Value || r.Name == dummyRole.Value)?.Name ?? dummyRole.Value;
    public static bool DummiesFollowVote => dummiesFollowVote.Value;
    public static void ToggleDummiesFollowVote() => dummiesFollowVote.Value = !dummiesFollowVote.Value;

    public static void BindConfig(ConfigFile config)
    {
        testRole = config.Bind("Freeplay-Test", "Rolle", "Sheriff", "Rolle, die du im Freeplay bekommst (Name der Rolle, leer = keine).");
        dummyRole = config.Bind("Freeplay-Test", "DummyRolle", "Jester", "Rolle, die ein Dummy per Knopf bekommt.");
        dummyImpostors = config.Bind("Freeplay-Test", "DummyImpostors", 2, "So viele Dummies gelten im Freeplay als Impostor.");
        dummiesFollowVote = config.Bind("Freeplay-Test", "DummiesFollowVote", true, "Dummies stimmen im Meeting so ab wie du (zum Testen von Rausvote-Rollen).");
    }

    /// <summary>Andere Dummy-Testrolle waehlen (zum Durchblaettern).</summary>
    public static void CycleDummyRole(int direction)
    {
        var names = new List<string> { "" };
        names.AddRange(RoleRegistry.Roles.Where(r => !r.IsGhostRole).Select(r => r.ConfigSection));
        int index = Mathf.Max(0, names.IndexOf(dummyRole.Value));
        dummyRole.Value = names[(index + direction + names.Count) % names.Count];
    }

    /// <summary>Deine Testrolle direkt setzen (aus der Liste gewaehlt).</summary>
    public static void SetTestRole(string section)
    {
        testRole.Value = section ?? "";
        if (IsFreeplay && ShipStatus.Instance) Apply();
    }

    /// <summary>Einem freien Dummy die gewaehlte Rolle geben.</summary>
    public static void GiveDummyRole(string section)
    {
        if (!IsFreeplay || !ShipStatus.Instance) return;
        var role = RoleRegistry.Roles.FirstOrDefault(r => r.ConfigSection == section || r.Name == section);
        if (role == null) return;
        var dummy = PlayerControl.AllPlayerControls.ToArray()
            .FirstOrDefault(p => p && p.isDummy && p != PlayerControl.LocalPlayer && !dummyRoles.ContainsKey(p.PlayerId));
        if (!dummy) return;
        dummyRoles[dummy.PlayerId] = role.Id;
        Apply();
    }

    /// <summary>Genau diesem Dummy die gewaehlte Rolle geben (leer = Rolle entfernen).</summary>
    public static void SetDummyRole(byte dummyId, string section)
    {
        if (!IsFreeplay || !ShipStatus.Instance) return;
        if (string.IsNullOrEmpty(section)) dummyRoles.Remove(dummyId);
        else
        {
            var role = RoleRegistry.Roles.FirstOrDefault(r => r.ConfigSection == section || r.Name == section);
            if (role == null) return;
            dummyRoles[dummyId] = role.Id;
        }
        Apply();
    }

    /// <summary>Zugewiesene Rolle eines Dummys (oder null).</summary>
    public static CustomRole DummyRole(byte dummyId) =>
        dummyRoles.TryGetValue(dummyId, out var id) ? RoleRegistry.Get(id) : null;

    /// <summary>Alle Dummy-Rollen wieder entfernen.</summary>
    public static void ClearDummyRoles()
    {
        dummyRoles.Clear();
        if (IsFreeplay && ShipStatus.Instance) Apply();
    }

    public static int DummyRoleCount => dummyRoles.Count;

    /// <summary>Sich selbst umbringen (zum Testen, auch als Impostor).</summary>
    public static void SelfKill()
    {
        var local = PlayerControl.LocalPlayer;
        if (IsFreeplay && local && CustomRole.IsAlive(local)) local.RpcMurderPlayer(local, true);
    }

    /// <summary>Jedes Frame im Meeting: Hast du schon abgestimmt, stimmen alle Dummies sofort genauso ab (und das Meeting endet).</summary>
    public static void MeetingVoteFollow(MeetingHud meeting)
    {
        if (!IsFreeplay || !dummiesFollowVote.Value || !meeting) return;
        var local = PlayerControl.LocalPlayer;
        if (!local) return;

        PlayerVoteArea localState = null;
        foreach (var s in meeting.playerStates)
            if (s && (byte)s.PlayerId == local.PlayerId) { localState = s; break; }
        if (localState == null || !localState.DidVote) return;

        byte target = (byte)localState.VotedForId;
        bool any = false;
        foreach (var s in meeting.playerStates)
        {
            if (!s || s.DidVote) continue;
            var p = CustomRole.FindPlayer((byte)s.PlayerId);
            if (p && p.isDummy && CustomRole.IsAlive(p))
            {
                try { meeting.CastVote((byte)s.PlayerId, target); any = true; }
                catch { }
            }
        }
        if (any) { try { meeting.CheckForEndVoting(); } catch { } }
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
        dummyRoles.Clear();
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

        // Dummy-Rollen mit aufnehmen und die passende Vanilla-Grundrolle setzen
        foreach (var kv in dummyRoles.ToArray())
        {
            var d = CustomRole.FindPlayer(kv.Key);
            var dRole = RoleRegistry.Get(kv.Value);
            if (!d || !d.isDummy || dRole == null) { dummyRoles.Remove(kv.Key); continue; }
            bool wantImp = dRole.Team == RoleTeam.Impostor;
            if (d.Data != null && d.Data.Role && d.Data.Role.IsImpostor != wantImp)
            {
                RoleManager.Instance.SetRole(d, wantImp ? RoleTypes.Impostor : RoleTypes.Crewmate);
                if (wantImp) d.ClearTasks();
            }
            if (pairs.All(x => x.Item1 != d.PlayerId)) pairs.Add((d.PlayerId, dRole.Id));
        }
        RoleRegistry.SetAssignments(pairs);
        ButtonManager.OnRolesAssigned(); // Knopf-Timer neu (im Freeplay 3s)

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

