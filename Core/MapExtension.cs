using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Core;

/// <summary>
/// Prototyp (nur The Skeld): baut per Code einen zusaetzlichen leeren Raum noerdlich der Cafeteria in den freien
/// dunklen Rand. Der Raum hat Boden + Waende mit Collidern, eine begehbare Oeffnung in der Cafeteria-Nordwand
/// (dort werden die Wand-Collider weggenommen) und einen ECHTEN Vent: ein geklonter Spiel-Vent, der mit dem
/// naechstgelegenen vorhandenen Vent verbunden und in AllVents eingetragen wird, also normal nutzbar ist.
///
/// Erst EIN Muster-Raum zum Testen. Alle Clients bauen deterministisch dasselbe (gleiche Vent-Id) -> kein Desync.
/// Nur aktiv, wenn im Reiter "Spiel" die Option "Map-Erweiterung" an ist und die Map The Skeld ist.
/// </summary>
internal static class MapExtension
{
    private static readonly List<GameObject> objects = new();
    private static readonly List<EdgeCollider2D> carvedOriginals = new(); // originale Wand-Linien (deaktiviert, solange die Tuer offen ist)
    private static readonly List<EdgeCollider2D> carvedPieces = new();     // neu erzeugte Teilstuecke mit Luecke
    private static Vent addedVent;
    private static Vent linkedTo;
    private static bool built;
    private static System.IntPtr builtFor;

    private static Sprite floorSprite, discSprite, ringSprite, starSprite;

    public static void Reset()
    {
        foreach (var o in objects) if (o) Object.Destroy(o);
        objects.Clear();

        // Tuer wieder zumauern: Teilstuecke weg, Originallinien zurueck
        foreach (var p in carvedPieces) if (p) Object.Destroy(p);
        carvedPieces.Clear();
        foreach (var o in carvedOriginals) if (o) o.enabled = true;
        carvedOriginals.Clear();

        RemoveAddedVent();

        built = false;
        builtFor = System.IntPtr.Zero;
    }

    public static void Update()
    {
        bool want = GeneralOptions.MapExtension != null && GeneralOptions.MapExtension.Bool
                    && ShipStatus.Instance && IsSkeld();
        if (!want)
        {
            if (built) Reset();
            return;
        }
        if (built && builtFor != ShipStatus.Instance.Pointer) Reset(); // neue Runde -> neu bauen
        if (!built) Build();
    }

    private static bool IsSkeld()
    {
        try { return GameOptionsManager.Instance.CurrentGameOptions.MapId == 0; }
        catch { return false; }
    }

    // ---------- Aufbau ----------
    private static void Build()
    {
        built = true; // gleich setzen, damit ein Fehler nicht jeden Frame neu versucht
        builtFor = ShipStatus.Instance.Pointer;
        try { BuildRoom(); }
        catch (System.Exception e) { Plugin.Logger.LogError($"Map-Erweiterung Raum: {e.Message}"); }
        try { BuildVent(); }
        catch (System.Exception e) { Plugin.Logger.LogError($"Map-Erweiterung Vent: {e.Message}"); }
        Plugin.Logger.LogInfo("Map-Erweiterung (Skeld) gebaut.");
    }

