using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Crew: setzt eine mobile Kamera (1 pro Runde, bis zum nächsten Meeting), für alle sichtbar und blinkend.
/// Die Kamera hängt sich in JEDE Überwachung ein - über den eigenen Knopf und über die echte Security-Konsole
/// (siehe <see cref="SurveillanceFeeds"/>). Mit dem Knopf öffnet der Kameramann die Überwachung von überall;
/// seine Zuschau-Zeit ist pro Runde begrenzt und wird ihm angezeigt.
/// </summary>
public sealed class Cameraman : CustomRole
{
    public override string Name => Lang.T("Kameramann", "Cameraman");
    public override Color Color => new Color32(60, 70, 90, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Du hast alles im Blick", "You see everything");
    public override string Description => Lang.T("Setz eine Kamera (F),\nöffne die Überwachung (C).", "Place a camera (F),\nopen surveillance (C).");
    public override string LongDescription => Lang.T(
        "Du bist Crew. Mit Taste F setzt du an deiner Stelle eine mobile Kamera - eine pro Runde (bis zum nächsten Meeting), für alle sichtbar; sie blinkt, wenn jemand zuschaut. Sie erscheint auch in der normalen Security-Konsole. Mit Taste C öffnest du die Überwachung von überall und blätterst mit den Pfeilen durch die Kameras, auch deine mobile. Du kannst die Überwachung pro Runde mehrmals öffnen (Anzahl am Knopf), jeweils so lange du willst. Beim Zuschauen stehst du still.",
        "You are crew. Press F to drop a mobile camera - one per round (until the next meeting), visible to all; it blinks while someone is watching. It also shows up in the normal security console. Press C to open surveillance from anywhere and flip through the cameras with the arrows, including your mobile one. You can open surveillance several times per round (count on the button), each for as long as you like. You stand still while watching.");

    private const byte ActionPlace = 0;

    private RoleOption usesPerRound;
    private RoleButton survButton;

    private Vector2 camPos;
    private GameObject camObj;
    private static Sprite camSprite;

    private bool watching;
    private bool placedThisRound;
    private int usesLeft;

    protected override void Setup()
    {
        usesPerRound = AddOption("Uses", "Nutzungen pro Runde|Uses per round", 5f, 1f, 15f, 1f);

        AddButton(new RoleButton
        {
            Label = "Kamera setzen|Place camera",
            Icon = "radar",
            GameIcon = _ => RealCamSprite(), // echte In-Game-Kamera
            NeedsTarget = false,
            Cooldown = () => 1f,
            StartCooldown = 5f,
            Hotkey = KeyCode.F,
            CanUse = () => !watching && !placedThisRound,
            OnClick = _ => { var l = PlayerControl.LocalPlayer; if (l) SendToAll(ActionPlace, l.PlayerId); },
        });

        survButton = AddButton(new RoleButton
        {
            Label = "Überwachung|Surveillance",
            Icon = "check",
            GameIcon = _ => SecurityConsoleSprite(), // echtes Icon der Security-Konsole
            NeedsTarget = false,
            Cooldown = () => 1f,                 // praktisch keine Abklingzeit (nur >0, damit die Anzeige nicht bricht)
            StartCooldown = 1f,
            Hotkey = KeyCode.C,
            UsesLeft = () => usesLeft,           // Zahl am Knopf: verbleibende Nutzungen pro Runde
            CanUse = () => !watching && usesLeft > 0 && !InMeeting && !Minigame.Instance,
            OnClick = _ => OpenSecurity(),
        });
    }

    public override void OnAssigned(PlayerControl player)
    {
        SurveillanceFeeds.SetPresent(true); // Kameramann spielt mit -> Standard-Konsole zeigt 1 Bild + Pfeile
        if (player == PlayerControl.LocalPlayer) usesLeft = Mathf.RoundToInt(usesPerRound.Value);
    }

    public override void ResetState()
    {
        placedThisRound = false;
        watching = false;
        usesLeft = Mathf.RoundToInt(usesPerRound.Value); // volle Nutzungen zum Rundenstart
        SurveillanceFeeds.SetPresent(false);             // räumt auch die mobile Kamera weg
        if (camObj) Object.Destroy(camObj);
        camObj = null;
    }

    public override void OnMeetingStart()
    {
        placedThisRound = false;
        watching = false;
        SurveillanceFeeds.ClearMobile();
        if (camObj) Object.Destroy(camObj);
        camObj = null;
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionPlace || data.Length < 1 || !RoleRegistry.Has(sender, this)) return;
        var who = FindPlayer(data[0]);
        if (!who) return;
        camPos = who.GetTruePosition();
        SurveillanceFeeds.Place(camPos);      // auf allen Clients: Kamera ist jetzt in jeder Überwachung
        SpawnCamObject();                      // blinkendes Kamera-Symbol in der Welt
        if (sender == PlayerControl.LocalPlayer) { placedThisRound = true; Sfx.Play(Sfx.TaskComplete, 0.7f); }
    }

