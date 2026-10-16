using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Halloween;

/// <summary>
/// Neutral: Wird er getoetet, steht er nach dem naechsten Meeting als Untoter wieder auf. Im Meeting gilt er als tot
/// (keine Stimme). Sammelt er Seelen (an fremden Leichen stehen), wird er wieder lebendig und spielt fuer die Crew.
/// Schafft er das nicht bis Spielende, verliert er.
/// </summary>
public sealed class Undead : CustomRole
{
    public override string ConfigSection => "Untoter";
    public override string Name => Lang.T("Untoter", "Undead");
    public override Color Color => new Color32(120, 170, 110, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override bool TasksCount => true;
    public override string IntroText => Lang.T("Der Tod ist erst der Anfang", "Death is only the beginning");
    public override string Description => Stage(PlayerControl.LocalPlayer) switch
    {
        UndeadStage.Risen => Lang.T($"Sammle Seelen: Steh an fremden Leichen.\n({SoulsOf(PlayerControl.LocalPlayer)}/{souls.Int})", $"Collect souls: stand at other bodies.\n({SoulsOf(PlayerControl.LocalPlayer)}/{souls.Int})"),
        UndeadStage.Restored => Lang.T("Du lebst wieder - hilf der Crew.", "You live again - help the crew."),
        _ => Lang.T("Wirst du getötet, stehst du\nnach dem Meeting wieder auf.", "If you are killed, you rise\nagain after the meeting."),
    };
    public override string LongDescription => Lang.T(
        "Halloween-Neutral. Wirst du getötet, bist du bis zum Ende des nächsten Meetings ein Geist - aber ohne Rollen-Infos. Danach stehst du als Untoter wieder auf.\n\nAls Untoter läufst du sichtbar herum, giltst im Meeting aber als tot (keine Stimme, du schweigst). Steh an fremden Leichen, um Seelen zu sammeln. Hast du genug, lebst du wieder und gewinnst mit der Crew. Schaffst du es nicht, verlierst du. Ein zweiter Tod ist endgültig.",
        "Halloween neutral. If you are killed, you are a ghost until the end of the next meeting - but without role info. Then you rise again as undead.\n\nAs undead you walk around visibly, but count as dead in meetings (no vote, you stay silent). Stand at other bodies to collect souls. Once you have enough, you live again and win with the crew. If you fail, you lose. A second death is final.");

    private const byte ActionRise = 0, ActionSoul = 1, ActionRestore = 2;

    internal static Undead Instance { get; private set; }

    private RoleOption souls, soulTime;

    // bei allen Clients
    private readonly Dictionary<byte, UndeadStage> stages = new();
    private readonly Dictionary<byte, int> collected = new();

    // lokal beim Untoten: Leiche -> Zeit an ihr; schon gesammelte Leichen
    private readonly Dictionary<byte, float> standing = new();
    private readonly HashSet<byte> harvested = new();

    /// <summary>Namen der Untoten, die gerade NICHT gewinnen duerfen (fuer die Siegerliste am Spielende).</summary>
    internal static readonly HashSet<string> LosingNames = new();

    protected override void Setup()
    {
        Instance = this;
        souls = AddOption("Souls", "Benötigte Seelen|Souls needed", 2f, 1f, 5f, 1f);
        soulTime = AddOption("SoulTime", "Zeit an der Leiche|Time at the body", 5f, 1f, 15f, 1f, "s");
    }

    public override void ResetState()
    {
        stages.Clear();
        collected.Clear();
        standing.Clear();
        harvested.Clear();
        // LosingNames bleibt bis zum Ende-Bildschirm stehen (wird dort benutzt und geleert)
    }

    internal UndeadStage Stage(PlayerControl p) =>
        p && stages.TryGetValue(p.PlayerId, out var s) ? s : UndeadStage.Alive;

    private int SoulsOf(PlayerControl p) => p && collected.TryGetValue(p.PlayerId, out var c) ? c : 0;

    /// <summary>Toter Untoter, der noch aufstehen wird (bekommt keine Geisterrolle).</summary>
    internal bool WillRise(PlayerControl p) => RoleRegistry.Has(p, this) && Stage(p) == UndeadStage.Dead;

    /// <summary>Untoter, der im Meeting als tot gilt.</summary>
    internal bool CountsAsDeadInMeeting(PlayerControl p) => RoleRegistry.Has(p, this) && Stage(p) == UndeadStage.Risen && IsAlive(p);

    /// <summary>Toter Untoter, der noch aufstehen wird - bekommt keine Rollen-Infos.</summary>
    internal bool HidesRoles(PlayerControl viewer) => RoleRegistry.Has(viewer, this) && Stage(viewer) is UndeadStage.Dead or UndeadStage.Risen;

    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        if (!RoleRegistry.Has(victim, this)) return;
        var stage = Stage(victim);
        // Erster Tod durch Kill (auch ohne Leiche) -> steht wieder auf; Rauswurf oder zweiter Tod -> endgueltig
        stages[victim.PlayerId] = stage == UndeadStage.Alive && cause != DeathCause.Exile ? UndeadStage.Dead : UndeadStage.Final;
        if (victim == PlayerControl.LocalPlayer && stages[victim.PlayerId] == UndeadStage.Dead)
            Notifier.Show($"<color={ColorHex}>{Lang.T("Du bist gestorben ... aber nicht für immer. Nach dem nächsten Meeting stehst du wieder auf.", "You died ... but not forever. You will rise again after the next meeting.")}</color>", 6f);
    }

