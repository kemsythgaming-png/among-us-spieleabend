using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Erzeugt einfache Button-Symbole direkt im Code (weiße Form auf transparent). So bekommt eine Rolle ihr eigenes
/// Symbol, ohne eine PNG mitzuliefern. Nutzung am Knopf: GameIcon = _ => ProcIcons.Mortar() usw.
/// Koordinaten in der Form-Funktion sind -1..1 (Mitte = 0,0).
/// </summary>
internal static class ProcIcons
{
    private static readonly Dictionary<string, Sprite> cache = new();

    public static Sprite Mask(string key, Func<float, float, bool> inside, int size = 96, Color? color = null)
    {
        if (cache.TryGetValue(key, out var s) && s) return s;
        var col = color ?? Color.white;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        float c = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = (x - c) / c, ny = (y - c) / c;
            bool on = false;
            try { on = inside(nx, ny); } catch { }
            tex.SetPixel(x, y, new Color(col.r, col.g, col.b, on ? col.a : 0f));
        }
        tex.Apply();
        var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 1.15f);
        sp.hideFlags = HideFlags.HideAndDontSave;
        cache[key] = sp;
        return sp;
    }

    /// <summary>Für "proc:xxx"-Icon-Namen (Namens-/Meeting-Symbole): Schlüssel -> fertiges Symbol.</summary>
    public static Sprite ByKey(string key) => key switch
    {
        "virus" => Virus(),
        "task" => Task(),
        _ => null,
    };

    private static bool Ellipse(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = (x - cx) / rx, dy = (y - cy) / ry;
        return dx * dx + dy * dy <= 1f;
    }

    /// <summary>Mörser: Explosions-Stern (Zacken).</summary>
    public static Sprite Mortar() => Mask("moerser", (x, y) =>
    {
        float r = Mathf.Sqrt(x * x + y * y);
        float ang = Mathf.Atan2(y, x);
        float edge = 0.55f + 0.4f * Mathf.Abs(Mathf.Sin(ang * 4f)); // 8-zackiger Stern
        return r <= edge;
    });

    /// <summary>Dummy-Spawner: Among-Us-Figur (Körper-Oval mit Visier).</summary>
    public static Sprite Crewmate() => Mask("dummyfigur", (x, y) =>
    {
        bool body = Ellipse(x, y, 0f, -0.05f, 0.52f, 0.66f);
        bool visor = Ellipse(x, y, 0.12f, 0.18f, 0.3f, 0.18f);
        return body && !visor;
    });

    /// <summary>Deaktivator: An/Aus-Symbol (Ring mit Lücke oben + senkrechter Strich).</summary>
    public static Sprite Power() => Mask("deaktivator", (x, y) =>
    {
        float r = Mathf.Sqrt(x * x + y * y);
        bool ring = r >= 0.5f && r <= 0.74f && !(y > 0.45f && Mathf.Abs(x) < 0.22f);
        bool bar = Mathf.Abs(x) < 0.1f && y > 0.1f && y < 0.82f;
        return ring || bar;
    });

    /// <summary>Chamäleon: durchgestrichenes Auge (unsichtbar).</summary>
    public static Sprite InvisibleEye() => Mask("chamaeleon", (x, y) =>
    {
        bool almond = Mathf.Abs(x) < 0.85f && Mathf.Abs(y) <= 0.5f * (1f - (x * x) / (0.85f * 0.85f));
        bool pupil = x * x + y * y <= 0.17f * 0.17f;
        bool slash = Mathf.Abs(y - x) < 0.12f; // diagonaler Strich
        return (almond || pupil) && !slash;
    });

    /// <summary>Spurensucher: zwei Fußabdrücke.</summary>
    public static Sprite Footprints() => Mask("spurensucher", (x, y) =>
    {
        bool fa = Ellipse(x, y, -0.3f, 0.12f, 0.2f, 0.34f) || Ellipse(x, y, -0.3f, 0.52f, 0.16f, 0.16f);
        bool fb = Ellipse(x, y, 0.3f, -0.42f, 0.2f, 0.34f) || Ellipse(x, y, 0.3f, -0.02f, 0.16f, 0.16f);
        return fa || fb;
    });

    /// <summary>Virus: grüner Erreger (Kreis mit Stacheln + Punkten).</summary>
    public static Sprite Virus() => Mask("virus", (x, y) =>
    {
        float r = Mathf.Sqrt(x * x + y * y);
        float ang = Mathf.Atan2(y, x);
        float core = 0.48f + 0.10f * Mathf.Sin(ang * 8f); // leicht welliger Kern
        if (r <= core) return true;
        // Stacheln (8 Keulen) mit Knopf am Ende
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            var tip = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.82f;
            if ((new Vector2(x, y) - tip).sqrMagnitude <= 0.11f * 0.11f) return true;       // Knopf
            if (r <= 0.82f && Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, a * Mathf.Rad2Deg)) < 6f) return true; // Stiel
        }
        return false;
    }, 96, new Color(0.35f, 0.85f, 0.3f, 1f));

    /// <summary>Ausbilder: Doktorhut (Lehrer/Ausbilder).</summary>
    public static Sprite Graduation() => Mask("graduation", (x, y) =>
    {
        bool cap = Mathf.Abs(x) / 0.68f + Mathf.Abs(y - 0.3f) / 0.26f <= 1f;                 // Mütze (Rhombus)
        bool head = y < 0.18f && y > -0.5f && Mathf.Abs(x) < 0.36f && !(y > -0.32f && Mathf.Abs(x) < 0.24f); // Kopf-U
        bool tassel = (Mathf.Abs(x - 0.52f) < 0.045f && y < 0.3f && y > -0.08f) || new Vector2(x - 0.52f, y + 0.12f).magnitude < 0.08f;
        return cap || head || tassel;
    });

    /// <summary>Ausbilder (alt): Klemmbrett mit Plus (neue Aufgabe).</summary>
    public static Sprite Task() => Mask("task", (x, y) =>
    {
        bool board = Mathf.Abs(x) < 0.52f && Mathf.Abs(y) < 0.72f && !(Mathf.Abs(x) < 0.42f && Mathf.Abs(y) < 0.62f); // Rahmen
        bool clip = Mathf.Abs(x) < 0.2f && y > 0.6f && y < 0.82f; // Klemme oben
        bool plus = (Mathf.Abs(x) < 0.09f && Mathf.Abs(y) < 0.3f) || (Mathf.Abs(y) < 0.09f && Mathf.Abs(x) < 0.3f); // Plus
        return board || clip || plus;
    }, 96, new Color(0.6f, 0.8f, 1f, 1f));
}
