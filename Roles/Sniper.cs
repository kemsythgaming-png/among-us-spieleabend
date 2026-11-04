using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor (Sniper): Knopf druecken -> die Kamera zoomt fuer 15 s heraus und man kann sich nicht mehr bewegen.
/// In dieser Zeit klickt man mit der Maus auf einen Spieler, um ihn zu erschiessen (auch durch Waende). Nach dem
/// Schuss oder nach 15 s endet der Zoom und eine Abklingzeit laeuft. Normaler Kill bleibt.
/// </summary>
public sealed class Sniper : CustomRole
{
    public override string ConfigSection => "Sniper";
    public override string Name => Lang.T("Sniper", "Sniper");
    public override Color Color => new Color32(90, 110, 80, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Aus der Ferne", "From afar");
    public override string Description => Lang.T("Zoom (F): 15 s stillstehen,\ndann Spieler anklicken = Kill.", "Zoom (F): hold still 15 s,\nthen click a player = kill.");
    public override string LongDescription => Lang.T(
        "Impostor: Mit Taste F zoomst du für 15 Sekunden heraus - in dieser Zeit kannst du dich nicht bewegen. Klick mit der Maus auf einen Spieler, um ihn zu erschießen (auch durch Wände, egal wie weit). Nach dem Schuss oder nach Ablauf endet der Zoom und eine Abklingzeit läuft. Deinen normalen Kill hast du zusätzlich.",
        "Impostor: press F to zoom out for 15 seconds - you can't move during that time. Click a player with the mouse to shoot them (through walls, any distance). After the shot or when time runs out the zoom ends and a cooldown runs. You also keep your normal kill.");

    private const byte ActionKill = 0;

    private const float ScopeCooldown = 45f; // fest 45 s
    private const float ZoomFactor = 1.2f;   // fest 1,2 (nicht über Config änderbar)
    private RoleOption duration, range;
    private RoleButton button;
    private bool scoped;
    private float scopeUntil, armedAt, baseSize = -1f;

    protected override void Setup()
    {
        duration = AddOption("Duration", "Zoom-Dauer|Scope time", 15f, 5f, 30f, 1f, "s");
        range = AddOption("Range", "Reichweite|Range", 6f, 3f, 15f, 1f);

        button = AddButton(new RoleButton
        {
            Label = "Zoom|Scope",
            Icon = "shoot",
            NeedsTarget = false,
            Cooldown = () => 0.2f,                 // echte Abklingzeit startet nach dem Zoom
            StartCooldown = 5f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer) && !scoped,
            OnClick = _ => { scoped = true; scopeUntil = Time.time + duration.Value; armedAt = Time.time; },
        });
    }

    public override void ResetState() { EndScope(false); }
    public override void OnMeetingStart() { EndScope(false); }

    /// <summary>Beim Zoomen steht der Sniper still.</summary>
    public override float SpeedMultiplier(PlayerControl local) =>
        IsLocalRole && scoped && !InMeeting ? 0f : 1f;

    public override void Update()
    {
        if (!IsLocalRole) return;
        var local = PlayerControl.LocalPlayer;
        var cam = Camera.main;

        if (scoped && IsAlive(local) && !InMeeting)
        {
            if (cam)
            {
                if (baseSize < 0f) baseSize = cam.orthographicSize;
                cam.orthographicSize = baseSize * ZoomFactor;
            }
            // Dunkelheit (Sichtbegrenzung) während des Zielens ausblenden, damit man weit sieht
            SetShadow(false);

            // Mit der Maus auf einen Spieler klicken = Schuss (nicht der Aktivierungs-Klick)
            if (Input.GetMouseButtonDown(0) && Time.time - armedAt > 0.1f && !Ui.IsTyping && cam)
            {
                Vector3 w = cam.ScreenToWorldPoint(Input.mousePosition);
                Vector2 me = local.GetTruePosition();
                var victim = PlayerControl.AllPlayerControls.ToArray()
                    .Where(p => p && p != local && IsAlive(p) && !IsImpostor(p) && !p.inVent
                        && Vector2.Distance(me, p.GetTruePosition()) <= range.Value) // in Reichweite des Snipers
                    .OrderBy(p => Vector2.Distance(new Vector2(w.x, w.y), p.GetTruePosition()))
                    .FirstOrDefault();
                if (victim && Vector2.Distance(new Vector2(w.x, w.y), victim.GetTruePosition()) <= 0.8f)
                {
                    SendToHost(ActionKill, victim.PlayerId);
                    Sfx.Play(Sfx.Kill, 0.9f); // Kill-Sound beim Treffer
                    EndScope(true);
                }
            }

            if (Time.time >= scopeUntil) EndScope(true);
        }
        else if (scoped) EndScope(true);
    }

    private void EndScope(bool startCooldown)
    {
        scoped = false;
        var cam = Camera.main;
        if (baseSize > 0f && cam) cam.orthographicSize = baseSize;
        baseSize = -1f;
        SetShadow(true);
        if (startCooldown && button != null) button.Timer = ScopeCooldown;
    }

    private static void SetShadow(bool on)
    {
        try { if (HudManager.InstanceExists && HudManager.Instance.ShadowQuad) HudManager.Instance.ShadowQuad.gameObject.SetActive(on); }
        catch { }
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionKill || !AmHost || data.Length < 1 || !RoleRegistry.Has(sender, this) || !IsAlive(sender)) return;
        var target = FindPlayer(data[0]);
        if (IsAlive(target)) KillSystem.RangedKill(sender, target); // kein Teleport zum Opfer
    }
}
