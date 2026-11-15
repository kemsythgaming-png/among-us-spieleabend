using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Wiederverwendbare Karten-Auswahl: öffnet die echte Among-Us-Karte, der Spieler steht still und klickt auf eine
/// Stelle - der Welt-Punkt wird zurückgegeben (ohne Teleport). Jede Rolle hat eine eigene Instanz.
/// Rechtsklick bricht ab. Genutzt von Mörser (Zielpunkt) und Deaktivator (Raum).
/// </summary>
internal sealed class MapPicker
{
    private bool active;
    private float openedAt;
    private float mapScale = 4.4f;

    public bool Active => active;

    public bool Open()
    {
        var local = PlayerControl.LocalPlayer;
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
        catch (System.Exception e) { Plugin.Logger.LogWarning($"Karten-Auswahl: {e.Message}"); active = false; return false; }
    }

    public void Cancel()
    {
        active = false;
        if (MapBehaviour.Instance) try { MapBehaviour.Instance.Close(); } catch { }
    }

    /// <summary>Jedes Frame aufrufen. true = eine Stelle wurde angeklickt (world gesetzt); die Karte schließt dann.</summary>
    public bool Poll(out Vector2 world)
    {
        world = default;
        if (!active) return false;
        var local = PlayerControl.LocalPlayer;
        var map = MapBehaviour.Instance;
        if (!local || !CustomRole.IsAlive(local) || CustomRole.InMeeting || !map || !map.isActiveAndEnabled || !map.HerePoint)
        {
            active = false;
            return false;
        }

        var frame = map.HerePoint.transform.parent != null ? map.HerePoint.transform.parent : map.transform;
        Vector3 here = map.HerePoint.transform.localPosition;
        float hm = new Vector2(here.x, here.y).magnitude;
        if (hm > 0.6f) mapScale = local.GetTruePosition().magnitude / hm;

        if (Time.time - openedAt < 0.3f) return false;

        if (Input.GetMouseButtonDown(1)) { Cancel(); return false; }

        if (Input.GetMouseButtonDown(0) && Camera.main)
        {
            float z = Mathf.Abs(Camera.main.transform.position.z - frame.position.z);
            Vector3 onPlane = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, z));
            Vector3 clickLocal = frame.InverseTransformPoint(onPlane);
            world = new Vector2(clickLocal.x, clickLocal.y) * mapScale;
            active = false;
            try { map.Close(); } catch { }
            return true;
        }
        return false;
    }
}
