using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Spieleabend.Core;

/// <summary>
/// Draft-Spielmodus: Zu Rundenbeginn wählt jeder Spieler aus 3 zufälligen Rollen seines Teams eine aus. Die anderen
/// sehen links nur, WELCHES Team (Crew/Impostor/Neutral) jemand gewählt hat - nicht die konkrete Rolle.
/// Wie oft welches Team vorkommt, stellt der Host vorher ein (Impostor-/Neutral-Anzahl, Rest = Crew). Es werden nur
/// Rollen vorgeschlagen, die auf "getestet" stehen.
///
/// Ablauf: Das normale Intro läuft zuerst (zeigt nur Crewmate/Impostor, weil die Mod-Rollen noch nicht verteilt sind).
/// Sobald die Runde läuft, startet der Host den Draft; eine Vollbild-Auswahl legt sich über den HUD, die Bewegung ist
/// so lange gesperrt. Nach der Wahl (oder Ablauf des Countdowns) verteilt der Host die gewählten Rollen ganz normal.
/// </summary>
internal static class DraftMode
{
    public static bool Active { get; private set; }

    private static float endTime;
    private static bool started;            // Host: in dieser Runde schon gestartet?
    private static bool finalizing;

    private sealed class Entry
    {
        public RoleTeam Team;
        public readonly byte[] Opts = { 255, 255, 255 };
        public byte Picked = 255;
    }

    private static readonly Dictionary<byte, Entry> entries = new(); // nur Spieler, die wählen dürfen
    private static readonly HashSet<byte> locked = new();            // wer hat schon gewählt (für die Liste links)
    private static byte localChoice = 255;

    /// <summary>Ist der Draft-Modus in den Einstellungen an?</summary>
    public static bool On => GeneralOptions.DraftMode != null && GeneralOptions.DraftMode.Int == 1;

    public static void Reset()
    {
        if (Active) Unfreeze();
        Active = false;
        started = false;
        finalizing = false;
        endTime = 0f;
        entries.Clear();
        locked.Clear();
        localChoice = 255;
    }

    // ---------------- Host: Steuerung jeden Frame ----------------

    /// <summary>Host, jeden Frame: Draft starten (nach dem Intro) und am Ende die Rollen verteilen.</summary>
    public static void HostTick()
    {
        if (!CustomRole.AmHost) return;

        if (!started)
        {
            if (FreeplayTest.IsFreeplay) return;        // im Freeplay nur per Testknopf (StartFreeplay) - kein Auto-Start
            if (!On) return;
            // Erst wenn die Runde wirklich läuft und das Intro vorbei ist.
            if (!ShipStatus.Instance || IntroCutscene.Instance || CustomRole.InMeeting) return;
            if (RoleRegistry.HasAssignments) { started = true; return; } // Sicherheitsnetz
            StartAsHost();
            return;
        }

        // Ab hier auch im Freeplay: laufenden Draft fertig abrechnen.
        if (!Active || finalizing) return;

        bool allPicked = entries.Keys.All(locked.Contains);
        if (allPicked || Time.time >= endTime) FinalizeDraft();
    }

    /// <summary>Freeplay-Test: Draft manuell starten. Du wählst selbst; die Dummys bekommen nach Ablauf zufällig eine ihrer 3 Rollen.</summary>
    public static void StartFreeplay()
    {
        if (!FreeplayTest.IsFreeplay || !ShipStatus.Instance || Active) return;
        started = false;
        finalizing = false;
        StartAsHost();
    }

