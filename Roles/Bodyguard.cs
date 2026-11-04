using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Crew (Bodyguard): wählt einmal pro Runde einen Schützling. Versucht irgendjemand (egal ob Crew, Impostor oder
/// Neutral) etwas gegen den Schützling zu tun (Angriff/Kill-Versuch), blitzt der Bildschirm des Bodyguards blau auf -
/// als Warnung. Der Bodyguard selbst stirbt dabei NICHT.
/// </summary>
public sealed class Bodyguard : CustomRole
{
    public override string Name =>
        Lang.T("Bodyguard", "Bodyguard");
    public override Color Color => new Color32(93, 129, 217, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText =>
        Lang.T("Behalt deinen Schützling im Blick", "Keep an eye on your ward");
    public override string Description =>
        Lang.T("Wähle einen Schützling (F).\nBild blitzt blau, wenn er bedroht wird.", "Pick a ward (F).\nScreen flashes blue if threatened.");
    public override string LongDescription =>
        Lang.T("Du bist Crew. Einmal pro Runde wählst du mit Taste F einen Schützling.\n\nVersucht irgendjemand - egal ob Crew, Impostor oder Neutral - etwas gegen ihn zu tun (ihn anzugreifen/zu töten), blitzt dein Bildschirm kurz blau auf. So merkst du sofort, dass dein Schützling bedroht wird - aber nicht, von wem.\n\nNur du siehst, wer dein Schützling ist.",
                "You are crew. Once per round you pick a ward with key F.\n\nIf anyone - crew, impostor or neutral - tries to do something to them (attack/kill), your screen briefly flashes blue. So you instantly notice your ward is threatened - but not by whom.\n\nOnly you can see who your ward is.");

    private const byte ActionGuard = 0, ActionAlert = 1;
    private const byte None = byte.MaxValue;

    internal static Bodyguard Instance { get; private set; }
    private static bool hooked;

    private RoleOption endsOnMeeting;

    private byte guarded = None;
    private byte guardId = None;
    private int uses = 1;

    private float flashUntil = -1f;
    private static Texture2D flashTex;
    private GUIStyle flashStyle;

    protected override void Setup()
    {
        Instance = this;
        endsOnMeeting = AddToggle("EndsOnMeeting", "Schützling endet beim Meeting|Ward ends at meeting", true);

        // Jedes Mal, wenn jemand einen Ziel-Knopf auf einen Spieler benutzt (egal welche Rolle): ist es der
        // Schützling, meldet der handelnde Client dem Bodyguard einen Blitz. Auch ohne Kill (z. B. Trankbrauer).
        if (!hooked)
        {
            RoleButton.UsedOnTarget += (role, tgt) => Instance?.OnSomeoneTargeted(role, tgt);
            hooked = true;
        }

        AddButton(new RoleButton
        {
            Label = "Bewachen|Guard",
            Icon = "guard",
            Cooldown = () => 1f,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            UsesLeft = () => uses,                 // einmal pro Runde
            CanUse = () => uses > 0 && guarded == None,
            OnClick = target =>
            {
                if (!target || uses <= 0) return;
                uses = 0;
                SendToAll(ActionGuard, target.PlayerId);
            },
        });
    }

    public override void ResetState()
    {
        Instance = this;
        guarded = None;
        guardId = None;
        uses = 1;
        flashUntil = -1f;
    }

    /// <summary>Läuft beim handelnden Spieler: zielt jemand (außer dem Bodyguard selbst) auf den Schützling, Blitz melden.</summary>
    private void OnSomeoneTargeted(CustomRole role, PlayerControl target)
    {
        if (guarded == None || !target || target.PlayerId != guarded) return;
        if (role is Bodyguard) return;                      // eigene Bewachen-Auswahl nicht melden
        SendToAll(ActionAlert);
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionGuard && data.Length > 0 && RoleRegistry.Has(sender, this))
        {
            guarded = data[0];
            guardId = sender.PlayerId;
            if (sender == PlayerControl.LocalPlayer) Sfx.Play(Sfx.Protect);
        }
        else if (action == ActionAlert)
        {
            // Warn-Blitz nur fuer den Bodyguard selbst
            if (PlayerControl.LocalPlayer && PlayerControl.LocalPlayer.PlayerId == guardId)
            {
                flashUntil = Time.time + 0.7f;
                Sfx.Play(Sfx.Warning, 0.7f);
            }
        }
    }

    public override void OnMeetingStart()
    {
        flashUntil = -1f;
        if (endsOnMeeting.Bool) guarded = None;
    }

    public override void OnMeetingEnd(PlayerControl exiled) => uses = 1; // pro Runde ein neuer Schützling

    /// <summary>Kein Opfern mehr - nur Warnung: Angriff auf den Schützling meldet dem Bodyguard einen blauen Blitz.</summary>
    public override KillIntercept InterceptKill(PlayerControl killer, PlayerControl target)
    {
        if (guarded == None || !target || target.PlayerId != guarded) return KillIntercept.None;
        if (killer && killer.PlayerId == guarded) return KillIntercept.None; // Selbst-Kill nicht melden
        SendToAll(ActionAlert);
        return KillIntercept.None; // der Kill wird NICHT geblockt
    }

    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        if (victim.PlayerId == guarded || victim.PlayerId == guardId) guarded = None;
    }

    public override void DrawGUI()
    {
        if (!IsLocalRole || flashUntil < 0f) return;
        float left = flashUntil - Time.time;
        if (left <= 0f) { flashUntil = -1f; return; }

        float a = Mathf.Clamp01(left / 0.7f) * 0.45f;
        EnsureFlashTex(a);
        if (flashStyle == null) flashStyle = new GUIStyle();
        flashStyle.normal.background = flashTex;
        GUI.Box(new Rect(0f, 0f, Ui.ScreenWidth, 1080f), GUIContent.none, flashStyle);
    }

    private static void EnsureFlashTex(float alpha)
    {
        if (!flashTex)
            flashTex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var col = new Color(0.35f, 0.55f, 1f, alpha);
        var px = new Color[16];
        for (int i = 0; i < px.Length; i++) px[i] = col;
        flashTex.SetPixels(px);
        flashTex.Apply();
    }

    /// <summary>Die Schutzblase sieht nur der Bodyguard selbst.</summary>
    public override IEnumerable<(PlayerControl Target, Color Color)> GetProtectionBubbles(PlayerControl viewer)
    {
        if (guarded != None && viewer && viewer.PlayerId == guardId) yield return (FindPlayer(guarded), Color);
    }

    public override string GetNameMark(PlayerControl viewer, PlayerControl target) =>
        guarded != None && target.PlayerId == guarded && viewer.PlayerId == guardId ? $"<color={ColorHex}>{Lang.T("[Bewacht]", "[Guarded]")}</color>" : null;
}
