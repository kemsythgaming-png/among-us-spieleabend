using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Modifiers;

/// <summary>Anpassung (Crew): Wer den Koeder toetet, meldet die Leiche automatisch sofort selbst.</summary>
public sealed class Bait : CustomRole
{
    public override string ConfigSection => "Koeder";
    public override string Name => Lang.T("Köder", "Bait");
    public override Color Color => new Color32(0, 200, 220, 255);
    public override RoleTeam Team => RoleTeam.Modifier;
    public override bool ModifierAllowsImpostors => false;
    public override string IntroText => Lang.T("Wer dich tötet, verrät sich", "Whoever kills you gets exposed");
    public override string Description => Lang.T("Dein Mörder meldet deine Leiche sofort.", "Your killer reports your body instantly.");
    public override string LongDescription => Lang.T(
        "Anpassung (nur Crew): Wer dich tötet, meldet deine Leiche automatisch selbst - kurz nach dem Kill. So steht der Mörder sofort im Rampenlicht. Deine Rolle bleibt.",
        "Modifier (crew only): whoever kills you automatically reports your body right after the kill - putting the killer in the spotlight. Your role stays.");

    private RoleOption delay;
    private byte pendingBody = byte.MaxValue;
    private float reportAt;

    protected override void Setup()
    {
        delay = AddOption("Delay", "Verzögerung|Delay", 0.5f, 0f, 3f, 0.5f, "s");
    }

    public override void ResetState() => pendingBody = byte.MaxValue;

    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        // Laeuft beim Killer selbst: er "meldet" die Leiche (eine normale Aktion seines Clients)
        if (cause != DeathCause.Kill || !killer || killer == victim || killer != PlayerControl.LocalPlayer) return;
        if (!RoleRegistry.Has(victim, this)) return;
        pendingBody = victim.PlayerId;
        reportAt = Time.time + delay.Value;
    }

    public override void Update()
    {
        if (pendingBody == byte.MaxValue || Time.time < reportAt) return;
        var local = PlayerControl.LocalPlayer;
        var body = FindPlayer(pendingBody);
        pendingBody = byte.MaxValue;
        if (!IsAlive(local) || InMeeting || !body || body.Data == null) return;
        local.CmdReportDeadBody(body.Data);
    }
}

/// <summary>
/// Crew: Wird er getoetet, steht er nach dem naechsten Meeting wieder auf. Bis dahin ist er ein Geist, der an der Stelle
/// seines Todes festhaengt und alle Vorbeilaufenden nur getarnt sieht (er weiss nicht, wer ihn getoetet hat).
/// Zurueck im Spiel kann er kein Meeting mehr einberufen.
/// </summary>
public sealed class SecondLife : CustomRole
{
    public override string ConfigSection => "ZweitesLeben";
    public override string Name => "Survivor";
    public override Color Color => new Color32(255, 215, 90, 255);
    public override RoleTeam Team => RoleTeam.Crewmate; // eigene Crew-Rolle (war frueher eine Anpassung)
    public override string IntroText => Lang.T("Katzen haben neun - du hast zwei", "Cats have nine - you have two");
    public override string Description => Stage(PlayerControl.LocalPlayer) switch
    {
        SurvivorStage.Waiting => Lang.T("Du bist tot - nach dem nächsten\nMeeting kommst du zurück.", "You are dead - you come back\nafter the next meeting."),
        SurvivorStage.Back => Lang.T("Zurück! Aber du kannst kein\nMeeting mehr einberufen.", "Back! But you can't call\nmeetings anymore."),
        _ => Lang.T("Wirst du getötet, kommst du nach\ndem nächsten Meeting zurück.", "If you get killed, you come back\nafter the next meeting."),
    };
    public override string LongDescription => Lang.T(
        "Du bist Crew. Wirst du getötet, siehst du nicht, wer es war - der Mörder ist in der Kill-Animation getarnt.\n\nBis zum nächsten Meeting bist du ein Geist, der an der Stelle seines Todes festhängt. Alle, die vorbeilaufen, siehst du nur getarnt (grau, ohne Namen). Nach dem Meeting stehst du wieder auf - kannst aber kein Meeting mehr einberufen (weder Notfallknopf noch Leiche melden). Reden kannst du also nur, wenn jemand anderes ein Meeting startet. Ein zweiter Tod ist endgültig.",
        "You are crew. If you get killed, you don't see who did it - the killer is camouflaged in the kill animation.\n\nUntil the next meeting you are a ghost stuck at the spot of your death. Everyone walking by looks camouflaged to you (gray, no name). After the meeting you rise again - but you can't call meetings anymore (no emergency button, no reporting). So you can only talk when someone else starts a meeting. A second death is final.");

