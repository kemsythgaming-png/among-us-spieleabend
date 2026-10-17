using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Schutz-Animationen: pulsierende Blase um geschuetzte Spieler und ein Aufprall-Ring, wenn ein Angriff abgewehrt wird.
/// Rein lokal - jeder Client zeigt nur, was die Rolle dem lokalen Spieler erlaubt (CustomRole.GetProtectionBubbles).
/// Grafiken werden per Code erzeugt.
/// </summary>
public static class ProtectionVisuals
{
    private const string Prefix = "Spieleabend_Protection_";

    private sealed class Bubble
    {
        public GameObject Obj;
        public SpriteRenderer Renderer;
        public byte Target;
        public float Phase;
    }

    private sealed class Burst
    {
        public GameObject Obj;
        public SpriteRenderer Renderer;
        public float Start;
        public Color Color;
    }

    private static readonly Dictionary<string, Bubble> bubbles = new();
    private static readonly List<Burst> bursts = new();
    private static Sprite ringSprite;

    private const float BurstDuration = 0.7f;

    /// <summary>Aufprall-Ring an einem Spieler (z. B. Medic-Schild bricht). Nur lokal - selbst entscheiden, wer ihn sieht.</summary>
    public static void ShowBurst(PlayerControl target, Color color)
    {
        if (!target) return;
        EnsureSprite();
        var go = new GameObject(Prefix + "Burst");
        go.transform.SetParent(target.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ringSprite;
        sr.color = color;
        bursts.Add(new Burst { Obj = go, Renderer = sr, Start = Time.time, Color = color });
    }

    public static void Reset()
    {
        foreach (var b in bubbles.Values)
            if (b.Obj) Object.Destroy(b.Obj);
        bubbles.Clear();
        foreach (var b in bursts)
            if (b.Obj) Object.Destroy(b.Obj);
        bursts.Clear();
    }

    internal static void Update()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !ShipStatus.Instance)
        {
            Reset();
            return;
        }

        // 1. Welche Blasen soll der lokale Spieler gerade sehen?
        // Waehrend der Tarnung (Camouflager) KEINE Schutz-Blasen zeigen - sonst sieht man, wer geschuetzt ist.
        bool showBubbles = !CustomRole.InMeeting && !Camouflage.Active;
        var wanted = new Dictionary<string, (PlayerControl Target, Color Color)>();
        if (showBubbles)
            foreach (var (target, color) in FirstDeathShield.Bubbles()) wanted[$"first_{target.PlayerId}"] = (target, color);
        if (RoleRegistry.HasAssignments && showBubbles)
        {
            foreach (var role in RoleRegistry.Roles)
            {
                foreach (var (target, color) in role.GetProtectionBubbles(local))
                {
                    if (CustomRole.IsAlive(target)) wanted[$"{role.Id}_{target.PlayerId}"] = (target, color);
                }
            }
        }

        // 2. Ueberfluessige entfernen, fehlende anlegen
        foreach (var key in bubbles.Keys.Where(k => !wanted.ContainsKey(k)).ToList())
        {
            if (bubbles[key].Obj) Object.Destroy(bubbles[key].Obj);
            bubbles.Remove(key);
        }
        foreach (var (key, (target, color)) in wanted)
        {
            if (bubbles.TryGetValue(key, out var existing) && existing.Obj) continue;
            bubbles[key] = CreateBubble(target, color);
        }

        // 3. Animieren
        float t = Time.time;
        foreach (var b in bubbles.Values)
        {
            if (!b.Obj) continue;
            var target = CustomRole.FindPlayer(b.Target);
            bool visible = target && target.cosmetics && target.cosmetics.nameText.gameObject.activeInHierarchy && !target.inVent;
            b.Renderer.enabled = visible;
            float pulse = 1f + 0.06f * Mathf.Sin(t * 3f + b.Phase);
            b.Obj.transform.localScale = new Vector3(pulse, pulse, 1f);
            var c = b.Renderer.color;
            b.Renderer.color = new Color(c.r, c.g, c.b, 0.55f + 0.2f * Mathf.Sin(t * 2.2f + b.Phase));
        }

        for (int i = bursts.Count - 1; i >= 0; i--)
        {
            var b = bursts[i];
            float p = (t - b.Start) / BurstDuration;
            if (!b.Obj || p >= 1f)
            {
                if (b.Obj) Object.Destroy(b.Obj);
                bursts.RemoveAt(i);
                continue;
            }
            float scale = 1f + p * 1.6f;
            b.Obj.transform.localScale = new Vector3(scale, scale, 1f);
            b.Renderer.color = new Color(b.Color.r, b.Color.g, b.Color.b, 1f - p);
        }
    }

    private static Bubble CreateBubble(PlayerControl target, Color color)
    {
        EnsureSprite();
        var go = new GameObject(Prefix + "Bubble");
        go.transform.SetParent(target.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ringSprite;
        sr.color = new Color(color.r, color.g, color.b, 0.6f);
        return new Bubble { Obj = go, Renderer = sr, Target = target.PlayerId, Phase = target.PlayerId * 0.9f };
    }

    private static void EnsureSprite()
    {
        if (ringSprite) return;
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
            // heller Ring nahe am Rand + zarte Fuellung + Glanzpunkt oben links
            float ring = Mathf.Exp(-Mathf.Pow((d - 0.86f) / 0.06f, 2f));
            float fill = d < 0.86f ? 0.12f + 0.1f * d : 0f;
            float shine = Mathf.Exp(-(Mathf.Pow((x - 40f) / 10f, 2f) + Mathf.Pow((y - 88f) / 7f, 2f))) * 0.6f;
            float a = Mathf.Clamp01(ring + fill + shine);
            if (d > 1f) a = 0f;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 110f);
        ringSprite.hideFlags = HideFlags.HideAndDontSave;
    }
}