    /// <summary>Diagnose: grosse Boden-/Wand-Sprites (Raum-Hintergruende) ins Log, um echte Boeden zu klonen.</summary>
    private static void DiscoverFloors()
    {
        var ship = ShipStatus.Instance;
        foreach (var sr in ship.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!sr || !sr.sprite) continue;
            if (sr.name.StartsWith("Spieleabend")) continue;
            var b = sr.bounds;
            float mx = Mathf.Max(b.size.x, b.size.y);
            if (mx < 2.5f || mx > 16f) continue;
            Plugin.Logger.LogInfo($"[MapExt-Floor] '{sr.name}' order={sr.sortingOrder} pos=({b.center.x:0.0},{b.center.y:0.0}) size=({b.size.x:0.0},{b.size.y:0.0}) parent='{(sr.transform.parent ? sr.transform.parent.name : "-")}'");
        }
        Plugin.Logger.LogInfo("[MapExt-Floor] Ende.");
    }

    private static Vector2 roomCenter; // Mitte des gebauten Raums (fuer den Vent)

    /// <summary>
    /// Baut das Aussichtsdeck noerdlich der Cafeteria aus ECHTEN Skeld-Grafiken: klont den Weapons-Raum
    /// (Boden + Waende, inkl. Fenster ins All) als Optik, legt unsichtbare Kollision an die Kanten und stellt Props rein.
    /// </summary>
    private static void BuildRoom()
    {
        var ship = ShipStatus.Instance;
        Vector2 caf = ship.MeetingSpawnCenter;
        float northY = CafeteriaNorthEdge(ship, caf);
        int wallLayer = FindWallLayer(ship);

        // 1) Echten Raum (Boden+Waende) klonen und so setzen, dass seine Unterkante an der Cafeteria sitzt
        Vector2 start = new Vector2(caf.x, northY + 4f);
        var room = CloneRoomVisual("weap_", start, 1.0f);
        if (room == null) { Plugin.Logger.LogWarning("Map-Erweiterung: Raum-Grafik nicht gefunden - abgebrochen."); return; }
        Bounds bb = RendererBounds(room);
        room.transform.position += new Vector3(0f, (northY + 0.6f) - bb.min.y, 0f);
        bb = RendererBounds(room);
        roomCenter = bb.center;

        // 2) Unsichtbare Kollision an die Kanten (unten eine DoorW breite Tuerluecke bei caf.x)
        float gap = DoorW / 2f;
        AddWallInv(new Vector2(bb.center.x, bb.max.y + T / 2f), new Vector2(bb.size.x + T, T), wallLayer); // oben
        AddWallInv(new Vector2(bb.min.x - T / 2f, bb.center.y), new Vector2(T, bb.size.y), wallLayer);      // links
        AddWallInv(new Vector2(bb.max.x + T / 2f, bb.center.y), new Vector2(T, bb.size.y), wallLayer);      // rechts
        float lw = (caf.x - gap) - bb.min.x;
        if (lw > 0.1f) AddWallInv(new Vector2((bb.min.x + caf.x - gap) / 2f, bb.min.y - T / 2f), new Vector2(lw, T), wallLayer);
        float rw = bb.max.x - (caf.x + gap);
        if (rw > 0.1f) AddWallInv(new Vector2((caf.x + gap + bb.max.x) / 2f, bb.min.y - T / 2f), new Vector2(rw, T), wallLayer);

        // 3) Durchgang ("Hals") von der Cafeteria hoch in den Raum
        float neckTop = bb.min.y + 0.3f;
        float wallBottom = northY - 2.2f;
        float wallH = Mathf.Max(0.5f, neckTop - wallBottom);
        float wallMidY = (wallBottom + neckTop) / 2f;
        AddWallInv(new Vector2(caf.x - gap, wallMidY), new Vector2(T, wallH), wallLayer);
        AddWallInv(new Vector2(caf.x + gap, wallMidY), new Vector2(T, wallH), wallLayer);
        float floorBottom = northY - 2.4f;
        AddArt(FloorSprite(), new Vector2(caf.x, (floorBottom + neckTop) / 2f), new Vector2(DoorW, neckTop - floorBottom), 0f, new Color(0.33f, 0.37f, 0.44f), 2); // Hals-Boden ueber dem Klon

        // 4) Cafeteria-Wand auftrennen + gemalte Wand an der Tuer ueberdecken
        CarveDoorway(ship, caf.x);
        AddArt(FloorSprite(), new Vector2(caf.x, northY - 0.6f), new Vector2(DoorW, 2.4f), 0f, new Color(0.33f, 0.37f, 0.44f), 3);

        // 5) Props (echte Objekte) ins Deck
        BuildProps(bb.center);
    }

    private const float T = 0.5f, DoorW = 2.2f;

    /// <summary>Klont die Boden+Wand-Grafik eines Raums ('Ground'-Objekt, das ein Kind-Sprite mit childSig enthaelt).</summary>
    private static GameObject CloneRoomVisual(string childSig, Vector2 pos, float scale)
    {
        var ship = ShipStatus.Instance;
        Transform ground = null;
        foreach (var t in ship.GetComponentsInChildren<Transform>(true))
        {
            if (!t || t.name != "Ground") continue;
            bool match = false;
            foreach (var sr in t.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr && sr.name.ToLower().Contains(childSig)) { match = true; break; }
            if (match) { ground = t; break; }
        }
        if (!ground) return null;

        var go = Object.Instantiate(ground.gameObject, ship.transform);
        go.transform.localScale = ground.localScale * scale;
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        go.SetActive(true);
        foreach (var col in go.GetComponentsInChildren<Collider2D>(true)) col.enabled = false; // reine Optik
        foreach (var con in go.GetComponentsInChildren<Console>(true)) Object.Destroy(con);
        objects.Add(go);
        return go;
    }

    private static Bounds RendererBounds(GameObject go)
    {
        Bounds b = default; bool has = false;
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!sr || !sr.sprite) continue;
            if (!has) { b = sr.bounds; has = true; } else b.Encapsulate(sr.bounds);
        }
        return b;
    }

    /// <summary>Unsichtbare Kollisionswand (nur Collider, kein Sprite).</summary>
    private static void AddWallInv(Vector2 pos, Vector2 size, int layer)
    {
        var go = new GameObject("Spieleabend_MapExtWall");
        if (ShipStatus.Instance) go.transform.SetParent(ShipStatus.Instance.transform, true);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = new Vector3(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y), 1f);
        go.layer = layer;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
        objects.Add(go);
    }

    /// <summary>Nordkante der Cafeteria ueber ihren Raumbereich finden (sonst grob geschaetzt).</summary>
    private static float CafeteriaNorthEdge(ShipStatus ship, Vector2 caf)
    {
        try
        {
            if (ship.AllRooms != null)
                foreach (var r in ship.AllRooms)
                    if (r && r.roomArea && r.roomArea.OverlapPoint(caf))
                        return r.roomArea.bounds.max.y;
        }
        catch { }
        return caf.y + 3.5f;
    }

    /// <summary>
    /// Oeffnet den Durchgang in der Cafeteria-Nordwand: An der Oberkante liegen ZWEI Wand-Linien ('Ground' bei y~6 und
    /// 'Room' bei y~7). Beide werden an der Tuerstelle aufgetrennt (oberes Segment -> zwei Teilstuecke mit DoorW-Luecke
    /// bei x=doorWorldX). Der Rest der Waende bleibt stehen.
    /// </summary>
    private static void CarveDoorway(ShipStatus ship, float doorWorldX)
    {
        int carved = 0;
        foreach (var col in ship.GetComponentsInChildren<Collider2D>(true))
        {
            if (!col || col.isTrigger || !col.enabled) continue;
            if (col.name != "Room" && col.name != "Ground") continue;
            if (!col.transform.parent || col.transform.parent.name != "Cafeteria") continue;
            var edge = col.TryCast<EdgeCollider2D>();
            if (edge != null && CarveEdge(edge, doorWorldX)) carved++;
        }
        Plugin.Logger.LogInfo($"Map-Erweiterung: {carved} Wand-Linie(n) an der Tuer (x={doorWorldX:0.0}) aufgetrennt.");
    }

    /// <summary>Eine Wand-Linie am obersten Segment, das die Tuer-x enthaelt, in zwei Teilstuecke mit Luecke zerlegen.</summary>
    private static bool CarveEdge(EdgeCollider2D edge, float doorWorldX)
    {
        var local = edge.points;
        int n = local.Length;
        if (n < 2) return false;

        var world = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 w = edge.transform.TransformPoint(new Vector3(local[i].x, local[i].y, 0f));
            world[i] = new Vector2(w.x, w.y);
        }

        // oberstes Segment, dessen x-Bereich die Tuer enthaelt
        float maxY = world.Max(p => p.y);
        int seg = -1; float bestY = float.MinValue;
        for (int i = 0; i < n - 1; i++)
        {
            float lo = Mathf.Min(world[i].x, world[i + 1].x), hi = Mathf.Max(world[i].x, world[i + 1].x);
            float segY = Mathf.Min(world[i].y, world[i + 1].y);
            if (world[i].y > maxY - 1.5f && world[i + 1].y > maxY - 1.5f && doorWorldX > lo + 0.2f && doorWorldX < hi - 0.2f && segY > bestY)
            { seg = i; bestY = segY; }
        }
        if (seg < 0) return false;

        Vector2 a = world[seg], b = world[seg + 1];
        float t = (doorWorldX - a.x) / (b.x - a.x);
        float yDoor = Mathf.Lerp(a.y, b.y, t);
        float half = DoorW / 2f;
        bool ascending = a.x < b.x;
        Vector2 leftW = new Vector2(doorWorldX - half, yDoor);
        Vector2 rightW = new Vector2(doorWorldX + half, yDoor);
        Vector2 nearA = ascending ? leftW : rightW;
        Vector2 nearB = ascending ? rightW : leftW;

        var chainA = new List<Vector2>();
        for (int i = 0; i <= seg; i++) chainA.Add(new Vector2(local[i].x, local[i].y));
        chainA.Add(ToLocal(edge, nearA));
        var chainB = new List<Vector2>();
        chainB.Add(ToLocal(edge, nearB));
        for (int i = seg + 1; i < n; i++) chainB.Add(new Vector2(local[i].x, local[i].y));

        var pieceA = edge.gameObject.AddComponent<EdgeCollider2D>();
        var pieceB = edge.gameObject.AddComponent<EdgeCollider2D>();
        pieceA.points = ToArray(chainA);
        pieceB.points = ToArray(chainB);
        edge.enabled = false;

        carvedOriginals.Add(edge);
        carvedPieces.Add(pieceA);
        carvedPieces.Add(pieceB);
        Plugin.Logger.LogInfo($"[MapExt] '{edge.name}' aufgetrennt bei y={yDoor:0.0}.");
        return true;
    }

    private static Vector2 ToLocal(Component c, Vector2 world)
    {
        Vector3 l = c.transform.InverseTransformPoint(new Vector3(world.x, world.y, 0f));
        return new Vector2(l.x, l.y);
    }

    private static Il2CppStructArray<Vector2> ToArray(List<Vector2> list)
    {
        var arr = new Il2CppStructArray<Vector2>(list.Count);
        for (int i = 0; i < list.Count; i++) arr[i] = list[i];
        return arr;
    }

    // ---------- echter Vent ----------
    private static void BuildVent()
    {
        var ship = ShipStatus.Instance;
        if (ship.AllVents == null || ship.AllVents.Count == 0) return;

        Vector2 caf = ship.MeetingSpawnCenter;
        // Ziel: naechster vorhandener Vent zur Cafeteria (mit dem wird verbunden)
        var target = ship.AllVents.ToArray()
            .Where(v => v)
            .OrderBy(v => Vector2.Distance(caf, v.transform.position))
            .FirstOrDefault();
        if (!target) return;

        // einen vorhandenen Vent klonen -> echtes Aussehen + Animation + Nutzbarkeit
        var src = target;
        var go = Object.Instantiate(src.gameObject, ship.transform);
        var vent = go.GetComponent<Vent>();
        if (!vent) { Object.Destroy(go); return; }

        Vector3 pos = new Vector3(roomCenter.x + 1.9f, roomCenter.y - 1.6f, src.transform.position.z); // unten rechts im Raum, gut erreichbar
        go.transform.position = pos;
        go.SetActive(true);

        int newId = ship.AllVents.ToArray().Where(v => v).Max(v => v.Id) + 1;
        vent.Id = newId;

        // gegenseitig verbinden: freien Nachbar-Slot am Ziel nutzen
        vent.Right = target;
        vent.Left = null;
        vent.Center = null;
        if (!target.Right) target.Right = vent;
        else if (!target.Center) target.Center = vent;
        else if (!target.Left) target.Left = vent;

        // in AllVents eintragen, damit Impostor den Vent betreten koennen
        var old = ship.AllVents;
        var arr = new Il2CppReferenceArray<Vent>(old.Count + 1);
        for (int i = 0; i < old.Count; i++) arr[i] = old[i];
        arr[old.Count] = vent;
        ship.AllVents = arr;

        addedVent = vent;
        linkedTo = target;
        objects.Add(go);
        Plugin.Logger.LogInfo($"Map-Erweiterung: Vent #{newId} mit Vent #{target.Id} verbunden.");
    }

    /// <summary>Diagnose (Weg A): vorhandene Skeld-Konsolen und markante Deko-Sprites ins Log, um echte Objekte zu klonen.</summary>
    private static void DiscoverProps()
    {
        var ship = ShipStatus.Instance;
        int n = 0;
        foreach (var con in ship.GetComponentsInChildren<Console>(true))
        {
            if (!con) continue;
            var p = con.transform.position;
            Plugin.Logger.LogInfo($"[MapExt-Prop] Console '{con.name}' pos=({p.x:0.0},{p.y:0.0}) parent='{(con.transform.parent ? con.transform.parent.name : "-")}'");
            if (++n > 80) break;
        }
        foreach (var sr in ship.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!sr || !sr.sprite) continue;
            string nm = sr.name.ToLower();
            if (nm.Contains("console") || nm.Contains("screen") || nm.Contains("table") || nm.Contains("panel")
                || nm.Contains("nav") || nm.Contains("admin") || nm.Contains("weapon") || nm.Contains("telescope")
                || nm.Contains("scope") || nm.Contains("task") || nm.Contains("monitor") || nm.Contains("map"))
            {
                var b = sr.bounds;
                Plugin.Logger.LogInfo($"[MapExt-Prop] SR '{sr.name}' pos=({b.center.x:0.0},{b.center.y:0.0}) size=({b.size.x:0.0},{b.size.y:0.0}) parent='{(sr.transform.parent ? sr.transform.parent.name : "-")}'");
            }
        }
        Plugin.Logger.LogInfo("[MapExt-Prop] Ende.");
    }

    private static void RemoveAddedVent()
    {
        var ship = ShipStatus.Instance;
        // Verbindung am Ziel wieder loesen
        if (linkedTo && addedVent)
        {
            if (linkedTo.Right == addedVent) linkedTo.Right = null;
            if (linkedTo.Center == addedVent) linkedTo.Center = null;
            if (linkedTo.Left == addedVent) linkedTo.Left = null;
        }
        // aus AllVents entfernen
        if (ship && addedVent && ship.AllVents != null)
        {
            var kept = ship.AllVents.ToArray().Where(v => v && v != addedVent).ToArray();
            var arr = new Il2CppReferenceArray<Vent>(kept.Length);
            for (int i = 0; i < kept.Length; i++) arr[i] = kept[i];
            ship.AllVents = arr;
        }
        addedVent = null;
        linkedTo = null;
    }

    // ---------- Helfer ----------
    private static void AddWall(Vector2 pos, Vector2 size, int layer)
    {
        var go = AddSprite(pos, size, new Color(0.15f, 0.17f, 0.20f), -40);
        go.layer = layer;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one; // Sprite ist 1x1 Einheit; die Skala macht die Groesse
    }

    private static GameObject AddSprite(Vector2 pos, Vector2 scale, Color color, int order)
    {
        var go = new GameObject("Spieleabend_MapExt");
        if (ShipStatus.Instance) go.transform.SetParent(ShipStatus.Instance.transform, true);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = FloorSprite();
        sr.color = color;
        sr.sortingOrder = order;
        objects.Add(go);
        return go;
    }

    /// <summary>Beliebiges Deko-Sprite (kein Collider), mit Drehung.</summary>
    private static GameObject AddArt(Sprite sprite, Vector2 pos, Vector2 scale, float angleDeg, Color color, int order)
    {
        var go = new GameObject("Spieleabend_MapExt");
        if (ShipStatus.Instance) go.transform.SetParent(ShipStatus.Instance.transform, true);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        objects.Add(go);
        return go;
    }

    /// <summary>Gedrehte Wand mit Collider (fuer das runde Achteck).</summary>
    private static void AddWallRot(Vector2 pos, float length, float thickness, float angleDeg, int layer)
    {
        var go = AddArt(FloorSprite(), pos, new Vector2(length, thickness), angleDeg, new Color(0.17f, 0.19f, 0.24f), -40);
        go.layer = layer;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
    }

    /// <summary>Echte Skeld-Objekte als Deko in den Raum klonen (reine Optik, keine Funktion).</summary>
    private static void BuildProps(Vector2 center)
    {
        // Ueberwachungs-Monitore an der oberen Wand = Beobachtungs-/Sternenkarten-Schirme
        CloneProp("SurvConsole", new Vector2(center.x, center.y + 1.9f), 1.2f);
        // Justier-/Ziel-Geraet = Teleskop in der Mitte
        CloneProp("AlignEngineConsole", new Vector2(center.x + 0.5f, center.y - 0.2f), 1.6f);
        // Kapitaenssitz als Aussichts-Sessel
        CloneProp("parasite_skeld_navigation_captainsChair", new Vector2(center.x - 1.9f, center.y - 1.1f), 1.0f);
        // Wand-Terminal links
        CloneProp("UploadDataConsole", new Vector2(center.x - 2.3f, center.y + 1.1f), 1.0f);
    }

    /// <summary>Ein vorhandenes Spiel-Objekt (per SpriteRenderer-Name) klonen und als reine Deko platzieren.</summary>
    private static GameObject CloneProp(string srName, Vector2 pos, float scale)
    {
        var ship = ShipStatus.Instance;
        Transform src = null;
        foreach (var sr in ship.GetComponentsInChildren<SpriteRenderer>(true))
            if (sr && sr.name == srName) { src = sr.transform; break; }
        if (!src) { Plugin.Logger.LogWarning($"Map-Erweiterung: Prop '{srName}' nicht gefunden."); return null; }

        var go = Object.Instantiate(src.gameObject, ship.transform);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = src.localScale * scale;
        go.SetActive(true);

        // Interaktion/Physik entfernen -> reine Optik
        foreach (var col in go.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;
        foreach (var con in go.GetComponentsInChildren<Console>(true)) Object.Destroy(con);
        foreach (var pb in go.GetComponentsInChildren<PassiveButton>(true)) Object.Destroy(pb);

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

    private static Sprite FloorSprite()
    {
        if (floorSprite) return floorSprite;
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        var px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        floorSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4); // 4 px / 4 ppu = 1 Einheit
        floorSprite.hideFlags = HideFlags.HideAndDontSave;
        return floorSprite;
    }

    /// <summary>Weiche weisse Kreisscheibe (1 Einheit Durchmesser bei Skala 1).</summary>
    private static Sprite DiscSprite()
    {
        if (discSprite) return discSprite;
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f;
            float a = d >= 1f ? 0f : Mathf.Clamp01((1f - d) / 0.06f); // weiche Kante
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        discSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        discSprite.hideFlags = HideFlags.HideAndDontSave;
        return discSprite;
    }

    /// <summary>Ring (heller Rand) fuer Raum-Trim und Rahmen.</summary>
    private static Sprite RingSprite()
    {
        if (ringSprite) return ringSprite;
        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f)) / 63.5f;
            float ring = Mathf.Exp(-Mathf.Pow((d - 0.93f) / 0.05f, 2f));
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, d > 1f ? 0f : Mathf.Clamp01(ring)));
        }
        tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        ringSprite.hideFlags = HideFlags.HideAndDontSave;
        return ringSprite;
    }

    /// <summary>Dunkles Weltall mit Sternen (Fenster-Hintergrund), deterministisch.</summary>
    private static Sprite StarfieldSprite()
    {
        if (starSprite) return starSprite;
        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
            tex.SetPixel(x, y, new Color(0.03f, 0.04f, 0.12f, 1f)); // tiefes Blau-Schwarz
        var rnd = new System.Random(1234);
        for (int i = 0; i < 90; i++)
        {
            int cx = rnd.Next(S), cy = rnd.Next(S);
            float b = 0.5f + (float)rnd.NextDouble() * 0.5f;
            tex.SetPixel(cx, cy, new Color(b, b, b, 1f));
            if (rnd.NextDouble() > 0.6) // ein paar hellere/groessere Sterne
            {
                var c2 = new Color(b, b, b, 0.6f);
                if (cx + 1 < S) tex.SetPixel(cx + 1, cy, c2);
                if (cy + 1 < S) tex.SetPixel(cx, cy + 1, c2);
            }
        }
        tex.Apply();
        starSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        starSprite.hideFlags = HideFlags.HideAndDontSave;
        return starSprite;
    }
}
