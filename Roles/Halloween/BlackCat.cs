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
        if (!IsAlive(FindPlayer(targetId))) return;

        // Ganz leise: keine Nachricht, kein Geraeusch - nur die Knoepfe verschwinden
        usedThisMeeting = true;
        SendToAll(ActionMark, targetId);
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        // Alle Clients merken sich das Ziel (fuer die Anzeige), der Host zaehlt
        if (action != ActionMark || data.Length < 1) return;
        if (!RoleRegistry.Has(sender, this) || !IsAlive(sender) || !MeetingHud.Instance) return;
        protectedThisMeeting.Add(data[0]);
        Plugin.Logger.LogInfo($"Schwarze Katze: eine Stimme gegen {data[0]} zählt nicht.");
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
