using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using BepInEx.Configuration;
using Random = UnityEngine.Random;

namespace Spieleabend.Core;

/// <summary>Kennt alle Rollen, verteilt sie (Host) und merkt sich, wer welche Rolle und Anpassungen hat.</summary>
public static class RoleRegistry
{
    public static readonly List<CustomRole> Roles = new();

    /// <summary>PlayerId -> Hauptrolle</summary>
    private static readonly Dictionary<byte, byte> mainRoles = new();

    /// <summary>PlayerId -> Anpassungen</summary>
    private static readonly Dictionary<byte, List<byte>> modifiers = new();

    /// <summary>Findet automatisch alle Klassen, die von CustomRole erben.</summary>
    internal static void DiscoverRoles(ConfigFile config)
    {
        var types = typeof(CustomRole).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(CustomRole).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal); // feste Reihenfolge = gleiche Ids bei allen Spielern

        byte id = 0;
        foreach (var type in types)
        {
            var role = (CustomRole)Activator.CreateInstance(type);
            role.Id = id++;
            role.Init(config);
            Roles.Add(role);
        }
    }

    public static CustomRole Get(byte roleId) => roleId < Roles.Count ? Roles[roleId] : null;

    /// <summary>Hauptrolle des Spielers (ohne Anpassungen), oder null.</summary>
    public static CustomRole GetRole(PlayerControl player) =>
        player && mainRoles.TryGetValue(player.PlayerId, out var roleId) ? Get(roleId) : null;

    public static IEnumerable<CustomRole> GetModifiers(PlayerControl player) =>
        player && modifiers.TryGetValue(player.PlayerId, out var list) ? list.Select(Get).Where(r => r != null) : Enumerable.Empty<CustomRole>();

    /// <summary>Geisterrolle des Spielers (nach dem Tod), oder null.</summary>
    public static CustomRole GetGhostRole(PlayerControl player) => GetModifiers(player).FirstOrDefault(r => r.IsGhostRole);

    /// <summary>Was angezeigt wird: als Geist nur die Geisterrolle, sonst Hauptrolle + Anpassungen (ohne geheime).</summary>
    public static IEnumerable<CustomRole> GetDisplayed(PlayerControl player)
    {
        var ghost = GetGhostRole(player);
        return ghost != null ? new[] { ghost } : GetAll(player).Where(r => !r.IsSecret(player));
    }

    /// <summary>Hauptrolle + Anpassungen.</summary>
    public static IEnumerable<CustomRole> GetAll(PlayerControl player)
    {
        var main = GetRole(player);
        if (main != null) yield return main;
        foreach (var m in GetModifiers(player)) yield return m;
    }

    public static bool Has(PlayerControl player, CustomRole role) =>
        role.IsExtra ? GetModifiers(player).Contains(role) : GetRole(player) == role;

    public static CustomRole LocalRole => GetRole(PlayerControl.LocalPlayer);

    public static bool HasAssignments => mainRoles.Count > 0 || modifiers.Count > 0;

    /// <summary>true waehrend OnAssigned wegen eines Rollenwechsels mitten in der Runde (z. B. Zombie-Infektion) aufgerufen wird.</summary>
    public static bool IsRoleChange { get; private set; }

    public static IEnumerable<PlayerControl> PlayersWith(CustomRole role)
    {
        foreach (var p in PlayerControl.AllPlayerControls)
            if (p && Has(p, role)) yield return p;
    }

    public static void Reset()
    {
        mainRoles.Clear();
        modifiers.Clear();
        KillSystem.Reset();
        Camouflage.Reset();
        ProtectionVisuals.Reset();
        FireEffect.Reset();
        SpeedControl.Reset();
        GhostRoles.Reset();
        foreach (var role in Roles) role.ResetState();
        ButtonManager.Reset();
        RoleUI.Reset();
        FreeplayTest.Reset();
    }

    /// <summary>Alle Zuweisungen als (Spieler, Rolle)-Paare (fuer das Netzwerk).</summary>
    internal static List<(byte Player, byte Role)> ExportPairs()
    {
        var pairs = mainRoles.Select(p => (p.Key, p.Value)).ToList();
        foreach (var p in modifiers)
            foreach (var m in p.Value) pairs.Add((p.Key, m));
        return pairs;
    }

    internal static void SetAssignments(List<(byte Player, byte Role)> pairs)
    {
        mainRoles.Clear();
        modifiers.Clear();
        GhostRoles.Reset();
        EndSummary.ResetKills();
        foreach (var role in Roles) role.ResetState();

        foreach (var (playerId, roleId) in pairs) AddInternal(playerId, roleId);
        ButtonManager.OnRolesAssigned();

        foreach (var (playerId, roleId) in pairs)
        {
            var role = Get(roleId);
            var player = CustomRole.FindPlayer(playerId);
            if (role == null || !player) continue;
            Plugin.Logger.LogInfo($"{player.Data?.PlayerName ?? playerId.ToString()} ist {role.Name}");
            role.OnAssigned(player);
        }

        // Rollen-Ansage zusaetzlich zum Intro (falls das Intro nicht greift oder schon vorbei ist)
        var local = PlayerControl.LocalPlayer;
        var mine = GetDisplayed(local).ToList();
        if (mine.Count > 0)
            Notifier.Show(Lang.T("Deine Rolle: ", "Your role: ") + string.Join(" + ", mine.Select(r => r.ColoredName)), 8f);
    }

    private static void AddInternal(byte playerId, byte roleId)
    {
        var role = Get(roleId);
        if (role == null) return;
        if (role.IsExtra)
        {
            if (!modifiers.TryGetValue(playerId, out var list)) modifiers[playerId] = list = new List<byte>();
            if (!list.Contains(roleId)) list.Add(roleId);
        }
        else mainRoles[playerId] = roleId;
    }

    /// <summary>Host: einem Spieler mitten in der Runde eine Zusatzrolle geben (Geisterrolle beim Tod).</summary>
    public static void AddRole(PlayerControl player, CustomRole role)
    {
        if (!CustomRole.AmHost || !player || role == null) return;
        RoleRpc.SendAddRole(player.PlayerId, role.Id);
        ApplyAddRole(player.PlayerId, role.Id);
    }

    internal static void ApplyAddRole(byte playerId, byte roleId)
    {
        var role = Get(roleId);
        var player = CustomRole.FindPlayer(playerId);
        if (role == null || !player) return;
        AddInternal(playerId, roleId);
        foreach (var b in role.Buttons) b.ResetTimer(true);
        Plugin.Logger.LogInfo($"{player.Data?.PlayerName} ist jetzt auch {role.Name}");
        IsRoleChange = true;
        try { role.OnAssigned(player); }
        finally { IsRoleChange = false; }
        if (player == PlayerControl.LocalPlayer)
            Notifier.Show(Lang.T("Du bist jetzt ein Geist: ", "You are now a ghost: ") + role.ColoredName + "\n<size=70%>" + role.Description + "</size>", 8f);
        RoleUI.Reset();
    }

    /// <summary>Host: Hauptrolle eines Spielers waehrend der Runde aendern (z. B. Executioner -> Jester). null = keine Mod-Rolle.</summary>
    public static void ChangeRole(PlayerControl player, CustomRole newRole)
    {
        if (!CustomRole.AmHost || !player) return;
        RoleRpc.SendChangeRole(player.PlayerId, newRole?.Id ?? byte.MaxValue);
        ApplyChangeRole(player.PlayerId, newRole?.Id ?? byte.MaxValue);
    }

    internal static void ApplyChangeRole(byte playerId, byte roleId)
    {
        var role = Get(roleId);
        if (role == null) mainRoles.Remove(playerId);
        else mainRoles[playerId] = roleId;

        var player = CustomRole.FindPlayer(playerId);
        if (role != null && player)
        {
            foreach (var b in role.Buttons) b.ResetTimer(false);
            Plugin.Logger.LogInfo($"{player.Data?.PlayerName} ist jetzt {role.Name}");
            IsRoleChange = true;
            try { role.OnAssigned(player); }
            finally { IsRoleChange = false; }
        }
        RoleUI.Reset();
    }

    /// <summary>Host: verteilt die Rollen nach Anzahl und Wahrscheinlichkeit und schickt das Ergebnis an alle.</summary>
    internal static void AssignAsHost()
    {
        if (!CustomRole.AmHost) return;

        var pairs = new List<(byte, byte)>();
        if (Random.Range(0, 100) < GeneralOptions.ModRolesChance.Int)
        {
            // Nur normale Crewmates/Impostor bekommen Mod-Rollen - Spiel-Rollen (Engineer, Shapeshifter ...) bleiben unangetastet
            var crew = new List<PlayerControl>();
            var impostors = new List<PlayerControl>();
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (!CustomRole.IsAlive(p) || !p.Data.Role) continue;
                if (p.Data.Role.Role == RoleTypes.Crewmate) crew.Add(p);
                else if (p.Data.Role.Role == RoleTypes.Impostor) impostors.Add(p);
            }

            var perTeam = new Dictionary<RoleTeam, int>();
            foreach (var role in Roles.Where(r => !r.IsExtra).OrderBy(_ => Random.value))
            {
                var pool = role.Team == RoleTeam.Impostor ? impostors : crew;
                for (int i = 0; i < role.Count.Int; i++)
                {
                    perTeam.TryGetValue(role.Team, out int used);
                    if (used >= GeneralOptions.MaxFor(role.Team) || pool.Count == 0) break;
                    if (Random.Range(0, 100) >= role.Chance.Int) continue;

                    var chosen = pool[Random.Range(0, pool.Count)];
                    pool.Remove(chosen);
                    pairs.Add((chosen.PlayerId, role.Id));
                    perTeam[role.Team] = used + 1;
                }
            }

            // Anpassungen: hoechstens eine pro Spieler; die Hauptrolle entscheidet mit, wer welche bekommen darf
            var mainOf = pairs.ToDictionary(x => x.Item1, x => x.Item2);
            var free = PlayerControl.AllPlayerControls.ToArray().Where(CustomRole.IsAlive).ToList();
            int modsUsed = 0;
            foreach (var role in Roles.Where(r => r.IsModifier).OrderBy(_ => Random.value))
            {
                for (int i = 0; i < role.Count.Int; i++)
                {
                    if (modsUsed >= GeneralOptions.MaxFor(RoleTeam.Modifier)) break;
                    if (Random.Range(0, 100) >= role.Chance.Int) continue;

                    var candidates = free.Where(p => role.CanReceiveModifier(p, mainOf.TryGetValue(p.PlayerId, out var m) ? Get(m) : null)).ToList();
                    if (candidates.Count < role.PlayersPerCount) break;
                    for (int k = 0; k < role.PlayersPerCount; k++)
                    {
                        var chosen = candidates[Random.Range(0, candidates.Count)];
                        candidates.Remove(chosen);
                        free.Remove(chosen);
                        pairs.Add((chosen.PlayerId, role.Id));
                    }
                    modsUsed++;
                }
            }
        }

        // Erst senden, dann lokal anwenden: OnAssigned kann selbst Nachrichten schicken (z. B. Executioner-Ziel),
        // die bei den anderen erst NACH der Rollenverteilung ankommen duerfen.
        RoleRpc.SendRoles(pairs);
        SetAssignments(pairs);
    }
}