    internal static SecondLife Instance { get; private set; }

    private const byte ActionRise = 0;

    // bei allen Clients
    private readonly Dictionary<byte, SurvivorStage> stages = new();

    // lokal: wo man gestorben ist
    private Vector3 deathPos;

    protected override void Setup() => Instance = this;

    public override void ResetState() => stages.Clear();

    internal SurvivorStage Stage(PlayerControl p) =>
        p && stages.TryGetValue(p.PlayerId, out var s) ? s : SurvivorStage.Alive;

    /// <summary>Toter Survivor, der wieder aufsteht (bekommt keine Geisterrolle und keine Rollen-Infos).</summary>
    internal bool WillRise(PlayerControl p) => RoleRegistry.Has(p, this) && Stage(p) == SurvivorStage.Waiting;

    /// <summary>Zurueckgekehrter Survivor: darf kein Meeting einberufen.</summary>
    internal bool CannotCallMeetings(PlayerControl p) => RoleRegistry.Has(p, this) && Stage(p) == SurvivorStage.Back && IsAlive(p);

    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        if (!RoleRegistry.Has(victim, this)) return;
        bool first = Stage(victim) == SurvivorStage.Alive && cause != DeathCause.Exile;
        stages[victim.PlayerId] = first ? SurvivorStage.Waiting : SurvivorStage.Final;
        if (!first || victim != PlayerControl.LocalPlayer) return;

        deathPos = victim.transform.position;
        Notifier.Show($"<color={ColorHex}>{Lang.T("Du bist gestorben ... aber nach dem nächsten Meeting kommst du zurück.", "You died ... but you come back after the next meeting.")}</color>", 6f);
    }

    public override void OnMeetingEnd(PlayerControl exiled)
    {
        if (!AmHost) return;
        foreach (var pair in stages.Where(s => s.Value == SurvivorStage.Waiting).ToList())
        {
            var p = FindPlayer(pair.Key);
            if (!p || p.Data == null || p.Data.Disconnected || p == exiled) continue;
            SendToAll(ActionRise, pair.Key);
        }
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionRise || data.Length < 1 || !sender) return;
        if (AmongUsClient.Instance && sender.OwnerId != AmongUsClient.Instance.HostId) return; // nur der Host laesst aufstehen
        var p = FindPlayer(data[0]);
        if (!p || Stage(p) != SurvivorStage.Waiting) return;
        stages[p.PlayerId] = SurvivorStage.Back;
        p.Revive();
        if (AmHost) p.RpcSetRole(AmongUs.GameOptions.RoleTypes.Crewmate, true);
        ProtectionVisuals.ShowBurst(p, Color);
        if (p == PlayerControl.LocalPlayer)
        {
            Sfx.Play(Sfx.Protect);
            Notifier.Show($"<color={ColorHex}>{Lang.T("Du bist zurück! Ein Meeting kannst du aber nicht mehr einberufen.", "You are back! But you can't call a meeting anymore.")}</color>", 6f);
        }
    }

    public override void Update()
    {
        var local = PlayerControl.LocalPlayer;
        if (!IsLocalRole || Stage(local) != SurvivorStage.Waiting || InMeeting) return;

        // Als Geist an der Stelle des Todes festhalten
        if (local.MyPhysics && local.MyPhysics.body) local.MyPhysics.body.velocity = Vector2.zero;
        if (((Vector2)local.transform.position - (Vector2)deathPos).sqrMagnitude > 0.0004f)
            local.transform.position = new Vector3(deathPos.x, deathPos.y, local.transform.position.z);

        // Alle Vorbeilaufenden nur getarnt sehen (nur bei ihm selbst)
        Camouflage.Start(0.5f);
    }
}

