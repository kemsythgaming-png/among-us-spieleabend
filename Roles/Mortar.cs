using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor (Mörser): Knopf drücken -> die Karte geht auf, du stehst still und klickst eine Stelle an. Von DEINER
/// Position fliegt dann eine Granate dorthin (Schatten + Geschoss im Bogen) und tötet beim Einschlag alle im Umkreis.
/// </summary>
public sealed class Mortar : CustomRole
{
    public override string ConfigSection => "Moerser";
    public override string Name => Lang.T("Mörser", "Mortar");
    public override Color Color => new Color32(180, 120, 60, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Feuer frei", "Open fire");
    public override string Description => Lang.T("Karte öffnen, Stelle anklicken (F).\nGranate fliegt hin, tötet im Umkreis.", "Open map, click a spot (F).\nShell flies there, kills in a radius.");
    public override string LongDescription => Lang.T(
        "Du bist Impostor. Mit Taste F geht die Karte auf - du stehst dabei still. Klick auf eine Stelle: Von deiner Position fliegt eine Mörsergranate im Bogen dorthin (Schatten läuft mit, Geschoss fliegt los). Beim Einschlag stirbt jeder im Umkreis - auch andere Impostor. Danach läuft eine Abklingzeit. Rechtsklick bricht das Zielen ab.",
        "You are impostor. Press F to open the map - you stand still. Click a spot: from your position a mortar shell arcs there (shadow follows, shell flies off). On impact everyone in the radius dies - other impostors too. Then a cooldown runs. Right-click cancels aiming.");

    private const byte ActionFire = 0, ActionBoom = 1;

    private RoleOption cooldown, radius, fuse;
    private RoleButton button;
    private readonly MapPicker picker = new();

    private sealed class Shell
    {
        public byte Owner; public Vector2 Start, Target; public float FiredAt, Fuse; public bool Exploded;
        public GameObject Shadow, Projectile, Flash, Core;
    }
    private readonly List<Shell> shells = new();
    private static Sprite discSprite;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 30f, 10f, 90f, 5f, "s");
        radius = AddOption("Radius", "Explosions-Radius|Blast radius", 2f, 0.8f, 4f, 0.2f);
        fuse = AddOption("Fuse", "Flugzeit|Flight time", 3.5f, 2f, 6f, 0.5f, "s");