    private static void StartAsHost()
    {
        started = true;
        entries.Clear();
        locked.Clear();

        var alive = PlayerControl.AllPlayerControls.ToArray().Where(CustomRole.IsAlive).ToList();
        var impostors = alive.Where(p => p.Data != null && p.Data.Role && p.Data.Role.Role == AmongUs.GameOptions.RoleTypes.Impostor).ToList();
        var crew = alive.Where(p => !impostors.Contains(p)).ToList();

        // Impostoren, die eine Mod-Impostor-Rolle draften (Rest bleibt normaler Impostor).
        int impCount = Mathf.Min(GeneralOptions.DraftImpostors.Int, impostors.Count);
        var impDraft = impostors.OrderBy(_ => Random.value).Take(impCount).ToList();

        // Von den Crewmates werden ein paar zu Neutralen, der Rest bleibt Crew.
        crew = crew.OrderBy(_ => Random.value).ToList();
        int neutralCount = Mathf.Min(GeneralOptions.DraftNeutrals.Int, crew.Count);
        var neutralDraft = crew.Take(neutralCount).ToList();
        var crewDraft = crew.Skip(neutralCount).ToList();

        foreach (var p in impDraft) AddEntry(p, RoleTeam.Impostor);
        foreach (var p in neutralDraft) AddEntry(p, RoleTeam.Neutral);
        foreach (var p in crewDraft) AddEntry(p, RoleTeam.Crewmate);

        if (entries.Count == 0) return; // keine getesteten Rollen -> nichts zu draften, normales Spiel

        var blob = Encode();
        RoleRpc.SendDraftStart(blob);
        ApplyStart(blob); // beim Host selbst auch anwenden
    }

    /// <summary>Einem Spieler ein Team + 3 zufällige getestete Rollen dieses Teams geben (nur wenn es welche gibt).</summary>
    private static void AddEntry(PlayerControl p, RoleTeam team)
    {
        var pool = RoleRegistry.Roles
            .Where(r => !r.IsExtra && r.Team == team && TestStatus.IsTested(r))
            .OrderBy(_ => Random.value).Take(3).ToList();
        if (pool.Count == 0) return; // kein Vorschlag -> Spieler bleibt normale Vanilla-Rolle

        var e = new Entry { Team = team };
        for (int i = 0; i < pool.Count; i++) e.Opts[i] = pool[i].Id;
        entries[p.PlayerId] = e;
    }

    private static byte[] Encode()
    {
        var list = new List<byte> { (byte)entries.Count };
        foreach (var kv in entries)
        {
            list.Add(kv.Key);
            list.Add((byte)kv.Value.Team);
            list.Add(kv.Value.Opts[0]);
            list.Add(kv.Value.Opts[1]);
            list.Add(kv.Value.Opts[2]);
        }
        return list.ToArray();
    }

    // ---------------- Alle Clients: Draft empfangen ----------------

    public static void ApplyStart(byte[] data)
    {
        entries.Clear();
        locked.Clear();
        localChoice = 255;
        if (data == null || data.Length < 1) return;

        int n = data[0];
        int i = 1;
        for (int k = 0; k < n && i + 4 < data.Length; k++, i += 5)
        {
            var e = new Entry { Team = (RoleTeam)data[i + 1] };
            e.Opts[0] = data[i + 2];
            e.Opts[1] = data[i + 3];
            e.Opts[2] = data[i + 4];
            entries[data[i]] = e;
        }

        endTime = Time.time + Mathf.Max(5, GeneralOptions.DraftTime.Int);
        Active = entries.Count > 0;
        finalizing = false;
    }

    /// <summary>Host: eine Wahl eines Spielers entgegennehmen (gültig nur, wenn sie zu seinen 3 Vorschlägen passt).</summary>
    public static void HostReceivePick(byte playerId, byte roleId)
    {
        if (!CustomRole.AmHost) return;
        if (!entries.TryGetValue(playerId, out var e)) return;
        if (System.Array.IndexOf(e.Opts, roleId) < 0) return;
        e.Picked = roleId;
        BroadcastLock(playerId);
    }

    private static void BroadcastLock(byte playerId)
    {
        ApplyLock(playerId);
        RoleRpc.SendDraftLock(playerId);
    }

    public static void ApplyLock(byte playerId) => locked.Add(playerId);