internal enum SurvivorStage
{
    Alive,
    Waiting,
    Back,
    Final,
}

/// <summary>Zurueckgekehrter Survivor: kein Notfallknopf, keine Leiche melden.</summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdReportDeadBody))]
internal static class SurvivorNoMeetingPatch
{
    public static bool Prefix(PlayerControl __instance)
    {
        var survivor = SecondLife.Instance;
        if (survivor == null || !survivor.CannotCallMeetings(__instance)) return true;
        if (__instance == PlayerControl.LocalPlayer)
            Notifier.Show($"<color={survivor.ColorHex}>{Lang.T("Du kannst kein Meeting einberufen.", "You can't call a meeting.")}</color>", 2.5f);
        return false;
    }
}

/// <summary>Anpassung: bei Stimmengleichstand entscheidet seine Stimme.</summary>
public sealed class Tiebreaker : CustomRole
{
    public override string Name => "Tiebreaker";
    public override Color Color => new Color32(150, 200, 255, 255);
    public override RoleTeam Team => RoleTeam.Modifier;
    public override string IntroText => Lang.T("Du hast das letzte Wort", "You have the final say");
    public override string Description => Lang.T("Bei Gleichstand zählt deine Stimme.", "Your vote breaks ties.");
    public override string LongDescription => Lang.T(
        "Anpassung: Steht es im Meeting unentschieden, gewinnt die Seite, für die du gestimmt hast. Deine Rolle bleibt.",
        "Modifier: if a meeting vote is tied, the side you voted for wins. Your role stays.");

    internal static Tiebreaker Instance { get; private set; }

    protected override void Setup() => Instance = this;
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CalculateVotes))]
[HarmonyPriority(Priority.Low)] // nach dem Mayor
internal static class TiebreakerVotePatch
{
    public static void Postfix(MeetingHud __instance, Il2CppSystem.Collections.Generic.Dictionary<byte, int> __result)
    {
        var tb = Tiebreaker.Instance;
        if (tb == null || __result == null || !RoleRegistry.HasAssignments || __result.Count == 0) return;

        var counts = new Dictionary<byte, int>();
        foreach (var pair in __result) counts[pair.Key] = pair.Value;
        int max = counts.Values.Max();
        var tied = counts.Where(c => c.Value == max).Select(c => c.Key).ToList();
        if (tied.Count < 2) return;

        foreach (var state in __instance.playerStates)
        {
            if (!state || !state.DidVote) continue;
            var voter = CustomRole.FindPlayer((byte)state.PlayerId);
            if (!CustomRole.IsAlive(voter) || !RoleRegistry.Has(voter, tb)) continue;
            byte votedFor = (byte)state.VotedForId;
            if (!tied.Contains(votedFor)) continue;
            __result[votedFor] = __result[votedFor] + 1;
            return;
        }
    }
}

/// <summary>Survivor: In seiner Kill-Animation ist der Moerder getarnt (grau, ohne Kosmetik) - er sieht nicht, wer es war.</summary>
[HarmonyPatch(typeof(KillOverlay), nameof(KillOverlay.ShowKillAnimation), typeof(OverlayKillAnimation), typeof(KillOverlayInitData))]
internal static class SurvivorKillOverlayPatch
{
    public static void Prefix(ref KillOverlayInitData initData)
    {
        var survivor = SecondLife.Instance;
        var local = PlayerControl.LocalPlayer;
        if (survivor == null || initData == null || !local || !RoleRegistry.Has(local, survivor)) return;
        if (initData.victimOutfit == null || local.Data == null || initData.victimOutfit.PlayerName != local.Data.DefaultOutfit.PlayerName) return;

        var hidden = new NetworkedPlayerInfo.PlayerOutfit
        {
            ColorId = 15, // grau wie bei der Tarnung
            HatId = "hat_NoHat",
            SkinId = "skin_None",
            VisorId = "visor_EmptyVisor",
            PetId = "pet_EmptyPet",
            NamePlateId = "nameplate_NoPlate",
            PlayerName = "",
        };
        initData = new KillOverlayInitData(hidden, initData.killerBodyType, initData.victimOutfit, initData.victimBodyType);
    }
}
