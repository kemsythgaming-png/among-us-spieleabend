using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Crew (Vertauscher): Im Meeting klickt er auf zwei Spielerkarten. Wenn die Abstimmung vorbei ist, tauschen die
/// beiden Karten sichtbar (animiert) die Plätze - und ihre erhaltenen Stimmen werden vertauscht, wer für den einen
/// gestimmt hat, zählt für den anderen. Einmal pro Meeting.
/// </summary>
public sealed class VoteSwapper : CustomRole
{
    public override string ConfigSection => "Vertauscher";
    public override string Name => Lang.T("Vertauscher", "Vote Swapper");
    public override Color Color => new Color32(150, 110, 210, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Dreh die Stimmen", "Twist the votes");
    public override string Description => Lang.T("Im Meeting zwei Karten wählen -\nnach der Abstimmung tauschen sie.", "In the meeting pick two cards -\nafter voting they swap.");
    public override string LongDescription => Lang.T(
        "Du bist Crew. Im Meeting kannst du auf zwei Spielerkarten klicken (kleiner Knopf auf der Karte). Sobald die Abstimmung vorbei ist, tauschen die beiden Karten sichtbar die Plätze, und ihre erhaltenen Stimmen werden vertauscht - wer für den einen gestimmt hat, zählt für den anderen. Nur einmal pro Meeting.",
        "You are crew. In the meeting you can click two player cards (small button on the card). Once voting is over, the two cards visibly swap seats and their received votes are swapped - a vote for one counts for the other. Only once per meeting.");

    private const byte ActionPair = 0, ActionClear = 1;

    internal static VoteSwapper Instance { get; private set; }
    internal static byte PairA = byte.MaxValue, PairB = byte.MaxValue;

    private byte firstId = byte.MaxValue;   // erste im Meeting gewählte Karte
    private bool usedThisMeeting;

    // Animierter Karten-Tausch nach Abstimmungsende - statisch, da er bei ALLEN Clients laufen muss
    // (OnMeetingUpdate läuft nur beim Besitzer; der MeetingHud.Update-Patch unten treibt die Animation überall).
    internal static bool Pending, Animating;
    private static float animStart;
    private static Vector3 startA, startB;
    private static PlayerVoteArea areaA, areaB;

    protected override void Setup() => Instance = this;

    public override void ResetState() { Instance = this; ResetMeeting(); }

    public override void OnMeetingStart() => ResetMeeting();

    private void ResetMeeting()
    {
        firstId = byte.MaxValue;
        usedThisMeeting = false;
        PairA = PairB = byte.MaxValue;
        Pending = Animating = false;
        areaA = areaB = null;
    }

    public override void OnMeetingEnd(PlayerControl exiled)
    {
        if (AmHost) SendToAll(ActionClear);
        PairA = PairB = byte.MaxValue;
        Pending = Animating = false;
    }

    public override void OnMeetingUpdate(MeetingHud meeting)
    {
        if (!meeting || !IsLocalRole) return;

        // Auswahl-Knöpfe auf den Karten (nur solange noch nicht abgestimmt/getauscht wird)
        bool canPick = !Pending && !Animating && !VotingDone(meeting);
        foreach (var state in meeting.playerStates)
        {
            if (!state) continue;
            var p = FindPlayer((byte)state.PlayerId);
            bool can = canPick && IsAlive(p) && !usedThisMeeting;
            var card = state.transform.Find("Spieleabend_SwapBtn");
            if (!card && IsAlive(p) && state.CancelButton) card = CreateSwapButton(state, (byte)state.PlayerId);
            if (card)
            {
                card.gameObject.SetActive(can);
                foreach (var sr in card.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sr.sprite) sr.color = (byte)state.PlayerId == firstId ? Color : UnityEngine.Color.white;
            }
        }
    }

    /// <summary>Ist die Abstimmung vorbei (Ergebnis/Weiter)?</summary>
    internal static bool VotingDone(MeetingHud meeting)
    {
        try { return (int)meeting.state >= 4; } // Results = 4, Proceeding = 5
        catch { return false; }
    }

    /// <summary>Wird vom MeetingHud.Update-Patch bei JEDEM Client aufgerufen - startet/führt die Tausch-Animation.</summary>
    internal static void MeetingTick(MeetingHud meeting)
    {
        if (!meeting) return;
        if (Animating) { StepAnim(); return; }
        if (Pending && VotingDone(meeting) && PairA != byte.MaxValue && PairB != byte.MaxValue)
            BeginAnim(meeting);
    }

    private static void BeginAnim(MeetingHud meeting)
    {
        areaA = areaB = null;
        foreach (var s in meeting.playerStates)
        {
            if (!s) continue;
            if ((byte)s.PlayerId == PairA) areaA = s;
            else if ((byte)s.PlayerId == PairB) areaB = s;
        }
        Pending = false;
        if (!areaA || !areaB) return;
        startA = areaA.transform.localPosition;
        startB = areaB.transform.localPosition;
        Animating = true;
        animStart = Time.time;
    }

    private static void StepAnim()
    {
        if (!areaA || !areaB) { Animating = false; return; }
        float t = Mathf.Clamp01((Time.time - animStart) / 0.6f);
        float e = Mathf.SmoothStep(0f, 1f, t);
        areaA.transform.localPosition = Vector3.Lerp(startA, startB, e);
        areaB.transform.localPosition = Vector3.Lerp(startB, startA, e);
        if (t >= 1f) Animating = false;
    }

    private Transform CreateSwapButton(PlayerVoteArea state, byte targetId)
    {
        var go = Object.Instantiate(state.CancelButton.gameObject, state.transform);
        go.name = "Spieleabend_SwapBtn";
        go.transform.localPosition = new Vector3(1.05f, 0.03f, -1.3f);
        go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
        go.SetActive(true);
        var swap = Swapper.SwapSprite();
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            if (sr.sprite && swap) sr.sprite = swap;
        var button = go.GetComponent<PassiveButton>();
        if (button)
        {
            button.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            button.OnClick.AddListener((System.Action)(() => Select(targetId)));
        }
        return go.transform;
    }

    private void Select(byte id)
    {
        if (usedThisMeeting || !IsLocalRole) return;
        Sfx.Play(Sfx.TaskUpdate, 0.7f); // normaler Klick-Ton
        if (firstId == byte.MaxValue) { firstId = id; return; }
        if (id == firstId) { firstId = byte.MaxValue; return; } // dieselbe nochmal = abwählen
        usedThisMeeting = true;
        SendToAll(ActionPair, firstId, id);
        firstId = byte.MaxValue;
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionPair && data.Length >= 2 && RoleRegistry.Has(sender, this))
        {
            PairA = data[0]; PairB = data[1];
            Pending = true; // sichtbarer Tausch kommt erst nach Abstimmungsende
        }
        else if (action == ActionClear) { PairA = PairB = byte.MaxValue; Pending = false; }
    }
}

/// <summary>Treibt den animierten Karten-Tausch des Vertauschers bei jedem Client (nach Abstimmungsende).</summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
internal static class VoteSwapAnimPatch
{
    public static void Postfix(MeetingHud __instance)
    {
        if (VoteSwapper.Instance != null) VoteSwapper.MeetingTick(__instance);
    }
}

/// <summary>Stimmen der beiden getauschten Spieler tauschen (nach Mayor/Tiebreaker).</summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CalculateVotes))]
[HarmonyPriority(Priority.VeryLow)]
internal static class VoteSwapPatch
{
    public static void Postfix(Il2CppSystem.Collections.Generic.Dictionary<byte, int> __result)
    {
        if (VoteSwapper.Instance == null || __result == null) return;
        byte a = VoteSwapper.PairA, b = VoteSwapper.PairB;
        if (a == byte.MaxValue || b == byte.MaxValue) return;

        int va = __result.ContainsKey(a) ? __result[a] : 0;
        int vb = __result.ContainsKey(b) ? __result[b] : 0;
        __result[a] = vb;
        __result[b] = va;
    }
}
