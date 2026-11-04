using System;
using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor (Thor): wirft einen Hammer in die angeklickte Richtung. Der Hammer fliegt ueber die ganze Karte, auch
/// durch Waende, und toetet jeden Nicht-Impostor, an dem er vorbeifliegt. Ein zweiter Knopfdruck ruft ihn zurueck;
/// nach einem Meeting/Report ist der Hammer wieder bei Thor.
/// </summary>
public sealed class ThorRole : CustomRole
{
    public override string ConfigSection => "Thor";
    public override string Name => Lang.T("Thor", "Thor");
    public override Color Color => new Color32(120, 170, 230, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Wirf den Hammer", "Throw the hammer");
    public override string Description => Lang.T("Knopf, dann Richtung klicken (F).\nHammer tötet unterwegs.", "Button, then click a direction (F).\nHammer kills on the way.");
    public override string LongDescription => Lang.T(
        "Impostor: Mit Taste F zielst du - danach klickst du auf den Bildschirm, und dein Hammer fliegt in diese Richtung über die ganze Karte, auch durch Wände, und tötet jeden Nicht-Impostor, an dem er vorbeifliegt. Am Ende schwebt er und wartet. Ein zweiter Druck ruft ihn zurück - auch auf dem Rückweg tötet er. Solange er unterwegs ist, kannst du weder erneut werfen noch normal killen (gemeinsamer Cooldown). Ist er wieder da, läuft 30 s Abklingzeit. Nach einem Meeting ist er wieder bei dir.",
        "Impostor: press F to aim - then click on screen and your hammer flies that way across the whole map, through walls, killing every non-impostor it passes. At the end it hovers and waits. A second press recalls it - it kills on the way back too. While it's out you can neither throw again nor kill normally (shared cooldown). Once it's back, a 30 s cooldown runs. After a meeting it's back with you.");

    private const byte ActionThrow = 0, ActionRecall = 1;
    private const float KillRadius = 0.7f;
    private const float MaxReach = 40f; // fliegt quer über die Map und schwebt dann, bis er zurückgerufen wird

    private RoleOption speed;
    private RoleButton button;
    private bool aiming;
    private float armedAt;
    private sealed class Hammer { public byte Owner; public Vector2 Pos; public Vector2 Dir; public bool Recall; public GameObject Obj; public float Travel; public float NextWhoosh; }
    private readonly Dictionary<byte, Hammer> hammers = new();

    protected override void Setup()
    {
        speed = AddOption("Speed", "Tempo|Speed", 9f, 4f, 20f, 1f);

        button = AddButton(new RoleButton
        {
            Label = "Hammer|Hammer",
            GameIcon = _ => HammerSprite(),
            Icon = "bolt",
            NeedsTarget = false,
            Cooldown = () => 0.2f,                 // kurze Klicksperre; die echten 30 s setzt die Rückkehr
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            // Nicht klickbar beim Zielen und solange der Hammer zurückfliegt (bis er wieder da ist).
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer) && !aiming && !LocalRecalling(),
            OnClick = _ => OnButton(),
        });
    }

    private bool LocalRecalling()
    {
        var l = PlayerControl.LocalPlayer;
        return l && hammers.TryGetValue(l.PlayerId, out var h) && h.Recall;
    }

