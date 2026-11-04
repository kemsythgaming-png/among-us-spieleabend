using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Gemeinsame Karten-Teleport-Logik: echte Among-Us-Karte öffnen, auf eine Stelle klicken und dort landen
/// (nie in einer Wand - auf dem Vent des Raums, O2/Comms Mitte, Storage unten). Genutzt vom Teleporter
/// und vom Gambler-Teleport. Nur für den lokalen Spieler.
/// </summary>
internal static class MapTeleport
{
    private static bool active;
    private static float openedAt;
    private static float mapScale = 4.4f; // Karten-Rahmen -> Welt, wird laufend aus dem HerePoint gelernt

    public static bool Active => active;

    /// <summary>Karte öffnen. false = ging nicht (Meeting, tot, keine Karte).</summary>
    public static bool Open()
    {
        var local = PlayerControl.LocalPlayer;
        // MapBehaviour.Instance ist beim ALLERERSTEN Mal noch null (Karte nie geöffnet) - ToggleMapVisible erzeugt sie.
        if (CustomRole.InMeeting || !CustomRole.IsAlive(local) || !HudManager.InstanceExists) return false;
        try
        {
            HudManager.Instance.ToggleMapVisible(new MapOptions
            {
                Mode = MapOptions.Modes.Normal,
                AllowMovementWhileMapOpen = false,
                ShowLivePlayerPosition = true,
            });
            active = true;
            openedAt = Time.time;
            return true;
        }
        catch (System.Exception e) { Plugin.Logger.LogWarning($"Teleport-Karte: {e.Message}"); active = false; return false; }
    }

    public static void Cancel()
    {
        active = false;
        if (MapBehaviour.Instance) try { MapBehaviour.Instance.Close(); } catch { }
    }

    /// <summary>Jedes Frame aufrufen, solange aktiv. true = es wurde teleportiert (Aufrufer startet dann z. B. seinen Cooldown).</summary>
    public static bool Update()
    {
        if (!active) return false;
        var local = PlayerControl.LocalPlayer;
        var map = MapBehaviour.Instance;
        if (!local || !CustomRole.IsAlive(local) || CustomRole.InMeeting || !map || !map.isActiveAndEnabled || !map.HerePoint)
        {
            active = false;
            return false;
        }

        // Der HerePoint liegt in SEINEM Eltern-Transform; genau in diesem Rahmen rechnen wir den Klick zurück.
        var frame = map.HerePoint.transform.parent != null ? map.HerePoint.transform.parent : map.transform;
        Vector3 here = map.HerePoint.transform.localPosition;
        float hm = new Vector2(here.x, here.y).magnitude;
        if (hm > 0.6f) mapScale = local.GetTruePosition().magnitude / hm;

        if (Time.time - openedAt < 0.3f) return false;

        if (Input.GetMouseButtonDown(0) && Camera.main)
        {
            float z = Mathf.Abs(Camera.main.transform.position.z - frame.position.z);
            Vector3 onPlane = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, z));
            Vector3 clickLocal = frame.InverseTransformPoint(onPlane);
            var target = new Vector2(clickLocal.x, clickLocal.y) * mapScale;
            Teleport(local, target);
            active = false;
            try { map.Close(); } catch { }
            return true;
        }
        return false;
    }

    private static void Teleport(PlayerControl local, Vector2 clicked)
    {
        var room = RoomAt(clicked) ?? NearestRoom(clicked);
        Vector2 land = clicked;
        if (room != null && room.roomArea)
        {
            var vent = VentInRoom(room);
            if (vent) land = (Vector2)(vent.transform.position + vent.Offset);           // Raum mit Vent -> auf das Vent
            else if (room.RoomId == SystemTypes.Storage)                                  // Storage -> unten
            {
                var b = room.roomArea.bounds;
                land = new Vector2(b.center.x, b.min.y + 1.5f);
            }
            else land = room.roomArea.bounds.center;                                       // O2, Communications ... -> Mitte
        }

        if (local.NetTransform) local.NetTransform.RpcSnapTo(land);
        else
        {
            local.transform.position = new Vector3(land.x, land.y, local.transform.position.z);
            if (local.MyPhysics && local.MyPhysics.body) local.MyPhysics.body.velocity = Vector2.zero;
        }
        Sfx.Play(Sfx.Shapeshift, 0.8f);
    }

    private static PlainShipRoom RoomAt(Vector2 p)
    {
        var ship = ShipStatus.Instance;
        if (ship != null && ship.AllRooms != null)
            foreach (var r in ship.AllRooms)
                if (r && r.roomArea && r.roomArea.OverlapPoint(p)) return r;
        return null;
    }

    private static PlainShipRoom NearestRoom(Vector2 near)
    {
        var ship = ShipStatus.Instance;
        PlainShipRoom best = null;
        float bestD = float.MaxValue;
        if (ship != null && ship.AllRooms != null)
            foreach (var r in ship.AllRooms)
            {
                if (!r || !r.roomArea) continue;
                float d = ((Vector2)r.roomArea.bounds.center - near).sqrMagnitude;
                if (d < bestD) { bestD = d; best = r; }
            }
        return best;
    }

    private static Vent VentInRoom(PlainShipRoom room)
    {
        var ship = ShipStatus.Instance;
        if (ship == null || ship.AllVents == null || !room.roomArea) return null;
        foreach (var v in ship.AllVents)
            if (v && room.roomArea.OverlapPoint((Vector2)(v.transform.position + v.Offset))) return v;
        return null;
    }
}
