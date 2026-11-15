using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Neutral (Virus): steckt Spieler an - per Knopf oder einfach dadurch, dass jemand lange genug in der Naehe eines
/// Angesteckten (oder des Virus selbst) bleibt. Nach einem Meeting verlieren alle Angesteckten ihre Faehigkeit.
/// Der Virus gewinnt, sobald kein anderer lebender Spieler mehr eine Faehigkeit hat.
/// </summary>
public sealed class Syphilis : CustomRole
{
    public override string ConfigSection => "Ansteckung"; // Schlüssel bleibt, damit alte Einstellungen gelten
    public override string Name => Lang.T("Virus", "Virus");
    public override Color Color => new Color32(140, 200, 90, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override string IntroText => Lang.T("Stecke alle an", "Infect everyone");
    public override string Description => Lang.T("Stecke Spieler an (F) oder durch Nähe.\nAngesteckte verlieren nach dem Meeting\nihre Fähigkeit.", "Infect players (F) or by proximity.\nInfected lose their ability\nafter the meeting.");
    public override string LongDescription => Lang.T(
        "Du bist neutral. Mit Taste F steckst du den nächsten Spieler sofort an. Außerdem steckt sich jeder an, der lange genug (Standard 20 s) in der Nähe eines Angesteckten - oder von dir - bleibt. Angesteckte sehen ein Symbol an ihrem Namen (nur sie selbst und du sehen es). Nach jedem Meeting verlieren alle Angesteckten ihre Fähigkeit. Du gewinnst, sobald kein anderer lebender Spieler mehr eine Fähigkeit hat.",
        "You are neutral. Press F to instantly infect the nearest player. Everyone who stays close enough (default 20 s) to an infected player - or to you - gets infected too. Infected players see a mark next to their name (only they and you see it). After every meeting all infected players lose their ability. You win once no other living player has an ability left.");

    private const byte ActionInfect = 0, ActionSync = 1, ActionLose = 2;

    private RoleOption cooldown, proximityTime, spreadRadius;

    // Host
    private readonly Dictionary<byte, float> nearTime = new(); // Spieler -> wie lange schon in Naehe (kumuliert)
    private float lastTick;

    // auf allen Clients gespiegelt
    private static readonly HashSet<byte> infected = new();
    private static readonly HashSet<byte> abilityLost = new();

    internal static bool IsAbilityLost(PlayerControl p) => p && abilityLost.Contains(p.PlayerId);

    protected override void Setup()
    {
        cooldown = AddOption("CooldownV2", "Abklingzeit|Cooldown", 100f, 5f, 120f, 5f, "s"); // neuer Schlüssel -> alter (zu niedriger) Wert wird verworfen, Standard 100 greift
        proximityTime = AddOption("Proximity", "Nähe bis Ansteckung|Proximity to infect", 20f, 5f, 60f, 5f, "s");
        spreadRadius = AddOption("Spread", "Ansteckungs-Radius|Spread radius", 1.6f, 0.5f, 4f, 0.2f);

        AddButton(new RoleButton
        {
            Label = "Anstecken|Infect",
            Icon = "curse",
            GameIcon = _ => ProcIcons.Virus(),
            NeedsTarget = true,
            TargetFilter = p => IsAlive(p),
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer), // als Geist nicht mehr anstecken
            OnClick = target => { if (target) SendToHost(ActionInfect, target.PlayerId); },
        });

        // Globale Faehigkeits-Sperre fuer angesteckte Spieler (gilt fuer ALLE Rollen-Knoepfe).
        RoleButton.AbilityBlocked = IsAbilityLost;
    }

