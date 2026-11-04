using System;
using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor (Zauberer): Knopf druecken, dann auf den Bildschirm klicken, um die Richtung zu waehlen. Ein Magiestrahl
/// schiesst dorthin, bis zur eingestellten Reichweite oder zur naechsten Wand. Der erste getroffene Spieler stirbt.
/// </summary>
public sealed class Wizard : CustomRole
{
    public override string ConfigSection => "Zauberer";
    public override string Name => Lang.T("Zauberer", "Wizard");
    public override Color Color => new Color32(150, 80, 220, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Ein Strahl genügt", "One beam is enough");
    public override string Description => Lang.T("Knopf, dann Richtung klicken (F).\nStrahl geht durch Wände.", "Button, then click a direction (F).\nBeam goes through walls.");
    public override string LongDescription => Lang.T(
        "Impostor: Mit Taste F zielst du - danach klickst du irgendwo auf den Bildschirm, und ein Magiestrahl schießt in diese Richtung quer über die Karte, auch durch Wände. ALLE Spieler auf der Linie sterben. Danach läuft eine Abklingzeit.",
        "Impostor: press F to aim - then click anywhere on screen and a magic beam shoots that way across the map, through walls. EVERY player on the line dies. Then a cooldown runs.");

    private const byte ActionKill = 0, ActionBeam = 1;

    private RoleOption range, band;
    private RoleButton button;
    private bool aiming;
    private float armedAt;

    private sealed class Beam { public GameObject Obj; public SpriteRenderer Sr; public float Born; }
    private readonly List<Beam> beams = new();

    protected override void Setup()
    {
        range = AddOption("Range", "Reichweite|Range", 40f, 10f, 60f, 5f); // quer über die Map
        band = AddOption("Band", "Trefferbreite|Hit width", 0.8f, 0.3f, 2f, 0.1f);

        button = AddButton(new RoleButton
        {
            Label = "Magiestrahl|Magic beam",
            GameIcon = _ => WandSprite(),
            Icon = "shoot",
            NeedsTarget = false,
            Cooldown = () => 45f,          // fest 45 s
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer) && !aiming,
            OnClick = _ => { aiming = true; armedAt = Time.time; Sfx.Play(Sfx.Investigate, 0.5f); },
        });
    }

    /// <summary>Beim Zielen bleibt der Zauberer stehen.</summary>
    public override float SpeedMultiplier(PlayerControl local) =>
        IsLocalRole && aiming && IsAlive(local) && !InMeeting ? 0f : 1f;

    public override void Update()
    {
        // Zielen: nach dem Knopfdruck auf den Bildschirm klicken
        if (aiming)
        {
            var local = PlayerControl.LocalPlayer;
            if (InMeeting || !IsAlive(local)) { aiming = false; AimLine.Hide(); }
            else
            {
                AimLine.Show(local.GetTruePosition(), Color); // Ziel-Pfeil, damit man „wohin" sieht
                if (Input.GetMouseButtonDown(1) || Time.time - armedAt > 6f) { aiming = false; AimLine.Hide(); button.Timer = 0f; }
                else if (Input.GetMouseButtonDown(0) && Time.time - armedAt > 0.08f && !Ui.IsTyping)
                {
                    aiming = false;
                    AimLine.Hide();
                    Fire(AimLine.MouseDir(local.GetTruePosition()));
                }
            }
        }

        for (int i = beams.Count - 1; i >= 0; i--)
        {
            var b = beams[i];
            float p = (Time.time - b.Born) / 1.2f; // deutlich länger sichtbar
            if (!b.Obj || p >= 1f) { if (b.Obj) Object.Destroy(b.Obj); beams.RemoveAt(i); continue; }
            var c = b.Sr.color; b.Sr.color = new Color(c.r, c.g, c.b, 0.95f * (1f - p));
        }
    }

    private void Fire(Vector2 dir)
    {
        var local = PlayerControl.LocalPlayer;
        if (!local) return;
        Vector2 origin = local.GetTruePosition();
        float r = range.Value;

        // ALLE Spieler auf der Strahllinie (proj) im schmalen Band (perp) - durch Wände, mehrere Treffer möglich
        var hits = PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p && p != local && IsAlive(p) && !p.inVent)
            .Select(p => (p, d: (Vector2)p.GetTruePosition() - origin))
            .Select(t => (t.p, proj: Vector2.Dot(t.d, dir), perp: (t.d - dir * Vector2.Dot(t.d, dir)).magnitude))
            .Where(t => t.proj > 0f && t.proj <= r && t.perp <= band.Value)
            .Select(t => t.p);

        byte ci = (byte)UnityEngine.Random.Range(0, BeamColors.Length);
        var data = new byte[17];
        BitConverter.GetBytes(origin.x).CopyTo(data, 0);
        BitConverter.GetBytes(origin.y).CopyTo(data, 4);
        BitConverter.GetBytes(dir.x).CopyTo(data, 8);
        BitConverter.GetBytes(dir.y).CopyTo(data, 12);
        data[16] = ci;
        SendToAll(ActionBeam, data);

