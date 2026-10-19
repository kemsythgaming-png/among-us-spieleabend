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
    public override string Description
    {
        get
        {
            var local = PlayerControl.LocalPlayer;
            if (local && RoleRegistry.Has(local, this))
                return Lang.T($"Im Meeting: Stimme streichen (Pfote).\nCounter: {Count(local.PlayerId)} - jeder eigene Kill +1.",
                              $"In meetings: cancel a vote (paw).\nCounters: {Count(local.PlayerId)} - each own kill +1.");
            return Lang.T("Im Meeting: eine Stimme gegen\neinen Spieler fällt weg (Pfote).", "In meetings: one vote against\na player is dropped (paw).");
        }
    }
    public override string LongDescription => Lang.T(
        "Halloween-Impostor mit normalem Kill. Im Meeting tippst du auf das Katzen-Symbol an einer Spielerkarte (auch an deiner eigenen) - eine Stimme gegen diesen Spieler zählt dann nicht.\n\nDu startest mit 1 Counter; jeder Kill, den du selbst machst, gibt dir einen weiteren. Deine Counter-Zahl steht oben links bei der Rolle. Aber Vorsicht: In der Auswertung fehlt der Kopf des gestrichenen Wählers.",
        "Halloween impostor with a normal kill. In meetings you tap the cat symbol on a player card (your own works too) - one vote against that player then doesn't count.\n\nYou start with 1 counter; each kill you make yourself gives you one more. Your counter count shows with your role (top left). But careful: the dropped voter's head is missing in the results.");

    private const byte ActionMark = 0;
    private const byte None = byte.MaxValue;

    internal static BlackCat Instance { get; private set; }

    // Counter je Katze (bei allen Clients gleich): Start 1, pro eigenem Kill +1, pro Streichung -1
    private readonly Dictionary<byte, int> counters = new();

    // beim Host: geschuetzte Spieler in diesem Meeting (eine Stimme weniger je Streichung)
    private readonly List<byte> protectedThisMeeting = new();

    protected override void Setup() => Instance = this;

    private int Count(byte id) => counters.TryGetValue(id, out int n) ? n : 0;

    public override void OnAssigned(PlayerControl player)
    {
        if (player) counters[player.PlayerId] = 1; // Start-Counter (auf allen Clients)
    }

    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        // Nur selbst gemachte Kills geben einen Counter
        if (cause != DeathCause.Kill || !killer || killer == victim || !RoleRegistry.Has(killer, this)) return;
        counters[killer.PlayerId] = Count(killer.PlayerId) + 1;
    }

    public override void ResetState()
    {
        counters.Clear();
        protectedThisMeeting.Clear();
    }

    public override void OnMeetingStart() => protectedThisMeeting.Clear();

    private bool CanUse(MeetingHud meeting) =>
        meeting && IsAlive(PlayerControl.LocalPlayer) && Count(PlayerControl.LocalPlayer.PlayerId) > 0
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
        if (!IsAlive(FindPlayer(targetId))) return;

        // Counter-Abzug passiert in OnAction (bei allen gleich)
        SendToAll(ActionMark, targetId);
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        // Alle Clients merken sich das Ziel (fuer die Anzeige), der Host zaehlt; Counter überall abziehen
        if (action != ActionMark || data.Length < 1) return;
        if (!RoleRegistry.Has(sender, this) || !IsAlive(sender) || !MeetingHud.Instance) return;
        if (Count(sender.PlayerId) <= 0) return;
        counters[sender.PlayerId] = Count(sender.PlayerId) - 1;
        protectedThisMeeting.Add(data[0]);
        Plugin.Logger.LogInfo($"Schwarze Katze: eine Stimme gegen {data[0]} zählt nicht ({Count(sender.PlayerId)} Counter übrig).");
    }

    /// <summary>Wie viele Stimmen gegen diesen Spieler wegfallen.</summary>
    internal int RemovedVotes(byte playerId) => protectedThisMeeting.Count(id => id == playerId);

    internal IEnumerable<byte> ProtectedTargets => protectedThisMeeting.Distinct();
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

/// <summary>
/// Auswertung (bei allen): Fuer jede weggefallene Stimme wird beim Ziel der erste Kopf gar nicht erst angezeigt.
/// Alle Clients bekommen die Stimmen in derselben Reihenfolge, darum fehlt ueberall derselbe Kopf.
/// </summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.PopulateResults))]
internal static class BlackCatResultStartPatch
{
    /// <summary>Wie viele Koepfe pro Ziel noch weggelassen werden muessen.</summary>
    internal static readonly Dictionary<byte, int> ToSkip = new();

    public static void Prefix()
    {
        ToSkip.Clear();
        var cat = BlackCat.Instance;
        if (cat == null) return;
        foreach (var id in cat.ProtectedTargets) ToSkip[id] = cat.RemovedVotes(id);
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.BloopAVoteIcon))]
internal static class BlackCatVoteIconPatch
{
    public static bool Prefix(MeetingHud __instance, Transform parent)
    {
        if (BlackCatResultStartPatch.ToSkip.Count == 0 || !parent) return true;
        var area = parent.GetComponentInParent<PlayerVoteArea>();
        if (!area) return true;
        byte target = (byte)area.PlayerId;
        if (!BlackCatResultStartPatch.ToSkip.TryGetValue(target, out int left) || left <= 0) return true;
        BlackCatResultStartPatch.ToSkip[target] = left - 1;
        Plugin.Logger.LogInfo($"Schwarze Katze: eine Stimme gegen {target} wird nicht angezeigt.");
        return false; // diesen Kopf nicht anzeigen
    }
}
