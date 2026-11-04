using UnityEngine;

namespace Spieleabend.Core;

/// <summary>Per Code erzeugte, wiederverwendbare Sprites (Ring, gefuellter Kreis, Vollflaeche) fuer Zonen/Overlays.</summary>
internal static class Shapes
{
    private static Sprite ring, disc, square;

    /// <summary>Heller Ring mit zarter Fuellung (fuer sichtbare Zonen-Umrandungen).</summary>
    public static Sprite Ring()
    {
        if (ring) return ring;
        const int S = 128; float c = (S - 1) / 2f;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
            float r = Mathf.Exp(-Mathf.Pow((d - 0.9f) / 0.07f, 2f));
            float fill = d < 0.9f ? 0.2f : 0f;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(fill + r) * (d > 1f ? 0f : 1f)));
        }
        tex.Apply();
        ring = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        ring.hideFlags = HideFlags.HideAndDontSave;
        return ring;
    }

    /// <summary>Weicher, gefuellter Kreis (fuer Glanz/Flaechen).</summary>
    public static Sprite Disc()
    {
        if (disc) return disc;
        const int S = 128; float c = (S - 1) / 2f;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
            float a = Mathf.Clamp01(1f - d);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
        }
        tex.Apply();
        disc = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        disc.hideFlags = HideFlags.HideAndDontSave;
        return disc;
    }

    /// <summary>Weisse Vollflaeche (1 Einheit), zum Einfaerben als Bildschirm-Overlay.</summary>
    public static Sprite Square()
    {
        if (square) return square;
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        for (int i = 0; i < 16; i++) tex.SetPixel(i % 4, i / 4, Color.white);
        tex.Apply();
        square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        square.hideFlags = HideFlags.HideAndDontSave;
        return square;
    }
}
