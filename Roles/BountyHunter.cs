using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor (Kopfgeldjaeger): bekommt ein geheimes Ziel angezeigt (nur er sieht die Markierung). Toetet er genau
/// dieses Ziel, ist seine Kill-Abklingzeit sofort kurz und er bekommt ein neues Ziel. Normaler Kill bleibt.
/// </summary>
public sealed class BountyHunter : CustomRole
{
    public override string ConfigSection => "Kopfgeldjaeger";
    public override string Name => Lang.T("Kopfgeldjäger", "Bounty Hunter");
    public override Color Color => new Color32(200, 160, 40, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Erfüll den Auftrag", "Fulfill the contract");
    public override string Description => Lang.T("Töte dein markiertes Ziel\nfür kurze Abklingzeit.", "Kill your marked target\nfor a short cooldown.");
    public override string LongDescription => Lang.T(
        "Impostor: Du siehst ein geheimes Ziel markiert (nur du). Tötest du genau dieses Ziel, ist deine Kill-Abklingzeit sofort kurz und du bekommst ein neues Ziel. Tötest du jemand anderen, passiert nichts Besonderes. Deinen normalen Kill hast du zusätzlich.",
        "Impostor: you see a secret marked target (only you). If you kill exactly that target, your kill cooldown becomes short at once and you get a new target. Killing someone else does nothing special. You also keep your normal kill.");

    private const byte ActionTarget = 0;

    private RoleOption reward;
    // alle Clients: Jaeger-Id -> Ziel-Id
    private readonly Dictionary<byte, byte> targets = new();

    protected override void Setup()
    {
        reward = AddOption("Reward", "Kurze Abklingzeit|Short cooldown", 5f, 0f, 20f, 1f, "s");
    }

    public override void ResetState() => targets.Clear();

    public override void OnAssigned(PlayerControl player)
    {
        if (AmHost && player) AssignTarget(player);
    }

    public override void OnMeetingEnd(PlayerControl exiled)
    {
        if (!AmHost) return;
        foreach (var h in Players.ToList()) if (IsAlive(h)) AssignTarget(h);
    }

    private void AssignTarget(PlayerControl hunter)
    {
        var pool = PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p && p != hunter && IsAlive(p) && !IsImpostor(p)).ToList();
        if (pool.Count == 0) return;
        var pick = pool[Random.Range(0, pool.Count)];
        SendToAll(ActionTarget, hunter.PlayerId, pick.PlayerId);
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionTarget || data.Length < 2) return;
        targets[data[0]] = data[1];
    }

    /// <summary>Freeplay/Host: hat der lokale Jäger noch kein Ziel, eines zuweisen.</summary>
    public override void Update()
    {
        var local = PlayerControl.LocalPlayer;
        if (AmHost && IsLocalRole && IsAlive(local) && !InMeeting && !targets.ContainsKey(local.PlayerId))
            AssignTarget(local);
    }

    /// <summary>Ziel-Name in der Aufgabenliste oben links - nur für den Jäger selbst (auch im Freeplay).</summary>
    public override string GetTaskNote(PlayerControl local)
    {
        if (!local || !targets.TryGetValue(local.PlayerId, out var tid)) return null;
        var target = FindPlayer(tid);
        if (!target || target.Data == null) return null;
        return $"◎ {Lang.T("Ziel", "Target")}: {target.Data.PlayerName}";
    }

    // Kein Symbol mehr über dem Ziel-Kopf - das Ziel steht nur noch im Aufgaben-Text (GetTaskNote).

    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        if (!killer || !victim || cause == DeathCause.Exile) return;
        if (!targets.TryGetValue(killer.PlayerId, out var tid) || tid != victim.PlayerId) return;

        // Treffer: der Jaeger belohnt sich selbst (kurze Abklingzeit) auf seinem Client
        if (killer == PlayerControl.LocalPlayer)
        {
            killer.SetKillTimer(reward.Value);
            Notifier.Show($"<color={ColorHex}>{Lang.T("Ziel erledigt - Abklingzeit verkürzt!", "Target down - cooldown shortened!")}</color>", 3f);
        }
        targets.Remove(killer.PlayerId);
        if (AmHost && IsAlive(killer)) AssignTarget(killer); // neues Ziel
    }
}
