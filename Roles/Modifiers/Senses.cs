using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Modifiers;

/// <summary>Anpassung: meldet er eine Leiche, erfaehrt er die Rolle des Toten.</summary>
public sealed class Sleuth : CustomRole
{
    public override string ConfigSection => "Spuernase";
    public override string Name => Lang.T("Spürnase", "Sleuth");
    public override Color Color => new Color32(140, 110, 80, 255);
    public override RoleTeam Team => RoleTeam.Modifier;
    public override string IntroText => Lang.T("Die Toten reden mit dir", "The dead talk to you");
    public override string Description => Lang.T("Meldest du eine Leiche, siehst du ihre Rolle.", "Report a body to learn its role.");
    public override string LongDescription => Lang.T(
        "Anpassung: Meldest du eine Leiche, erfährst du die Rolle des Toten - sie steht danach an seinem Namen (nur du siehst das). Deine Rolle bleibt.",
        "Modifier: when you report a body, you learn the dead player's role - it shows next to their name (only you see it). Your role stays.");

    internal static Sleuth Instance { get; private set; }
    private readonly Dictionary<byte, string> learned = new();

    protected override void Setup() => Instance = this;

    public override void ResetState() => learned.Clear();

    internal void OnReported(PlayerControl reporter, NetworkedPlayerInfo body)
    {
        if (body == null || reporter != PlayerControl.LocalPlayer || !IsLocalRole) return;
        var dead = body.Object;
        if (!dead) return;
        string role = RoleUI.RoleLabel(dead);
        learned[dead.PlayerId] = role;
        Notifier.Show($"{body.PlayerName}: {role}", 6f);
        Sfx.Play(Sfx.Investigate);
    }

    public override string GetNameMark(PlayerControl viewer, PlayerControl target) =>
        RoleRegistry.Has(viewer, this) && learned.TryGetValue(target.PlayerId, out var r) ? r : null;
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.StartMeeting))]
internal static class SleuthReportPatch
{
    public static void Postfix(PlayerControl __instance, NetworkedPlayerInfo target) =>
        Sleuth.Instance?.OnReported(__instance, target);
}

/// <summary>Crew: auf Knopfdruck zeigt ein Pfeil kurz zum naechsten Spieler.</summary>
public sealed class Radar : CustomRole
{
    public override string Name => "Radar";
    public override Color Color => new Color32(90, 220, 120, 255);
    public override RoleTeam Team => RoleTeam.Crewmate; // eigene Crew-Rolle (war frueher eine Anpassung)
    public override string IntroText => Lang.T("Du spürst, wo die anderen sind", "You sense where the others are");
    public override string Description => Lang.T("Orte den nächsten Spieler (F):\nein Pfeil zeigt kurz hin.", "Locate the nearest player (F):\nan arrow briefly points there.");
    public override string LongDescription => Lang.T(
        "Du bist Crew. Mit Taste F schaltest du dein Radar ein: Ein paar Sekunden lang zeigt ein Pfeil um dich herum zum nächsten lebenden Spieler - auch durch Wände.",
        "You are crew. Press F to switch on your radar: for a few seconds an arrow around you points to the nearest living player - even through walls.");

    private GameObject arrow;
    private static Sprite arrowSprite;
    private RoleButton button;
    private RoleOption cooldown, duration, showColor;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 25f, 5f, 60f, 2.5f, "s");
        duration = AddOption("Duration", "Radar-Dauer|Radar duration", 8f, 2f, 20f, 1f, "s");
        showColor = AddToggle("ShowColor", "Spielerfarbe am Pfeil|Player color on arrow", true);
        button = AddButton(new RoleButton
        {
            Label = "Radar|Radar",
            Icon = "radar",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            Duration = () => duration.Value,
            Hotkey = KeyCode.F,
            OnClick = _ => Sfx.Play(Sfx.Investigate),
        });
    }

    public override void ResetState()
    {
        if (arrow) Object.Destroy(arrow);
        arrow = null;
    }

    public override void Update()
    {
        var local = PlayerControl.LocalPlayer;
        if (!IsLocalRole || !IsAlive(local) || InMeeting || button == null || !button.EffectActive)
        {
            if (arrow) arrow.SetActive(false);
            return;
        }

        var nearest = PlayerControl.AllPlayerControls.ToArray()
            .Where(p => IsAlive(p) && p != local)
            .OrderBy(p => Vector2.Distance(p.GetTruePosition(), local.GetTruePosition()))
            .FirstOrDefault();
        if (!nearest)
        {
            if (arrow) arrow.SetActive(false);
            return;
        }

        if (!arrow) arrow = CreateArrow(local);
        arrow.SetActive(true);

        // Pfeil in der Farbe des nächsten Spielers (abschaltbar) - sonst Radar-Grün.
        var sr = arrow.GetComponent<SpriteRenderer>();
        if (sr)
        {
            Color col = Color;
            if (showColor.Bool && nearest.Data != null && Palette.PlayerColors != null)
                col = Palette.PlayerColors[nearest.Data.DefaultOutfit.ColorId % Palette.PlayerColors.Length];
            sr.color = col;
        }

        Vector2 dir = nearest.GetTruePosition() - local.GetTruePosition();
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        arrow.transform.localPosition = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * 0.75f, Mathf.Sin(angle * Mathf.Deg2Rad) * 0.75f + 0.1f, -0.05f);
        arrow.transform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    private GameObject CreateArrow(PlayerControl local)
    {
        var go = new GameObject("Spieleabend_RadarArrow");
        go.transform.SetParent(local.transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ArrowSprite();
        sr.color = Color;
        return go;
    }

    private static Sprite ArrowSprite()
    {
        if (arrowSprite) return arrowSprite;
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            // Pfeilspitze nach oben: Dreieck mit dunklem Rand
            float half = (S - 1 - y) * 0.45f;             // unten breit, oben spitz
            float dx = Mathf.Abs(x - (S - 1) / 2f);
            bool inside = y > 8 && dx <= half;
            bool edge = inside && (dx > half - 4f || y < 13);
            tex.SetPixel(x, y, !inside ? Color.clear : edge ? new Color(0.1f, 0.1f, 0.12f, 1f) : Color.white);
        }
        tex.Apply();
        arrowSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 200f);
        arrowSprite.hideFlags = HideFlags.HideAndDontSave;
        return arrowSprite;
    }
}

