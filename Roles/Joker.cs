using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor: heißt immer "Joker", aber nach JEDER Nutzung bekommt er die echte Fähigkeit (und den Knopf) einer
/// zufälligen anderen Impostor-Rolle. Nur der Knopf wechselt - über dem Namen steht weiter "Joker".
/// </summary>
public sealed class Joker : CustomRole
{
    public override string ConfigSection => "Joker";
    public override string Name => "Joker";
    public override Color Color => new Color32(200, 60, 160, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Jeder Knopf anders", "Every button different");
    public override string Description => Lang.T("Nach jeder Nutzung eine andere\nImpostor-Fähigkeit.", "A different impostor ability\nafter every use.");
    public override string LongDescription => Lang.T(
        "Impostor: Du heißt immer Joker. Du bekommst die echte Fähigkeit (und den Knopf) einer zufälligen anderen Impostor-Rolle - nach jeder Nutzung wechselt sie auf eine andere. Nur der Knopf ändert sich, über deinem Namen steht weiter Joker. Deinen normalen Kill hast du zusätzlich.",
        "Impostor: you are always called Joker. You get the real ability (and button) of a random other impostor role - after every use it switches to another. Only the button changes; your name stays Joker. You also keep your normal kill.");

    internal static Joker Instance { get; private set; }

    private const byte ActionMark = 9, ActionReroll = 8;
    private readonly HashSet<byte> jokers = new();
    private static bool hooked;
    private bool pendingReroll;   // lokal: nach der Nutzung neu würfeln, sobald der Effekt durch ist
    private float rerollAt;       // frühestens zu diesem Zeitpunkt (damit RPCs/Effekte sauber durchlaufen)
    private CustomRole lastLocalRole;

    protected override void Setup()
    {
        Instance = this;
        if (!hooked)
        {
            RoleButton.Used += role => Instance?.OnUsed(role); // nach jeder Nutzung neu würfeln
            hooked = true;
        }
    }

    public override void ResetState() => jokers.Clear();

    /// <summary>Ist dieser Spieler ein Joker (für die Namensanzeige)?</summary>
    public static bool IsJoker(byte playerId) => Instance != null && Instance.jokers.Contains(playerId);

    public override void OnAssigned(PlayerControl player)
    {
        if (!AmHost || RoleRegistry.IsRoleChange || !player) return; // nur bei der ERSTEN Zuweisung
        SendToAll(ActionMark, player.PlayerId);
        BecomeRandom(player);
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionMark && data.Length >= 1) jokers.Add(data[0]);
        else if (action == ActionReroll && AmHost && data.Length >= 1)
        {
            var p = FindPlayer(data[0]);
            if (IsAlive(p) && jokers.Contains(data[0])) BecomeRandom(p);
        }
    }

    private void OnUsed(CustomRole usedRole)
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !jokers.Contains(local.PlayerId)) return;
        // Nutzung gemerkt - neu gewürfelt wird erst kurz danach (damit Kill-RPC/Effekt sauber durchlaufen)
        if (usedRole != null && usedRole != this && RoleRegistry.GetRole(local) == usedRole)
        {
            pendingReroll = true;
            rerollAt = Time.time + 1.5f;
        }
    }

    public override void Update()
    {
        var local = PlayerControl.LocalPlayer;
        if (local && jokers.Contains(local.PlayerId))
        {
            var role = RoleRegistry.GetRole(local);
            // Sicherheitsnetz: nach einem Rollenwechsel den Collider wieder an (falls ein Phase-Effekt hing)
            if (role != lastLocalRole)
            {
                lastLocalRole = role;
                if (local.Collider && !local.Collider.enabled && IsAlive(local) && !local.inVent && !InMeeting)
                    local.Collider.enabled = true;
            }
        }

        if (!pendingReroll) return;
        if (!local || !jokers.Contains(local.PlayerId)) { pendingReroll = false; return; }
        var cur = RoleRegistry.GetRole(local);
        if (cur == null || cur == this) { pendingReroll = false; return; }
        // Warten, bis die kurze Pause um ist und keine Wirkung mehr läuft (sonst bleibt z. B. der Collider aus)
        if (Time.time >= rerollAt && cur.Buttons.All(b => !b.EffectActive))
        {
            pendingReroll = false;
            SendToHost(ActionReroll, local.PlayerId);
        }
    }

    private void BecomeRandom(PlayerControl player)
    {
        var current = RoleRegistry.GetRole(player);
        var pool = RoleRegistry.Roles
            .Where(r => r.Team == RoleTeam.Impostor && !r.IsGhostRole && r.Buttons.Count > 0 && r != this && r != current
                        && r is not VoodooDoll                       // Übernahme funktioniert geliehen nicht sauber
                        && r is not Halloween.Scarecrow              // Menü-Auswahl bricht beim Rollenwechsel ab
                        && r is not Halloween.Poltergeist)           // Durch-Wände klemmt beim Rollenwechsel (Wand/Vent)
            .ToList();
        if (pool.Count == 0) return;
        var pick = pool[Random.Range(0, pool.Count)];
        RoleRegistry.ChangeRole(player, pick);
    }
}
