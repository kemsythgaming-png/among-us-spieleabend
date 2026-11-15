using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Crew (Chamäleon): per Knopf wirst du über 3 Sekunden unsichtbar - für dich UND die anderen. Solange du stehen
/// bleibst, bleibst du unsichtbar (ohne Zeitlimit). Sobald du dich bewegst, wirst du wieder sichtbar und erst dann
/// läuft die Abklingzeit. Eine Leiche ist wieder sichtbar.
/// </summary>
public sealed class Chameleon : CustomRole
{
    public override string ConfigSection => "Chamaeleon";
    public override string Name => Lang.T("Chamäleon", "Chameleon");
    public override Color Color => new Color32(90, 190, 120, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Verschwinde", "Vanish");
    public override string Description => Lang.T("Werde unsichtbar (F).\nStillstehen = unsichtbar bleiben.", "Turn invisible (F).\nStand still = stay invisible.");
    public override string LongDescription => Lang.T(
        "Du bist Crew. Mit Taste F wirst du über 3 Sekunden unsichtbar - für dich und für die anderen. Solange du still stehst, bleibst du unsichtbar (kein Zeitlimit). Sobald du dich bewegst, wirst du wieder sichtbar - und erst dann läuft die Abklingzeit. Eine Leiche ist normal sichtbar.",
        "You are crew. Press F to fade to invisible over 3 seconds - for you and everyone else. As long as you stand still you stay invisible (no time limit). The moment you move you become visible again - and only then the cooldown runs. A body is visible as normal.");

    private const byte ActionHide = 0, ActionShow = 1;

    private RoleOption cooldown, fadeTime;
    private static readonly Dictionary<byte, float> fadeStart = new(); // Spieler -> Beginn des Verschwindens
    private static readonly Dictionary<byte, float> applied = new();    // Spieler -> zuletzt gesetztes Alpha

    private bool localCloaking;
    private Vector2 hideFrom;
    private RoleButton button;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 25f, 5f, 90f, 5f, "s");
        fadeTime = AddOption("Duration", "Verschwinde-Zeit|Fade time", 3f, 1f, 5f, 0.5f, "s"); // Schlüssel bleibt "Duration"

        button = AddButton(new RoleButton
        {
            Label = "Tarnen|Cloak",
            Icon = "camo",
            GameIcon = _ => ProcIcons.InvisibleEye(),
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            Visible = () => !localCloaking, // während du unsichtbar bist, ist der Knopf (und sein Timer) weg
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer) && !localCloaking,
            OnClick = _ =>
            {
                var l = PlayerControl.LocalPlayer;
                if (!l) return;
                localCloaking = true;
                hideFrom = l.GetTruePosition();
                SendToAll(ActionHide, l.PlayerId);
            },
        });
    }

    /// <summary>Aktuelles Sichtbarkeits-Alpha eines Spielers durch die Chamäleon-Tarnung (1 = voll sichtbar, 0 = unsichtbar).</summary>
    public static float VisibilityAlpha(byte id) => applied.TryGetValue(id, out var a) ? a : 1f;

    public override void ResetState() { ShowAll(); localCloaking = false; }
    public override void OnMeetingStart() { ShowAll(); localCloaking = false; }

    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        if (victim && fadeStart.ContainsKey(victim.PlayerId)) { fadeStart.Remove(victim.PlayerId); ApplyAlpha(victim, 1f); }
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (data.Length < 1 || !RoleRegistry.Has(sender, this)) return;
        var p = FindPlayer(data[0]);
        if (action == ActionHide) { fadeStart[data[0]] = Time.time; if (p && sender == PlayerControl.LocalPlayer) Sfx.Play(Sfx.Shapeshift, 0.6f); }
        else if (action == ActionShow) { fadeStart.Remove(data[0]); if (p) ApplyAlpha(p, 1f); }
    }

    public override void Update()
    {
        // Lokal: Bewegung beendet die Tarnung -> wieder sichtbar + Abklingzeit startet JETZT.
        if (localCloaking)
        {
            var l = PlayerControl.LocalPlayer;
            bool moved = l && (l.GetTruePosition() - hideFrom).sqrMagnitude > 0.05f;
            if (!l || !IsAlive(l) || InMeeting || moved)
            {
                localCloaking = false;
                if (l) SendToAll(ActionShow, l.PlayerId);
                if (button != null) button.Timer = cooldown.Value; // Cooldown läuft erst ab jetzt (sichtbar)
            }
        }

        // Alle Clients: Verschwinde-Animation fahren und Unsichtbarkeit halten.
        float fade = Mathf.Max(0.1f, fadeTime.Value);
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!p) continue;
            if (fadeStart.TryGetValue(p.PlayerId, out var start) && IsAlive(p) && !InMeeting)
            {
                float a = 1f - Mathf.Clamp01((Time.time - start) / fade); // 1 -> 0 über die Verschwinde-Zeit, bleibt 0
                ApplyAlpha(p, a);
            }
            else if (applied.TryGetValue(p.PlayerId, out var av) && av < 1f)
            {
                ApplyAlpha(p, 1f); // einmal zurücksetzen
            }
        }
    }

    private static void ApplyAlpha(PlayerControl p, float a)
    {
        if (!p) return;
        try { if (p.cosmetics) p.cosmetics.SetPhantomRoleAlpha(a); } catch { }
        try { if (p.cosmetics && p.cosmetics.nameText) { var c = p.cosmetics.nameText.color; c.a = a; p.cosmetics.nameText.color = c; } } catch { }
        try { if (p.cosmetics) p.cosmetics.SetPetVisible(a >= 1f); } catch { } // Haustier mit verschwinden lassen
        applied[p.PlayerId] = a;
    }

    private static void ShowAll()
    {
        foreach (var id in new List<byte>(fadeStart.Keys))
        {
            var p = FindPlayer(id);
            if (p) ApplyAlpha(p, 1f);
        }
        fadeStart.Clear();
    }
}
