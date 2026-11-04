using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Core;

/// <summary>
/// Hängt die mobile Kamera des Kameramanns in JEDE Überwachung ein - egal ob über den Rollen-Knopf oder die
/// echte Security-Konsole geöffnet. Statt des 4er-Rasters zeigt die Konsole dann EIN großes Bild, durch das man
/// mit den Pfeilen blättert (inkl. der mobilen Kamera). Der mobile Feed nutzt das echte Kamera-Prefab des Spiels,
/// rendert also Spieler korrekt. Nur aktiv, solange eine mobile Kamera gesetzt ist (sonst bleibt alles vanilla).
/// </summary>
public static class SurveillanceFeeds
{
    // Spielt ein Kameramann mit? (auf allen Clients gesetzt) -> Standard-Konsole zeigt dann 1 Bild + Pfeile.
    public static bool Present { get; private set; }
    // von allen Clients gesetzt, sobald der Kameramann eine Kamera platziert
    public static bool MobileActive { get; private set; }
    private static Vector2 mobilePos;

    public static void SetPresent(bool value)
    {
        Present = value;
        if (!value) ClearMobile();
    }

    private static SurveillanceMinigame surv;
    private static bool pending;
    private static float pendingAt;
    private static bool active;

    private static readonly List<Texture> feeds = new();
    private static int shownIndex;
    private static int mobileIndex = -1;
    private static MeshRenderer viewPort;
    private static Material viewMat;
    private static readonly List<GameObject> renderGOs = new();
    private static readonly List<RenderTexture> rts = new();

    private static GUIStyle arrowStyle, feedLabel;

    // Blink-Steuerung des Welt-Symbols: nur blinken, wenn jemand die Überwachung nutzt.
    private static SurvCamera markerCam;
    private static Spieleabend.Roles.CamBlink markerBlink;
    private static bool lastWatched;

    public static void Place(Vector2 pos)
    {
        mobilePos = pos;
        MobileActive = true;
        markerCam = null;
        markerBlink = null;
        lastWatched = false;
    }

    public static void SetMarker(SurvCamera cam) { markerCam = cam; if (cam) try { cam.SetAnimation(false); } catch { } }
    public static void SetMarker(Spieleabend.Roles.CamBlink blink) { markerBlink = blink; if (blink) blink.Active = false; }

    public static void ClearMobile()
    {
        MobileActive = false;
        markerCam = null;
        markerBlink = null;
        Stop();
    }

    /// <summary>Nutzt gerade jemand die Überwachung? (networked über das Security-System, plus lokal geöffnet)</summary>
    private static bool AnyoneWatching()
    {
        if (Minigame.Instance && Minigame.Instance.TryCast<SurveillanceMinigame>() != null) return true;
        var ship = ShipStatus.Instance;
        if (!ship) return false;
        try
        {
            var sys = ship.Systems[SystemTypes.Security];
            var sec = sys?.TryCast<SecurityCameraSystemType>();
            return sec != null && sec.InUse;
        }
        catch { return false; }
    }

    private static void UpdateBlink()
    {
        if (!MobileActive) return;
        bool now = AnyoneWatching();
        if (now == lastWatched) return;
        lastWatched = now;
        if (markerCam) try { markerCam.SetAnimation(now); } catch { }
        if (markerBlink) markerBlink.Active = now;
    }

    /// <summary>Harmony-Postfix von SurveillanceMinigame.Begin ruft das für jede geöffnete Überwachung auf.</summary>
    public static void OnBegin(SurveillanceMinigame mg)
    {
        if (!Present || mg == null) return;   // nur wenn ein Kameramann mitspielt
        surv = mg;
        pending = true;
        pendingAt = Time.time;
        active = false;
    }

    /// <summary>Wird aus SettingsWindow.Update jedes Frame gerufen.</summary>
    public static void Tick()
    {
        if (pending && surv && Time.time - pendingAt > 0.15f) { DoSetup(); pending = false; }
        if (active && (!surv || Minigame.Instance == null)) Stop();
        UpdateBlink();
    }

