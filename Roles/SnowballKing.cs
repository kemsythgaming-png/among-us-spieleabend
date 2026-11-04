using System;
using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Neutral (Schneeballkoenig): Knopf druecken, dann auf den Bildschirm klicken - ein Schneeball fliegt in diese
/// Richtung und toetet den ersten Spieler, den er trifft (wie der Zauberer, nur als fliegende Kugel).
/// </summary>
public sealed class SnowballKing : CustomRole
{
    public override string ConfigSection => "Schneeballkoenig";
    public override string Name => Lang.T("Schneeballkönig", "Snowball King");
    public override Color Color => new Color32(120, 200, 255, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override string IntroText => Lang.T("Ein Treffer genügt", "One hit is enough");
    public override string Description => Lang.T("Knopf, dann Richtung klicken (F).\nBall prallt ab, tötet beim Treffer.", "Button, then click a direction (F).\nBall bounces, kills on hit.");
    public override string LongDescription => Lang.T(
        "Du bist neutral. Mit Taste F zielst du - danach klickst du auf den Bildschirm, und ein Schneeball fliegt in diese Richtung. Er prallt an Wänden ab (nicht durch Wände) und tötet den Spieler, den er trifft. Wo er liegen bleibt, bleibt ein weißer Fleck. Danach läuft eine Abklingzeit.",
        "You are neutral. Press F to aim - then click on screen and a snowball flies that way. It bounces off walls (not through them) and kills the player it hits. Where it stops, a white mark stays. Then a cooldown runs.");

    private const byte ActionBall = 0;
    private const float Speed = 11f;
    private const int MaxBounces = 4;

    private RoleOption cooldown, killRadius;
    private RoleButton button;
    private bool aiming;
    private float armedAt;

    private sealed class Ball { public byte Owner; public GameObject Obj; public Vector2 Pos; public Vector2 Dir; public float Travel; public float Max; public int Bounces; }
    private readonly List<Ball> balls = new();
    private readonly List<GameObject> splats = new();

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 20f, 5f, 60f, 2.5f, "s");
        killRadius = AddOption("KillRadius", "Trefferradius|Hit radius", 0.6f, 0.3f, 1.5f, 0.1f);