    public override void ResetState()
    {
        nearTime.Clear(); lastTick = 0f;
        infected.Clear(); abilityLost.Clear();
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionInfect)
        {
            if (!AmHost || data.Length < 1 || !RoleRegistry.Has(sender, this) || !IsAlive(sender)) return; // toter Virus steckt nicht an
            MarkInfected(data[0]);
        }
        else if (action == ActionSync)
        {
            infected.Clear();
            foreach (var b in data) infected.Add(b);
        }
        else if (action == ActionLose)
        {
            abilityLost.Clear();
            foreach (var b in data) abilityLost.Add(b);
        }
    }

    private PlayerControl Self => Players.FirstOrDefault(p => p); // irgendein Virus-Spieler (Host-Sicht)

    private void MarkInfected(byte id)
    {
        if (infected.Contains(id)) return;
        var p = FindPlayer(id);
        if (!IsAlive(p) || RoleRegistry.Has(p, this)) return; // der Virus steckt sich nicht selbst an
        infected.Add(id);
        BroadcastInfected();
    }

    private void BroadcastInfected() => SendToAll(ActionSync, infected.ToArray());

    public override void Update()
    {
        if (!AmHost) { BlockLocalKill(); return; }
        BlockLocalKill();

        // Naehe-Ansteckung: wer lange genug neben einem Angesteckten (oder dem Virus) steht, wird selbst angesteckt.
        float now = Time.time;
        if (now - lastTick < 0.5f || InMeeting) { return; }
        float dt = now - lastTick;
        lastTick = now;

        // Ansteckungsquellen: alle Angesteckten + alle lebenden Virus-Spieler
        var sources = new List<Vector2>();
        foreach (var id in infected)
        {
            var c = FindPlayer(id);
            if (IsAlive(c) && !c.inVent) sources.Add(c.GetTruePosition());
        }
        foreach (var v in Players)
            if (IsAlive(v) && !v.inVent) sources.Add(v.GetTruePosition());
        if (sources.Count == 0) return;

        float r = spreadRadius.Value, need = proximityTime.Value;
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!IsAlive(p) || p.inVent || infected.Contains(p.PlayerId) || RoleRegistry.Has(p, this)) continue;
            Vector2 at = p.GetTruePosition();
            bool close = sources.Any(s => Vector2.Distance(at, s) <= r);
            if (!close) { nearTime.Remove(p.PlayerId); continue; }
            nearTime.TryGetValue(p.PlayerId, out float t);
            t += dt;
            if (t >= need) { nearTime.Remove(p.PlayerId); MarkInfected(p.PlayerId); }
            else nearTime[p.PlayerId] = t;
        }
    }

    /// <summary>Angesteckte verlieren auch den normalen Kill (falls Impostor): Kill-Timer hochhalten.</summary>
    private static void BlockLocalKill()
    {
        var local = PlayerControl.LocalPlayer;
        if (IsAbilityLost(local) && local.Data != null && !local.Data.IsDead && local.Data.Role && local.Data.Role.IsImpostor)
            local.SetKillTimer(60f);
    }

    public override void OnMeetingEnd(PlayerControl exiled)
    {
        if (!AmHost) return;
        if (NoVirusAlive()) { ClearAll(); return; } // Virus rausgevotet -> alle bekommen ihre Fähigkeit zurück
        nearTime.Clear();
        // Alle aktuell Angesteckten verlieren ihre Faehigkeit.
        foreach (var id in infected) abilityLost.Add(id);
        SendToAll(ActionLose, abilityLost.ToArray());
        CheckWin();
    }

    /// <summary>Stirbt der Virus (Kill/Rauswurf), bekommen alle Angesteckten ihre Fähigkeit zurück.</summary>
    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        if (AmHost && RoleRegistry.Has(victim, this) && NoVirusAlive()) ClearAll();
    }

    private bool NoVirusAlive() => !Players.Any(IsAlive);

    private void ClearAll()
    {
        nearTime.Clear();
        infected.Clear();
        abilityLost.Clear();
        SendToAll(ActionSync);  // leere Liste -> keine Angesteckten mehr
        SendToAll(ActionLose);  // leere Liste -> alle Fähigkeiten zurück (inkl. Impostor-Kill)
    }

    private void CheckWin()
    {
        if (!AmHost) return;
        var virus = Self;
        if (!IsAlive(virus)) return;
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!IsAlive(p) || RoleRegistry.Has(p, this)) continue;
            if (HasAbility(p)) return; // es gibt noch jemanden mit Faehigkeit -> kein Sieg
        }
        CustomWin.Trigger(this, Players.Where(IsAlive).ToArray());
    }

    /// <summary>Hat der Spieler (noch) eine nutzbare Faehigkeit? Angesteckte (nach Meeting) nicht mehr.</summary>
    private static bool HasAbility(PlayerControl p)
    {
        if (abilityLost.Contains(p.PlayerId)) return false;
        if (IsImpostor(p)) return true;
        var role = RoleRegistry.GetRole(p);
        return role != null && role.Buttons.Count > 0;
    }

    // Symbol am Namen: nur der Angesteckte selbst und der Virus sehen es.
    private bool CanSeeMark(PlayerControl viewer, PlayerControl target) =>
        infected.Contains(target.PlayerId) && (viewer == target || RoleRegistry.Has(viewer, this));

    public override string GetNameIcon(PlayerControl viewer, PlayerControl target) =>
        CanSeeMark(viewer, target) ? "proc:virus" : null;

    public override string GetMeetingIcon(PlayerControl viewer, PlayerControl target) =>
        CanSeeMark(viewer, target) ? "proc:virus" : null;

    public override string GetTaskNote(PlayerControl local)
    {
        if (!RoleRegistry.Has(local, this)) return null;
        return Lang.T($"Angesteckt: {infected.Count}", $"Infected: {infected.Count}");
    }
}
