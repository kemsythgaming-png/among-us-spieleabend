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

/// <summary>Anpassung: ueberlebt den ersten Angriff einmal.</summary>
public sealed class SecondLife : CustomRole
{
    public override string ConfigSection => "ZweitesLeben";
    public override string Name => "Survivor";
    public override Color Color => new Color32(255, 215, 90, 255);
    public override RoleTeam Team => RoleTeam.Crewmate; // eigene Crew-Rolle (war frueher eine Anpassung)
    public override string IntroText => Lang.T("Katzen haben neun - du hast zwei", "Cats have nine - you have two");
    public override string Description => Lang.T("Du überlebst den ersten Angriff.", "You survive the first attack.");
    public override string LongDescription => Lang.T(
        "Du bist Crew. Der erste Angriff auf dich scheitert (der Angreifer sieht nur einen fehlgeschlagenen Kill). Danach bist du normal sterblich.\n\nWirst du getötet, siehst du in der Kill-Animation nicht, wer es war - der Mörder ist getarnt.",
        "You are crew. The first attack on you fails (the attacker only sees a failed kill). After that you are mortal as usual.\n\nIf you get killed, the kill animation doesn't show who did it - the killer is camouflaged.");

    internal static SecondLife Instance { get; private set; }

    private const byte ActionUsed = 0;
    private readonly HashSet<byte> used = new();

    protected override void Setup() => Instance = this;

    public override void ResetState() => used.Clear();

    public override KillIntercept InterceptKill(PlayerControl killer, PlayerControl target)
    {
        if (!RoleRegistry.Has(target, this) || used.Contains(target.PlayerId) || killer == target) return KillIntercept.None;
        killer.RpcMurderPlayer(target, false);
        SendToAll(ActionUsed, target.PlayerId);
        return KillIntercept.Handled;
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionUsed || data.Length < 1) return;
        used.Add(data[0]);
        var local = PlayerControl.LocalPlayer;
        if (local && local.PlayerId == data[0])
        {
            ProtectionVisuals.ShowBurst(local, Color);
            Sfx.Play(Sfx.Protect);
            Notifier.Show($"<color={ColorHex}>{Lang.T("Dein zweites Leben hat dich gerettet!", "Your second life saved you!")}</color>");
        }
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
