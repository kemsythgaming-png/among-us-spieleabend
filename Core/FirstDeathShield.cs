using System.Collections.Generic;
using System.Linq;
using Hazel;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Spielregel "Schutz fuer die ersten Toten": Wer in einer Runde als Erstes getoetet wurde (auch mehrere,
/// wenn sie quasi gleichzeitig sterben), bekommt in der naechsten Runde einen fuer alle sichtbaren Schild
/// und kann bis zum ersten Meeting nicht getoetet werden. Rausgevotet werden zaehlt nicht.
/// Der Host merkt sich die Spieler ueber ihre Client-Id (bleibt gleich, solange sie in der Lobby sind).
/// </summary>
public static class FirstDeathShield
{
    private const float SameMomentSeconds = 1f;
    public static readonly Color ShieldColor = new Color32(255, 210, 60, 255);

    // Host: erste Tote der laufenden Runde
    private static float firstDeathTime = -1f;
    private static readonly List<int> firstDeadThisRound = new();

    // Host: wer in der naechsten Runde geschuetzt wird (Client-Ids)
    private static readonly List<int> shieldNextRound = new();

    // Alle: aktuell geschuetzte Spieler (bis zum ersten Meeting)
    private static readonly HashSet<byte> shielded = new();

    public static bool Active => shielded.Count > 0;

    public static bool IsShielded(PlayerControl p) => p && shielded.Contains(p.PlayerId);

    /// <summary>Host: bei jedem Tod aufgerufen.</summary>
    internal static void OnDeath(PlayerControl victim, DeathCause cause)
    {
        if (!CustomRole.AmHost || cause == DeathCause.Exile || !victim || FreeplayTest.IsFreeplay) return;
        float now = Time.time;
        if (firstDeathTime < 0f) firstDeathTime = now;
        if (now - firstDeathTime <= SameMomentSeconds && !firstDeadThisRound.Contains(victim.OwnerId))
            firstDeadThisRound.Add(victim.OwnerId);
    }

    /// <summary>Host: Spielende - die ersten Toten fuer die naechste Runde merken.</summary>
    internal static void OnGameEnd()
    {
        shieldNextRound.Clear();
        if (CustomRole.AmHost) shieldNextRound.AddRange(firstDeadThisRound);
        firstDeadThisRound.Clear();
        firstDeathTime = -1f;
        shielded.Clear();
    }

    /// <summary>Host: Rundenstart - Schilde verteilen und an alle schicken.</summary>
    internal static void AssignAsHost()
    {
        shielded.Clear();
        firstDeadThisRound.Clear();
        firstDeathTime = -1f;
        if (!CustomRole.AmHost || !GeneralOptions.FirstDeathShield.Bool || shieldNextRound.Count == 0) return;

        var ids = PlayerControl.AllPlayerControls.ToArray()
            .Where(p => CustomRole.IsAlive(p) && shieldNextRound.Contains(p.OwnerId))
            .Select(p => p.PlayerId)
            .ToList();
        if (ids.Count == 0) return;

        var writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)RpcId.FirstDeathShield, SendOption.Reliable, -1);
        writer.Write((byte)ids.Count);
        foreach (var id in ids) writer.Write(id);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
        Apply(ids);
    }

    internal static void Handle(MessageReader reader)
    {
        int count = reader.ReadByte();
        var ids = new List<byte>();
        for (int i = 0; i < count; i++) ids.Add(reader.ReadByte());
        Apply(ids);
    }

    private static void Apply(List<byte> ids)
    {
        shielded.Clear();
        foreach (var id in ids) shielded.Add(id);

        var local = PlayerControl.LocalPlayer;
        if (local && shielded.Contains(local.PlayerId))
            Notifier.Show($"<color=#FFD23C>{Lang.T("Du bist letzte Runde zuerst gestorben - bis zum ersten Meeting bist du geschützt!", "You died first last round - you are protected until the first meeting!")}</color>", 7f);
        Plugin.Logger.LogInfo($"Schutz fuer die ersten Toten: {ids.Count} Spieler");
    }

    /// <summary>Host: Angriff auf einen geschuetzten Spieler abfangen.</summary>
    internal static bool Intercept(PlayerControl killer, PlayerControl target)
    {
        if (!IsShielded(target) || killer == target) return false;
        killer.RpcMurderPlayer(target, false);
        return true;
    }

    /// <summary>Erstes Meeting: Schutz endet.</summary>
    internal static void OnMeetingStart() => shielded.Clear();

    /// <summary>Lobby/Hauptmenue: laufende Schilde weg (die Liste fuer die naechste Runde bleibt).</summary>
    internal static void ResetActive() => shielded.Clear();

    /// <summary>Schutzblasen - fuer alle sichtbar.</summary>
    internal static IEnumerable<(PlayerControl Target, Color Color)> Bubbles()
    {
        foreach (var id in shielded)
        {
            var p = CustomRole.FindPlayer(id);
            if (CustomRole.IsAlive(p)) yield return (p, ShieldColor);
        }
    }
}