    private void OnButton()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local) return;
        if (hammers.TryGetValue(local.PlayerId, out var h))
        {
            if (!h.Recall) SendToAll(ActionRecall, local.PlayerId); // Hammer zurückrufen
            return;                                                  // fliegt zurück -> kein weiterer Klick
        }
        aiming = true; armedAt = Time.time; Sfx.Play(Sfx.Investigate, 0.5f);
    }

    public override void ResetState() { aiming = false; AimLine.Hide(); Clear(); }
    public override void OnMeetingStart() { aiming = false; AimLine.Hide(); Clear(); }

    private void Clear()
    {
        foreach (var h in hammers.Values) if (h.Obj) Object.Destroy(h.Obj);
        hammers.Clear();
    }

    private void Throw(Vector2 dir)
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || hammers.ContainsKey(local.PlayerId)) return;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir = dir.normalized;
        Vector2 origin = local.GetTruePosition();
        var data = new byte[17];
        BitConverter.GetBytes(origin.x).CopyTo(data, 0);
        BitConverter.GetBytes(origin.y).CopyTo(data, 4);
        BitConverter.GetBytes(dir.x).CopyTo(data, 8);
        BitConverter.GetBytes(dir.y).CopyTo(data, 12);
        data[16] = local.PlayerId;
        SendToAll(ActionThrow, data);
        try { local.SetKillTimer(30f); } catch { } // normaler Kill ist gesperrt, solange der Hammer unterwegs ist
    }

    /// <summary>Freeplay-Test: ein Thor-Dummy wirft seinen Hammer auf den lokalen Spieler (lokal, direkt).</summary>
    public override bool FreeplayAbility(PlayerControl attacker, PlayerControl target)
    {
        if (hammers.TryGetValue(attacker.PlayerId, out var existing)) { existing.Recall = true; return true; }
        Vector2 origin = attacker.GetTruePosition();
        Vector2 dir = (Vector2)target.GetTruePosition() - origin;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        SpawnHammer(attacker.PlayerId, origin, dir.normalized, true);
        return true;
    }

    private void SpawnHammer(byte owner, Vector2 pos, Vector2 dir, bool sound)
    {
        if (hammers.ContainsKey(owner)) return;
        var obj = new GameObject("Spieleabend_ThorHammer");
        if (ShipStatus.Instance) obj.transform.SetParent(ShipStatus.Instance.transform, true);
        obj.transform.localScale = Vector3.one * 0.5f;
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = HammerSprite();
        sr.color = Color.white;
        hammers[owner] = new Hammer { Owner = owner, Pos = pos, Dir = dir, Obj = obj };
        if (sound) Sfx.Play(Sfx.Splash, 0.8f); // Wurf-Zischen
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionThrow && data.Length >= 17 && RoleRegistry.Has(sender, this))
        {
            byte owner = data[16];
            var pos = new Vector2(BitConverter.ToSingle(data, 0), BitConverter.ToSingle(data, 4));
            var dir = new Vector2(BitConverter.ToSingle(data, 8), BitConverter.ToSingle(data, 12));
            SpawnHammer(owner, pos, dir, sender == PlayerControl.LocalPlayer);
        }
        else if (action == ActionRecall && data.Length >= 1)
        {
            if (hammers.TryGetValue(data[0], out var h)) h.Recall = true;
        }
    }

    public override void Update()
    {
        // Zielen: nach dem Knopfdruck auf den Bildschirm klicken
        if (aiming)
        {
            var local = PlayerControl.LocalPlayer;
            if (InMeeting || !IsAlive(local)) { aiming = false; AimLine.Hide(); }
            else
            {
                AimLine.Show(local.GetTruePosition(), Color);
                if (Input.GetMouseButtonDown(1) || Time.time - armedAt > 6f) { aiming = false; AimLine.Hide(); if (button != null) button.Timer = 0f; }
                else if (Input.GetMouseButtonDown(0) && Time.time - armedAt > 0.08f && !Ui.IsTyping)
                {
                    aiming = false;
                    AimLine.Hide();
                    Throw(AimLine.MouseDir(local.GetTruePosition()));
                }
            }
        }

        var localP = PlayerControl.LocalPlayer;
        if (hammers.Count == 0) return;
        float dt = Time.deltaTime;
        var remove = new List<byte>();
        var returnedHome = new List<byte>();
        foreach (var h in hammers.Values)
        {
            var owner = FindPlayer(h.Owner);
            bool moving = true;
            if (h.Recall)
            {
                if (!owner) { remove.Add(h.Owner); continue; }
                Vector2 to = owner.GetTruePosition() - h.Pos;
                if (to.magnitude < 0.6f) { remove.Add(h.Owner); returnedHome.Add(h.Owner); continue; }
                h.Dir = to.normalized;
            }
            else if (h.Travel >= MaxReach)
            {
                moving = false; // am Ziel angekommen -> schwebt und wartet auf den Rückruf
            }

            if (moving)
            {
                float step = speed.Value * dt;
                h.Pos += h.Dir * step;
                h.Travel += step;
            }
            if (h.Obj)
            {
                h.Obj.transform.position = new Vector3(h.Pos.x, h.Pos.y, h.Pos.y / 1000f - 0.05f);
                h.Obj.transform.Rotate(0f, 0f, 720f * dt); // Hammer dreht sich im Flug
            }

            // Vorbeiflug-Zischen, wenn der Hammer nah am lokalen Spieler vorbeifliegt
            if (localP && Time.time >= h.NextWhoosh && Vector2.Distance(localP.GetTruePosition(), h.Pos) <= 2.5f)
            {
                h.NextWhoosh = Time.time + 0.35f;
                Sfx.Play(Sfx.Splash, 0.35f);
            }

            // Tötet in BEIDE Richtungen (Hin- und Rückflug) - auch Impostor, nur nicht den Werfer selbst
            if (AmHost)
                foreach (var p in PlayerControl.AllPlayerControls)
                    if (IsAlive(p) && p.PlayerId != h.Owner && !p.inVent
                        && Vector2.Distance(p.GetTruePosition(), h.Pos) <= KillRadius)
                    {
                        KillSystem.ForceKill(p, p);
                        if (localP && h.Owner == localP.PlayerId) Sfx.Play(Sfx.Kill, 0.8f); // Kill-Sound für den Werfer
                    }
        }
        foreach (var id in remove) { if (hammers.TryGetValue(id, out var h) && h.Obj) Object.Destroy(h.Obj); hammers.Remove(id); }

        // Hammer wieder da (beim lokalen Werfer): 30 s Abklingzeit auf Hammer UND Kill.
        foreach (var id in returnedHome)
            if (localP && id == localP.PlayerId)
            {
                if (button != null) button.Timer = 30f;
                try { localP.SetKillTimer(30f); } catch { }
            }

        // Solange der eigene Hammer unterwegs ist, bleibt der normale Kill gesperrt - nur selten nachtopfen (kein Flackern).
        if (localP && hammers.ContainsKey(localP.PlayerId) && localP.killTimer < 1f)
            try { localP.SetKillTimer(30f); } catch { }
    }

    // ---------- Eigenes Hammer-Symbol ----------
    private static Sprite hammerSprite;
    private static Sprite HammerSprite()
    {
        if (hammerSprite) return hammerSprite;
        const int S = 96;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) tex.SetPixel(x, y, Color.clear);
        var metal = new Color(0.78f, 0.82f, 0.9f, 1f);
        var handle = new Color(0.55f, 0.38f, 0.22f, 1f);
        // Kopf (breiter Block oben)
        for (int y = 60; y <= 84; y++)
        for (int x = 22; x <= 74; x++)
            tex.SetPixel(x, y, metal);
        // Stiel (senkrecht nach unten)
        for (int y = 10; y < 62; y++)
        for (int x = 42; x <= 54; x++)
            tex.SetPixel(x, y, handle);
        tex.Apply();
        hammerSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        hammerSprite.hideFlags = HideFlags.HideAndDontSave;
        return hammerSprite;
    }
}
