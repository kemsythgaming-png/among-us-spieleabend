using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Hazel;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Eigene Siege (z. B. Jester gewinnt). Der Host ruft Trigger auf: alle bekommen per RPC
/// Gewinner-Rolle und Gewinner-Spieler, danach beendet der Host das Spiel.
/// Auf dem Endbildschirm werden Siegerliste und Text ersetzt.
/// </summary>
public static class CustomWin
{
    public static CustomRole WinningRole { get; private set; }
    private static readonly List<byte> winnerIds = new();
    private static List<CachedPlayerData> cachedWinners;

    public static bool Active => WinningRole != null;

    private static float freeplayCooldownUntil;

    // Mehrere Rollen koennen im SELBEN Moment gewinnen (z. B. Jester + Executioner, der den Jester als Ziel hatte).
    // Wir sammeln alle Sieger dieses Moments und beenden das Spiel erst im naechsten Frame EINMAL mit allen.
    private static CustomRole pendingRole;
    private static readonly List<byte> pendingIds = new();
    private static bool pending;

    /// <summary>Host: Sieg fuer eine Rolle ausloesen. Wird (mit evtl. weiteren gleichzeitigen Siegern) gesammelt beendet.</summary>
    public static void Trigger(CustomRole role, IEnumerable<PlayerControl> winners)
    {
        if (!AmongUsClient.Instance.AmHost || Active || role == null) return;
        var ids = winners.Where(p => p).Select(p => p.PlayerId).ToList();

        if (FreeplayTest.IsFreeplay)
        {
            if (Time.time < freeplayCooldownUntil) return;
            freeplayCooldownUntil = Time.time + 8f;
            Notifier.Show($"<color={role.ColorHex}>{role.Name} {Lang.T("gewinnt!", "wins!")}</color>  ({Lang.T("Freeplay: Spiel läuft weiter", "Freeplay: game continues")})", 6f);
            return;
        }

        pendingRole ??= role;
        foreach (var id in ids) if (!pendingIds.Contains(id)) pendingIds.Add(id);
        pending = true;
    }

    /// <summary>Host, jedes Frame: gesammelte Sieger finalisieren und das Spiel beenden.</summary>
    internal static void FinalizePending()
    {
        if (!pending || Active || !AmongUsClient.Instance || !AmongUsClient.Instance.AmHost) return;
        pending = false;
        var role = pendingRole;
        var ids = pendingIds.ToList();
        pendingRole = null;
        pendingIds.Clear();
        if (role == null || PlayerControl.LocalPlayer == null) return;

        var writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)RpcId.CustomWin, SendOption.Reliable, -1);
        writer.Write(role.Id);
        writer.Write((byte)ids.Count);
        foreach (var id in ids) writer.Write(id);
        AmongUsClient.Instance.FinishRpcImmediately(writer);

        Set(role, ids);
        GameManager.Instance.RpcEndGame(GameOverReason.ImpostorsByVote, false);
    }

    internal static void Handle(MessageReader reader)
    {
        var role = RoleRegistry.Get(reader.ReadByte());
        int count = reader.ReadByte();
        var ids = new List<byte>();
        for (int i = 0; i < count; i++) ids.Add(reader.ReadByte());
        if (role != null) Set(role, ids);
    }

    private static void Set(CustomRole role, List<byte> ids)
    {
        WinningRole = role;
        winnerIds.Clear();
        winnerIds.AddRange(ids);
        Plugin.Logger.LogInfo($"{role.Name} gewinnt!");
    }

    public static void Reset()
    {
        WinningRole = null;
        winnerIds.Clear();
        cachedWinners = null;
        pending = false;
        pendingRole = null;
        pendingIds.Clear();
    }

    /// <summary>Beim Spielende (GameData existiert noch): Siegerliste bauen.</summary>
    internal static void BuildWinners()
    {
        if (!Active || !GameData.Instance) return;
        cachedWinners = new List<CachedPlayerData>();
        foreach (var id in winnerIds)
        {
            var info = GameData.Instance.GetPlayerById(id);
            if (info != null) cachedWinners.Add(new CachedPlayerData(info));
        }
        ApplyWinners();
    }

    internal static void ApplyWinners()
    {
        if (!Active || cachedWinners == null) return;
        var list = new Il2CppSystem.Collections.Generic.List<CachedPlayerData>();
        foreach (var w in cachedWinners) list.Add(w);
        EndGameResult.CachedWinners = list;
    }
}

[HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
internal static class EndGameScreenPatch
{
    public static void Prefix() => CustomWin.ApplyWinners();

    public static void Postfix(EndGameManager __instance)
    {
        if (!CustomWin.Active) return;
        var role = CustomWin.WinningRole;

        __instance.WinText.text = $"{role.Name} {Lang.T("gewinnt!", "wins!")}";
        __instance.WinText.color = role.Color;
        __instance.BackgroundBar.material.color = role.Color;
    }
}

/// <summary>Nach dem Rauswurf: Rolle des Rausgevoteten benachrichtigen.</summary>
internal static class ExileHandling
{
    private static bool handled;

    public static void Reset() => handled = false;

    public static void OnWrapUp(ExileController controller)
    {
        if (handled) return;
        handled = true;

        var exiled = controller.initData?.networkedPlayer;
        var player = exiled != null ? exiled.Object : null;

        foreach (var role in RoleRegistry.Roles)
        {
            try { role.OnMeetingEnd(player); }
            catch (System.Exception e) { Plugin.Logger.LogError($"{role.Name}.OnMeetingEnd: {e.Message}"); }
        }
        if (!player) return;

        foreach (var role in RoleRegistry.GetAll(player).ToList()) role.OnExiled(player);
        foreach (var role in RoleRegistry.Roles) role.OnAnyExiled(player);
    }
}

[HarmonyPatch(typeof(ExileController), nameof(ExileController.Begin))]
internal static class ExileBeginPatch
{
    public static void Postfix(ExileController __instance)
    {
        ExileHandling.Reset();

        var exiled = __instance.initData?.networkedPlayer;
        var player = exiled != null ? exiled.Object : null;
        var text = RoleRegistry.GetRole(player)?.GetExileText(player);
        if (text != null) __instance.completeString = text;
    }
}

[HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
internal static class ExileWrapUpPatch
{
    public static void Prefix(ExileController __instance) => ExileHandling.OnWrapUp(__instance);
}

[HarmonyPatch(typeof(AirshipExileController), nameof(AirshipExileController.WrapUpAndSpawn))]
internal static class AirshipExileWrapUpPatch
{
    public static void Prefix(AirshipExileController __instance) => ExileHandling.OnWrapUp(__instance);
}

/// <summary>Aufgaben von Rollen mit TasksCount = false zaehlen nicht fuer den Crew-Sieg.</summary>
[HarmonyPatch(typeof(GameData), nameof(GameData.RecomputeTaskCounts))]
internal static class TaskCountPatch
{
    public static void Postfix(GameData __instance)
    {
        if (!RoleRegistry.HasAssignments) return;
        foreach (var info in __instance.AllPlayers)
        {
            if (info == null || info.Disconnected || info.Tasks == null || info.Role == null || info.Role.IsImpostor) continue;
            var role = RoleRegistry.GetGhostRole(info.Object) ?? RoleRegistry.GetRole(info.Object); // Spukgestalt zaehlt nicht mit
            if (role == null || role.TasksCount) continue;

            foreach (var task in info.Tasks)
            {
                __instance.TotalTasks--;
                if (task.Complete) __instance.CompletedTasks--;
            }
        }
    }
}