        button = AddButton(new RoleButton
        {
            Label = "Mörser|Mortar",
            Icon = "bolt",
            GameIcon = _ => ProcIcons.Mortar(),
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer) && !picker.Active,
            OnClick = _ => { if (picker.Open()) Sfx.Play(Sfx.Investigate, 0.5f); },
        });
    }

    public override void ResetState()
    {
        picker.Cancel();
        foreach (var s in shells) DestroyShell(s);
        shells.Clear();
        if (frozen && PlayerControl.LocalPlayer) PlayerControl.LocalPlayer.moveable = true;
        frozen = false;
    }
    public override void OnMeetingStart() => ResetState();

    public override void Update()
    {
        // Zielen über die Karte: Klick -> Granate von der eigenen Position zum Ziel schicken.
        if (picker.Active && picker.Poll(out var tgt))
        {
            var local = PlayerControl.LocalPlayer;
            if (local)
            {
                var data = new byte[9];
                data[0] = local.PlayerId;
                WorldMarker.Encode(local.GetTruePosition()).CopyTo(data, 1);
                WorldMarker.Encode(tgt).CopyTo(data, 5);
                SendToAll(ActionFire, data);
            }
            else button.Timer = 0f;
        }

        float t = Time.time;
        for (int i = shells.Count - 1; i >= 0; i--)
        {
            var s = shells[i];

            if (!s.Exploded)
            {
                float k = Mathf.Clamp01((t - s.FiredAt) / s.Fuse);
                const float High = 7f; // Steighöhe
                if (k < 0.5f)
                {
                    // Aufstieg am Start: NICHT sichtbar (das Wegfliegen soll man nicht sehen).
                    SetShellVisible(s, false);
                }
                else
                {
                    SetShellVisible(s, true);
                    // Abstieg: Granate TAUCHT über dem Ziel auf und fällt herab; Schatten wächst zum Einschlag.
                    float u = (k - 0.5f) / 0.5f;
                    if (s.Projectile)
                    {
                        s.Projectile.transform.position = new Vector3(s.Target.x, s.Target.y + (1f - u) * High, s.Target.y / 1000f - 0.5f);
                        s.Projectile.transform.localScale = Vector3.one * Mathf.Lerp(0.16f, 0.32f, u);
                        SetAlpha(s.Projectile, Mathf.Lerp(0.15f, 1f, u));
                    }
                    if (s.Shadow)
                    {
                        s.Shadow.transform.position = new Vector3(s.Target.x, s.Target.y, s.Target.y / 1000f - 0.02f);
                        float sc = Mathf.Lerp(0.1f, radius.Value * 1.8f, u);
                        s.Shadow.transform.localScale = new Vector3(sc, sc, 1f);
                    }
                }
                if (k >= 1f) Explode(s);
            }
            else
            {
                float age = t - s.FiredAt - s.Fuse;
                // Scale = 2*Radius entspricht genau dem Kill-Kreis; *2.2 = nur minimal größer.
                if (s.Flash)
                {
                    float sc = Mathf.Lerp(0.4f, radius.Value * 2.2f, age / 0.4f);
                    s.Flash.transform.localScale = new Vector3(sc, sc, 1f);
                    var sr = s.Flash.GetComponent<SpriteRenderer>();
                    if (sr) { var c = sr.color; c.a = Mathf.Clamp01(1f - age / 0.6f); sr.color = c; }
                }
                if (s.Core)
                {
                    float sc = Mathf.Lerp(0.3f, radius.Value * 1.5f, age / 0.3f);
                    s.Core.transform.localScale = new Vector3(sc, sc, 1f);
                    var sr = s.Core.GetComponent<SpriteRenderer>();
                    if (sr) { var c = sr.color; c.a = Mathf.Clamp01(1f - age / 0.3f); sr.color = c; }
                }
                if (age >= 0.6f) { DestroyShell(s); shells.RemoveAt(i); }
            }
        }

        // Stehen bleiben: ab Karten-Öffnung und bis die eigene Granate eingeschlagen ist.
        var lp = PlayerControl.LocalPlayer;
        bool busy = picker.Active;
        if (lp && !busy) foreach (var s in shells) if (!s.Exploded && s.Owner == lp.PlayerId) { busy = true; break; }
        if (lp)
        {
            if (busy)
            {
                lp.moveable = false;
                if (lp.MyPhysics && lp.MyPhysics.body) lp.MyPhysics.body.velocity = Vector2.zero; // nicht mit dem letzten Input weiterlaufen
                frozen = true;
            }
            else if (frozen) { lp.moveable = true; frozen = false; }
        }
    }

    private bool frozen;

    private static void SetAlpha(GameObject go, float a)
    {
        if (!go) return;
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr) { var c = sr.color; c.a = a; sr.color = c; }
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionBoom) { Sfx.Play(Sfx.Kill, 1f); return; } // Geräusch nur, wenn wirklich jemand gestorben ist
        if (action != ActionFire || data.Length < 9 || !RoleRegistry.Has(sender, this)) return;
        var start = WorldMarker.Decode(data, 1);
        var target = WorldMarker.Decode(data, 5);
        var s = new Shell { Owner = data[0], Start = start, Target = target, FiredAt = Time.time, Fuse = fuse.Value };
        s.Shadow = Disc(start, new Color(0f, 0f, 0f, 0.4f), 0.25f, start.y / 1000f - 0.02f);
        s.Projectile = Disc(start, new Color(0.25f, 0.2f, 0.18f, 1f), 0.26f, start.y / 1000f - 0.5f);
        shells.Add(s);
        Sfx.Play(Sfx.Shapeshift, 0.4f); // Abschuss-Zischen (kein Kill-Sound)
    }

    private void Explode(Shell s)
    {
        s.Exploded = true;
        if (s.Shadow) { Object.Destroy(s.Shadow); s.Shadow = null; }
        if (s.Projectile) { Object.Destroy(s.Projectile); s.Projectile = null; }
        // Mehr Licht + mehr Explosion: großer oranger Feuerball + heller weißer Kern.
        s.Flash = Disc(s.Target, new Color(1f, 0.7f, 0.25f, 1f), 0.4f, s.Target.y / 1000f - 0.6f);
        s.Core = Disc(s.Target, new Color(1f, 1f, 0.9f, 1f), 0.3f, s.Target.y / 1000f - 0.61f);

        if (AmHost)
        {
            var killer = FindPlayer(s.Owner);
            int killed = 0;
            if (killer)
                foreach (var p in PlayerControl.AllPlayerControls)
                    if (p && IsAlive(p) && !p.inVent
                        && Vector2.Distance(p.GetTruePosition(), s.Target) <= radius.Value)
                        if (KillSystem.RangedKill(killer, p)) killed++; // auch der Mörser selbst stirbt im Umkreis
            if (killed > 0) SendToAll(ActionBoom); // Geräusch bei allen - nur wenn etwas starb
        }
    }

    private static GameObject Disc(Vector2 pos, Color col, float scale, float z)
    {
        var go = new GameObject("Spieleabend_Mortar");
        if (ShipStatus.Instance) go.transform.SetParent(ShipStatus.Instance.transform, true);
        go.transform.position = new Vector3(pos.x, pos.y, z);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = DiscSprite();
        sr.color = col;
        AimLine.OnTop(sr);
        return go;
    }

    private static void SetShellVisible(Shell s, bool vis)
    {
        foreach (var go in new[] { s.Shadow, s.Projectile, s.Flash, s.Core })
        {
            if (!go) continue;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr && sr.enabled != vis) sr.enabled = vis;
        }
    }

    private static void DestroyShell(Shell s)
    {
        if (s.Shadow) Object.Destroy(s.Shadow);
        if (s.Projectile) Object.Destroy(s.Projectile);
        if (s.Flash) Object.Destroy(s.Flash);
        if (s.Core) Object.Destroy(s.Core);
    }

    private static Sprite DiscSprite()
    {
        if (discSprite) return discSprite;
        const int S = 64; float c = (S - 1) / 2f;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, d <= 1f ? 1f : 0f));
        }
        tex.Apply();
        discSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        discSprite.hideFlags = HideFlags.HideAndDontSave;
        return discSprite;
    }
}
