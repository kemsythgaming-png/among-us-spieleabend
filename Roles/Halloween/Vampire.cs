using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Halloween;

/// <summary>Impostor: beisst Spieler - das Opfer stirbt erst nach einigen Sekunden (oder spaetestens beim naechsten Meeting).</summary>
public sealed class Vampire : CustomRole
{
    public override string ConfigSection => "Vampir"; // fest, damit Einstellungen erhalten bleiben
    public override string Name =>
        Lang.T("Vampir", "Vampire");
    public override Color Color => new Color32(160, 0, 35, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText =>
        Lang.T("Beiße zu und verschwinde", "Bite and disappear");
    public override string Description =>
        Lang.T("Beiße Spieler (F) -\nsie sterben erst später.", "Bite players (F) -\nthey die later.");
    public override string LongDescription =>
        Lang.T("Halloween-Impostor. Mit Taste F beißt du jemanden - er stirbt erst nach ein paar Sekunden, dort wo er dann steht.\n\nKommt vorher ein Meeting, stirbt er sofort. Biss und normaler Kill teilen sich die Abklingzeit.", "Halloween impostor. Press F to bite someone - they die a few seconds later, wherever they are then.\n\nIf a meeting starts first, they die right away. Bite and normal kill share the cooldown.");

    private const byte ActionBite = 0;

    private RoleOption cooldown, delay;

    /// <summary>Beim Host: Opfer -> (Vampir, Todeszeitpunkt)</summary>
    private readonly Dictionary<byte, (byte Vampire, float DiesAt)> bitten = new();

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Biss-Abklingzeit|Bite cooldown", 30f, 10f, 60f, 2.5f, "s");
        delay = AddOption("Delay", "Zeit bis zum Tod|Time until death", 10f, 3f, 20f, 1f, "s");

        AddButton(new RoleButton
        {
            Label = "Beißen|Bite",
            Icon = "bite",
            SharesKillCooldown = true, // kein Doppelkill mit dem normalen Kill-Button
            Duration = () => delay.Value, // Anzeige: so lange lebt das Opfer noch
            Cooldown = () => cooldown.Value,
            Hotkey = KeyCode.F,
            TargetFilter = p => !IsImpostor(p),
            OnClick = target =>
            {
                myBites.Add(target.PlayerId); // Kill-Geraeusch erst, wenn das Opfer wirklich stirbt
                SendToHost(ActionBite, target.PlayerId);
            },
        });
    }

    /// <summary>Lokal beim Vampir: wen er gebissen hat.</summary>
    private readonly HashSet<byte> myBites = new();

    public override void ResetState()
    {
        bitten.Clear();
        myBites.Clear();
    }

    /// <summary>Nach dem Meeting sind alle Bisse erledigt (gestorben oder vom Schild abgefangen).</summary>
    public override void OnMeetingEnd(PlayerControl exiled) => myBites.Clear();

    /// <summary>Der Vampir hoert das Kill-Geraeusch genau dann, wenn sein Opfer tot umfaellt.</summary>
    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        if (!myBites.Remove(victim.PlayerId)) return;
        if (cause != DeathCause.Exile) Sfx.Play(Sfx.Kill);
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionBite || data.Length < 1 || !AmHost) return;
        if (!RoleRegistry.Has(sender, this) || !IsAlive(sender) || InMeeting) return;

        var target = FindPlayer(data[0]);
        if (!IsAlive(target) || bitten.ContainsKey(target.PlayerId)) return;
        if (target.protectedByGuardianId >= 0) return;
        if (KillSystem.Intercept(sender, target)) return; // Medic-Schild, Bodyguard ...

        bitten[target.PlayerId] = (sender.PlayerId, Time.time + delay.Value);
    }

    public override void Update()
    {
        if (!AmHost || bitten.Count == 0 || InMeeting) return;
        foreach (var pair in bitten.Where(b => Time.time >= b.Value.DiesAt).ToList())
        {
            bitten.Remove(pair.Key);
            var victim = FindPlayer(pair.Key);
            if (IsAlive(victim)) KillSystem.ForceKill(victim, victim); // Leiche dort, wo das Opfer gerade ist
        }
    }

    public override void OnMeetingStart()
    {
        if (!AmHost) return;
        foreach (var pair in bitten.ToList())
        {
            var victim = FindPlayer(pair.Key);
            if (IsAlive(victim)) KillSystem.KillNoBody(victim, FindPlayer(pair.Value.Vampire));
        }
        bitten.Clear();
    }
}