    /// <summary>Hält nur fest, ob gerade eine Überwachung offen ist (sperrt Neu-Öffnen / Kamera-Setzen).</summary>
    public override void Update()
    {
        if (!IsLocalRole) return;
        var local = PlayerControl.LocalPlayer;
        bool survOpen = Minigame.Instance && Minigame.Instance.TryCast<SurveillanceMinigame>() != null;
        watching = survOpen && local && IsAlive(local) && !InMeeting;
    }

    private void OpenSecurity()
    {
        var local = PlayerControl.LocalPlayer;
        if (watching || !local || !IsAlive(local) || InMeeting || Minigame.Instance) return;
        var prefab = SurvPrefab();
        if (!prefab || !Camera.main) { Notifier.Show(Lang.T("Hier gibt es keine Kameras.", "No cameras here."), 2f); return; }
        try
        {
            var mg = Object.Instantiate(prefab, Camera.main.transform, false);
            mg.transform.SetParent(Camera.main.transform, false);
            mg.transform.localPosition = new Vector3(0f, 0f, -50f);
            mg.Begin(null);   // der Begin-Patch hängt die mobile Kamera + Pfeile ein
            watching = true;
            if (usesLeft > 0) usesLeft--;   // eine Nutzung dieser Runde verbraucht
        }
        catch (System.Exception e) { Plugin.Logger.LogWarning($"Security-Menü: {e.Message}"); watching = false; }
    }

    private static Minigame SurvPrefab()
    {
        foreach (var c in Object.FindObjectsOfType<SystemConsole>())
            if (c && c.MinigamePrefab && c.MinigamePrefab.TryCast<SurveillanceMinigame>() != null) return c.MinigamePrefab;
        return null;
    }

    // ---------- sichtbares, blinkendes Kamera-Symbol in der Welt ----------

    private void SpawnCamObject()
    {
        if (camObj) Object.Destroy(camObj);
        var ship = ShipStatus.Instance;

        // ECHTE Security-Kamera klonen, aber OHNE die SurvCamera-Komponente (deren Awake wirft NREs/roten Punkt).
        // So haben wir exakt die Spiel-Grafik inkl. der original Sprite-Animation (Linse blinkt) - die steuern wir selbst.
        var src = FirstRealCam(ship);
        if (src)
        {
            bool wasActive = src.gameObject.activeSelf;
            src.gameObject.SetActive(false);                 // Klon entsteht inaktiv -> SurvCamera.Awake läuft noch nicht
            var clone = Object.Instantiate(src.gameObject);
            src.gameObject.SetActive(wasActive);

            var comp = clone.GetComponent<SurvCamera>();
            if (comp) Object.DestroyImmediate(comp);         // Komponente VOR dem Aktivieren weg: kein Awake, kein Registrieren

            if (ship) clone.transform.SetParent(ship.transform, true);
            clone.transform.position = new Vector3(camPos.x, camPos.y, src.transform.position.z);
            clone.SetActive(true);
            camObj = clone;

            var blink = clone.AddComponent<CamBlink>();       // spielt OnAnim (an) / OffAnim (aus) wie das echte Spiel
            blink.Anim = clone.GetComponentInChildren<PowerTools.SpriteAnim>(true);
            blink.OnAnim = src.OnAnim;
            blink.OffAnim = src.OffAnim;
            SurveillanceFeeds.SetMarker(blink);
            return;
        }

        // Fallback (keine echte Kamera auf der Map): eigenes Sprite, ohne Animation.
        camObj = new GameObject("Spieleabend_MobileCam");
        if (ship) camObj.transform.SetParent(ship.transform, true);
        camObj.transform.position = new Vector3(camPos.x, camPos.y + 0.25f, camPos.y / 1000f - 0.05f);
        camObj.transform.localScale = Vector3.one * 0.55f;
        camObj.AddComponent<SpriteRenderer>().sprite = CamSprite();
    }

