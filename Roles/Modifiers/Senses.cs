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
    private RoleOption cooldown, duration;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 25f, 5f, 60f, 2.5f, "s");
        duration = AddOption("Duration", "Radar-Dauer|Radar duration", 8f, 2f, 20f, 1f, "s");
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

/// <summary>Crew: bekommt eine Warnung, wenn ganz in der Naehe jemand getoetet wird.</summary>
public sealed class Paranoid : CustomRole
{
    public override string Name => Lang.T("Paranoid", "Paranoid");
    public override Color Color => new Color32(200, 90, 90, 255);
    public override RoleTeam Team => RoleTeam.Crewmate; // eigene Crew-Rolle (war frueher eine Anpassung)
    public override string IntroText => Lang.T("Hast du das gehört?", "Did you hear that?");
    public override string Description => Lang.T("Du merkst, wenn in der Nähe jemand stirbt.", "You notice when someone dies nearby.");
    public override string LongDescription => Lang.T(
        "Du bist Crew. Wird ganz in deiner Nähe jemand getötet, bekommst du eine Warnung - aber nicht, wo genau.",
        "You are crew. When someone is killed close to you, you get a warning - but not exactly where.");

    private RoleOption range;

    protected override void Setup()
    {
        range = AddOption("Range", "Reichweite|Range", 6f, 2f, 15f, 1f);
    }

    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        var local = PlayerControl.LocalPlayer;
        if (cause != DeathCause.Kill || !IsLocalRole || !IsAlive(local) || victim == local) return;
        if (Vector2.Distance(victim.GetTruePosition(), local.GetTruePosition()) > range.Value) return;
        Notifier.Show($"<color={ColorHex}>{Lang.T("Du hast etwas gehört ... ganz in der Nähe ist jemand gestorben!", "You heard something ... someone died close by!")}</color>", 4f);
        Sfx.Play(Sfx.Warning);
    }
}
