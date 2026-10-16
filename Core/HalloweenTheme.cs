using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Core;

/// <summary>
/// Halloween-Design (Einstellung "Allgemein" im F1-Fenster, vom Host fuer alle):
/// duestere lila Farbstimmung, flackernde Kuerbisse neben den Vents, schwebende Geister an Aufgaben,
/// dunkler Weltraum. Funktioniert auf jeder Map und in der Lobby. Rein optisch und lokal.
/// Alle Grafiken werden per Code erzeugt (keine Bilddateien noetig).
/// </summary>
internal static class HalloweenTheme
{
    private static readonly Color Tint = new(0.62f, 0.5f, 0.72f, 1f);
    private static readonly Color SpaceColor = new(0.07f, 0.02f, 0.1f, 1f);
    private const string DecoPrefix = "Halloween_";

    private static IntPtr appliedTo;
    private static readonly List<(SpriteRenderer Renderer, Color Original)> tinted = new();
    private static readonly List<(GameObject Obj, int Kind, float Phase, Vector3 Base)> decorations = new(); // Kind: 0 Kuerbis-Glow, 1 Geist
    private static Color? originalBackground;

    private static Sprite pumpkinSprite, glowSprite, ghostSprite;

    public static void Update()
    {
        GameObject root = ShipStatus.Instance ? ShipStatus.Instance.gameObject
            : LobbyBehaviour.Instance ? LobbyBehaviour.Instance.gameObject
            : null;
        bool enabled = GeneralOptions.Halloween.Bool && root;
        IntPtr target = enabled ? root.Pointer : IntPtr.Zero;

        if (target != appliedTo)
        {
            Remove();
            if (enabled)
            {
                try { Apply(root); }
                catch (Exception e) { Plugin.Logger.LogWarning($"Halloween-Design: {e.Message}"); }
            }
            appliedTo = target;
        }

        if (appliedTo != IntPtr.Zero) Animate();
    }

    // ---------- Anwenden / Entfernen ----------

