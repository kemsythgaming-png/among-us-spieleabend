using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Modifiers;

/// <summary>
/// Anpassung: Der Traeger leuchtet in festen Abstaenden kurz auf. Wer in der Naehe ist, sieht das Leuchten -
/// auch durch duenne Waende (das Leucht-Symbol wird ueber der Dunkelheit gezeichnet). Der Takt wird vom Host
/// per RPC gesetzt, damit alle gleichzeitig leuchten sehen.
/// </summary>
public sealed class Firefly : CustomRole
{
    public override string ConfigSection => "Leuchtkaefer";
    public override string Name => Lang.T("Leuchtkäfer", "Firefly");
    public override Color Color => new Color32(180, 255, 90, 255);
    public override RoleTeam Team => RoleTeam.Modifier;
    public override string IntroText => Lang.T("Du leuchtest auf", "You light up");
    public override string Description => Lang.T("Alle paar Sekunden leuchtest du\nkurz auf - die Nähe sieht dich.", "Every few seconds you glow\nbriefly - nearby players see you.");
    public override string LongDescription => Lang.T(
        "Anpassung: Alle 20 Sekunden leuchtest du für 3 Sekunden auf. Jeder in deiner Nähe sieht das Leuchten - auch durch dünne Wände. So bist du regelmäßig kurz verräterisch sichtbar. Deine eigentliche Rolle bleibt.",
        "Modifier: every 20 seconds you glow for 3 seconds. Everyone near you sees the glow - even through thin walls. So you are briefly visible on a regular beat. Your actual role stays.");

    private const byte ActionGlow = 0;

    private RoleOption interval, duration, radius;

    // Host: wann leuchtet dieser Traeger das naechste Mal auf?
    private readonly Dictionary<byte, float> nextGlowAt = new();
    // alle Clients: bis wann leuchtet dieser Spieler gerade?
    private readonly Dictionary<byte, float> glowUntil = new();

    // lokale Grafik je leuchtendem Spieler
    private sealed class Glow { public GameObject Obj; public SpriteRenderer Renderer; }
    private readonly Dictionary<byte, Glow> glows = new();
    private static Sprite glowSprite;

    protected override void Setup()
    {
        interval = AddOption("Interval", "Abstand|Interval", 20f, 5f, 60f, 5f, "s");
        duration = AddOption("Duration", "Leuchtdauer|Glow time", 3f, 1f, 8f, 0.5f, "s");
        radius = AddOption("Radius", "Sichtweite|Range", 4f, 1f, 12f, 0.5f, "m");
    }

    public override void ResetState()
    {
        nextGlowAt.Clear();
        glowUntil.Clear();
        foreach (var g in glows.Values) if (g.Obj) Object.Destroy(g.Obj);
        glows.Clear();
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        // nur der Host loest das Leuchten aus
        if (action != ActionGlow || data.Length < 1 || !sender) return;
        if (AmongUsClient.Instance && sender.OwnerId != AmongUsClient.Instance.HostId) return;
        glowUntil[data[0]] = Time.time + duration.Value;
    }

    public override void Update()
    {
        // Host: Takt setzen und Leuchten ausloesen
        if (AmHost)
        {
            foreach (var p in Players)
            {
                if (!IsAlive(p)) { nextGlowAt.Remove(p.PlayerId); continue; }
                if (!nextGlowAt.TryGetValue(p.PlayerId, out float next))
                {
                    nextGlowAt[p.PlayerId] = Time.time + interval.Value; // erstes Leuchten nach einem Abstand
                    continue;
                }
                if (Time.time >= next)
                {
                    SendToAll(ActionGlow, p.PlayerId);
                    nextGlowAt[p.PlayerId] = Time.time + interval.Value;
                }
            }
        }

        UpdateVisuals();
    }

    /// <summary>Alle leuchtenden Spieler in der Naehe des lokalen Spielers anzeigen (ueber der Dunkelheit).</summary>
    private void UpdateVisuals()
    {
        var local = PlayerControl.LocalPlayer;
        float now = Time.time;

        // Welche sollen gerade leuchten?
        var wanted = new HashSet<byte>();
        if (local && ShipStatus.Instance && !InMeeting)
        {
            Vector2 me = local.GetTruePosition();
            float r = radius.Value;
            foreach (var pair in glowUntil)
            {
                if (pair.Value <= now) continue;
                var p = FindPlayer(pair.Key);
                if (!IsAlive(p) || p.inVent) continue;
                if (((Vector2)p.GetTruePosition() - me).sqrMagnitude <= r * r) wanted.Add(pair.Key);
            }
        }

        // Ueberfluessige entfernen
        foreach (var id in glows.Keys.Where(k => !wanted.Contains(k)).ToList())
        {
            if (glows[id].Obj) Object.Destroy(glows[id].Obj);
            glows.Remove(id);
        }

        // Fehlende anlegen, alle animieren
        foreach (var id in wanted)
        {
            if (!glows.TryGetValue(id, out var g) || !g.Obj)
            {
                var p = FindPlayer(id);
                if (!p) continue;
                g = CreateGlow(p);
                if (g == null) continue;
                glows[id] = g;
            }
            float pulse = 0.75f + 0.25f * Mathf.Sin(now * 6f + id);
            g.Renderer.color = new Color(Color.r, Color.g, Color.b, pulse);
            float s = 0.9f + 0.1f * Mathf.Sin(now * 6f + id);
            g.Obj.transform.localScale = new Vector3(s, s, 1f);
        }
    }

    private static Glow CreateGlow(PlayerControl target)
    {
        var hud = HudManager.Instance;
        var go = new GameObject("Spieleabend_FireflyGlow");
        go.transform.SetParent(target.transform, false);
        go.transform.localPosition = new Vector3(0f, 0.1f, -0.02f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GlowSprite();
        // Ueber der Dunkelheit (ShadowQuad) zeichnen, damit man es auch durch duenne Waende sieht
        if (hud && hud.ShadowQuad)
        {
            sr.sortingLayerID = hud.ShadowQuad.sortingLayerID;
            sr.sortingOrder = hud.ShadowQuad.sortingOrder + 1;
        }
        return new Glow { Obj = go, Renderer = sr };
    }

    private static Sprite GlowSprite()
    {
        if (glowSprite) return glowSprite;
        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f)) / 64f; // 0 = Mitte, 1 = Rand
            // weicher, heller Kern, der nach aussen ausblendet
            float a = Mathf.Clamp01(Mathf.Exp(-Mathf.Pow(d / 0.42f, 2f)));
            if (d > 1f) a = 0f;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 85f);
        glowSprite.hideFlags = HideFlags.HideAndDontSave;
        return glowSprite;
    }
}
