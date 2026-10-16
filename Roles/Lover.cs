using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Anpassung fuer zwei Spieler: Stirbt einer, stirbt der andere mit (gebrochenes Herz).
/// Sind beide unter den letzten 3 Lebenden, gewinnen sie zusammen.
/// </summary>
public sealed class Lover : CustomRole
{
    public override string Name =>
        Lang.T("Lover", "Lover");
    public override Color Color => new Color32(255, 105, 180, 255);
    public override RoleTeam Team => RoleTeam.Modifier;
    public override string IntroText =>
        Lang.T("Du bist verliebt", "You are in love");
    public override string Description =>
        Lang.T("Stirbt dein Partner (♥), stirbst du mit.", "If your partner (♥) dies, you die too.");
    public override string LongDescription =>
        Lang.T("Anpassung: Du und ein anderer Spieler seid verliebt (♥). Eure Rollen bleiben.\n\nStirbt einer, stirbt der andere mit. Seid ihr beide unter den letzten 3, gewinnt ihr zusammen.", "Modifier: you and another player are in love (♥). Your roles stay.\n\nIf one dies, the other dies too. If you are both among the last 3, you win together.");
    public override int PlayersPerCount => 2;
    public override bool ModifierAllowsImpostors => impostorCanBeLover?.Bool ?? false;

    private RoleOption impostorCanBeLover, dieTogether, winTogether;

    private readonly List<(byte A, byte B)> couples = new();
    private byte waiting = byte.MaxValue;

    protected override void Setup()
    {
        impostorCanBeLover = AddToggle("ImpostorCanBeLover", "Impostor kann Lover sein|Impostor can be a lover", false);
        dieTogether = AddToggle("DieTogether", "Partner stirbt mit|Partner dies too", true);
        winTogether = AddToggle("WinTogether", "Gewinnen zusammen als letzte 3|Win together as last 3", true);
    }

    public override void ResetState()
    {
        couples.Clear();
        waiting = byte.MaxValue;
    }

    // Paare entstehen in Verteilungs-Reihenfolge - bei allen Clients gleich
    public override void OnAssigned(PlayerControl player)
    {
        if (waiting == byte.MaxValue) waiting = player.PlayerId;
        else
        {
            couples.Add((waiting, player.PlayerId));
            waiting = byte.MaxValue;
        }
    }

    private PlayerControl PartnerOf(PlayerControl p)
    {
        foreach (var (a, b) in couples)
        {
            if (a == p.PlayerId) return FindPlayer(b);
            if (b == p.PlayerId) return FindPlayer(a);
        }
        return null;
    }

    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        if (!dieTogether.Bool) return;
        var partner = PartnerOf(victim);
        if (!IsAlive(partner)) return;

        if (partner == PlayerControl.LocalPlayer)
            Notifier.Show($"<color={ColorHex}>♥ {victim.Data.PlayerName} {Lang.T("ist gestorben - du stirbst an gebrochenem Herzen.", "died - you die of a broken heart.")}</color>");
        if (AmHost) KillSystem.KillNoBody(partner);
    }

    public override void Update()
    {
        if (!AmHost || !winTogether.Bool || InMeeting || CustomWin.Active) return;

        int alive = PlayerControl.AllPlayerControls.ToArray().Count(IsAlive);
        if (alive > 3) return;
        foreach (var (a, b) in couples)
        {
            var pa = FindPlayer(a);
            var pb = FindPlayer(b);
            if (IsAlive(pa) && IsAlive(pb)) CustomWin.Trigger(this, new[] { pa, pb });
        }
    }

    public override string GetNameMark(PlayerControl viewer, PlayerControl target)
    {
        if (!RoleRegistry.Has(viewer, this)) return null;
        bool isCouple = target == viewer || PartnerOf(viewer) == target;
        return isCouple ? $"<color={ColorHex}>♥</color>" : null;
    }
}