    private static void Apply(GameObject root)
    {
        // 1. Farbstimmung fuer alle Map-Grafiken
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!sr || sr.name.StartsWith(DecoPrefix)) continue;
            var c = sr.color;
            tinted.Add((sr, c));
            sr.color = new Color(c.r * Tint.r, c.g * Tint.g, c.b * Tint.b, c.a);
        }

        // 2. Dunkler Weltraum
        var cam = Camera.main;
        if (cam)
        {
            originalBackground = cam.backgroundColor;
            cam.backgroundColor = SpaceColor;
        }

        // 3. Deko an sicheren Stellen (Vents/Konsolen liegen immer auf begehbarem Boden bzw. an Waenden)
        var ship = root.GetComponent<ShipStatus>();
        if (ship)
        {
            int i = 0;
            foreach (var vent in ship.AllVents)
            {
                if (!vent || i >= 24) break;
                var offset = new Vector3(i % 2 == 0 ? 0.55f : -0.55f, 0.1f, 0f);
                AddPumpkin(root, vent.transform.position + offset, vent.GetComponent<SpriteRenderer>(), i++);
            }

            int c = 0;
            foreach (var console in ship.AllConsoles)
            {
                if (!console || c >= 40) break;
                if (c++ % 5 != 0) continue;
                AddGhost(root, console.transform.position + new Vector3(0f, 0.7f, 0f), console.GetComponent<SpriteRenderer>(), c);
            }
        }
        else if (LobbyBehaviour.Instance)
        {
            var spawns = LobbyBehaviour.Instance.SpawnPositions;
            for (int i = 0; i < spawns.Length; i += 2)
            {
                var p = spawns[i];
                AddPumpkin(root, new Vector3(p.x + 0.6f, p.y - 0.2f, p.y / 1000f), null, i);
            }
        }

        Plugin.Logger.LogInfo($"Halloween-Design aktiv ({tinted.Count} Grafiken eingefaerbt, {decorations.Count} Deko-Objekte).");
    }

    private static void Remove()
    {
        foreach (var (sr, original) in tinted)
            if (sr) sr.color = original;
        tinted.Clear();

        foreach (var deco in decorations)
            if (deco.Obj) Object.Destroy(deco.Obj);
        decorations.Clear();

        var cam = Camera.main;
        if (cam && originalBackground.HasValue) cam.backgroundColor = originalBackground.Value;
        originalBackground = null;
    }

    private static void AddPumpkin(GameObject root, Vector3 pos, SpriteRenderer reference, int index)
    {
        EnsureSprites();
        float z = (reference ? reference.transform.position.z : pos.z) - 0.01f;

        var glow = MakeSprite(root, "Glow", glowSprite, new Vector3(pos.x, pos.y + 0.25f, z + 0.001f), reference);
        glow.transform.localScale = new Vector3(2.4f, 2.4f, 1f);
        decorations.Add((glow, 0, index * 1.7f, glow.transform.position));

        var pumpkin = MakeSprite(root, "Pumpkin", pumpkinSprite, new Vector3(pos.x, pos.y, z), reference);
        decorations.Add((pumpkin, -1, 0f, pumpkin.transform.position));
    }

    private static void AddGhost(GameObject root, Vector3 pos, SpriteRenderer reference, int index)
    {
        EnsureSprites();
        float z = (reference ? reference.transform.position.z : pos.z) - 0.02f;
        var ghost = MakeSprite(root, "Ghost", ghostSprite, new Vector3(pos.x, pos.y, z), reference);
        decorations.Add((ghost, 1, index * 2.3f, ghost.transform.position));
    }

    private static GameObject MakeSprite(GameObject root, string name, Sprite sprite, Vector3 position, SpriteRenderer reference)
    {
        var go = new GameObject(DecoPrefix + name);
        go.transform.SetParent(root.transform, true);
        go.transform.position = position;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        if (reference)
        {
            go.layer = reference.gameObject.layer;
            sr.sortingLayerID = reference.sortingLayerID;
            sr.sortingOrder = reference.sortingOrder + 1;
        }
        return go;
    }

    private static void Animate()
    {
        float t = Time.time;
        foreach (var (obj, kind, phase, basePos) in decorations)
        {
            if (!obj) continue;
            if (kind == 0)
            {
                // Kerzenflackern
                float flicker = 0.55f + 0.25f * Mathf.Sin(t * 7f + phase) + 0.1f * Mathf.Sin(t * 13f + phase * 2f);
                var sr = obj.GetComponent<SpriteRenderer>();
                if (sr) sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(flicker));
            }
            else if (kind == 1)
            {
                // Geist schwebt auf und ab
                obj.transform.position = basePos + new Vector3(Mathf.Sin(t * 0.7f + phase) * 0.15f, Mathf.Sin(t * 1.6f + phase) * 0.18f, 0f);
                var sr = obj.GetComponent<SpriteRenderer>();
                if (sr) sr.color = new Color(1f, 1f, 1f, 0.55f + 0.2f * Mathf.Sin(t * 2.1f + phase));
            }
        }
    }

    // ---------- Grafiken per Code ----------

    private static void EnsureSprites()
    {
        if (pumpkinSprite && glowSprite && ghostSprite) return;
        pumpkinSprite = ToSprite(DrawPumpkin(), new Vector2(0.5f, 0f));
        glowSprite = ToSprite(DrawGlow(), new Vector2(0.5f, 0.5f));
        ghostSprite = ToSprite(DrawGhost(), new Vector2(0.5f, 0.5f));
    }

    private static Sprite ToSprite(Texture2D tex, Vector2 pivot)
    {
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Texture2D NewTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        return tex;
    }

    private static Texture2D DrawPumpkin()
    {
        const int S = 64;
        var tex = NewTexture(S, S);
        var face = new Color(1f, 0.85f, 0.25f, 1f);
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            Color c = Color.clear;
            float nx = (x - 32f) / 30f, ny = (y - 24f) / 22f;
            float d = Mathf.Sqrt(nx * nx + ny * ny);

            if (d <= 1f)
            {
                float rib = 0.72f + 0.28f * Mathf.Abs(Mathf.Cos(nx * Mathf.PI * 1.5f));
                float shade = rib * (1f - 0.3f * Mathf.Pow(d, 4f));
                c = new Color(0.97f * shade, 0.48f * shade, 0.06f * shade, 1f);
                if (d > 0.93f) c = new Color(0.32f, 0.13f, 0.02f, 1f);

                // Augen (Dreiecke) und Mund
                bool leftEye = y >= 26 && y <= 34 && Mathf.Abs(x - 21) <= (34 - y) * 0.7f;
                bool rightEye = y >= 26 && y <= 34 && Mathf.Abs(x - 43) <= (34 - y) * 0.7f;
                float mx = (x - 32f) / 15f, my = (y - 18f) / 8f;
                bool mouth = mx * mx + my * my <= 1f && y <= 18 && !((x >= 25 && x <= 28 || x >= 36 && x <= 39) && y >= 14);
                if (leftEye || rightEye || mouth) c = face;
            }
            else if (x >= 29 && x <= 35 && y >= 44 && y <= 55)
            {
                c = new Color(0.25f, 0.38f, 0.1f, 1f); // Stiel
            }
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D DrawGlow()
    {
        const int S = 64;
        var tex = NewTexture(S, S);
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
            float a = Mathf.Clamp01(1f - d);
            tex.SetPixel(x, y, new Color(1f, 0.55f, 0.1f, a * a * 0.6f));
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D DrawGhost()
    {
        const int W = 64, H = 80;
        var tex = NewTexture(W, H);
        var body = new Color(0.92f, 0.95f, 1f, 1f);
        var dark = new Color(0.08f, 0.05f, 0.12f, 1f);
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            bool head = Vector2.Distance(new Vector2(x, y), new Vector2(32f, 50f)) <= 24f && y >= 40;
            float wave = 12f + 6f * Mathf.Abs(Mathf.Sin(x * Mathf.PI / 16f));
            bool torso = x >= 8 && x <= 56 && y >= wave && y <= 50;
            Color c = head || torso ? body : Color.clear;

            if (c.a > 0f)
            {
                bool eye = Ellipse(x, y, 24, 54, 4, 6) || Ellipse(x, y, 40, 54, 4, 6);
                bool mouth = Ellipse(x, y, 32, 41, 4, 5);
                if (eye || mouth) c = dark;
            }
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    private static bool Ellipse(int x, int y, float cx, float cy, float rx, float ry)
    {
        float nx = (x - cx) / rx, ny = (y - cy) / ry;
        return nx * nx + ny * ny <= 1f;
    }
}
