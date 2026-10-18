using System;
using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles.Ghosts;

/// <summary>Impostor-Geist: legt eine Frost-Zone (Kreis) ab. Wer darin steht, bewegt sich 9x langsamer - fuer 10 Sekunden.</summary>
public sealed class VengefulSpirit : CustomRole
{
    public override string ConfigSection => "Rachegeist";
    public override string Name => Lang.T("Rachegeist", "Vengeful Spirit");
    public override Color Color => new Color32(150, 220, 255, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override bool IsGhostRole => true;
    public override bool CanHaunt(PlayerControl dead) => IsImpostor(dead);
    public override string IntroText => Lang.T("Auch tot hilfst du deinem Team", "Even dead you help your team");
    public override string Description => Lang.T("Geist: Leg eine Frost-Zone ab (F).\nDarin sind alle extrem langsam.", "Ghost: drop a frost zone (F).\nEveryone inside is very slow.");
    public override string LongDescription => Lang.T(
        "Geisterrolle für tote Impostor. Schwebe an eine Stelle und drück F: Dort erscheint ein Frost-Kreis (etwa so groß wie der Admin-Tisch). Jeder lebende Crewmate oder Neutrale, der darin steht, bewegt sich 9-mal langsamer. Der Kreis bleibt 10 Sekunden liegen.",
        "Ghost role for dead impostors. Float to a spot and press F: a frost circle appears (about the size of the admin table). Any living crewmate or neutral standing inside moves 9x slower. The circle stays for 10 seconds.");

    private const byte ActionZone = 0;
    private const float SlowFactor = 1f / 9f, Radius = 1.8f;
    private static Sprite zoneSprite;

    private RoleOption cooldown, duration;

    private sealed class Zone { public Vector2 Pos; public float Until; public GameObject Obj; }
    private readonly List<Zone> zones = new();

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 40f, 15f, 90f, 2.5f, "s");
        duration = AddOption("Duration", "Zone-Dauer|Zone duration", 10f, 5f, 20f, 1f, "s");

        AddButton(new RoleButton
        {
            Label = "Frost-Zone|Frost zone",
            Icon = "frost",
            NeedsTarget = false,
            UsableWhenDead = true,
            Visible = () => !IsAlive(PlayerControl.LocalPlayer),
            Cooldown = () => cooldown.Value,
            StartCooldown = 15f,
            Hotkey = KeyCode.F,
            OnClick = _ =>
            {
                var local = PlayerControl.LocalPlayer;
                if (!local) return;
                var pos = local.GetTruePosition();
                var data = new byte[8];
                BitConverter.GetBytes(pos.x).CopyTo(data, 0);
                BitConverter.GetBytes(pos.y).CopyTo(data, 4);
                SendToAll(ActionZone, data);
            },
        });
    }

    public override void ResetState()
    {
        foreach (var z in zones) if (z.Obj) Object.Destroy(z.Obj);
        zones.Clear();
    }

    public override void OnMeetingStart() => ResetState();

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionZone || data.Length < 8 || !RoleRegistry.Has(sender, this)) return;
        var pos = new Vector2(BitConverter.ToSingle(data, 0), BitConverter.ToSingle(data, 4));

        var go = new GameObject("Spieleabend_FrostZone");
        if (ShipStatus.Instance) go.transform.SetParent(ShipStatus.Instance.transform, true);
        go.transform.position = new Vector3(pos.x, pos.y, pos.y / 1000f + 0.02f);
        go.transform.localScale = Vector3.one * (Radius * 2f);
        go.AddComponent<SpriteRenderer>().sprite = ZoneSprite();
        zones.Add(new Zone { Pos = pos, Until = Time.time + duration.Value, Obj = go });

        if (sender == PlayerControl.LocalPlayer) Sfx.Play(Sfx.Creepy);
    }

    public override void Update()
    {
        float t = Time.time;
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            if (t >= zones[i].Until || !zones[i].Obj)
            {
                if (zones[i].Obj) Object.Destroy(zones[i].Obj);
                zones.RemoveAt(i);
                continue;
            }
            // sanftes Pulsieren
            var sr = zones[i].Obj.GetComponent<SpriteRenderer>();
            if (sr) sr.color = new Color(0.6f, 0.86f, 1f, 0.35f + 0.1f * Mathf.Sin(t * 3f));
        }
    }

    public override float SpeedMultiplier(PlayerControl local)
    {
        if (!IsAlive(local) || IsImpostor(local) || InMeeting || zones.Count == 0) return 1f;
        Vector2 me = local.GetTruePosition();
        foreach (var z in zones)
            if (z.Until > Time.time && Vector2.Distance(me, z.Pos) <= Radius) return SlowFactor;
        return 1f;
    }

    /// <summary>Weicher, halbdurchsichtiger Eis-Kreis - im Code erzeugt.</summary>
    private static Sprite ZoneSprite()
    {
        if (zoneSprite) return zoneSprite;
        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f)) / 63.5f;
            float ring = Mathf.Exp(-Mathf.Pow((d - 0.9f) / 0.08f, 2f)); // heller Rand
            float fill = d < 1f ? 0.30f : 0f;
            float a = Mathf.Clamp01(fill + ring * 0.7f);
            if (d > 1f) a = 0f;
            tex.SetPixel(x, y, new Color(0.65f, 0.88f, 1f, a));
        }
        tex.Apply();
        zoneSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        zoneSprite.hideFlags = HideFlags.HideAndDontSave;
        return zoneSprite;
    }
}