    private static void DoSetup()
    {
        try
        {
            var ports = surv.ViewPorts;
            var texs = surv.textures;
            if (ports == null || ports.Count == 0) return;

            feeds.Clear();
            mobileIndex = -1;
            ClearRenders();

            // Alle echten Kameras scharf neu rendern (eigene RenderTexture pro Kamera - wie die mobile)
            var ship0 = ShipStatus.Instance;
            if (ship0 != null && ship0.AllCameras != null)
                foreach (var cam in ship0.AllCameras)
                    if (cam) { var rt = CreateFeed(cam.transform.position); if (rt) feeds.Add(rt); }

            if (MobileActive)
            {
                var mrt = CreateFeed(mobilePos);
                if (mrt) { feeds.Add(mrt); mobileIndex = feeds.Count - 1; }
            }

            // Fallback, falls kein eigenes Rendern möglich war: die Vanilla-Texturen
            if (feeds.Count == 0)
                for (int i = 0; i < ports.Count; i++)
                {
                    Texture t = (texs != null && i < texs.Count) ? texs[i] : null;
                    if (!t && ports[i] && ports[i].material) t = ports[i].material.mainTexture;
                    feeds.Add(t);
                }

            // Mitte der Viewports bestimmen, Viewport 0 groß dorthin, Rest ausblenden.
            Vector3 center = Vector3.zero; int cnt = 0;
            for (int i = 0; i < ports.Count; i++) if (ports[i]) { center += ports[i].transform.localPosition; cnt++; }
            if (cnt > 0) center /= cnt;

            viewPort = ports[0];
            for (int i = 1; i < ports.Count; i++) if (ports[i]) ports[i].gameObject.SetActive(false);
            if (viewPort)
            {
                viewPort.transform.localPosition = center;
                viewPort.transform.localScale *= 2.0f; // Bild etwas größer, aber Monitor-Rahmen bleibt sichtbar
                viewMat = viewPort.material;
            }

            surv.enabled = false; // Update einfrieren, damit es den Viewport-Inhalt nicht zurücksetzt

            shownIndex = mobileIndex >= 0 ? mobileIndex : 0;
            ApplyFeed();
            active = true;
        }
        catch (System.Exception e) { Plugin.Logger.LogWarning($"[Cam] Setup: {e.Message}"); }
    }

    /// <summary>Eigene scharfe Render-Kamera an einer Position; gibt die RenderTexture zurück.</summary>
    private static RenderTexture CreateFeed(Vector2 pos)
    {
        try
        {
            if (surv == null || !surv.CameraPrefab) return null;
            var clone = Object.Instantiate(surv.CameraPrefab);
            clone.gameObject.SetActive(true);
            clone.enabled = true;
            clone.orthographic = true;
            clone.transform.position = new Vector3(pos.x, pos.y, -50f);
            clone.farClipPlane = Mathf.Max(clone.farClipPlane, 1000f);
            var rt = new RenderTexture(640, 640, 16) { filterMode = FilterMode.Bilinear, antiAliasing = 2, hideFlags = HideFlags.HideAndDontSave };
            rt.Create();
            clone.targetTexture = rt;
            renderGOs.Add(clone.gameObject);
            rts.Add(rt);
            return rt;
        }
        catch (System.Exception e) { Plugin.Logger.LogWarning($"[Cam] Feed: {e.Message}"); return null; }
    }

    private static void ClearRenders()
    {
        foreach (var go in renderGOs) if (go) Object.Destroy(go);
        renderGOs.Clear();
        foreach (var rt in rts) if (rt) { rt.Release(); Object.Destroy(rt); }
        rts.Clear();
    }

    private static void ShowFeed(int delta)
    {
        if (feeds.Count == 0) return;
        shownIndex = ((shownIndex + delta) % feeds.Count + feeds.Count) % feeds.Count;
        ApplyFeed();
        Sfx.Play(Sfx.TaskUpdate, 0.6f);
    }

    private static void ApplyFeed()
    {
        if (!viewPort || feeds.Count == 0) return;
        var mat = viewMat ? viewMat : viewPort.material;
        if (mat) mat.mainTexture = feeds[shownIndex];
    }

    /// <summary>Wird aus SettingsWindow.OnGUI gezeichnet (für JEDEN Zuschauer).</summary>
    public static void Draw()
    {
        if (!active || feeds.Count <= 1) return;

        arrowStyle ??= new GUIStyle(Ui.ButtonStyle) { fontSize = 120, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        feedLabel ??= new GUIStyle(Ui.Title) { alignment = TextAnchor.MiddleCenter, fontSize = 30 };

        float w = Ui.ScreenWidth, cy = 540f;
        var leftR = new Rect(40f, cy - 170f, 150f, 340f);
        var rightR = new Rect(w - 190f, cy - 170f, 150f, 340f);

        Ui.Block(leftR); Ui.Block(rightR);
        if (GUI.Button(leftR, new GUIContent("‹"), arrowStyle)) ShowFeed(-1);
        if (GUI.Button(rightR, new GUIContent("›"), arrowStyle)) ShowFeed(1);

        string name = shownIndex == mobileIndex
            ? Lang.T("📹 Mobile Kamera", "📹 Mobile camera")
            : $"{Lang.T("Kamera", "Camera")} {shownIndex + 1}";
        GUI.Label(new Rect(w / 2f - 200f, cy + 190f, 400f, 44f), $"<color=#9FE0FF>{name}</color>", feedLabel);
    }

    private static void Stop()
    {
        active = false;
        pending = false;
        surv = null;
        viewPort = null;
        viewMat = null;
        feeds.Clear();
        mobileIndex = -1;
        ClearRenders();
    }
}

[HarmonyPatch(typeof(SurveillanceMinigame), nameof(SurveillanceMinigame.Begin))]
internal static class SurveillanceMinigame_Begin_Patch
{
    public static void Postfix(SurveillanceMinigame __instance)
    {
        try { SurveillanceFeeds.OnBegin(__instance); }
        catch (System.Exception e) { Plugin.Logger.LogWarning($"[Cam] Begin-Patch: {e.Message}"); }
    }
}
