using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Core;

/// <summary>
/// Fügt (nur auf The Skeld) zusätzliche, einzeln im Menü schaltbare Vents ein:
///  - Cafeteria (oben rechts) ↔ O2 und Upper Engine
///  - Communications ↔ Shields und Admin
/// Die neuen Vents werden aus einem echten Spiel-Vent geklont (echtes Aussehen/Animation/Nutzbarkeit),
/// beidseitig mit den Ziel-Vents verbunden und in AllVents eingetragen. Alle Clients bauen deterministisch
/// dasselbe (gleiche Optionen -> gleiche Ids), daher kein Desync.
/// </summary>
internal static class ExtraVents
{
    private static readonly List<Vent> added = new();
    private static readonly List<(Vent Target, Vent V)> links = new();
    private static bool builtCaf, builtComms;
    private static System.IntPtr builtFor;

    public static void Reset()
    {
        RemoveAll();
        builtCaf = builtComms = false;
        builtFor = System.IntPtr.Zero;
    }

    public static void Update()
    {
        bool ok = ShipStatus.Instance && IsSkeld();
        bool wantCaf = ok && GeneralOptions.VentCafeteria != null && GeneralOptions.VentCafeteria.Bool;
        bool wantComms = ok && GeneralOptions.VentComms != null && GeneralOptions.VentComms.Bool;
        bool sameRound = ok && builtFor == ShipStatus.Instance.Pointer;

        if (sameRound && wantCaf == builtCaf && wantComms == builtComms) return; // nichts zu tun

        RemoveAll();
        builtCaf = builtComms = false;
        if (!ok) { builtFor = System.IntPtr.Zero; return; }

        builtFor = ShipStatus.Instance.Pointer;
        int nextId = MaxVentId() + 1;
        if (wantCaf) { try { BuildCafeteria(nextId); builtCaf = true; nextId++; } catch (System.Exception e) { Plugin.Logger.LogWarning($"Extra-Vent Cafeteria: {e.Message}"); } }
        if (wantComms) { try { BuildComms(nextId); builtComms = true; nextId++; } catch (System.Exception e) { Plugin.Logger.LogWarning($"Extra-Vent Comms: {e.Message}"); } }
    }

    private static bool IsSkeld()
    {
        try { return GameOptionsManager.Instance.CurrentGameOptions.MapId == 0; }
        catch { return false; }
    }

    private static int MaxVentId()
    {
        var ship = ShipStatus.Instance;
        if (ship == null || ship.AllVents == null) return 0;
        int max = 0;
        foreach (var v in ship.AllVents) if (v && v.Id > max) max = v.Id;
        return max;
    }

    // ---------- die beiden Vents ----------

    private static void BuildCafeteria(int id)
    {
        var room = Room(SystemTypes.Cafeteria);
        if (room == null || !room.roomArea) return;
        var b = room.roomArea.bounds;
        // nördlich des Tisch-Emblems auf dem freien Boden (nicht auf dem Tisch, nicht an der Wand)
        var pos = new Vector2(b.center.x + b.size.x * 0.05f, b.center.y + b.size.y * 0.30f);
        MakeVent(pos, id, VentNear(SystemTypes.LifeSupp), VentNear(SystemTypes.UpperEngine), "Cafeteria");
    }

    private static void BuildComms(int id)
    {
        var room = Room(SystemTypes.Comms);
        if (room == null || !room.roomArea) return;
        var pos = (Vector2)room.roomArea.bounds.center;
        MakeVent(pos, id, VentNear(SystemTypes.Shields), VentNear(SystemTypes.Admin), "Communications");
    }

    private static void MakeVent(Vector2 pos, int id, Vent a, Vent b, string label)
    {
        var ship = ShipStatus.Instance;
        var src = a ? a : (b ? b : FirstVent());
        if (!src) { Plugin.Logger.LogWarning($"Extra-Vent {label}: kein Quell-Vent gefunden."); return; }

        var go = Object.Instantiate(src.gameObject, ship.transform);
        var vent = go.GetComponent<Vent>();
        if (!vent) { Object.Destroy(go); return; }

        go.transform.position = new Vector3(pos.x, pos.y, src.transform.position.z);
        go.SetActive(true);
        vent.Id = id;

        // Verbindungen (beidseitig, damit man hin und zurück kann).
        vent.Right = a;
        vent.Left = b;
        vent.Center = null;
        AttachBack(a, vent);
        AttachBack(b, vent);

        AddToAllVents(vent);
        added.Add(vent);
        if (a) links.Add((a, vent));
        if (b) links.Add((b, vent));
        Plugin.Logger.LogInfo($"Extra-Vent {label} #{id} verbunden mit #{(a ? a.Id : -1)} und #{(b ? b.Id : -1)}.");
    }

    /// <summary>Das neue Vent in einen freien Nachbar-Slot des Ziels eintragen (für den Rückweg).</summary>
    private static void AttachBack(Vent target, Vent v)
    {
        if (!target) return;
        if (!target.Right) target.Right = v;
        else if (!target.Center) target.Center = v;
        else if (!target.Left) target.Left = v;
    }

    private static void AddToAllVents(Vent vent)
    {
        var ship = ShipStatus.Instance;
        var old = ship.AllVents;
        var arr = new Il2CppReferenceArray<Vent>(old.Count + 1);
        for (int i = 0; i < old.Count; i++) arr[i] = old[i];
        arr[old.Count] = vent;
        ship.AllVents = arr;
    }

    // ---------- Aufräumen ----------

    private static void RemoveAll()
    {
        var ship = ShipStatus.Instance;

        // Rückwege an den Zielen lösen
        foreach (var (target, v) in links)
        {
            if (!target) continue;
            if (target.Right == v) target.Right = null;
            if (target.Center == v) target.Center = null;
            if (target.Left == v) target.Left = null;
        }
        links.Clear();

        // aus AllVents entfernen
        if (ship && ship.AllVents != null && added.Count > 0)
        {
            var kept = ship.AllVents.ToArray().Where(v => v && !added.Contains(v)).ToArray();
            var arr = new Il2CppReferenceArray<Vent>(kept.Length);
            for (int i = 0; i < kept.Length; i++) arr[i] = kept[i];
            ship.AllVents = arr;
        }

        foreach (var v in added) if (v) Object.Destroy(v.gameObject);
        added.Clear();
    }

    // ---------- Helfer ----------

    private static PlainShipRoom Room(SystemTypes id)
    {
        var ship = ShipStatus.Instance;
        if (ship != null && ship.AllRooms != null)
            foreach (var r in ship.AllRooms)
                if (r && r.RoomId == id) return r;
        return null;
    }

    /// <summary>Vent in diesem Raum, sonst der nächstgelegene vorhandene Vent zur Raummitte.</summary>
    private static Vent VentNear(SystemTypes id)
    {
        var ship = ShipStatus.Instance;
        if (ship == null || ship.AllVents == null) return null;
        var room = Room(id);
        if (room != null && room.roomArea)
        {
            foreach (var v in ship.AllVents)
                if (v && !added.Contains(v) && room.roomArea.OverlapPoint((Vector2)v.transform.position)) return v;
            Vector2 c = room.roomArea.bounds.center;
            return ship.AllVents.ToArray().Where(v => v && !added.Contains(v))
                .OrderBy(v => Vector2.Distance(c, (Vector2)v.transform.position)).FirstOrDefault();
        }
        return null;
    }

    private static Vent FirstVent()
    {
        var ship = ShipStatus.Instance;
        if (ship != null && ship.AllVents != null)
            foreach (var v in ship.AllVents) if (v && !added.Contains(v)) return v;
        return null;
    }
}
