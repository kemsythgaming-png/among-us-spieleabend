using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Crew (Mayor): Sammelt Extra-Stimmen. Jedes Meeting, in dem er NICHT abstimmt (Skip/keine Wahl), bekommt er eine
/// gesparte Extra-Stimme. Tippt er im Meeting eine Spielerkarte an, erscheint neben Haken/X ein dritter Knopf -
/// damit setzt er seine volle Stimmkraft (Basis 1 + gesparte) auf dieses Ziel. Danach ist der Vorrat leer.
/// </summary>
public sealed class Mayor : CustomRole
{
    public override string Name =>
        Lang.T("Mayor", "Mayor");
    public override Color Color => new Color32(112, 79, 168, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText =>
        Lang.T("Deine Stimme hat Gewicht", "Your vote counts more");
    public override string Description =>
        Lang.T("Spar Stimmen (Skip) und setz sie\nmit dem 3. Knopf gebündelt ein.", "Save votes (skip) and cast them\nbundled with the 3rd button.");
    public override string LongDescription =>
        Lang.T("Du bist Crew. Für jedes Meeting, in dem du NICHT abstimmst (Skip oder keine Wahl), bekommst du eine gesparte Extra-Stimme. Oben links / unten siehst du deine aktuellen Stimmen (immer mindestens 1). Tippst du im Meeting eine Spielerkarte an, erscheint neben Haken und X ein dritter Knopf: Damit setzt du deine volle Stimmkraft (1 + alle gesparten) auf dieses Ziel - es erscheinen entsprechend viele Köpfe. Danach ist dein Vorrat wieder leer. Wählst du normal mit dem Haken, zählt deine Stimme 1 und der Vorrat bleibt. Keine Obergrenze.",
                "You are crew. For every meeting in which you do NOT vote (skip/abstain) you gain one saved extra vote. You always have at least 1 vote, shown in the corner. Tap a player card in a meeting and a third button appears next to the check and X: it casts your full voting power (1 + all saved) on that target - that many heads appear. Then your stock is empty again. Voting normally with the check counts as 1 and keeps your stock. No limit.");

    internal static Mayor Instance { get; private set; }

    private const byte ActionSaved = 0, ActionArm = 1;

    // Host-autoritativ: gesparte Extra-Stimmen je Mayor (0 = nur die normale Stimme).
    private readonly Dictionary<byte, int> saved = new();
    // Auf allen Clients gespiegelt (Anzeige).
    private readonly Dictionary<byte, int> shownSaved = new();
    // Host: welche Mayors setzen DIESES Meeting ihre gesparten Stimmen ein (per Knopf ausgelöst)?
    private readonly HashSet<byte> armed = new();
    // Host: dieses Auszählen schon entschieden (CalculateVotes läuft mehrfach -> Entscheidung nur 1x).
    private readonly HashSet<byte> tallied = new();
    // Host: je Mayor die Extra-Stimme, die bei JEDEM CalculateVotes aufs frische Ergebnis gelegt wird.
    private readonly Dictionary<byte, (byte Target, int Extra)> applied = new();

    // Zusätzliche Vote-Icons beim Auszählen (Mayor-ID je Extra-Stimme -> Ziel); in CalculateVotes gefüllt,
    // in RpcVotingComplete ins VoterState-Array gehängt, damit alle die Extra-Köpfe sehen.
    internal static readonly List<(byte Voter, byte Target)> ExtraIcons = new();

    private static GUIStyle labelStyle;

    protected override void Setup() => Instance = this;

    public override void ResetState() { saved.Clear(); shownSaved.Clear(); armed.Clear(); tallied.Clear(); applied.Clear(); slideTargets.Clear(); slideStart.Clear(); }

    public override void OnMeetingStart() { if (AmHost) armed.Clear(); tallied.Clear(); applied.Clear(); ExtraIcons.Clear(); }

    internal bool Tallied(byte id) => tallied.Contains(id);
    internal void MarkTallied(byte id) => tallied.Add(id);
    internal void SetApplied(byte id, byte target, int extra) => applied[id] = (target, extra);
    internal bool TryApplied(byte id, out byte target, out int extra)
    { if (applied.TryGetValue(id, out var a)) { target = a.Target; extra = a.Extra; return true; } target = 0; extra = 0; return false; }

    internal bool IsArmed(byte id) => armed.Contains(id);
    internal void ClearArmed(byte id) => armed.Remove(id);

    internal int GetSaved(byte id) => saved.TryGetValue(id, out var v) ? Mathf.Max(0, v) : 0;
    internal void SetSaved(byte id, int v)
    {
        saved[id] = Mathf.Max(0, v);
        shownSaved[id] = Mathf.Min(255, saved[id]);
        SendToAll(ActionSaved, id, (byte)Mathf.Min(255, saved[id]));
    }

    private int LocalTotal()
    {
        var local = PlayerControl.LocalPlayer;
        int s = local && shownSaved.TryGetValue(local.PlayerId, out var v) ? Mathf.Max(0, v) : 0;
        return s + 1; // immer mindestens 1 (die normale Stimme)
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionSaved && data.Length >= 2) { shownSaved[data[0]] = data[1]; return; }
        if (action == ActionArm && AmHost && data.Length >= 1 && RoleRegistry.Has(sender, this))
        {
            if (data[0] != 0) armed.Add(sender.PlayerId);
            else armed.Remove(sender.PlayerId);
        }
    }