    public override void OnMeetingEnd(PlayerControl exiled)
    {
        if (!AmHost) return;
        foreach (var pair in stages.Where(s => s.Value == UndeadStage.Dead).ToList())
        {
            var p = FindPlayer(pair.Key);
            if (!p || p.Data == null || p.Data.Disconnected || p == exiled) continue;
            SendToAll(ActionRise, pair.Key);
        }
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (data.Length < 1) return;
        var p = FindPlayer(data[0]);
        if (!p) return;

        switch (action)
        {
            case ActionRise:
                if (Stage(p) != UndeadStage.Dead) return;
                stages[p.PlayerId] = UndeadStage.Risen;
                p.Revive();
                if (AmHost) p.RpcSetRole(RoleTypes.Crewmate, true);
                ProtectionVisuals.ShowBurst(p, Color);
                if (p == PlayerControl.LocalPlayer)
                {
                    Sfx.Play(Sfx.Creepy);
                    Notifier.Show($"<color={ColorHex}>{Lang.T($"Du bist auferstanden! Sammle {souls.Int} Seelen an fremden Leichen.", $"You have risen! Collect {souls.Int} souls at other bodies.")}</color>", 6f);
                }
                break;

            case ActionSoul:
                if (Stage(p) != UndeadStage.Risen) return;
                collected[p.PlayerId] = SoulsOf(p) + 1;
                break;

            case ActionRestore:
                if (Stage(p) != UndeadStage.Risen) return;
                stages[p.PlayerId] = UndeadStage.Restored;
                ProtectionVisuals.ShowBurst(p, Color);
                if (p == PlayerControl.LocalPlayer)
                {
                    Sfx.Play(Sfx.TaskComplete);
                    Notifier.Show($"<color={ColorHex}>{Lang.T("Genug Seelen - du lebst wieder! Du spielst jetzt für die Crew.", "Enough souls - you live again! You now play for the crew.")}</color>", 6f);
                }
                break;
        }
    }

