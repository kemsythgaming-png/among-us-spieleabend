using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Halloween;

/// <summary>
/// Neutral: Ein Pfeil zeigt dem Zombie die naechste Leiche. Er frisst sie (sie verschwindet fuer alle und kann nicht
/// mehr gemeldet werden). Hat er genug Leichen gefressen, gewinnt er allein.
/// </summary>
public sealed class Zombie : CustomRole
{
    public override string Name => Lang.T("Zombie", "Zombie");
    public override Color Color => new Color32(110, 170, 60, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override string IntroText => Lang.T("Gehirne ... Gehirne ...", "Brains ... brains ...");
    public override string Description => Lang.T(
        $"Ein Pfeil zeigt dir Leichen. Friss sie (F)!\n({EatenByLocal}/{toWin?.Int})",
        $"An arrow shows you bodies. Eat them (F)!\n({EatenByLocal}/{toWin?.Int})");
    public override string LongDescription => Lang.T(
        "Halloween-Rolle, neutral. Liegt irgendwo eine Leiche, zeigt dir ein grüner Pfeil den Weg zur nächsten.\n\nStell dich an die Leiche und friss sie mit Taste F - sie verschwindet für alle und kann nicht mehr gemeldet werden. Hast du genug Leichen gefressen (einstellbar), gewinnst du allein. Töten kannst du selbst nicht.",
        "Halloween role, neutral. Whenever there is a body, a green arrow shows you the way to the nearest one.\n\nStand at the body and eat it with key F - it disappears for everyone and can't be reported anymore. Eat enough bodies (configurable) and you win alone. You can't kill by yourself.");

    private const byte ActionEat = 0;
    private const float EatRange = 1.2f;

    internal static Zombie Instance { get; private set; }

    private RoleOption cooldown, toWin;

    // bei allen Clients: wie viele Leichen jeder Zombie gefressen hat
    private readonly Dictionary<byte, int> eaten = new();

    private GameObject arrow;
    private static Sprite arrowSprite;
    private float frozenUntil; // nach dem Fressen kurz bewegungsunfaehig

    private int EatenByLocal => PlayerControl.LocalPlayer && eaten.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out int n) ? n : 0;

    protected override void Setup()
    {
        Instance = this;
        cooldown = AddOption("EatCooldownSec", "Abklingzeit nach dem Fressen|Cooldown after eating", 20f, 0f, 60f, 2.5f, "s");
        toWin = AddOption("BodiesToWin", "Leichen zum Sieg|Bodies to win", 3f, 1f, 8f, 1f);

        AddButton(new RoleButton
        {
            Label = "Fressen|Eat",
            Icon = "eat",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 5f,
            Hotkey = KeyCode.F,
            CanUse = () => NearbyBody() != null,
            OnClick = _ =>
            {
                var body = NearbyBody();
                if (body) SendToAll(ActionEat, body.ParentId);
            },
        });
    }

    public override void ResetState()
    {
        eaten.Clear();
        if (arrow) Object.Destroy(arrow);
        arrow = null;
        frozenUntil = 0f;
    }

    public override float SpeedMultiplier(PlayerControl local) =>
        frozenUntil > Time.time && IsLocalRole && IsAlive(local) && !InMeeting ? 0f : 1f;

    private static DeadBody NearbyBody()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || local.inVent) return null;
        Vector2 me = local.GetTruePosition();
        return Object.FindObjectsOfType<DeadBody>().Where(b => b && Vector2.Distance(me, b.TruePosition) <= EatRange)
            .OrderBy(b => Vector2.Distance(me, b.TruePosition)).FirstOrDefault();
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionEat || data.Length < 1 || !RoleRegistry.Has(sender, this) || !IsAlive(sender) || InMeeting) return;

        // Leiche verschwindet bei allen
        bool found = false;
        foreach (var body in Object.FindObjectsOfType<DeadBody>())
        {
            if (!body || body.ParentId != data[0]) continue;
            Object.Destroy(body.gameObject);
            found = true;
        }
        if (!found) return; // schon gefressen

        // Das Pet des gefressenen Spielers verschwindet mit (sonst laeuft es ohne Leiche weiter herum)
        var owner = FindPlayer(data[0]);
        if (owner && owner.cosmetics && owner.cosmetics.currentPet)
            owner.cosmetics.currentPet.gameObject.SetActive(false);

        eaten.TryGetValue(sender.PlayerId, out int n);
        eaten[sender.PlayerId] = ++n;

        if (sender == PlayerControl.LocalPlayer)
        {
            Sfx.Play(Sfx.Kill, 0.6f);
            frozenUntil = Time.time + 3f; // satt und traege - 3 Sekunden festgewachsen
            if (n < toWin.Int) Notifier.Show($"<color={ColorHex}>{Lang.T("Mahlzeit!", "Yum!")} {n}/{toWin.Int}</color>", 2.5f);
        }

        if (AmHost && n >= toWin.Int) CustomWin.Trigger(this, new[] { sender });
    }

    /// <summary>Pfeil zur naechsten Leiche (nur fuer den Zombie selbst).</summary>
    public override void Update()
    {
        var local = PlayerControl.LocalPlayer;
        DeadBody nearest = null;
        if (IsLocalRole && IsAlive(local) && !InMeeting)
        {
            Vector2 me = local.GetTruePosition();
            nearest = Object.FindObjectsOfType<DeadBody>().Where(b => b)
                .OrderBy(b => Vector2.Distance(me, b.TruePosition)).FirstOrDefault();
        }
        if (!nearest)
        {
            if (arrow) arrow.SetActive(false);
            return;
        }

        if (!arrow) arrow = CreateArrow(local);
        arrow.SetActive(true);
        Vector2 dir = nearest.TruePosition - local.GetTruePosition();
        float angle = Mathf.Atan2(dir.y, dir.x);
        arrow.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.8f, Mathf.Sin(angle) * 0.8f + 0.1f, -0.05f);
        arrow.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);
    }

    private GameObject CreateArrow(PlayerControl local)
    {
        var go = new GameObject("Spieleabend_ZombieArrow");
        go.transform.SetParent(local.transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ArrowSprite();
        sr.color = Color;
        return go;
    }

    private static Sprite ArrowSprite()
    {
        if (arrowSprite) return arrowSprite;
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float half = (S - 1 - y) * 0.45f;
            float dx = Mathf.Abs(x - (S - 1) / 2f);
            bool inside = y > 8 && dx <= half;
            bool edge = inside && (dx > half - 4f || y < 13);
            tex.SetPixel(x, y, !inside ? Color.clear : edge ? new Color(0.1f, 0.1f, 0.12f, 1f) : Color.white);
        }
        tex.Apply();
        arrowSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 200f);
        arrowSprite.hideFlags = HideFlags.HideAndDontSave;
        return arrowSprite;
    }

    public override string GetNameMark(PlayerControl viewer, PlayerControl target) =>
        viewer == target && RoleRegistry.Has(target, this) && IsAlive(target)
            ? $"<color={ColorHex}>[{EatenByLocal}/{toWin.Int}]</color>"
            : null;
}