        bool any = false;
        foreach (var hit in hits) { SendToHost(ActionKill, hit.PlayerId); any = true; }
        if (any) Sfx.Play(Sfx.Kill, 0.9f); // Kill-Sound für den Zauberer
    }

    // Verschiedene, kräftige Strahlfarben (zufällig je Schuss)
    private static readonly Color[] BeamColors =
    {
        new Color(0.70f, 0.35f, 1f),   // Violett
        new Color(1f, 0.35f, 0.5f),    // Pink
        new Color(0.3f, 0.8f, 1f),     // Cyan
        new Color(0.4f, 1f, 0.5f),     // Grün
        new Color(1f, 0.8f, 0.3f),     // Gold
        new Color(1f, 0.5f, 0.2f),     // Orange
    };

    /// <summary>Freeplay-Test: ein Zauberer-Dummy schießt seinen Strahl auf den lokalen Spieler (lokal, direkt).</summary>
    public override bool FreeplayAbility(PlayerControl attacker, PlayerControl target)
    {
        Vector2 origin = attacker.GetTruePosition();
        Vector2 dir = (Vector2)target.GetTruePosition() - origin;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir = dir.normalized;
        ShowBeam(origin, dir, BeamColors[UnityEngine.Random.Range(0, BeamColors.Length)]);
        Sfx.Play(Sfx.Splash, 0.7f);
        float r = range.Value;
        foreach (var p in PlayerControl.AllPlayerControls.ToArray())
        {
            if (!p || p == attacker || !IsAlive(p) || p.inVent) continue;
            Vector2 d = (Vector2)p.GetTruePosition() - origin;
            float proj = Vector2.Dot(d, dir), perp = (d - dir * proj).magnitude;
            if (proj > 0f && proj <= r && perp <= band.Value) KillSystem.RangedKill(attacker, p);
        }
        return true;
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionKill)
        {
            if (!AmHost || data.Length < 1 || !RoleRegistry.Has(sender, this) || !IsAlive(sender)) return;
            var target = FindPlayer(data[0]);
            if (IsAlive(target)) KillSystem.RangedKill(sender, target); // kein Teleport zum Opfer
        }
        else if (action == ActionBeam && data.Length >= 16)
        {
            var origin = new Vector2(BitConverter.ToSingle(data, 0), BitConverter.ToSingle(data, 4));
            var dir = new Vector2(BitConverter.ToSingle(data, 8), BitConverter.ToSingle(data, 12));
            var col = data.Length >= 17 ? BeamColors[data[16] % BeamColors.Length] : (Color)Color;
            ShowBeam(origin, dir, col);
            Sfx.Play(Sfx.Splash, 0.7f); // "ZOOOM"-Zischen für alle in der Nähe
        }
    }

    private void ShowBeam(Vector2 origin, Vector2 dir, Color col)
    {
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir = dir.normalized;
        float r = range.Value;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        var go = new GameObject("Spieleabend_WizardBeam");
        if (ShipStatus.Instance) go.transform.SetParent(ShipStatus.Instance.transform, true);
        // z klar nach vorne ziehen, damit der Strahl nicht hinter Boden/Objekten verschwindet
        go.transform.position = new Vector3(origin.x + dir.x * r * 0.5f, origin.y + dir.y * r * 0.5f, -5f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, ang);
        go.transform.localScale = new Vector3(r, 0.6f, 1f); // dicker
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Shapes.Square();
        sr.color = new Color(col.r, col.g, col.b, 0.95f);
        AimLine.OnTop(sr); // über der Dunkelheit zeichnen, auch durch Wände sichtbar
        beams.Add(new Beam { Obj = go, Sr = sr, Born = Time.time });
    }

    public override void ResetState() { aiming = false; AimLine.Hide(); ClearBeams(); }
    public override void OnMeetingStart() { aiming = false; AimLine.Hide(); ClearBeams(); }

    private void ClearBeams()
    {
        foreach (var b in beams) if (b.Obj) Object.Destroy(b.Obj);
        beams.Clear();
    }

    // ---------- Eigenes Zauberstab-Symbol (Stab mit Funkel-Stern) ----------
    private static Sprite wandSprite;
    private static Sprite WandSprite()
    {
        if (wandSprite) return wandSprite;
        const int S = 96;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) tex.SetPixel(x, y, Color.clear);
        var wood = new Color(0.8f, 0.7f, 0.95f, 1f);
        var star = new Color(1f, 0.95f, 0.5f, 1f);
        // Stab (diagonal von unten-links nach oben-rechts)
        for (int t = 0; t < 60; t++)
        {
            int x = 18 + t, y = 18 + t;
            for (int w = -3; w <= 3; w++)
            {
                int xx = x + w; if (xx >= 0 && xx < S && y >= 0 && y < S) tex.SetPixel(xx, y, wood);
            }
        }
        // Funkel-Stern oben rechts (Spitze bei ~78,78)
        var cC = new Vector2(78f, 78f);
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            var d = new Vector2(x, y) - cC;
            float ang = Mathf.Atan2(d.y, d.x);
            float spikes = 0.5f + 0.5f * Mathf.Abs(Mathf.Cos(ang * 2f));
            if (d.magnitude < 16f * spikes) tex.SetPixel(x, y, star);
        }
        tex.Apply();
        wandSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        wandSprite.hideFlags = HideFlags.HideAndDontSave;
        return wandSprite;
    }
}