    public override string GetTaskNote(PlayerControl local)
    {
        if (!RoleRegistry.Has(local, this)) return null;
        int s = shownSaved.TryGetValue(local.PlayerId, out var v) ? Mathf.Max(0, v) : 0;
        return Lang.T($"<color={ColorHex}>Aktuelle Stimmen: {s + 1}</color>", $"<color={ColorHex}>Current votes: {s + 1}</color>");
    }

    /// <summary>Im Meeting: Anzeige der aktuellen Stimmen (immer ≥ 1).</summary>
    public override void DrawGUI()
    {
        var local = PlayerControl.LocalPlayer;
        var mh = MeetingHud.Instance;
        if (!IsLocalRole || !mh || !IsAlive(local)) return;
        bool ready = mh.state == MeetingHud.MeetingStates.Discussion
                     || mh.state == MeetingHud.MeetingStates.NotVoted
                     || mh.state == MeetingHud.MeetingStates.Voted;
        if (!ready) return;

        labelStyle ??= new GUIStyle(Ui.Header) { alignment = TextAnchor.MiddleCenter, fontSize = 26 };
        var rect = new Rect(Ui.ScreenWidth / 2f - 320f, 902f, 640f, 40f);
        GUI.Label(rect, $"<color={ColorHex}>{Lang.T($"Aktuelle Stimmen: {LocalTotal()}", $"Current votes: {LocalTotal()}")}</color>", labelStyle);
    }

    // ---------- 3. Knopf, der beim Antippen der Karte neben ✓/X ausfährt (wie beim Guesser) ----------

    private readonly Dictionary<System.IntPtr, Vector3> slideTargets = new();
    private readonly Dictionary<System.IntPtr, float> slideStart = new();
    private const float SlideTime = 0.18f;

    public override void OnMeetingUpdate(MeetingHud meeting)
    {
        var local = PlayerControl.LocalPlayer;
        if (!IsLocalRole || !IsAlive(local)) return;

        bool voted = meeting.playerStates.Any(st => st && (byte)st.PlayerId == local.PlayerId && st.DidVote);
        bool canUse = !voted
                      && meeting.state != MeetingHud.MeetingStates.Results
                      && meeting.state != MeetingHud.MeetingStates.Proceeding;

        foreach (var state in meeting.playerStates)
        {
            if (!state) continue;
            var target = FindPlayer((byte)state.PlayerId);
            bool can = canUse && target && IsAlive(target) && target != local;

            if (!state.Buttons) continue;
            var slide = state.Buttons.transform.Find("Spieleabend_MayorBtn");
            if (!slide && can && state.CancelButton && state.ConfirmButton) slide = CreateSlideButton(state, (byte)state.PlayerId);
            if (slide)
            {
                slide.gameObject.SetActive(can);
                AnimateSlide(state, slide);
            }
        }
    }

    private Transform CreateSlideButton(PlayerVoteArea state, byte targetId)
    {
        var go = Object.Instantiate(state.CancelButton.gameObject, state.Buttons.transform);
        go.name = "Spieleabend_MayorBtn";

        // Zielposition: neben ✓/X, eine Knopfbreite weiter außen (links vom ✓).
        var confirm = state.Buttons.transform.InverseTransformPoint(state.ConfirmButton.transform.position);
        var cancel = state.Buttons.transform.InverseTransformPoint(state.CancelButton.transform.position);
        var target = confirm + (confirm - cancel);
        go.transform.localPosition = target;
        SetupVoteButton(go, targetId);
        slideTargets[go.transform.Pointer] = target;
        return go.transform;
    }

    private void AnimateSlide(PlayerVoteArea state, Transform slide)
    {
        if (!slideTargets.TryGetValue(slide.Pointer, out var target)) return;
        if (!state.Buttons.activeInHierarchy) { slideStart.Remove(state.Pointer); return; }
        if (!slideStart.ContainsKey(state.Pointer)) slideStart[state.Pointer] = Time.time;
        float t = Mathf.Clamp01((Time.time - slideStart[state.Pointer]) / SlideTime);
        var from = state.ConfirmButton ? state.Buttons.transform.InverseTransformPoint(state.ConfirmButton.transform.position) : target;
        slide.localPosition = Vector3.Lerp(from, target, 1f - (1f - t) * (1f - t));
    }