    public override void Update()
    {
        var local = PlayerControl.LocalPlayer;
        if (!IsLocalRole || Stage(local) != UndeadStage.Risen || !IsAlive(local) || InMeeting) return;

        Vector2 me = local.GetTruePosition();
        var near = new HashSet<byte>();
        foreach (var body in Object.FindObjectsOfType<DeadBody>())
        {
            if (!body || body.ParentId == local.PlayerId || harvested.Contains(body.ParentId)) continue;
            if (Vector2.Distance(me, body.TruePosition) > 1.2f) continue;
            near.Add(body.ParentId);
        }

        foreach (var id in standing.Keys.ToList())
            if (!near.Contains(id)) standing.Remove(id); // weggegangen -> von vorn

        foreach (var id in near)
        {
            standing.TryGetValue(id, out float t);
            t += Time.deltaTime;
            standing[id] = t;
            if (t < soulTime.Value) continue;

            standing.Remove(id);
            harvested.Add(id);
            SendToAll(ActionSoul, local.PlayerId);
            if (SoulsOf(local) >= souls.Int) SendToAll(ActionRestore, local.PlayerId);
            else
            {
                Sfx.Play(Sfx.TaskUpdate);
                Notifier.Show($"<color={ColorHex}>{Lang.T("Seele", "Soul")} {SoulsOf(local)}/{souls.Int}</color>", 3f);
            }
        }
    }

    /// <summary>Beim Spielende (bevor die Rollen geloescht werden): wer als Untoter nicht fertig wurde, verliert.</summary>
    internal void CaptureLosers()
    {
        LosingNames.Clear();
        foreach (var p in Players)
            if (p && p.Data != null && Stage(p) is UndeadStage.Dead or UndeadStage.Risen or UndeadStage.Final)
                LosingNames.Add(p.Data.PlayerName);
    }

    public override string GetNameMark(PlayerControl viewer, PlayerControl target)
    {
        if (!RoleRegistry.Has(target, this) || Stage(target) != UndeadStage.Risen || !IsAlive(target)) return null;
        // Alle sehen, dass er untot ist; er selbst sieht seinen Fortschritt
        if (viewer == target) return $"<color={ColorHex}>[{Lang.T("Untot", "Undead")} {SoulsOf(target)}/{souls.Int}]</color>";
        return $"<color={ColorHex}>[{Lang.T("Untot", "Undead")}]</color>";
    }
}

internal enum UndeadStage
{
    Alive,
    Dead,
    Risen,
    Restored,
    Final,
}

/// <summary>Im Meeting: Der Untote gilt als tot - Karte durchgestrichen, keine Stimme.</summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
internal static class UndeadMeetingStartPatch
{
    public static void Postfix(MeetingHud __instance)
    {
        var undead = Undead.Instance;
        if (undead == null) return;
        foreach (var state in __instance.playerStates)
        {
            if (!state) continue;
            var p = CustomRole.FindPlayer((byte)state.PlayerId);
            if (undead.CountsAsDeadInMeeting(p)) state.SetDead(true);
        }
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Select))]
internal static class UndeadSelectPatch
{
    public static bool Prefix(ref bool __result)
    {
        var undead = Undead.Instance;
        if (undead == null || !undead.CountsAsDeadInMeeting(PlayerControl.LocalPlayer)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CmdCastVote))]
internal static class UndeadCastVotePatch
{
    public static bool Prefix(byte playerId)
    {
        var undead = Undead.Instance;
        return undead == null || !undead.CountsAsDeadInMeeting(CustomRole.FindPlayer(playerId));
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
internal static class UndeadGameEndPatch
{
    public static void Prefix() => Undead.Instance?.CaptureLosers();
}

/// <summary>Spielende: Untote, die ihre Seelen nicht geschafft haben, stehen nicht bei den Gewinnern.</summary>
[HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
[HarmonyPriority(Priority.Low)]
internal static class UndeadWinnerPatch
{
    public static void Prefix()
    {
        if (Undead.LosingNames.Count == 0 || EndGameResult.CachedWinners == null) return;
        var keep = new Il2CppSystem.Collections.Generic.List<CachedPlayerData>();
        foreach (var w in EndGameResult.CachedWinners)
            if (w != null && !Undead.LosingNames.Contains(w.PlayerName)) keep.Add(w);
        EndGameResult.CachedWinners = keep;
    }

    public static void Postfix() => Undead.LosingNames.Clear();
}