/// <summary>
/// Crew (Paranoid): schaltet seinen Spuersinn ein (Knopf). Solange er laeuft, leuchtet sein Bildschirm kurz hellblau
/// auf, wenn irgendwo auf der Karte jemand getoetet wird - aber nie wer oder wo. Danach Abklingzeit.
/// </summary>
public sealed class Paranoid : CustomRole
{
    public override string Name => Lang.T("Paranoid", "Paranoid");
    public override Color Color => new Color32(200, 90, 90, 255);
    public override RoleTeam Team => RoleTeam.Crewmate; // eigene Crew-Rolle (war frueher eine Anpassung)
    public override string IntroText => Lang.T("Hast du das gehört?", "Did you hear that?");
    public override string Description => Lang.T("Spürsinn an (F): dein Bildschirm\nblitzt, wenn jemand stirbt.", "Senses on (F): your screen\nflashes when someone dies.");
    public override string LongDescription => Lang.T(
        "Du bist Crew. Mit Taste F schaltest du für einige Sekunden deinen Spürsinn ein. Solange er läuft und irgendwo auf der ganzen Karte jemand getötet wird, leuchtet dein Bildschirm kurz hellblau auf - aber du erfährst nie, wer oder wo. Danach läuft eine Abklingzeit.",
        "You are crew. Press F to switch on your senses for a few seconds. While active and someone is killed anywhere on the map, your screen briefly flashes light blue - but you never learn who or where. Then a cooldown runs.");

    private RoleButton button;
    private RoleOption cooldown;
    private float flashUntil = -1f;
    private static Texture2D flashTex;
    private GUIStyle flashStyle;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 45f, 10f, 90f, 5f, "s");

        button = AddButton(new RoleButton
        {
            Label = "Spürsinn|Senses",
            GameIcon = _ => AlertSprite(),
            Icon = "radar",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            // Aktiv-Dauer bei jeder Nutzung zufaellig 10-15 s
            Duration = () => Random.Range(10f, 15f),
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer),
            OnClick = _ => Sfx.Play(Sfx.Investigate, 0.6f),
        });
    }

    public override void ResetState() => flashUntil = -1f;
    public override void OnMeetingStart() => flashUntil = -1f;

    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        var local = PlayerControl.LocalPlayer;
        if (cause != DeathCause.Kill || !IsLocalRole || !IsAlive(local) || victim == local) return;
        if (button == null || !button.EffectActive) return; // nur solange der Spürsinn läuft
        flashUntil = Time.time + 0.7f;
        Sfx.Play(Sfx.Warning, 0.7f);
    }

    public override void DrawGUI()
    {
        if (!IsLocalRole || flashUntil < 0f) return;
        float left = flashUntil - Time.time;
        if (left <= 0f) { flashUntil = -1f; return; }

        float a = Mathf.Clamp01(left / 0.7f) * 0.45f; // weich ausblenden
        EnsureFlashTex(a);
        if (flashStyle == null) flashStyle = new GUIStyle();
        flashStyle.normal.background = flashTex;
        GUI.Box(new Rect(0f, 0f, Ui.ScreenWidth, 1080f), GUIContent.none, flashStyle);
    }

    /// <summary>Kleine, einfarbige Hellblau-Textur mit der gewuenschten Deckkraft (jeden Frame aktualisiert).</summary>
    private static void EnsureFlashTex(float alpha)
    {
        if (!flashTex)
            flashTex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var col = new Color(0.45f, 0.75f, 1f, alpha);
        var px = new Color[16];
        for (int i = 0; i < px.Length; i++) px[i] = col;
        flashTex.SetPixels(px);
        flashTex.Apply();
    }

    // ---------- Eigenes Alarm-Symbol (Ausrufezeichen) ----------
    private static Sprite alertSprite;
    private static Sprite AlertSprite()
    {
        if (alertSprite) return alertSprite;
        const int S = 96;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        var col = new Color(0.95f, 0.85f, 0.25f, 1f);
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            var c = new Vector2(x - S / 2f, y - S / 2f) / (S / 2f);
            // senkrechter Balken + Punkt = Ausrufezeichen
            bool bar = Mathf.Abs(c.x) < 0.12f && c.y > -0.15f && c.y < 0.72f;
            bool dot = (c - new Vector2(0f, -0.5f)).magnitude < 0.15f;
            tex.SetPixel(x, y, bar || dot ? col : Color.clear);
        }
        tex.Apply();
        alertSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        alertSprite.hideFlags = HideFlags.HideAndDontSave;
        return alertSprite;
    }
}