    private void SetupVoteButton(GameObject go, byte targetId)
    {
        go.SetActive(true);
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!sr.sprite) continue;
            var sprite = ButtonIcons.Get("ebutton", sr.sprite);
            if (sprite) sr.sprite = sprite;
        }
        var button = go.GetComponent<PassiveButton>();
        if (!button) return;
        button.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        button.OnClick.AddListener((System.Action)(() => CastPowerVote(targetId)));
    }

    /// <summary>Volle Stimmkraft (1 + gesparte) auf das Ziel setzen: normal abstimmen + Einsatz scharf machen.</summary>
    private void CastPowerVote(byte targetId)
    {
        var meeting = MeetingHud.Instance;
        var local = PlayerControl.LocalPlayer;
        if (!meeting || !IsAlive(local)) return;
        if (meeting.playerStates.Any(st => st && (byte)st.PlayerId == local.PlayerId && st.DidVote)) return; // schon gewählt

        SendToHost(ActionArm, 1);                 // Host: gesparte Stimmen dieses Mal anwenden
        try { meeting.CastVote(local.PlayerId, targetId); } catch { } // normale Stimme setzen
        Sfx.Play(Sfx.Investigate, 0.7f);
    }
}

/// <summary>
/// Beim Auszählen (Host): Mayor, der seinen Einsatz ausgelöst und einen echten Spieler gewählt hat, legt seine
/// gesparten Stimmen oben drauf (Vorrat -> 0). Wer nicht abstimmt (Skip), bekommt eine gesparte Stimme dazu.
/// </summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CalculateVotes))]
internal static class MayorVotePatch
{
    public static void Postfix(MeetingHud __instance, Il2CppSystem.Collections.Generic.Dictionary<byte, int> __result)
    {
        var mayor = Mayor.Instance;
        if (mayor == null || __result == null || !RoleRegistry.HasAssignments) return;

        // NICHT hier leeren: CalculateVotes wird mehrfach aufgerufen, sonst würden die gesammelten Icons wieder
        // verschwinden, bevor RpcVotingComplete sie nutzt. Geleert wird bei Meeting-Start und nach dem Verschicken.
        foreach (var state in __instance.playerStates)
        {
            if (!state) continue;
            var voter = CustomRole.FindPlayer((byte)state.PlayerId);
            if (!CustomRole.IsAlive(voter) || !RoleRegistry.Has(voter, mayor)) continue;

            byte id = (byte)state.PlayerId;
            byte votedFor = (byte)state.VotedForId;
            bool voted = state.DidVote && CustomRole.FindPlayer(votedFor) != null; // echter Spieler (Skip = Pseudo-ID 253)

            // Entscheidung nur EINMAL pro Auszählen treffen (CalculateVotes läuft mehrfach).
            if (!mayor.Tallied(id))
            {
                mayor.MarkTallied(id);
                int s = mayor.GetSaved(id);
                if (voted && mayor.IsArmed(id) && s > 0)
                {
                    mayor.SetApplied(id, votedFor, s);                 // wird unten bei jedem Aufruf angewandt
                    for (int k = 0; k < s; k++) Mayor.ExtraIcons.Add((id, votedFor));
                    mayor.SetSaved(id, 0);
                }
                else if (!voted) mayor.SetSaved(id, s + 1);
                mayor.ClearArmed(id);
            }

            // Jeden Aufruf auf das (jeweils frische) Ergebnis legen - sonst zählt der Einsatz beim Exil nicht.
            if (mayor.TryApplied(id, out var tgt, out var extra) && __result.ContainsKey(tgt))
                __result[tgt] = __result[tgt] + extra;
        }
    }
}

/// <summary>
/// Die gesammelten Extra-Stimmen des Mayors als zusätzliche VoterStates anhängen - so werden sie als eigene
/// Vote-Köpfe gezeigt. RpcVotingComplete deckt online ab (Host hängt vor dem Senden an), VotingComplete deckt
/// Freeplay/Clients ab (wird direkt aufgerufen).
/// </summary>
internal static class MayorVoterStateExpand
{
    public static void Expand(ref Il2CppStructArray<MeetingHud.VoterState> states)
    {
        if (Mayor.ExtraIcons.Count == 0 || states == null) return;
        int oldN = states.Length;
        var expanded = new Il2CppStructArray<MeetingHud.VoterState>(oldN + Mayor.ExtraIcons.Count);
        for (int i = 0; i < oldN; i++) expanded[i] = states[i];
        for (int i = 0; i < Mayor.ExtraIcons.Count; i++)
            expanded[oldN + i] = new MeetingHud.VoterState { VoterId = Mayor.ExtraIcons[i].Voter, VotedForId = Mayor.ExtraIcons[i].Target };
        states = expanded;
        Mayor.ExtraIcons.Clear();
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.RpcVotingComplete))]
internal static class MayorRpcVotingPatch
{
    public static void Prefix(ref Il2CppStructArray<MeetingHud.VoterState> states) => MayorVoterStateExpand.Expand(ref states);
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.VotingComplete))]
internal static class MayorVotingCompletePatch
{
    public static void Prefix(ref Il2CppStructArray<MeetingHud.VoterState> states) => MayorVoterStateExpand.Expand(ref states);
}
