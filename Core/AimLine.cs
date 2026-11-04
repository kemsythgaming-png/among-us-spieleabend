using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Kleine Ziel-Linie vom Spieler zur Maus - fuer Faehigkeiten, bei denen man erst den Knopf drueckt und dann auf
/// den Bildschirm klickt, um die Richtung zu waehlen (z. B. Zauberer, Thor). Nur lokal sichtbar.
/// </summary>
internal static class AimLine
{
    private static GameObject obj;
    private static SpriteRenderer sr;

    /// <summary>Aktuelle Richtung vom origin zur Maus (normalisiert); Vector2.right, wenn nichts bestimmbar.</summary>
    public static Vector2 MouseDir(Vector2 origin)
    {
        if (!Camera.main) return Vector2.right;
        Vector3 w = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 d = new Vector2(w.x, w.y) - origin;
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.right;
    }

    public static void Show(Vector2 origin, Color color)
    {
        if (!Camera.main) return;
        Vector3 w = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 dir = new Vector2(w.x, w.y) - origin;
        float len = Mathf.Clamp(dir.magnitude, 0.3f, 30f);
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        if (!obj)
        {
            obj = new GameObject("Spieleabend_AimArrow");
            sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = ArrowSprite();
        }
        if (ShipStatus.Instance && obj.transform.parent != ShipStatus.Instance.transform)
            obj.transform.SetParent(ShipStatus.Instance.transform, true);
        OnTop(sr); // über der Dunkelheit zeichnen, auch ohne Vision sichtbar

        obj.SetActive(true);
        sr.color = new Color(color.r, color.g, color.b, 0.75f);
        // Pfeil startet beim Spieler und zeigt zum Mauszeiger (Länge = Abstand, Dicke bleibt gleich)
        obj.transform.position = new Vector3(
            origin.x + Mathf.Cos(ang * Mathf.Deg2Rad) * len * 0.5f,
            origin.y + Mathf.Sin(ang * Mathf.Deg2Rad) * len * 0.5f,
            origin.y / 1000f - 0.1f);
        obj.transform.rotation = Quaternion.Euler(0f, 0f, ang);
        obj.transform.localScale = new Vector3(len, 0.5f, 1f);
    }

    public static void Hide() { if (obj) obj.SetActive(false); }

    /// <summary>Renderer über die Dunkelheit (ShadowQuad) heben, damit er auch ohne Vision sichtbar ist.</summary>
    internal static void OnTop(SpriteRenderer sr)
    {
        if (!sr) return;
        try
        {
            var shadow = HudManager.InstanceExists ? HudManager.Instance.ShadowQuad : null;
            if (shadow)
            {
                sr.sortingLayerID = shadow.sortingLayerID;
                sr.sortingOrder = shadow.sortingOrder + 20;
            }
            else sr.sortingOrder = 1000;
        }
        catch { sr.sortingOrder = 1000; }
    }

    // Pfeil nach rechts (+x): dünner Schaft + Spitze am rechten Ende. Beim Skalieren bleibt es als Pfeil lesbar.
    private static Sprite arrowSprite;
    private static Sprite ArrowSprite()
    {
        if (arrowSprite) return arrowSprite;
        const int W = 128, H = 64;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float cy = y - H / 2f;
            bool shaft = x < 88 && Mathf.Abs(cy) < 7f;       // Schaft
            bool head = x >= 80 && x <= 124 && Mathf.Abs(cy) <= (124 - x) * 0.75f; // Spitze (Dreieck)
            tex.SetPixel(x, y, (shaft || head) ? Color.white : Color.clear);
        }
        tex.Apply();
        arrowSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        arrowSprite.hideFlags = HideFlags.HideAndDontSave;
        return arrowSprite;
    }
}
