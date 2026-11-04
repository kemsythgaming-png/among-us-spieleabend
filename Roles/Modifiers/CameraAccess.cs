using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles.Modifiers;

/// <summary>
/// Anpassung: Der Traeger kann die Ueberwachungskameras von ueberall oeffnen (Taste C) - ohne an der
/// Security-Konsole zu stehen. Es sind die normalen Kameras der Karte; beim Zuschauen steht man still.
/// Deine eigentliche Rolle bleibt.
/// </summary>
public sealed class CameraAccess : CustomRole
{
    public override string ConfigSection => "KameraZugriff";
    public override string Name => Lang.T("Techniker", "Technician");
    public override Color Color => new Color32(90, 160, 210, 255);
    public override RoleTeam Team => RoleTeam.Modifier;
    public override string IntroText => Lang.T("Kameras von überall", "Cameras from anywhere");
    public override string Description => Lang.T("Öffne die Überwachung\nvon überall (C).", "Open surveillance\nfrom anywhere (C).");
    public override string LongDescription => Lang.T(
        "Anpassung: Mit Taste C öffnest du die Überwachungskameras von überall - du musst nicht an der Security-Konsole stehen. Es sind die normalen Kameras der Karte; beim Zuschauen stehst du still. Deine eigentliche Rolle bleibt.",
        "Modifier: press C to open the security cameras from anywhere - you don't need to stand at the security console. These are the map's normal cameras; you stand still while watching. Your actual role stays.");

    private const float ReuseCooldown = 45f; // fest 45 s zum Wieder-Öffnen
    private RoleOption hold;
    private RoleButton button;
    private bool watching;
    private float watchUntil;

    protected override void Setup()
    {
        hold = AddOption("Hold", "Offen halten|Watch time", 15f, 5f, 40f, 1f, "s");

        button = AddButton(new RoleButton
        {
            Label = "Überwachung|Surveillance",
            GameIcon = _ => Cameraman.SecurityConsoleSprite(), // gleiches Symbol wie beim Kameramann
            Icon = "check",
            NeedsTarget = false,
            Cooldown = () => ReuseCooldown, // Radial-Maximum
            StartCooldown = 5f,
            Hotkey = KeyCode.C,
            CanUse = () => !watching && !InMeeting && !Minigame.Instance && IsAlive(PlayerControl.LocalPlayer),
            OnClick = _ => OpenSecurity(),
        });
    }

    public override void ResetState() => watching = false;

    public override void OnMeetingStart() => watching = false;

    /// <summary>Überwachung offen halten, nach der Haltezeit schließen und DANACH die Abklingzeit starten.</summary>
    public override void Update()
    {
        if (!IsLocalRole) return;
        var local = PlayerControl.LocalPlayer;
        bool survOpen = Minigame.Instance && Minigame.Instance.TryCast<SurveillanceMinigame>() != null && local && IsAlive(local) && !InMeeting;

        // Nach der eingestellten Haltezeit automatisch schließen.
        if (watching && survOpen && Time.time >= watchUntil)
        {
            try { Minigame.Instance.Close(); } catch { }
            survOpen = false;
        }

        // Übergang offen -> geschlossen: JETZT die Abklingzeit starten.
        if (watching && !survOpen)
        {
            if (button != null) button.Timer = ReuseCooldown;
        }
        watching = survOpen;
    }

    /// <summary>Restzeit der laufenden Überwachung oben einblenden.</summary>
    public override void DrawGUI()
    {
        if (!IsLocalRole || !watching) return;
        float left = Mathf.Max(0f, watchUntil - Time.time);
        var style = new GUIStyle(GUI.skin.label) { fontSize = 80, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = new Color(0.45f, 0.75f, 1f, 0.95f); // Hellblau
        GUI.Label(new Rect(Ui.ScreenWidth / 2f - 150f, 390f, 300f, 110f), $"{Mathf.CeilToInt(left)}", style); // mittig im Kamera-Kreuz
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
            mg.Begin(null);
            watching = true;
            watchUntil = Time.time + hold.Value;
            if (button != null) button.Timer = 0f; // Abklingzeit startet erst beim Schließen, nicht beim Öffnen
        }
        catch (System.Exception e) { Plugin.Logger.LogWarning($"Security-Menü: {e.Message}"); watching = false; }
    }

    private static Minigame SurvPrefab()
    {
        foreach (var c in Object.FindObjectsOfType<SystemConsole>())
            if (c && c.MinigamePrefab && c.MinigamePrefab.TryCast<SurveillanceMinigame>() != null) return c.MinigamePrefab;
        return null;
    }
}
