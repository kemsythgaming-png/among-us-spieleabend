using System;
using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor (Lawinenleger): markiert eine Stelle. Nach ein paar Sekunden geht dort eine kleine Lawine ab - wer den
/// markierten Bereich bis dahin nicht verlassen hat, stirbt. Der Bereich ist fuer alle sichtbar.
/// </summary>
public sealed class AvalancheSetter : CustomRole
{
    public override string ConfigSection => "Lawinenleger";
    public override string Name => Lang.T("Lawinenleger", "Avalanche Setter");
    public override Color Color => new Color32(210, 230, 245, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Raus aus dem Gang", "Clear the corridor");
    public override string Description => Lang.T("Markiere einen Bereich (F).\nNach 5 s: Lawine.", "Mark an area (F).\nAfter 5 s: avalanche.");
    public override string LongDescription => Lang.T(
        "Impostor: Mit Taste F markierst du einen Bereich (alle sehen ihn). Nach 5 Sekunden geht dort eine Schneelawine ab - wer den Bereich bis dahin nicht verlassen hat, stirbt. Danach läuft eine Abklingzeit.",
        "Impostor: press F to mark an area (everyone sees it). After 5 seconds a snow avalanche hits - whoever hasn't left the area by then dies. Then a cooldown runs.");

    private const byte ActionSet = 0;

    private RoleOption cooldown, fuse, radius;
    private sealed class Slide { public Vector2 Pos; public float At; public GameObject Ring; }
    private readonly List<Slide> slides = new();

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 30f, 10f, 90f, 5f, "s");
        fuse = AddOption("Fuse", "Vorwarnzeit|Warning", 5f, 2f, 15f, 1f, "s");
        radius = AddOption("Radius", "Radius|Radius", 2.5f, 1f, 6f, 0.5f);

        AddButton(new RoleButton
        {
            Label = "Lawine legen|Set avalanche",
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
                SendToAll(ActionSet, data);
            },
        });
    }

    public override void ResetState() => Clear();
    public override void OnMeetingStart() => Clear();

    private void Clear()
    {
        foreach (var s in slides) if (s.Ring) Object.Destroy(s.Ring);
        slides.Clear();
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionSet || data.Length < 8 || !RoleRegistry.Has(sender, this)) return;
        var pos = new Vector2(BitConverter.ToSingle(data, 0), BitConverter.ToSingle(data, 4));

        var ring = new GameObject("Spieleabend_Avalanche");
        if (ShipStatus.Instance) ring.transform.SetParent(ShipStatus.Instance.transform, true);
        ring.transform.position = new Vector3(pos.x, pos.y, pos.y / 1000f + 0.02f);
        ring.transform.localScale = Vector3.one * (radius.Value * 2f);
        var rs = ring.AddComponent<SpriteRenderer>();
        rs.sprite = Shapes.Ring();
        rs.color = new Color(0.8f, 0.9f, 1f, 0.5f);
        slides.Add(new Slide { Pos = pos, At = Time.time + fuse.Value, Ring = ring });
    }

    public override void Update()
    {
        float t = Time.time;
        for (int i = slides.Count - 1; i >= 0; i--)
        {
            var s = slides[i];
            if (s.Ring)
            {
                float left = s.At - t;
                float blink = Mathf.Sin(t * (left < 2f ? 12f : 5f)) * 0.5f + 0.5f;
                var rr = s.Ring.GetComponent<SpriteRenderer>();
                if (rr) rr.color = new Color(0.8f, 0.9f, 1f, Mathf.Clamp01(0.25f + blink * 0.4f));
            }
            if (t < s.At) continue;

            int willDie = 0;
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (!IsAlive(p) || InMeeting || p.inVent) continue;
                if (Vector2.Distance(p.GetTruePosition(), s.Pos) > radius.Value) continue;
                willDie++;
                if (AmHost) KillSystem.ForceKill(p, p);
            }
            var local = PlayerControl.LocalPlayer;
            if (willDie > 0 && local && RoleRegistry.Has(local, this)) Sfx.Play(Sfx.Kill, 0.6f);
            if (s.Ring) Object.Destroy(s.Ring);
            slides.RemoveAt(i);
        }
    }
}
