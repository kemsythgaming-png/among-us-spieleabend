using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Core;

/// <summary>
/// Prototyp: baut zur Laufzeit einen zusaetzlichen Raum in die Map (oben ueber der Cafeteria, im dunklen Rand).
/// Vier Waende mit echten Collidern (man kommt nicht durch), Boden, zwei Teleport-Pads (rein/raus) und eine
/// Gluehzone als Spielerei. Rein im Code - keine Unity-Assets noetig, rollt wie alles andere ueber den Launcher aus.
/// Nur sichtbar, wenn im Reiter "Spiel" die Option "Test-Raum" an ist. Alle Clients bauen dasselbe (keine Desyncs).
/// </summary>
internal static class PrototypeRoom
{
    private static readonly List<GameObject> objects = new();
    private static Vector2 padOutside, padInside;
    private static bool built;
    private static System.IntPtr builtFor;
    private static float teleportLockUntil;
    private static GameObject glow;

    private static Sprite floorSprite, wallSprite, padSprite, glowSprite;

    public static void Reset()
    {
        foreach (var o in objects) if (o) Object.Destroy(o);
        objects.Clear();
        glow = null;
        built = false;
        builtFor = System.IntPtr.Zero;
    }

    public static void Update()
    {
        bool want = GeneralOptions.TestRoom != null && GeneralOptions.TestRoom.Bool && ShipStatus.Instance;
        if (!want)
        {
            if (built) Reset();
            return;
        }
        if (built && builtFor != ShipStatus.Instance.Pointer) Reset(); // neue Runde -> neu bauen
        if (!built) Build();
        Animate();
        TeleportCheck();
    }

    // ---------- Aufbau ----------
    private static void Build()
    {
        var ship = ShipStatus.Instance;
        Vector2 caf = ship.MeetingSpawnCenter; // Cafeteria-Mitte (auf den meisten Maps)
        padOutside = caf + new Vector2(0f, 3.2f);     // nordlich in der Cafeteria
        Vector2 center = caf + new Vector2(0f, 15f);  // Raum oben im dunklen Rand
        padInside = center + new Vector2(0f, -2.6f);

        int wallLayer = FindWallLayer(ship);
        const float w = 10f, h = 7f, t = 0.5f;

        // Boden
        AddSprite(FloorSprite(), center, new Vector2(w, h), new Color(0.32f, 0.36f, 0.42f), -50, false, 0);

        // Vier Waende (mit Collider)
        AddWall(center + new Vector2(0f, h / 2f), new Vector2(w + t, t), wallLayer);   // oben
        AddWall(center + new Vector2(0f, -h / 2f), new Vector2(w + t, t), wallLayer);  // unten
        AddWall(center + new Vector2(-w / 2f, 0f), new Vector2(t, h), wallLayer);      // links
        AddWall(center + new Vector2(w / 2f, 0f), new Vector2(t, h), wallLayer);       // rechts

        // Teleport-Pads
        AddSprite(PadSprite(), padOutside, Vector2.one * 1.1f, new Color(0.4f, 0.95f, 0.5f), -45, false, 0);
        AddSprite(PadSprite(), padInside, Vector2.one * 1.1f, new Color(0.4f, 0.95f, 0.5f), -45, false, 0);

        // Gimmick: Gluehzone in der Mitte
        glow = AddSprite(GlowSprite(), center, Vector2.one * 3.2f, new Color(0.5f, 0.75f, 1f, 0.5f), -44, false, 0);

        built = true;
        builtFor = ship.Pointer;
        Plugin.Logger.LogInfo("Test-Raum gebaut.");
    }

    private static void AddWall(Vector2 pos, Vector2 size, int layer)
    {
        var go = AddSprite(FloorSprite(), pos, size, new Color(0.15f, 0.17f, 0.2f), -40, false, 0);
        go.layer = layer;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one; // Sprite ist 1x1 Einheit, Skala macht die Groesse
    }

    private static GameObject AddSprite(Sprite sprite, Vector2 pos, Vector2 scale, Color color, int order, bool unused, float unused2)
    {
        var go = new GameObject("Spieleabend_TestRoom");
        if (ShipStatus.Instance) go.transform.SetParent(ShipStatus.Instance.transform, true);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        objects.Add(go);
        return go;
    }

    private static int FindWallLayer(ShipStatus ship)
    {
        foreach (var col in ship.GetComponentsInChildren<Collider2D>(true))
            if (col && !col.isTrigger && col.gameObject.layer != 0) return col.gameObject.layer;
        int ship2 = LayerMask.NameToLayer("Ship");
        return ship2 >= 0 ? ship2 : 0;
    }

    // ---------- Laufzeit ----------
    private static void Animate()
    {
        if (glow)
        {
            float p = 1f + 0.12f * Mathf.Sin(Time.time * 2.4f);
            glow.transform.localScale = new Vector3(3.2f * p, 3.2f * p, 1f);
        }
    }

    private static void TeleportCheck()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !CustomRole.IsAlive(local) || CustomRole.InMeeting || !local.CanMove || Time.time < teleportLockUntil) return;
        Vector2 me = local.GetTruePosition();

        if (Vector2.Distance(me, padOutside) < 0.6f) Teleport(local, padInside);
        else if (Vector2.Distance(me, padInside) < 0.6f) Teleport(local, padOutside);
    }

    private static void Teleport(PlayerControl local, Vector2 to)
    {
        local.transform.position = new Vector3(to.x, to.y + 0.9f, local.transform.position.z); // etwas neben dem Pad
        if (local.MyPhysics && local.MyPhysics.body) local.MyPhysics.body.velocity = Vector2.zero;
        teleportLockUntil = Time.time + 1.5f;
        Sfx.Play(Sfx.TaskComplete, 0.5f);
    }

    // ---------- Grafiken (im Code) ----------
    private static Sprite FloorSprite()
    {
        if (!floorSprite) floorSprite = Solid();
        return floorSprite;
    }

    private static Sprite PadSprite()
    {
        if (padSprite) return padSprite;
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f;
            float ring = Mathf.Exp(-Mathf.Pow((d - 0.82f) / 0.12f, 2f));
            float fill = d < 0.82f ? 0.35f : 0f;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(fill + ring) * (d > 1f ? 0f : 1f)));
        }
        tex.Apply();
        padSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        padSprite.hideFlags = HideFlags.HideAndDontSave;
        return padSprite;
    }

    private static Sprite GlowSprite()
    {
        if (glowSprite) return glowSprite;
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * (d > 1f ? 0f : 1f)));
        }
        tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        glowSprite.hideFlags = HideFlags.HideAndDontSave;
        return glowSprite;
    }

    /// <summary>1x1-Einheit grosse, volle weisse Flaeche (ueber Skala + Farbe angepasst).</summary>
    private static Sprite Solid()
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        var px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4); // 4 px / 4 ppu = 1 Einheit
        s.hideFlags = HideFlags.HideAndDontSave;
        return s;
    }
}