    /// <summary>Clients: wird aufgerufen, sobald die endgültigen Rollen ankommen -> Draft-Fenster schließen.</summary>
    public static void Stop()
    {
        if (!Active && !started) return;
        if (Active) Unfreeze();
        Active = false;
        finalizing = true;
    }

    // ---------------- Host: Rollen verteilen ----------------

    private static void FinalizeDraft()
    {
        finalizing = true;

        var pairs = new List<(byte, byte)>();
        foreach (var kv in entries)
        {
            byte role = kv.Value.Picked;
            if (role == 255) // nicht rechtzeitig gewählt -> zufällig einen der 3 Vorschläge
            {
                var opts = kv.Value.Opts.Where(o => o != 255).ToArray();
                if (opts.Length > 0) role = opts[Random.Range(0, opts.Length)];
            }
            if (role != 255) pairs.Add((kv.Key, role));
        }

        RoleRpc.SendRoles(pairs);
        RoleRegistry.SetAssignments(pairs);

        // Neutrale Rollen bekommen keine Aufgaben (im normalen Spiel macht das IntroBeginPatch).
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!p || p.Data == null) continue;
            var r = RoleRegistry.GetRole(p);
            if (r != null && r.Team == RoleTeam.Neutral && !r.KeepsTasks)
                try { p.Data.RpcSetTasks(new byte[0]); } catch { }
        }

        Stop();
    }

    // ---------------- Jeder Client: Bewegung sperren ----------------

    /// <summary>Jeden Frame (aus HudManager.Update): während des Drafts die Bewegung des lokalen Spielers sperren.</summary>
    public static void Update()
    {
        if (!Active) return;
        var lp = PlayerControl.LocalPlayer;
        if (lp) lp.moveable = false;
    }

    private static void Unfreeze()
    {
        var lp = PlayerControl.LocalPlayer;
        if (lp) lp.moveable = true;
    }

    // ---------------- Auswahl-Fenster (IMGUI) ----------------

    public static void Draw()
    {
        if (!Active) return;
        var local = PlayerControl.LocalPlayer;
        if (!local) return;

        float sw = Ui.ScreenWidth;
        // Vollbild abdunkeln + Klicks sperren
        GUI.Box(new Rect(0, 0, sw, 1080), new GUIContent(""), Ui.Panel);
        Ui.Block(new Rect(0, 0, sw, 1080));

        int remaining = Mathf.CeilToInt(Mathf.Max(0f, endTime - Time.time));

        // Kopfzeile
        bool hasEntry = entries.TryGetValue(local.PlayerId, out var mine);
        string head = hasEntry
            ? Lang.T("Wähle deine Rolle", "Pick your role")
            : Lang.T("Die anderen wählen ihre Rolle …", "The others are picking …");
        GUI.Label(new Rect(0, 70, sw, 60), $"<b>{head}</b>", Center(40));
        GUI.Label(new Rect(0, 135, sw, 40), Lang.T($"Start in {remaining}s", $"Start in {remaining}s"), Center(26));

        DrawPlayerList(local);

        if (hasEntry) DrawCards(mine);
        else GUI.Label(new Rect(0, 540, sw, 40), Lang.T("Du spielst deine normale Rolle.", "You keep your normal role."), Center(26));
    }

    /// <summary>Linke Spalte: alle Spieler mit ihrem Team (nicht die konkrete Rolle) + Haken, wenn gewählt.</summary>
    private static void DrawPlayerList(PlayerControl local)
    {
        float x = 60f, y = 230f, w = 360f, row = 46f;
        var box = new Rect(x - 16, y - 16, w + 32, 1080 - y - 40);
        GUI.Box(box, new GUIContent(""), Ui.Inner);
        GUI.Label(new Rect(x, y, w, 34), $"<b>{Lang.T("Spieler", "Players")}</b>", Ui.Header);
        y += 48f;

        foreach (var p in PlayerControl.AllPlayerControls.ToArray().Where(CustomRole.IsAlive))
        {
            bool has = entries.TryGetValue(p.PlayerId, out var e);
            RoleTeam team = has ? e.Team : (CustomRole.IsImpostor(p) ? RoleTeam.Impostor : RoleTeam.Crewmate);
            bool ready = !has || locked.Contains(p.PlayerId); // ohne Wahlrecht = immer bereit
            string tcol = "#" + ColorUtility.ToHtmlStringRGB(TeamColor(team));
            string name = p.Data?.PlayerName ?? ("#" + p.PlayerId);
            string mark = ready ? " <color=#5CEF55>✔</color>" : "";
            string me = p == local ? " <size=70%>(Du)</size>" : "";
            GUI.Label(new Rect(x, y, w, row), $"<color={tcol}>●</color> {name}{me}  <size=75%><color={tcol}>{TeamName(team)}</color></size>{mark}", Ui.Text);
            y += row;
        }
    }

    private static void DrawCards(Entry mine)
    {
        float sw = Ui.ScreenWidth;
        var opts = mine.Opts.Where(o => o != 255).Select(RoleRegistry.Get).Where(r => r != null).ToList();
        if (opts.Count == 0) return;

        float cardW = 360f, gap = 40f, cardH = 470f;
        float totalW = opts.Count * cardW + (opts.Count - 1) * gap;
        float startX = (sw + 300f) / 2f - totalW / 2f; // etwas nach rechts (Platz für die Spielerliste)
        if (startX < 480f) startX = 480f;
        float y = 220f;

        bool chosen = localChoice != 255;

        for (int i = 0; i < opts.Count; i++)
        {
            var role = opts[i];
            var rect = new Rect(startX + i * (cardW + gap), y, cardW, cardH);
            bool isMine = localChoice == role.Id;
            GUI.Box(rect, new GUIContent(""), isMine ? Ui.Selected : Ui.Inner);
            Ui.Block(rect);

            string nc = role.NameHex;
            GUI.Label(new Rect(rect.x + 18, rect.y + 20, rect.width - 36, 50), $"<b><color={nc}>{role.Name}</color></b>", Center(30));
            GUI.Label(new Rect(rect.x + 18, rect.y + 74, rect.width - 36, 34), $"<i>{role.IntroText}</i>", Center(20));
            GUI.Label(new Rect(rect.x + 24, rect.y + 120, rect.width - 48, cardH - 210), role.LongDescription, Ui.Small);

            var btn = new Rect(rect.x + 40, rect.yMax - 72, rect.width - 80, 52);
            if (Ui.Button(btn, isMine ? Lang.T("Gewählt", "Picked") : Lang.T("Wählen", "Pick"), isMine))
                Choose(role.Id);
            Ui.Block(btn);
        }

        if (chosen)
            GUI.Label(new Rect(startX, y + cardH + 16, totalW, 40),
                Lang.T("Gewählt – warte auf die anderen …", "Picked – waiting for the others …"), Center(24));
    }

    private static void Choose(byte roleId)
    {
        localChoice = roleId;
        locked.Add(PlayerControl.LocalPlayer.PlayerId); // sofortiges Feedback
        RoleRpc.SendDraftPick(roleId);
    }

    private static Color TeamColor(RoleTeam team) => team switch
    {
        RoleTeam.Impostor => CustomRole.ImpostorRed,
        RoleTeam.Neutral => new Color(0.85f, 0.65f, 1f),
        _ => CustomRole.CrewBlue,
    };

    private static string TeamName(RoleTeam team) => team switch
    {
        RoleTeam.Impostor => Lang.T("Impostor", "Impostor"),
        RoleTeam.Neutral => Lang.T("Neutral", "Neutral"),
        _ => Lang.T("Crew", "Crew"),
    };

    private static GUIStyle centerCache;
    private static GUIStyle Center(int size)
    {
        centerCache = new GUIStyle(Ui.Text) { fontSize = size, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        return centerCache;
    }
}
