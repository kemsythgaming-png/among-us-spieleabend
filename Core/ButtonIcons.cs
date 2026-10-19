using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Laedt die Button-Symbole aus der DLL (Assets/Icons/*.png, erzeugt mit Assets/make_icons.py).
/// Die Symbole werden so gross gemacht wie die Original-Grafik des Kill-Buttons.
/// </summary>
internal static class ButtonIcons
{
    private static readonly Dictionary<string, Texture2D> cache = new();

    /// <summary>Rohe Textur eines Symbols (z. B. fuer IMGUI-Anzeigen wie den Slot-Automaten).</summary>
    public static Texture2D Texture(string name) => Load(name);

    public static Sprite Get(string name, Sprite reference)
    {
        var tex = Load(name);
        if (!tex) return null;

        float units = reference ? Mathf.Max(reference.bounds.size.x, reference.bounds.size.y) : 1.15f;
        var pivot = reference ? new Vector2(reference.pivot.x / reference.rect.width, reference.pivot.y / reference.rect.height) : new Vector2(0.5f, 0.5f);
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, tex.width / units);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Texture2D Load(string name)
    {
        if (cache.TryGetValue(name, out var cached) && cached) return cached;

        using var stream = typeof(ButtonIcons).Assembly.GetManifestResourceStream($"Spieleabend.Assets.Icons.{name}.png");
        if (stream == null)
        {
            Plugin.Logger.LogWarning($"Symbol '{name}' nicht gefunden.");
            return null;
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);

        var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false) { hideFlags = HideFlags.HideAndDontSave };
        if (!ImageConversion.LoadImage(tex, memory.ToArray(), false))
        {
            Plugin.Logger.LogWarning($"Symbol '{name}' konnte nicht geladen werden.");
            return null;
        }
        tex.filterMode = FilterMode.Bilinear;
        cache[name] = tex;
        return tex;
    }
}
