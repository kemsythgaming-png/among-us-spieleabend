using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Core;

/// <summary>
/// Emotes fuer ALLE Spieler: Taste T oeffnet ein kleines Rad, ein Klick zeigt kurz eine Sprechblase ueber der eigenen
/// Figur - fuer alle in der Naehe sichtbar (per RPC synchron). Keine Rolle noetig.
/// </summary>
internal static class Emotes
{
    private const float Duration = 2.5f;

    private readonly struct Emote
    {
        public readonly string Text; public readonly Color Color;
        public Emote(string t, Color c) { Text = t; Color = c; }
    }

    private static readonly Emote[] List =
    {
        new("<3", new Color(1f, 0.35f, 0.5f)),      // Liebe
        new("!",  new Color(1f, 0.85f, 0.2f)),      // Achtung
        new("?",  new Color(0.4f, 0.85f, 1f)),      // Verwirrt
        new(":)", new Color(0.4f, 1f, 0.5f)),       // Froh
        new(":(", new Color(0.5f, 0.7f, 1f)),       // Traurig
        new(">:(", new Color(1f, 0.5f, 0.2f)),      // Wütend
        new("Zzz", new Color(0.75f, 0.75f, 0.8f)),  // Müde
        new("GG", new Color(0.8f, 0.5f, 1f)),       // Good game
    };

    private sealed class Shown { public GameObject Obj; public float Until; }
    private static readonly Dictionary<byte, Shown> shown = new();
    private static bool wheelOpen;

    public static void Reset()
    {
        foreach (var s in shown.Values) if (s.Obj) Object.Destroy(s.Obj);
        shown.Clear();
        wheelOpen = false;
    }

    private static bool CanEmote()
    {
        var local = PlayerControl.LocalPlayer;
        return ShipStatus.Instance && local && CustomRole.IsAlive(local) && !CustomRole.InMeeting;
    }

    public static void ToggleWheel()
    {
        if (!CanEmote()) { wheelOpen = false; return; }
        wheelOpen = !wheelOpen;
    }

    /// <summary>Eigenes Emote senden (lokal zeigen + an alle).</summary>
    public static void Send(int idx)
    {
        wheelOpen = false;
        var local = PlayerControl.LocalPlayer;
        if (!local || idx < 0 || idx >= List.Length) return;
        Apply(local.PlayerId, (byte)idx);
        RoleRpc.SendEmote(local.PlayerId, (byte)idx);
    }

    /// <summary>Emote eines Spielers anzeigen (lokal und per RPC).</summary>
    public static void Apply(byte playerId, int idx)
    {
        if (idx < 0 || idx >= List.Length) return;
        var player = CustomRole.FindPlayer(playerId);
        if (!player) return;

        if (shown.TryGetValue(playerId, out var old) && old.Obj) Object.Destroy(old.Obj);

        var go = new GameObject("Spieleabend_Emote");
        go.transform.SetParent(player.transform, false);
        go.transform.localPosition = new Vector3(0f, 0.8f, -3f);
        go.transform.localScale = Vector3.one;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = List[idx].Text;
        tmp.color = List[idx].Color;
        tmp.fontSize = 2.6f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        var src = player.cosmetics ? player.cosmetics.nameText : null;
        if (src && src.font) tmp.font = src.font;
        tmp.outlineColor = new Color32(0, 0, 0, 200);
        tmp.outlineWidth = 0.2f;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr) mr.sortingOrder = 100;

        shown[playerId] = new Shown { Obj = go, Until = Time.time + Duration };
    }

    /// <summary>Jedes Frame (HudUpdate): abgelaufene Blasen wegraeumen und leicht schweben lassen.</summary>
    public static void Update()
    {
        if (shown.Count == 0) return;
        float now = Time.time;
        foreach (var kv in shown.ToList())
        {
            var s = kv.Value;
            if (!s.Obj || now >= s.Until) { if (s.Obj) Object.Destroy(s.Obj); shown.Remove(kv.Key); continue; }
            s.Obj.transform.localPosition = new Vector3(0f, 0.8f + 0.05f * Mathf.Sin(now * 4f), -3f);
        }
    }

    // ---------- Rad (IMGUI) ----------
    public static void Draw()
    {
        if (!wheelOpen) return;
        if (!CanEmote()) { wheelOpen = false; return; }

        float pw = 580f, ph = 240f;
        var panel = new Rect((Ui.ScreenWidth - pw) / 2f, 740f, pw, ph);
        Ui.Block(panel);
        GUI.Box(panel, new GUIContent(""), Ui.Panel);
        GUI.Label(new Rect(panel.x + 20f, panel.y + 12f, pw - 40f, 34f), Lang.T("Emote wählen  (T schließt)", "Pick an emote  (T closes)"), Ui.Text);

        const int cols = 4;
        float bw = (pw - 20f - (cols - 1) * 10f - 20f) / cols;
        float bh = 76f;
        for (int i = 0; i < List.Length; i++)
        {
            int c = i % cols, r = i / cols;
            var rect = new Rect(panel.x + 20f + c * (bw + 10f), panel.y + 56f + r * (bh + 10f), bw, bh);
            var prev = GUI.contentColor;
            GUI.contentColor = List[i].Color;
            if (Ui.Button(rect, $"<size=130%><b>{List[i].Text}</b></size>")) Send(i);
            GUI.contentColor = prev;
        }
    }
}
