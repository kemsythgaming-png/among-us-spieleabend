using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Zentrale Kill-Logik. Alle Mod-Kills laufen ueber TryKill (nur Host), damit Guardian-Angel-Schutz,
/// Medic-Schild, Bodyguard usw. immer gelten. Vanilla-Impostor-Kills werden ueber CheckMurder ebenfalls abgefangen.
/// </summary>
public static class KillSystem
{
    private static readonly Dictionary<byte, byte> pendingNoBody = new();

    /// <summary>Host: killer toetet target - mit allen Schutz-Regeln. true = target ist gestorben.</summary>
    public static bool TryKill(PlayerControl killer, PlayerControl target)
    {
        if (!CustomRole.AmHost || !CustomRole.IsAlive(target) || !killer) return false;

        // Guardian Angel aus dem Spiel: Kill scheitert sichtbar
        if (target.protectedByGuardianId >= 0)
        {
            killer.RpcMurderPlayer(target, false);
            return false;
        }

        if (Intercept(killer, target)) return false;

        killer.RpcMurderPlayer(target, true);
        return true;
    }

    /// <summary>Host: Kill ohne Schutz-Regeln (z. B. Selbstmord des Sheriffs).</summary>
    public static void ForceKill(PlayerControl killer, PlayerControl target)
    {
        if (!CustomRole.AmHost || !CustomRole.IsAlive(target)) return;
        killer.RpcMurderPlayer(target, true);
    }

    /// <summary>Host: Spieler stirbt ohne Leiche (geht auch im Meeting).</summary>
    public static void KillNoBody(PlayerControl victim, PlayerControl killer = null)
    {
        if (!CustomRole.AmHost || !CustomRole.IsAlive(victim)) return;
        byte killerId = killer ? killer.PlayerId : byte.MaxValue;
        RoleRpc.SendKillNoBody(victim.PlayerId, killerId);
        ApplyKillNoBody(victim.PlayerId, killerId);
    }

    internal static bool Intercept(PlayerControl killer, PlayerControl target)
    {
        if (FirstDeathShield.Intercept(killer, target)) return true;
        foreach (var role in RoleRegistry.Roles)
            if (role.InterceptKill(killer, target) == KillIntercept.Handled) return true;
        return false;
    }

    internal static void ApplyKillNoBody(byte victimId, byte killerId)
    {
        var victim = CustomRole.FindPlayer(victimId);
        if (!CustomRole.IsAlive(victim)) return;

        pendingNoBody[victimId] = killerId;
        if (victim == PlayerControl.LocalPlayer) Sfx.Play(Sfx.Kill);
        victim.Exiled();
        MeetingCleanup(victim);
    }

    /// <summary>Im Meeting: Stimmen von/fuer den Toten zuruecksetzen und Karte als tot markieren.</summary>
    private static void MeetingCleanup(PlayerControl victim)
    {
        var meeting = MeetingHud.Instance;
        if (!meeting) return;

        foreach (var state in meeting.playerStates)
        {
            if (!state) continue;
            if ((byte)state.PlayerId == victim.PlayerId)
            {
                state.SetDead(true);
                if (state.DidVote) state.UnsetVote();
            }
            else if ((byte)state.VotedForId == victim.PlayerId)
            {
                state.UnsetVote();
            }
        }

        if (CustomRole.AmHost) meeting.CheckForEndVoting();
    }

    internal static void RaiseDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        if (!victim) return;
        FirstDeathShield.OnDeath(victim, cause);
        EndSummary.OnDeath(victim, killer, cause);
        if (RoleRegistry.HasAssignments)
        {
            foreach (var role in RoleRegistry.Roles)
            {
                try { role.OnPlayerDied(victim, killer, cause); }
                catch (System.Exception e) { Plugin.Logger.LogError($"{role.Name}.OnPlayerDied: {e}"); }
            }
        }
        GhostRoles.OnDeath(victim); // danach: z. B. der Untote weiss dann schon, dass er wieder aufsteht
    }

    internal static (bool isNoBody, PlayerControl killer) TakeNoBody(byte victimId)
    {
        if (!pendingNoBody.TryGetValue(victimId, out var killerId)) return (false, null);
        pendingNoBody.Remove(victimId);
        return (true, killerId == byte.MaxValue ? null : CustomRole.FindPlayer(killerId));
    }

    public static void Reset() => pendingNoBody.Clear();
}

/// <summary>Vanilla-Impostor-Kills (beim Host) durch Medic-Schild, Bodyguard ... abfangen.</summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckMurder))]
internal static class CheckMurderPatch
{
    public static bool Prefix(PlayerControl __instance, PlayerControl target)
    {
        if (!CustomRole.AmHost || (!RoleRegistry.HasAssignments && !FirstDeathShield.Active)) return true;
        if (!CustomRole.IsAlive(__instance) || !CustomRole.IsAlive(target)) return true;
        if (target.protectedByGuardianId >= 0) return true; // macht das Spiel selbst
        return !KillSystem.Intercept(__instance, target);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
internal static class MurderPlayerPatch
{
    public static void Prefix(PlayerControl target, out bool __state) => __state = CustomRole.IsAlive(target);

    public static void Postfix(PlayerControl __instance, PlayerControl target, bool __state)
    {
        if (__state && target && target.Data != null && target.Data.IsDead)
        {
            // Selbst-Kill (Vampir-Biss, Sheriff-Fehlschuss): das Spiel spielt dabei keinen Sound ab
            if (__instance == target && target == PlayerControl.LocalPlayer) Sfx.Play(Sfx.Kill);
            KillSystem.RaiseDied(target, __instance, DeathCause.Kill);
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Exiled))]
internal static class ExiledPatch
{
    public static void Prefix(PlayerControl __instance, out bool __state) => __state = CustomRole.IsAlive(__instance);

    public static void Postfix(PlayerControl __instance, bool __state)
    {
        if (!__state) return;
        var (noBody, killer) = KillSystem.TakeNoBody(__instance.PlayerId);
        KillSystem.RaiseDied(__instance, killer, noBody ? DeathCause.NoBody : DeathCause.Exile);
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
internal static class MeetingStartPatch
{
    public static void Postfix(MeetingHud __instance)
    {
        // Practice/Freeplay: Diskussionszeit sofort vorbei (direkt abstimmen), aber die Abstimmzeit bleibt bestehen -
        // sonst waere das Meeting sofort zu Ende, bevor man selbst waehlen kann.
        if (FreeplayTest.IsFreeplay && __instance)
        {
            // Nur ~3s Diskussion stehen lassen, dann abstimmen. NICHT genau auf DiscussionTime setzen,
            // sonst endet bei "Abstimmzeit = ewig" das Voting sofort.
            var o = GameOptionsManager.Instance?.CurrentGameOptions;
            float discuss = o != null ? o.GetInt(AmongUs.GameOptions.Int32OptionNames.DiscussionTime) : 0f;
            if (discuss > 3f) __instance.discussionTimer = discuss - 3f;
        }

        FirstDeathShield.OnMeetingStart();
        if (!RoleRegistry.HasAssignments) return;
        foreach (var role in RoleRegistry.Roles) role.OnMeetingStart();
    }
}
