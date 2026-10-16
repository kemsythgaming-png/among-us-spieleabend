using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Halloween;

/// <summary>
/// Impostor: tippt im Meeting eine Spielerkarte an (auch die eigene) - eine Stimme gegen diesen Spieler zaehlt nicht.
/// Der Kopf des Waehlers fehlt dafuer in der Auswertung - wer aufpasst, merkt es.
/// </summary>
public sealed class BlackCat : CustomRole
{
    public override string ConfigSection => "SchwarzeKatze";
    public override string Name => Lang.T("Schwarze Katze", "Black Cat");
    public override Color Color => new Color32(110, 70, 160, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Pech für die anderen", "Bad luck for the others");
    public override string Description => Lang.T("Im Meeting: eine Stimme gegen\neinen Spieler fällt weg (Pfote).", "In meetings: one vote against\na player is dropped (paw).");
    public override string LongDescription => Lang.T(
        "Halloween-Impostor mit normalem Kill. Im Meeting tippst du einmal auf das Katzen-Symbol an einer Spielerkarte - auch an deiner eigenen.\n\nEine Stimme gegen diesen Spieler zählt dann nicht. Aber Vorsicht: In der Auswertung fehlt der Kopf dieses Wählers. Merken die Leute das, wissen sie, wen du schützen wolltest.",
        "Halloween impostor with a normal kill. In meetings you tap the cat symbol on a player card once - your own works too.\n\nOne vote against that player then doesn't count. But careful: that voter's head is missing in the results. If people notice, they know whom you wanted to protect.");

    private const byte ActionMark = 0;
    private const byte None = byte.MaxValue;

    internal static BlackCat Instance { get; private set; }

    // lokal bei der Katze
    private bool usedThisMeeting;

    // beim Host: geschuetzte Spieler in diesem Meeting (eine Stimme weniger je Katze)
    private readonly List<byte> protectedThisMeeting = new();

    protected override void Setup() => Instance = this;

    public override void ResetState()
    {
        usedThisMeeting = false;
        protectedThisMeeting.Clear();
    }

    public override void OnMeetingStart()
    {
        usedThisMeeting = false;
        protectedThisMeeting.Clear();
    }

    private bool CanUse(MeetingHud meeting) =>
        meeting && IsAlive(PlayerControl.LocalPlayer) && !usedThisMeeting
        && meeting.state != MeetingHud.MeetingStates.Results && meeting.state != MeetingHud.MeetingStates.Proceeding;

    /// <summary>Katzen-Knopf auf jeder Karte eines lebenden Spielers.</summary>
    public override void OnMeetingUpdate(MeetingHud meeting)
    {
        bool can = CanUse(meeting);
        foreach (var state in meeting.playerStates)
        {
            if (!state) continue;
            var target = FindPlayer((byte)state.PlayerId);
            bool show = can && IsAlive(target) && !state.AmDead;

            var button = state.transform.Find("Spieleabend_CatButton");
            if (!button && show && state.CancelButton) button = CreateButton(state, target.PlayerId);
            if (button) button.gameObject.SetActive(show);
        }
    }

    private Transform CreateButton(PlayerVoteArea state, byte targetId)
    {
        var go = Object.Instantiate(state.CancelButton.gameObject, state.transform);
        go.name = "Spieleabend_CatButton";
        go.transform.localPosition = new Vector3(1.05f, 0.03f, -1.3f);
        go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
        go.SetActive(true);
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!sr.sprite) continue;
            var sprite = ButtonIcons.Get("cat", sr.sprite);
            if (sprite) sr.sprite = sprite;
        }

        var button = go.GetComponent<PassiveButton>();
        if (button)
        {
            button.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            button.OnClick.AddListener((System.Action)(() => Mark(targetId)));
        }
        return go.transform;
    }

    private void Mark(byte targetId)
    {
        if (!CanUse(MeetingHud.Instance)) return;
        var target = FindPlayer(targetId);
        if (!IsAlive(target)) return;

        usedThisMeeting = true;
        SendToHost(ActionMark, targetId);
        Sfx.Play(Sfx.Vanish);
        Notifier.Show($"<color={ColorHex}>{Lang.T("Eine Stimme gegen", "One vote against")} {target.Data.PlayerName} {Lang.T("zählt nicht.", "won't count.")}</color>");
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionMark || data.Length < 1 || !AmHost) return;
        if (!RoleRegistry.Has(sender, this) || !IsAlive(sender) || !MeetingHud.Instance) return;
        protectedThisMeeting.Add(data[0]);
    }

    /// <summary>Host: wie viele Stimmen gegen diesen Spieler wegfallen.</summary>
    internal int RemovedVotes(byte playerId) => protectedThisMeeting.Count(id => id == playerId);
}

/// <summary>Host beim Auszaehlen: Stimmen gegen geschuetzte Spieler abziehen (nach dem Mayor, vor dem Tiebreaker).</summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CalculateVotes))]
[HarmonyPriority(Priority.Normal - 1)]
internal static class BlackCatVotePatch
{
    public static void Postfix(Il2CppSystem.Collections.Generic.Dictionary<byte, int> __result)
    {
        var cat = BlackCat.Instance;
        if (cat == null || __result == null || !CustomRole.AmHost) return;
        var keys = new List<byte>();
        foreach (var pair in __result) keys.Add(pair.Key);
        foreach (var key in keys)
        {
            int removed = cat.RemovedVotes(key);
            if (removed > 0) __result[key] = Mathf.Max(0, __result[key] - removed);
        }
    }
}

/// <summary>Host: den Kopf der weggefallenen Stimme aus der Auswertung entfernen - so sieht es jeder.</summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.RpcVotingComplete))]
internal static class BlackCatResultPatch
{
    public static void Prefix(ref Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<MeetingHud.VoterState> states)
    {
        var cat = BlackCat.Instance;
        if (cat == null || states == null || !CustomRole.AmHost) return;

        var list = new List<MeetingHud.VoterState>();
        foreach (var s in states) list.Add(s);

        bool changed = false;
        foreach (var targetId in list.Select(s => (byte)s.VotedForId).Distinct().ToList())
        {
            int remove = cat.RemovedVotes(targetId);
            for (int i = 0; i < remove; i++)
            {
                var votes = list.Where(s => (byte)s.VotedForId == targetId).ToList();
                if (votes.Count == 0) break;
                // Bevorzugt nicht die Stimme des Mayors entfernen (die zaehlt mehrfach)
                var pick = votes.OrderBy(s => IsMayor((byte)s.VoterId) ? 1 : 0).ThenBy(_ => Random.value).First();
                list.Remove(pick);
                changed = true;
            }
        }
        if (changed) states = list.ToArray();
    }

    private static bool IsMayor(byte playerId) =>
        Mayor.Instance != null && RoleRegistry.Has(CustomRole.FindPlayer(playerId), Mayor.Instance);
}
