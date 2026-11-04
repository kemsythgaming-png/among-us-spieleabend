using System;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor (Frostbeisser): friert einen Bereich ein. Alle anderen Spieler darin koennen sich 8 Sekunden nicht bewegen
/// und bekommen Eis ueber den Bildschirm - nur der Frostbeisser selbst (und Immune) bleiben frei. Cooldown 35 s.
/// </summary>
public sealed class Frostbiter : CustomRole
{
    public override string ConfigSection => "Frostbeisser";
    public override string Name => Lang.T("Frostbeißer", "Frostbiter");
    public override Color Color => new Color32(120, 210, 255, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Alles erstarrt", "Everything freezes");
    public override string Description => Lang.T("Friere den Bereich ein (F) -\nnur du bewegst dich.", "Freeze the area (F) -\nonly you move.");
    public override string LongDescription => Lang.T(
        "Impostor: Mit Taste F frierst du deinen Umkreis ein. Alle anderen Spieler darin können sich 8 Sekunden nicht bewegen und bekommen Eis über den Bildschirm. Nur du selbst (und wer gerade frost-immun ist) bleibst frei. Danach läuft eine Abklingzeit (35 s).",
        "Impostor: press F to freeze your surroundings. All other players inside can't move for 8 seconds and get ice over their screen. Only you (and anyone currently frost-immune) stay free. Then a cooldown runs (35 s).");

    private const byte ActionFreeze = 0;

    private RoleOption cooldown, duration, radius;
    private float frozenUntil;          // lokal: bis wann ist DER lokale Spieler eingefroren
    private GameObject iceOverlay;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 35f, 15f, 90f, 5f, "s");
        duration = AddOption("Duration", "Frost-Dauer|Freeze time", 8f, 3f, 15f, 1f, "s");
        radius = AddOption("Radius", "Bereich|Area", 4f, 2f, 10f, 0.5f);

        AddButton(new RoleButton
        {
            Label = "Einfrieren|Freeze",
            Icon = "frost",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer),
            OnClick = _ =>
            {
                var local = PlayerControl.LocalPlayer;
                if (!local) return;
                var pos = local.GetTruePosition();
                var data = new byte[8];
                BitConverter.GetBytes(pos.x).CopyTo(data, 0);
                BitConverter.GetBytes(pos.y).CopyTo(data, 4);
                SendToAll(ActionFreeze, data);
            },
        });
    }

    public override void ResetState() { frozenUntil = 0f; RemoveOverlay(); }
    public override void OnMeetingStart() { frozenUntil = 0f; RemoveOverlay(); }

    public override float SpeedMultiplier(PlayerControl local) =>
        IsAlive(local) && frozenUntil > Time.time && !InMeeting ? 0f : 1f;

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionFreeze || data.Length < 8 || !RoleRegistry.Has(sender, this)) return;
        var pos = new Vector2(BitConverter.ToSingle(data, 0), BitConverter.ToSingle(data, 4));

        var local = PlayerControl.LocalPlayer;
        if (!IsAlive(local) || local == sender) return;                 // der Frostbeisser bleibt frei
        if (WinterImmunity.Has(local.PlayerId)) return;                 // Thermo/Kamin schuetzt
        if (Vector2.Distance(local.GetTruePosition(), pos) > radius.Value) return;

        frozenUntil = Time.time + duration.Value;
        ShowOverlay();
        Sfx.Play(Sfx.TaskUpdate, 0.7f);
        Notifier.Show($"<color={ColorHex}>{Lang.T("Eingefroren!", "Frozen!")}</color>", duration.Value);
    }

    public override void Update()
    {
        if (frozenUntil > 0f && Time.time >= frozenUntil) { frozenUntil = 0f; RemoveOverlay(); }
        if (iceOverlay && !Camera.main) RemoveOverlay();
    }

    private void ShowOverlay()
    {
        RemoveOverlay();
        var hud = HudManager.Instance;
        var cam = Camera.main;
        if (!cam) return;
        iceOverlay = new GameObject("Spieleabend_Ice");
        iceOverlay.transform.SetParent(cam.transform, false);
        var sr = iceOverlay.AddComponent<SpriteRenderer>();
        sr.sprite = Shapes.Square();
        sr.color = new Color(0.7f, 0.9f, 1f, 0.5f);
        if (hud && hud.ShadowQuad) { sr.sortingLayerID = hud.ShadowQuad.sortingLayerID; sr.sortingOrder = hud.ShadowQuad.sortingOrder + 1; }
        float h = cam.orthographicSize * 2f * 1.1f, w = h * cam.aspect;
        var size = sr.sprite.bounds.size;
        float z = cam.transform.InverseTransformPoint(hud && hud.ShadowQuad ? hud.ShadowQuad.transform.position : cam.transform.position + cam.transform.forward).z - 0.05f;
        iceOverlay.transform.localPosition = new Vector3(0f, 0f, Mathf.Max(0.2f, z));
        iceOverlay.transform.localScale = new Vector3(w / size.x, h / size.y, 1f);
    }

    private void RemoveOverlay() { if (iceOverlay) Object.Destroy(iceOverlay); iceOverlay = null; }
}
