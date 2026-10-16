using System.Collections.Generic;
using System.Linq;
using Hazel;

namespace Spieleabend.Core;

internal enum RpcId : byte
{
    /// <summary>Host -> alle: Rollenverteilung + alle Optionswerte.</summary>
    SetRoles = 210,

    /// <summary>Beliebig: [roleId][action][bytes] -> CustomRole.OnAction</summary>
    RoleAction = 211,

    /// <summary>Host -> alle: alle Optionswerte (in der Lobby, damit jeder die Einstellungen sieht).</summary>
    SyncOptions = 212,

    /// <summary>Host -> alle: eigener Sieg [roleId][count][playerIds]</summary>
    CustomWin = 213,

    /// <summary>Host -> alle: Hauptrolle eines Spielers aendern [playerId][roleId|255]</summary>
    ChangeRole = 214,

    /// <summary>Host -> alle: Spieler stirbt ohne Leiche [playerId][killerId|255]</summary>
    KillNoBody = 215,

    /// <summary>Host -> alle: Schutz fuer die ersten Toten der letzten Runde [count][playerIds]</summary>
    FirstDeathShield = 216,
}

internal static class RoleRpc
{
    private static bool optionsDirty = true;
    private static int lastPlayerCount;

    public static void MarkOptionsDirty() => optionsDirty = true;

    public static void SendRoles(List<(byte Player, byte Role)> pairs)
    {
        var writer = Start(RpcId.SetRoles, -1);
        writer.Write((byte)pairs.Count);
        foreach (var (player, role) in pairs)
        {
            writer.Write(player);
            writer.Write(role);
        }
        WriteOptions(writer);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void SendChangeRole(byte playerId, byte roleId)
    {
        var writer = Start(RpcId.ChangeRole, -1);
        writer.Write(playerId);
        writer.Write(roleId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void SendKillNoBody(byte playerId, byte killerId)
    {
        var writer = Start(RpcId.KillNoBody, -1);
        writer.Write(playerId);
        writer.Write(killerId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void SendAction(CustomRole role, byte action, byte[] data, bool toHostOnly)
    {
        var local = PlayerControl.LocalPlayer;
        data ??= new byte[0];

        if (toHostOnly && AmongUsClient.Instance.AmHost)
        {
            role.OnAction(local, action, data);
            return;
        }

        try
        {
            var writer = Start(RpcId.RoleAction, toHostOnly ? AmongUsClient.Instance.HostId : -1);
            writer.Write(role.Id);
            writer.Write(action);
            writer.WriteBytesAndSize(data);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        catch (System.Exception e)
        {
            // z. B. im Practice ohne Verbindung - lokal trotzdem ausfuehren
            Plugin.Logger.LogWarning($"Senden fehlgeschlagen ({role.Name}): {e.Message}");
        }

        if (!toHostOnly) role.OnAction(local, action, data);
    }

    /// <summary>Host in der Lobby: Einstellungen an alle schicken, wenn geaendert oder jemand beigetreten ist.</summary>
    public static void UpdateOptionSync()
    {
        var client = AmongUsClient.Instance;
        if (!client || !client.AmHost || !client.AmConnected || !LobbyBehaviour.Instance || !PlayerControl.LocalPlayer) return;

        int count = PlayerControl.AllPlayerControls.Count;
        if (!optionsDirty && count == lastPlayerCount) return;
        optionsDirty = false;
        lastPlayerCount = count;

        var writer = Start(RpcId.SyncOptions, -1);
        WriteOptions(writer);
        client.FinishRpcImmediately(writer);
    }

    public static void Handle(PlayerControl sender, byte callId, MessageReader reader)
    {
        switch ((RpcId)callId)
        {
            case RpcId.SetRoles:
            {
                int count = reader.ReadByte();
                var pairs = new List<(byte, byte)>();
                for (int i = 0; i < count; i++) pairs.Add((reader.ReadByte(), reader.ReadByte()));
                ReadOptions(reader);
                RoleRegistry.SetAssignments(pairs);
                break;
            }
            case RpcId.RoleAction:
            {
                var role = RoleRegistry.Get(reader.ReadByte());
                byte action = reader.ReadByte();
                byte[] data = reader.ReadBytesAndSize().ToArray();
                role?.OnAction(sender, action, data);
                break;
            }
            case RpcId.CustomWin:
                CustomWin.Handle(reader);
                break;
            case RpcId.SyncOptions:
                ReadOptions(reader);
                break;
            case RpcId.ChangeRole:
                RoleRegistry.ApplyChangeRole(reader.ReadByte(), reader.ReadByte());
                break;
            case RpcId.FirstDeathShield:
                FirstDeathShield.Handle(reader);
                break;
            case RpcId.KillNoBody:
                KillSystem.ApplyKillNoBody(reader.ReadByte(), reader.ReadByte());
                break;
        }
    }

    private static MessageWriter Start(RpcId id, int targetClientId) =>
        AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)id, SendOption.Reliable, targetClientId);

    private static IEnumerable<RoleOption> AllOptions =>
        GeneralOptions.Options.Concat(RoleRegistry.Roles.SelectMany(r => r.Options));

    private static void WriteOptions(MessageWriter writer)
    {
        foreach (var option in AllOptions) writer.Write(option.Value);
    }

    private static void ReadOptions(MessageReader reader)
    {
        foreach (var option in AllOptions) option.Synced = reader.ReadSingle();
    }

    /// <summary>Beim Verlassen einer Lobby: wieder die eigenen Werte anzeigen.</summary>
    public static void ClearSynced()
    {
        foreach (var option in AllOptions) option.Synced = null;
        optionsDirty = true;
    }
}
