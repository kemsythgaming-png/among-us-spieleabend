using System.Collections.Generic;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Feuer-Animation (z. B. Arsonist): mehrere flackernde Flammen steigen an einem Spieler auf.
/// Rein optisch und lokal; jeder Client spielt sie ab, wenn die Rolle es per Nachricht an alle meldet.
/// Grafik per Code erzeugt.
/// </summary>
public static class FireEffect
{
    private sealed class Fire
    {
        public GameObject Root;
        public List<(Transform Flame, SpriteRenderer Renderer, float Phase, float X)> Flames;
        public float Start;
        public float Duration;
    }

    private static readonly List<Fire> fires = new();
    private static Sprite flameSprite;

    /// <summary>Spielt Feuer an einem Spieler fuer duration Sekunden ab.</summary>
    public static void Play(PlayerControl target, float duration)
    {
        if (!target) return;
        EnsureSprite();

        var root = new GameObject("Spieleabend_Fire");
        root.transform.SetParent(target.transform, false);
        root.transform.localPosition = new Vector3(0f, -0.35f, -0.03f);

        var flames = new List<(Transform, SpriteRenderer, float, float)>();
        float[] xs = { -0.22f, 0.2f, 0f, -0.08f, 0.1f };
        for (int i = 0; i < xs.Length; i++)
        {
            var go = new GameObject("Flame");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(xs[i], 0f, -0.001f * i);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = flameSprite;
            flames.Add((go.transform, sr, i * 1.37f, xs[i]));
        }

        fires.Add(new Fire { Root = root, Flames = flames, Start = Time.time, Duration = duration });
    }

    public static void Reset()
    {
        foreach (var f in fires)
            if (f.Root) Object.Destroy(f.Root);
        fires.Clear();
    }

    internal static void Update()
    {
        float t = Time.time;
        for (int i = fires.Count - 1; i >= 0; i--)
        {
            var f = fires[i];
            float p = (t - f.Start) / f.Duration;
            if (!f.Root || p >= 1f)
            {
                if (f.Root) Object.Destroy(f.Root);
                fires.RemoveAt(i);
                continue;
            }

            // schnell auflodern, am Ende ausgehen
            float grow = Mathf.Clamp01(p * 4f);
            float fade = p > 0.8f ? 1f - (p - 0.8f) / 0.2f : 1f;
            foreach (var (flame, sr, phase, x) in f.Flames)
            {
                if (!flame) continue;
                float flicker = 0.85f + 0.2f * Mathf.Sin(t * 18f + phase) + 0.1f * Mathf.Sin(t * 31f + phase * 2f);
                float h = grow * flicker * (0.9f + 0.25f * Mathf.Sin(phase));
                flame.localScale = new Vector3(0.75f * grow, h, 1f);
                flame.localPosition = new Vector3(x + 0.03f * Mathf.Sin(t * 9f + phase), 0.05f * Mathf.Sin(t * 6f + phase), flame.localPosition.z);
                sr.color = new Color(1f, 1f, 1f, fade);
            }
        }
    }

    private static void EnsureSprite()
    {
        if (flameSprite) return;
        const int W = 48, H = 96;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            // Tropfenform: unten breit und rund, nach oben spitz
            float v = y / (float)H;                      // 0 unten, 1 oben
            float halfWidth = (W * 0.46f) * Mathf.Sqrt(Mathf.Clamp01(1f - v)) * Mathf.Clamp01(v * 3.5f + 0.35f);
            float dx = Mathf.Abs(x - W / 2f + 0.5f);
            if (dx > halfWidth || halfWidth <= 0f)
            {
                tex.SetPixel(x, y, Color.clear);
                continue;
            }
            float inner = 1f - dx / halfWidth;           // 1 = Mitte
            float heat = Mathf.Clamp01(inner * 0.8f + (1f - v) * 0.5f);
            // aussen rot, dann orange, innen gelb-weiss
            var outer = new Color(0.85f, 0.12f, 0.02f);
            var mid = new Color(1f, 0.55f, 0.05f);
            var core = new Color(1f, 0.95f, 0.6f);
            Color c = heat < 0.5f ? Color.Lerp(outer, mid, heat * 2f) : Color.Lerp(mid, core, (heat - 0.5f) * 2f);
            float a = Mathf.Clamp01(inner * 1.6f) * Mathf.Clamp01((1f - v) * 1.8f);
            tex.SetPixel(x, y, new Color(c.r, c.g, c.b, a));
        }
        tex.Apply();
        flameSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), 100f);
        flameSprite.hideFlags = HideFlags.HideAndDontSave;
    }
}
