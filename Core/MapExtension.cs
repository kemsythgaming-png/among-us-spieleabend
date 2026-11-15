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
    private static readonly List<SpriteRenderer> hiddenArt = new();        // Cafeteria-Wand-/Fenster-Grafik an der Tür (versteckt, solange offen)
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

        // versteckte Wand-/Fenster-Grafik wieder einblenden
        foreach (var sr in hiddenArt) if (sr) sr.enabled = true;
        hiddenArt.Clear();

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
    private static readonly Color FloorCol = new Color(0.27f, 0.31f, 0.37f);
    private static readonly Color TrimCol = new Color(0.55f, 0.62f, 0.72f);

    /// <summary>
    /// Baut ein sauberes, begehbares „Aussichtsdeck" direkt nördlich der Cafeteria: eigener Panel-Boden, heller Rahmen,
    /// Sternen-Fenster oben, eine BREITE Öffnung zur Cafeteria (kein enger Gang mehr) und ein echter Vent.
    /// </summary>
    private static void BuildRoom()
    {
        var ship = ShipStatus.Instance;
        Vector2 caf = ship.MeetingSpawnCenter;
        int wallLayer = FindWallLayer(ship);

        float cx = caf.x;
        // ZUERST die Cafeteria-Nordwand auftrennen und die echte Tür-Y als Anker nehmen (sonst schwebt der Raum).
        float doorY = CarveDoorway(ship, cx);
        float northY = !float.IsNaN(doorY) ? doorY : CafeteriaNorthEdge(ship, caf);
        // gemalte Cafeteria-Wand/Fenster an der Tür ausblenden (sonst steht die Scheibe wieder im Durchgang)
        HideWallArtAtDoor(ship, cx, northY);
        const float W = 9f, H = 5.2f;
        float bottom = northY + 0.1f;      // Raumboden knapp über der Cafeteria-Nordkante (praktisch bündig)
        float top = bottom + H;
        float left = cx - W / 2f, right = cx + W / 2f;
        float midY = (bottom + top) / 2f;
        float gap = DoorW / 2f;
        roomCenter = new Vector2(cx, midY);

        // --- Boden: dunkle Grundfläche + Panel-Kacheln drüber (ordentliche Optik) ---
        AddArt(FloorSprite(), new Vector2(cx, midY), new Vector2(W, H), 0f, FloorCol, -50);
        TileFloor(left, bottom, W, H, -49);

        // kurze, breite Verbindung zur Cafeteria (nur Wanddicke überbrücken - kein langer Gang)
        float neckBottom = northY - 0.9f;
        AddArt(FloorSprite(), new Vector2(cx, (neckBottom + bottom) / 2f), new Vector2(DoorW, bottom - neckBottom), 0f, FloorCol, -50);
        TileFloor(cx - gap, neckBottom, DoorW, bottom - neckBottom, -49);

        // --- Rahmen (heller Trim) rundum ---
        AddFrame(cx, midY, W, H);
        // Sternen-Fenster oben an der Wand
        AddArt(StarfieldSprite(), new Vector2(cx, top - 0.65f), new Vector2(W - 1.4f, 1.1f), 0f, Color.white, -48);
        AddArt(RingSprite(), new Vector2(cx, top - 0.65f), new Vector2(W - 1.2f, 1.3f), 0f, TrimCol, -47); // Fensterrahmen

        // --- unsichtbare Kollision: oben/links/rechts, unten mit breiter Tür-Lücke ---
        AddWallInv(new Vector2(cx, top + T / 2f), new Vector2(W + T, T), wallLayer);        // oben
        AddWallInv(new Vector2(left - T / 2f, midY), new Vector2(T, H), wallLayer);          // links
        AddWallInv(new Vector2(right + T / 2f, midY), new Vector2(T, H), wallLayer);         // rechts
        float lw = (cx - gap) - left;
        if (lw > 0.1f) AddWallInv(new Vector2((left + cx - gap) / 2f, bottom - T / 2f), new Vector2(lw, T), wallLayer);
        float rw = right - (cx + gap);
        if (rw > 0.1f) AddWallInv(new Vector2((cx + gap + right) / 2f, bottom - T / 2f), new Vector2(rw, T), wallLayer);
        // kurze Seitenwände der Öffnung (von der Cafeteria hoch)
        float neckH = bottom - neckBottom;
        AddWallInv(new Vector2(cx - gap, (neckBottom + bottom) / 2f), new Vector2(T, neckH), wallLayer);
        AddWallInv(new Vector2(cx + gap, (neckBottom + bottom) / 2f), new Vector2(T, neckH), wallLayer);

        // --- Cafeteria-Nordwand an der Tür auftrennen + gemalte Wand überdecken ---
        // (Cafeteria-Wand ist schon oben aufgetrennt.)
        // Naht/Boden im Durchgang: NEGATIVE Order -> immer HINTER dem Spieler (er wird nicht mehr verdeckt).
        AddArt(FloorSprite(), new Vector2(cx, northY - 0.5f), new Vector2(DoorW - 0.1f, 2.0f), 0f, FloorCol, -30);
        TileFloor(cx - gap + 0.05f, northY - 1.5f, DoorW - 0.1f, 2.0f, -29);

        // --- Props + Türschwellen-Trim ---
        AddArt(RingSprite(), new Vector2(cx, bottom + 0.05f), new Vector2(DoorW + 0.6f, 0.5f), 0f, TrimCol, -46); // Schwelle
        BuildProps(roomCenter);
        try { AddRoomTask(new Vector2(cx + 1.6f, midY - 0.3f)); } catch (System.Exception e) { Plugin.Logger.LogWarning($"Map-Erweiterung Aufgabe: {e.Message}"); }
    }

    /// <summary>Eine ECHTE Skeld-Aufgabe in den Raum setzen: die "Align Engine Output"-Konsole funktionsfähig klonen (zweiter Ort für diese Aufgabe).</summary>
    private static void AddRoomTask(Vector2 pos)
    {
        var ship = ShipStatus.Instance;
        Console src = null;
        foreach (var c in ship.GetComponentsInChildren<Console>(true))
        {
            if (!c || c.TaskTypes == null) continue;
            foreach (var tt in c.TaskTypes)
                if (tt == TaskTypes.AlignEngineOutput) { src = c; break; }
            if (src) break;
        }
        if (!src) { Plugin.Logger.LogWarning("Map-Erweiterung: keine Align-Engine-Konsole gefunden."); return; }

        var go = Object.Instantiate(src.gameObject, ship.transform); // mit Console + Collider + Sprite (funktionsfähig)
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        go.SetActive(true);
        var con = go.GetComponent<Console>();
        if (con) con.Room = SystemTypes.Cafeteria; // gehört jetzt zum Cafeteria-Bereich (an den der Raum angebunden ist)
        objects.Add(go);
        Plugin.Logger.LogInfo("Map-Erweiterung: Align-Engine-Aufgabe im Raum platziert.");
    }

    private const float T = 0.5f, DoorW = 4f; // breite Öffnung statt engem Gang

    /// <summary>Panel-Kacheln (1x1) als Boden-Optik über eine Fläche legen - sieht ordentlich aus wie Schiffsboden.</summary>
    private static void TileFloor(float x0, float y0, float w, float h, int order)
    {
        int cols = Mathf.Max(1, Mathf.CeilToInt(w));
        int rows = Mathf.Max(1, Mathf.CeilToInt(h));
        float tw = w / cols, th = h / rows;
        for (int i = 0; i < cols; i++)
        for (int j = 0; j < rows; j++)
        {
            float cxp = x0 + (i + 0.5f) * tw;
            float cyp = y0 + (j + 0.5f) * th;
            AddArt(PanelSprite(), new Vector2(cxp, cyp), new Vector2(tw, th), 0f, FloorCol, order);
        }
    }

    /// <summary>Heller Rahmen (vier dünne Leisten) um den Raum.</summary>
    private static void AddFrame(float cx, float cy, float w, float h)
    {
        float t = 0.18f;
        AddArt(FloorSprite(), new Vector2(cx, cy + h / 2f - t / 2f), new Vector2(w, t), 0f, TrimCol, -47); // oben
        AddArt(FloorSprite(), new Vector2(cx, cy - h / 2f + t / 2f), new Vector2(w, t), 0f, TrimCol, -47); // unten
        AddArt(FloorSprite(), new Vector2(cx - w / 2f + t / 2f, cy), new Vector2(t, h), 0f, TrimCol, -47); // links
        AddArt(FloorSprite(), new Vector2(cx + w / 2f - t / 2f, cy), new Vector2(t, h), 0f, TrimCol, -47); // rechts
    }

    private static Sprite panelSprite;
    /// <summary>Boden-Panel (1 Einheit): heller Kern, dunkler Rand -> beim Kacheln entstehen saubere Plattenfugen.</summary>
    private static Sprite PanelSprite()
    {
        if (panelSprite) return panelSprite;
        const int S = 32;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            bool edge = x < 2 || y < 2 || x > S - 3 || y > S - 3;
            float v = edge ? 0.72f : 1f; // Rand etwas dunkler
            tex.SetPixel(x, y, new Color(v, v, v, 1f));
        }
        tex.Apply();
        panelSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        panelSprite.hideFlags = HideFlags.HideAndDontSave;
        return panelSprite;
    }

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
    /// <summary>Trennt die Cafeteria-Nordwand an der Tür auf und gibt die (höchste) Tür-Y zurück (NaN = nichts aufgetrennt).</summary>
    private static float CarveDoorway(ShipStatus ship, float doorWorldX)
    {
        int carved = 0;
        float bestY = float.NaN;
        foreach (var col in ship.GetComponentsInChildren<Collider2D>(true))
        {
            if (!col || col.isTrigger || !col.enabled) continue;
            if (col.name != "Room" && col.name != "Ground") continue;
            if (!col.transform.parent || col.transform.parent.name != "Cafeteria") continue;
            var edge = col.TryCast<EdgeCollider2D>();
            if (edge == null) continue;
            float y = CarveEdge(edge, doorWorldX);
            if (!float.IsNaN(y)) { carved++; if (float.IsNaN(bestY) || y > bestY) bestY = y; }
        }
        Plugin.Logger.LogInfo($"Map-Erweiterung: {carved} Wand-Linie(n) an der Tuer (x={doorWorldX:0.0}) aufgetrennt, y={bestY:0.0}.");
        return bestY;
    }

    /// <summary>Eine Wand-Linie am obersten Segment, das die Tuer-x enthaelt, in zwei Teilstuecke mit Luecke zerlegen. Gibt die Tür-Y zurück (NaN = nicht aufgetrennt).</summary>
    private static float CarveEdge(EdgeCollider2D edge, float doorWorldX)
    {
        var local = edge.points;
        int n = local.Length;
        if (n < 2) return float.NaN;

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
        if (seg < 0) return float.NaN;

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
        return yDoor;
    }

    /// <summary>Blendet die gemalten Cafeteria-Wand-/Fenster-Sprites aus, die im Tür-Durchgang liegen (nur schmale Teile, nicht den ganzen Raum-Boden).</summary>
    private static void HideWallArtAtDoor(ShipStatus ship, float cx, float northY)
    {
        float half = DoorW / 2f + 0.4f;
        int hidden = 0;
        foreach (var sr in ship.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!sr || !sr.sprite || !sr.enabled) continue;
            if (sr.name.StartsWith("Spieleabend")) continue;
            if (!IsUnderCafeteria(sr.transform)) continue;
            var b = sr.bounds;
            if (b.size.x > 6f || b.size.y > 6f) continue;        // große Flächen (Boden/ganze Wand) in Ruhe lassen
            bool xOverlap = Mathf.Abs(b.center.x - cx) <= half + b.size.x / 2f;
            bool yNearWall = b.max.y >= northY - 0.4f && b.min.y <= northY + 2.2f;
            if (!xOverlap || !yNearWall) continue;
            sr.enabled = false;
            hiddenArt.Add(sr);
            hidden++;
        }
        Plugin.Logger.LogInfo($"Map-Erweiterung: {hidden} Wand-/Fenster-Sprite(s) im Durchgang ausgeblendet.");
    }

    private static bool IsUnderCafeteria(Transform t)
    {
        for (var p = t; p; p = p.parent)
            if (p.name == "Cafeteria") return true;
        return false;
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
        // (Die Align-Engine-Konsole kommt als ECHTE Aufgabe separat - siehe AddRoomTask.)
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
