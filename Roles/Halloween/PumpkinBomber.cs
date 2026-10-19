using System;
using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles.Halloween;

/// <summary>
/// Impostor: legt einen Kuerbis, der nach einer Weile explodiert. Alle sehen den Kuerbis und den Explosions-Radius
/// (geht auch durch Waende). Wer beim Knall im Radius ist, stirbt - andere Kuerbisbomber ausgenommen.
/// </summary>
public sealed class PumpkinBomber : CustomRole
{
    public override string ConfigSection => "Kuerbisbomber";
    public override string Name => Lang.T("Kürbisbomber", "Pumpkin Bomber");
    public override Color Color => new Color32(240, 140, 40, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Lass es krachen", "Make it go boom");
    public override string Description => Lang.T("Leg eine Kürbisbombe (F).\nSie explodiert nach einer Weile.", "Plant a pumpkin bomb (F).\nIt blows up after a while.");
    public override string LongDescription => Lang.T(
        "Impostor: Mit dem Knopf legst du einen Kürbis ab. Alle sehen ihn und den Explosions-Radius (der geht auch durch Wände). Nach einer Weile explodiert er - wer dann im Radius ist, stirbt. Achtung: Es trifft jeden im Radius, auch andere Impostor und dich selbst!",
        "Impostor: the button plants a pumpkin. Everyone sees it and the blast radius (which goes through walls). After a while it explodes - whoever is inside the radius dies. Careful: it hits everyone in range, including other impostors and yourself!");

    private const byte ActionPlant = 0;
    private static Sprite ringSprite;

    private RoleOption cooldown, fuse, radius;

    private sealed class Bomb { public Vector2 Pos; public float ExplodeAt; public GameObject Pumpkin; public GameObject Ring; }
    private readonly List<Bomb> bombs = new();

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 35f, 10f, 90f, 2.5f, "s");
        fuse = AddOption("Fuse", "Zündzeit|Fuse", 20f, 5f, 40f, 1f, "s");
        radius = AddOption("Radius", "Radius|Radius", 2f, 1f, 6f, 0.5f);

        AddButton(new RoleButton
        {
            Label = "Kürbisbombe|Pumpkin bomb",
            Icon = "pumpkin",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            OnClick = _ =>
            {
                var local = PlayerControl.LocalPlayer;
                if (!local) return;
                var pos = local.GetTruePosition();
                var data = new byte[8];
                BitConverter.GetBytes(pos.x).CopyTo(data, 0);
                BitConverter.GetBytes(pos.y).CopyTo(data, 4);
                SendToAll(ActionPlant, data);
            },
        });
    }

    public override void ResetState()
    {
        foreach (var b in bombs) { if (b.Pumpkin) Object.Destroy(b.Pumpkin); if (b.Ring) Object.Destroy(b.Ring); }
        bombs.Clear();
    }

    public override void OnMeetingStart() => ResetState();

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionPlant || data.Length < 8) return;
        var pos = new Vector2(BitConverter.ToSingle(data, 0), BitConverter.ToSingle(data, 4));
        float r = radius.Value;

        var pumpkin = new GameObject("Spieleabend_PumpkinBomb");
        if (ShipStatus.Instance) pumpkin.transform.SetParent(ShipStatus.Instance.transform, true);
        pumpkin.transform.position = new Vector3(pos.x, pos.y, pos.y / 1000f - 0.01f);
        pumpkin.transform.localScale = Vector3.one * 0.5f;
        pumpkin.AddComponent<SpriteRenderer>().sprite = ButtonIcons.Get("pumpkin", null);

        var ring = new GameObject("Spieleabend_PumpkinRadius");
        if (ShipStatus.Instance) ring.transform.SetParent(ShipStatus.Instance.transform, true);
        ring.transform.position = new Vector3(pos.x, pos.y, pos.y / 1000f + 0.03f);
        ring.transform.localScale = Vector3.one * (r * 2f);
        var rs = ring.AddComponent<SpriteRenderer>();
        rs.sprite = RingSprite();
        rs.color = new Color(1f, 0.5f, 0.1f, 0.35f);

        bombs.Add(new Bomb { Pos = pos, ExplodeAt = Time.time + fuse.Value, Pumpkin = pumpkin, Ring = ring });
    }

    public override void Update()
    {
        float t = Time.time;
        for (int i = bombs.Count - 1; i >= 0; i--)
        {
            var b = bombs[i];
            // Ring blinkt die ganze Zeit, kurz vor dem Knall schneller
            if (b.Ring)
            {
                float left = b.ExplodeAt - t;
                float speed = left < 3f ? 12f : 5f;
                float blink = Mathf.Sin(t * speed) * 0.5f + 0.5f;
                var rr = b.Ring.GetComponent<SpriteRenderer>();
                if (rr) rr.color = new Color(1f, 0.5f, 0.1f, Mathf.Clamp01(0.2f + blink * 0.4f));
            }

            if (t < b.ExplodeAt) continue;
            Explode(b);
            bombs.RemoveAt(i);
        }
    }

    private void Explode(Bomb b)
    {
        // Opfer auf jedem Client gleich berechnen (fuer den Sound); der Host loest die Kills aus.
        int willDie = 0;
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!IsAlive(p) || InMeeting || p.inVent) continue;    // im Vent ist man sicher
            if (Vector2.Distance(p.GetTruePosition(), b.Pos) > radius.Value) continue;
            willDie++;
            if (AmHost) KillSystem.ForceKill(p, p);               // Selbst-Kill: kein Teleport, trifft auch Impostor/eigene Bomber
        }

        // Kill-Geraeusch nur fuer den Bomber und nur, wenn die Bombe wirklich jemanden getroffen hat
        var local = PlayerControl.LocalPlayer;
        if (willDie > 0 && local && RoleRegistry.Has(local, this)) Sfx.Play(Sfx.Kill);

        if (b.Pumpkin) Object.Destroy(b.Pumpkin);
        if (b.Ring) Object.Destroy(b.Ring);
    }

    private static Sprite RingSprite()
    {
        if (ringSprite) return ringSprite;
        const int S = 128; float c = (S - 1) / 2f;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
            float ring = Mathf.Exp(-Mathf.Pow((d - 0.9f) / 0.07f, 2f));
            float fill = d < 0.9f ? 0.25f : 0f;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(fill + ring) * (d > 1f ? 0f : 1f)));
        }
        tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        ringSprite.hideFlags = HideFlags.HideAndDontSave;
        return ringSprite;
    }
}