        button = AddButton(new RoleButton
        {
            Label = "Schneeball|Snowball",
            GameIcon = _ => SnowballSprite(),
            Icon = "frost",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer) && !aiming,
            OnClick = _ => { aiming = true; armedAt = Time.time; Sfx.Play(Sfx.Investigate, 0.5f); },
        });
    }

    public override void ResetState() { aiming = false; ClearBalls(); ClearSplats(); }
    public override void OnMeetingStart() { aiming = false; ClearBalls(); ClearSplats(); }

    public override void Update()
    {
        if (aiming)
        {
            var local = PlayerControl.LocalPlayer;
            if (InMeeting || !IsAlive(local)) { aiming = false; AimLine.Hide(); }
            else
            {
                AimLine.Show(local.GetTruePosition(), Color); // Wurfrichtung anzeigen
                if (Input.GetMouseButtonDown(1) || Time.time - armedAt > 6f) { aiming = false; AimLine.Hide(); button.Timer = 0f; }
                else if (Input.GetMouseButtonDown(0) && Time.time - armedAt > 0.08f && !Ui.IsTyping)
                {
                    aiming = false;
                    AimLine.Hide();
                    Fire(AimLine.MouseDir(local.GetTruePosition()));
                }
            }
        }

        float dt = Time.deltaTime;
        for (int i = balls.Count - 1; i >= 0; i--)
        {
            var b = balls[i];
            float step = Speed * dt;

            // Wand voraus? -> abprallen, nicht hindurch
            RaycastHit2D hit = default;
            try { hit = Physics2D.Raycast(b.Pos, b.Dir, step + 0.12f, Constants.ShipAndObjectsMask); } catch { }
            if (hit.collider != null)
            {
                b.Pos = hit.point - b.Dir * 0.06f;
                b.Dir = Vector2.Reflect(b.Dir, hit.normal).normalized;
                b.Bounces++;
                Sfx.Play(Sfx.Splash, 0.3f);
            }
            else
            {
                b.Pos += b.Dir * step;
            }
            b.Travel += step;

            if (b.Obj)
            {
                b.Obj.transform.position = new Vector3(b.Pos.x, b.Pos.y, b.Pos.y / 1000f - 0.2f);
                b.Obj.transform.Rotate(0f, 0f, 540f * dt); // rollt
            }

            // Treffer auf einen Spieler (Host entscheidet)
            bool killed = false;
            if (AmHost)
            {
                var king = FindPlayer(b.Owner);
                foreach (var p in PlayerControl.AllPlayerControls)
                    if (p && p.PlayerId != b.Owner && IsAlive(p) && !p.inVent
                        && Vector2.Distance(p.GetTruePosition(), b.Pos) <= killRadius.Value)
                    {
                        if (king) KillSystem.RangedKill(king, p);
                        killed = true;
                        break;
                    }
            }

            if (killed || !b.Obj || b.Travel >= b.Max || b.Bounces > MaxBounces)
                Explode(b, i);
        }
    }

    private void Fire(Vector2 dir)
    {
        var local = PlayerControl.LocalPlayer;
        if (!local) return;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir = dir.normalized;
        Vector2 origin = local.GetTruePosition();

        var data = new byte[17];
        BitConverter.GetBytes(origin.x).CopyTo(data, 0);
        BitConverter.GetBytes(origin.y).CopyTo(data, 4);
        BitConverter.GetBytes(dir.x).CopyTo(data, 8);
        BitConverter.GetBytes(dir.y).CopyTo(data, 12);
        data[16] = local.PlayerId;
        SendToAll(ActionBall, data);
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionBall && data.Length >= 17)
        {
            var origin = new Vector2(BitConverter.ToSingle(data, 0), BitConverter.ToSingle(data, 4));
            var dir = new Vector2(BitConverter.ToSingle(data, 8), BitConverter.ToSingle(data, 12));
            SpawnBall(data[16], origin, dir.normalized);
            Sfx.Play(Sfx.Splash, 0.6f);
        }
    }

    /// <summary>Ball verschwindet und hinterlässt einen weißen Fleck.</summary>
    private void Explode(Ball b, int index)
    {
        if (b.Obj)
        {
            var splat = b.Obj;
            splat.transform.rotation = Quaternion.identity;
            splat.transform.localScale = Vector3.one * 0.9f;
            var sr = splat.GetComponent<SpriteRenderer>();
            if (sr) sr.color = new Color(0.95f, 0.98f, 1f, 0.85f);
            splat.transform.position = new Vector3(b.Pos.x, b.Pos.y, b.Pos.y / 1000f + 0.2f); // auf dem Boden
            splats.Add(splat);
            b.Obj = null; // nicht mit zerstören
        }
        balls.RemoveAt(index);
    }

    private void ClearSplats()
    {
        foreach (var s in splats) if (s) Object.Destroy(s);
        splats.Clear();
    }

    private void SpawnBall(byte owner, Vector2 origin, Vector2 dir)
    {
        var go = new GameObject("Spieleabend_Snowball");
        if (ShipStatus.Instance) go.transform.SetParent(ShipStatus.Instance.transform, true);
        go.transform.position = new Vector3(origin.x, origin.y, origin.y / 1000f - 0.2f);
        go.transform.localScale = Vector3.one * 0.55f; // runder Ball
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SnowballSprite(); // schattierter Ball statt flacher Scheibe
        sr.color = Color.white;
        AimLine.OnTop(sr); // auch ohne Vision sichtbar
        balls.Add(new Ball { Owner = owner, Obj = go, Pos = origin, Dir = dir, Travel = 0f, Max = 60f });
    }

    private void ClearBalls()
    {
        foreach (var b in balls) if (b.Obj) Object.Destroy(b.Obj);
        balls.Clear();
    }

    // ---------- Schneeball-Symbol für den Knopf ----------
    private static Sprite snowballSprite;
    private static Sprite SnowballSprite()
    {
        if (snowballSprite) return snowballSprite;
        const int S = 96;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        var white = new Color(0.95f, 0.98f, 1f, 1f);
        var shade = new Color(0.72f, 0.82f, 0.95f, 1f);
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            var c = new Vector2(x - S / 2f, y - S / 2f) / (S / 2f);
            float d = c.magnitude;
            if (d <= 0.92f)
            {
                // leichte Schattierung unten rechts für Ball-Look
                float sh = Mathf.Clamp01((c.x + c.y) * 0.5f + 0.5f);
                tex.SetPixel(x, y, Color.Lerp(white, shade, Mathf.Clamp01(d * 0.6f) * (1f - sh)));
            }
            else tex.SetPixel(x, y, Color.clear);
        }
        tex.Apply();
        snowballSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        snowballSprite.hideFlags = HideFlags.HideAndDontSave;
        return snowballSprite;
    }
}