    private static SurvCamera FirstRealCam(ShipStatus ship)
    {
        if (ship == null || ship.AllCameras == null) return null;
        foreach (var cam in ship.AllCameras) if (cam) return cam;
        return null;
    }

    /// <summary>Echtes In-Game-Kamera-Sprite (von einer SurvCamera der Map); Fallback: selbst gezeichnet.</summary>
    private static Sprite RealCamSprite()
    {
        var cam = FirstRealCam(ShipStatus.Instance);
        if (cam)
        {
            var sr = cam.GetComponentInChildren<SpriteRenderer>(true);
            if (sr && sr.sprite) return sr.sprite;
        }
        return CamSprite();
    }

    /// <summary>Echtes Icon der Security-Konsole (der Map); Fallback: Kamera-Sprite. Auch vom Techniker genutzt.</summary>
    internal static Sprite SecurityConsoleSprite()
    {
        foreach (var c in Object.FindObjectsOfType<SystemConsole>())
            if (c && c.MinigamePrefab && c.MinigamePrefab.TryCast<SurveillanceMinigame>() != null)
            {
                var sr = c.GetComponentInChildren<SpriteRenderer>(true);
                if (sr && sr.sprite) return sr.sprite;
            }
        return RealCamSprite();
    }

    /// <summary>Kleine Überwachungskamera: dunkler, liegender Körper mit cyanfarbener Linse (wie vom Nutzer gewünscht).</summary>
    private static Sprite CamSprite()
    {
        if (camSprite) return camSprite;
        const int W = 64, H = 40;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        var outline = new Color(0.05f, 0.06f, 0.09f, 1f);
        var bodyC = new Color(0.17f, 0.19f, 0.25f, 1f);
        var lensC = new Color(0.30f, 0.82f, 1f, 1f);
        var lensHi = new Color(0.75f, 0.95f, 1f, 1f);

        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            Color c = Color.clear;

            bool body = x >= 8 && x <= 44 && y >= 10 && y <= 30;          // liegender Körper
            bool barrel = x >= 44 && x <= 57 && y >= 16 && y <= 24;       // kurzer Linsen-Stutzen rechts
            // abgerundete Körper-Ecken wegschneiden
            if (body && (x < 11 || x > 41) && (y < 13 || y > 27)) body = false;
            if (body || barrel) c = bodyC;

            bool core = x >= 11 && x <= 41 && y >= 13 && y <= 27;         // 2px-Kontur um den Körper
            if (body && !core) c = outline;
            if (barrel && (x > 55 || y <= 17 || y >= 23)) c = outline;

            float dx = x - 23f, dy = y - 20f, d = dx * dx + dy * dy;      // Linse
            if (d <= 64f) c = outline;
            if (d <= 49f) c = lensC;
            if (d <= 10f) c = lensHi;                                      // heller Reflex

            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        camSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        camSprite.hideFlags = HideFlags.HideAndDontSave;
        return camSprite;
    }
}

/// <summary>
/// Steuert die ECHTE Kamera-Animation: spielt OnAnim (Linse blinkt rot), wenn jemand zuschaut, sonst OffAnim.
/// Genau die Technik des Spiels - nur ohne die SurvCamera-Komponente.
/// </summary>
public sealed class CamBlink : MonoBehaviour
{
    public CamBlink(System.IntPtr ptr) : base(ptr) { }

    public bool Active;
    public PowerTools.SpriteAnim Anim;
    public AnimationClip OnAnim, OffAnim;

    private bool applied;
    private bool shown;

    private void Update()
    {
        if (!Anim) return;
        if (applied && shown == Active) return;
        applied = true;
        shown = Active;
        var clip = Active ? OnAnim : OffAnim;
        if (clip) Anim.Play(clip, 1f);
        else Anim.Stop();
    }
}
