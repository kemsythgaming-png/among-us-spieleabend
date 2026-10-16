using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Spieleabend-Graffiti auf The Skeld: Sprueche der Mitspieler an Waenden, Tischen und Aufgaben.
/// Rein optisch und lokal. Positionen werden aus Raeumen, Aufgaben und Vents berechnet, damit sie auch auf der
/// gespiegelten Skeld passen.
/// </summary>
internal static class MapGraffiti
{
    private static System.IntPtr spawnedFor;
    private static readonly List<GameObject> objects = new();

    internal static void Update()
    {
        var ship = ShipStatus.Instance;
        if (!ship)
        {
            spawnedFor = System.IntPtr.Zero;
            objects.Clear();
            return;
        }
        if (spawnedFor == ship.Pointer || ship.Type != ShipStatus.MapType.Ship) return;

        var local = PlayerControl.LocalPlayer;
        if (!local || !local.cosmetics || !local.cosmetics.nameText) return; // Schrift kommt vom Namensschild
        spawnedFor = ship.Pointer;

        try { Spawn(ship, local.cosmetics.nameText); }
        catch (System.Exception e) { Plugin.Logger.LogWarning($"Graffiti: {e.Message}"); }
    }

    private static void Spawn(ShipStatus ship, TextMeshPro font)
    {
        // An Waenden (oberer Rand des Raums)
        Room(ship, SystemTypes.Cafeteria, 0.22f, 0.9f, "Waybe isses", new Color32(255, 200, 60, 255), -4f);
        Room(ship, SystemTypes.Electrical, 0.5f, 0.88f, "Ich bins der Imposter", new Color32(255, 60, 60, 255), 3f);
        Room(ship, SystemTypes.UpperEngine, 0.5f, 0.88f, "WoabWoabWoab", new Color32(120, 230, 90, 255), -3f);
        Room(ship, SystemTypes.Security, 0.5f, 0.88f, "Dennator is Save", new Color32(90, 170, 255, 255), 2f);

        // Herzen
        Heart(ship, SystemTypes.MedBay, 0.75f, 0.82f, "V + P", -8f);
        Heart(ship, SystemTypes.Storage, 0.3f, 0.78f, "I + M", 7f);

        // An Aufgaben
        var download = Consoles(ship, TaskTypes.UploadData)
            .OrderBy(c => c.Room == SystemTypes.Nav ? 0 : c.Room == SystemTypes.Weapons ? 1 : c.Room == SystemTypes.Admin ? 9 : 5)
            .FirstOrDefault();
        if (download) Text(font, (Vector2)download.transform.position + new Vector2(0f, 0.9f), "Lord Toasti's 4ter Download", new Color32(255, 150, 40, 255), 0.75f, 4f);

        var swipe = Consoles(ship, TaskTypes.SwipeCard).FirstOrDefault();
        if (swipe) Text(font, (Vector2)swipe.transform.position + new Vector2(0f, 0.8f), "Froschi das ist kein Bug.", new Color32(110, 220, 120, 255), 0.75f, -3f);

        // Am Vent, das der Cafeteria am naechsten ist
        var cafe = RoomBounds(ship, SystemTypes.Cafeteria);
        if (cafe.HasValue)
        {
            var vent = ship.AllVents.Where(v => v).OrderBy(v => Vector2.Distance(v.transform.position, cafe.Value.center)).FirstOrDefault();
            if (vent) Text(font, (Vector2)vent.transform.position + new Vector2(0f, 0.75f), "ich hab Kem Venten sehen", new Color32(230, 120, 255, 255), 0.75f, 5f);
        }

        void Room(ShipStatus s, SystemTypes room, float fx, float fy, string text, Color color, float angle)
        {
            var b = RoomBounds(s, room);
            if (b.HasValue) Text(font, At(b.Value, fx, fy), text, color, 1f, angle);
        }

        void Heart(ShipStatus s, SystemTypes room, float fx, float fy, string text, float angle)
        {
            var b = RoomBounds(s, room);
            if (!b.HasValue) return;
            var pos = At(b.Value, fx, fy);
            var go = new GameObject("Spieleabend_GraffitiHeart");
            go.transform.SetParent(s.transform, true);
            go.transform.position = new Vector3(pos.x, pos.y, pos.y / 1000f + 0.002f); // hinter dem Text
            go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
            go.AddComponent<SpriteRenderer>().sprite = ButtonIcons.Get("heart", null);
            objects.Add(go);
            Text(font, pos + new Vector2(0f, 0.05f), text, Color.white, 0.8f, angle);
        }
    }

    private static IEnumerable<Console> Consoles(ShipStatus ship, TaskTypes type) =>
        ship.AllConsoles.Where(c => c && c.TaskTypes != null && c.TaskTypes.Contains(type));

    private static Bounds? RoomBounds(ShipStatus ship, SystemTypes room)
    {
        foreach (var r in ship.AllRooms)
            if (r && r.RoomId == room && r.roomArea) return r.roomArea.bounds;
        return null;
    }

    private static Vector2 At(Bounds b, float fx, float fy) => new(b.min.x + b.size.x * fx, b.min.y + b.size.y * fy);

    /// <summary>Text im Stil der Spielernamen (gleiche Schrift mit Kontur), flach auf dem Boden/der Wand.</summary>
    private static void Text(TextMeshPro font, Vector2 pos, string text, Color color, float size, float angle)
    {
        var go = Object.Instantiate(font.gameObject, ShipStatus.Instance.transform);
        go.name = "Spieleabend_Graffiti";
        go.SetActive(true);
        foreach (Transform child in go.transform) Object.Destroy(child.gameObject);

        var tmp = go.GetComponent<TextMeshPro>();
        tmp.enabled = true;
        tmp.text = text;
        tmp.color = color;
        tmp.alpha = 0.85f;
        tmp.enableWordWrapping = false;
        tmp.alignment = TextAlignmentOptions.Center;

        var scale = font.transform.lossyScale * size;
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        go.transform.position = new Vector3(pos.x, pos.y, pos.y / 1000f + 0.001f); // knapp hinter Spielern auf gleicher Hoehe, vor dem Boden
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        objects.Add(go);
    }
}
