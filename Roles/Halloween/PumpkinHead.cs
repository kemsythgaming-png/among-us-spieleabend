using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Halloween;

/// <summary>
/// Crew: setzt kurz einen Kuerbis auf. Solange er ihn traegt, kann ihn niemand toeten - er sieht aber nur durch
/// zwei Augenloecher, und alle anderen sehen ihn nur als schwarze Silhouette ohne Namen.
/// </summary>
public sealed class PumpkinHead : CustomRole
{
    public override string ConfigSection => "KuerbiskopfCrew"; // nicht "Kuerbiskopf" - das war die entfernte Impostor-Rolle
    public override string Name => Lang.T("Kürbiskopf", "Pumpkin Head");
    public override Color Color => new Color32(240, 130, 30, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Hohl, aber unverwundbar", "Hollow, but invincible");
    public override string Description => Lang.T("Setz den Kürbis auf (F):\nunverwundbar, aber fast blind.", "Put on the pumpkin (F):\ninvincible, but almost blind.");
    public override string LongDescription => Lang.T(
        "Halloween-Crew. Mit Taste F setzt du für ein paar Sekunden einen Kürbis auf. Solange du ihn trägst, kann dich niemand töten.\n\nDafür siehst du nur durch zwei Augenlöcher und sehr wenig. Die anderen sehen dich in der Zeit nur als schwarze Silhouette - ohne Farbe und Namen.",
        "Halloween crew. Press F to put on a pumpkin for a few seconds. While you wear it, nobody can kill you.\n\nBut you only see through two eye holes and very little. Meanwhile the others only see you as a black silhouette - no color, no name.");

    private const byte ActionOn = 0, ActionBlocked = 1;
    private const int BlackColorId = 6;

    internal static PumpkinHead Instance { get; private set; }

    private RoleOption cooldown, duration, vision;

    // bei allen Clients: wer traegt bis wann einen Kuerbis
    private readonly Dictionary<byte, float> wearingUntil = new();
    private readonly Dictionary<byte, PlayerOutfitType> silhouettes = new();

    // lokal: Kuerbis-Sicht
    private GameObject overlay;
    private static Sprite overlaySprite;

    protected override void Setup()
    {
        Instance = this;
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 30f, 10f, 90f, 2.5f, "s");
        duration = AddOption("Duration", "Dauer|Duration", 10f, 3f, 20f, 1f, "s");
        vision = AddOption("Vision", "Sicht mit Kürbis|Vision with pumpkin", 0.4f, 0.1f, 1f, 0.05f, "x");

        AddButton(new RoleButton
        {
            Label = "Kürbis|Pumpkin",
            Icon = "pumpkin",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            Duration = () => duration.Value,
            Hotkey = KeyCode.F,
            OnClick = _ => SendToAll(ActionOn, PlayerControl.LocalPlayer.PlayerId),
        });
    }

    public override void ResetState()
    {
        wearingUntil.Clear();
        RestoreSilhouettes();
        if (overlay) Object.Destroy(overlay);
        overlay = null;
    }

    public override void OnMeetingStart()
    {
        wearingUntil.Clear();
        RestoreSilhouettes();
    }

    internal bool Wearing(PlayerControl p) =>
        p && wearingUntil.TryGetValue(p.PlayerId, out float until) && until > Time.time && !InMeeting && IsAlive(p);

    internal float VisionFactor => vision.Value;

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (data.Length < 1) return;
        var p = FindPlayer(data[0]);
        if (!p) return;

        if (action == ActionOn && RoleRegistry.Has(sender, this) && sender == p)
        {
            wearingUntil[p.PlayerId] = Time.time + duration.Value;
            if (p == PlayerControl.LocalPlayer) Sfx.Play(Sfx.Shapeshift);
        }
        else if (action == ActionBlocked)
        {
            ProtectionVisuals.ShowBurst(p, Color);
            if (p == PlayerControl.LocalPlayer) Sfx.Play(Sfx.Protect);
        }
    }

    public override KillIntercept InterceptKill(PlayerControl killer, PlayerControl target)
    {
        if (!Wearing(target) || killer == target) return KillIntercept.None;
        killer.RpcMurderPlayer(target, false); // Kill scheitert sichtbar, Abklingzeit des Killers startet neu
        SendToAll(ActionBlocked, target.PlayerId);
        return KillIntercept.Handled;
    }

    public override void Update()
    {
        var local = PlayerControl.LocalPlayer;

        // Silhouetten fuer alle anderen
        foreach (var id in wearingUntil.Keys.ToList())
        {
            var p = FindPlayer(id);
            bool show = Wearing(p) && p != local && !Camouflage.Active;
            if (show && !silhouettes.ContainsKey(id))
            {
                silhouettes[id] = p.CurrentOutfitType;
                p.RawSetOutfit(SilhouetteOutfit(), PlayerOutfitType.MushroomMixup);
            }
            else if (!show && silhouettes.ContainsKey(id)) Restore(id);
            if (!Wearing(p) && wearingUntil[id] <= Time.time) wearingUntil.Remove(id);
        }

        // Kuerbis-Sicht fuer den Traeger selbst
        bool mine = Wearing(local);
        if (mine && !overlay) overlay = CreateOverlay();
        if (overlay) overlay.SetActive(mine);
    }

    private void RestoreSilhouettes()
    {
        foreach (var id in silhouettes.Keys.ToList()) Restore(id);
        silhouettes.Clear();
    }

    private void Restore(byte id)
    {
        var type = silhouettes[id];
        silhouettes.Remove(id);
        var p = FindPlayer(id);
        if (!p || p.Data == null) return;

        NetworkedPlayerInfo.PlayerOutfit outfit = null;
        if (p.Data.Outfits.ContainsKey(type)) outfit = p.Data.Outfits[type];
        if (outfit == null)
        {
            outfit = p.Data.DefaultOutfit;
            type = PlayerOutfitType.Default;
        }
        p.RawSetOutfit(outfit, type);
    }

    private static NetworkedPlayerInfo.PlayerOutfit SilhouetteOutfit() => new()
    {
        ColorId = BlackColorId,
        HatId = "hat_NoHat",
        SkinId = "skin_None",
        VisorId = "visor_EmptyVisor",
        PetId = "pet_EmptyPet",
        NamePlateId = "nameplate_NoPlate",
        PlayerName = "",
    };

    /// <summary>Bildschirmfuellender Kuerbis mit zwei Augenloechern, eine Kopie der Vollbild-Flaeche des HUD (liegt unter den Knoepfen).</summary>
    private static GameObject CreateOverlay()
    {
        var hud = HudManager.Instance;
        var cam = Camera.main;
        if (!hud || !hud.FullScreen || !cam) return null;

        var go = Object.Instantiate(hud.FullScreen.gameObject, hud.FullScreen.transform.parent);
        go.name = "Spieleabend_PumpkinView";
        foreach (var c in go.GetComponents<MonoBehaviour>()) Object.Destroy(c);
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = OverlaySprite();
        sr.color = Color.white;

        // Sprite ist 1 Einheit hoch -> auf Bildschirmgroesse skalieren
        float height = cam.orthographicSize * 2f;
        float width = height * cam.aspect;
        var parentScale = go.transform.parent ? go.transform.parent.lossyScale : Vector3.one;
        var size = sr.sprite.bounds.size;
        go.transform.localPosition = new Vector3(0f, 0f, hud.FullScreen.transform.localPosition.z);
        go.transform.localScale = new Vector3(width / size.x / parentScale.x, height / size.y / parentScale.y, 1f);
        go.SetActive(true);
        return go;
    }

    private static Sprite OverlaySprite()
    {
        if (overlaySprite) return overlaySprite;
        const int W = 320, H = 180;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        var shell = new Color(0.55f, 0.25f, 0.03f, 0.96f);
        var ridge = new Color(0.42f, 0.18f, 0.02f, 0.96f);
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            // Augenloecher: Dreiecke (Spitze oben), links und rechts der Mitte
            float hole = Mathf.Max(EyeHole(x, y, W * 0.33f, H * 0.42f), EyeHole(x, y, W * 0.67f, H * 0.42f));
            var c = Mathf.Abs(Mathf.Sin(x / (float)W * Mathf.PI * 5f)) < 0.08f ? ridge : shell;
            c.a *= 1f - hole;
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        overlaySprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), H);
        overlaySprite.hideFlags = HideFlags.HideAndDontSave;
        return overlaySprite;
    }

    /// <summary>1 = im Loch, 0 = Schale, weicher Rand dazwischen.</summary>
    private static float EyeHole(int x, int y, float cx, float baseY)
    {
        const float halfWidth = 46f, height = 70f, soft = 4f;
        float dy = y - baseY;
        if (dy < -soft || dy > height + soft) return 0f;
        float w = halfWidth * (1f - Mathf.Clamp01(dy / height)); // oben spitz
        float d = w - Mathf.Abs(x - cx);
        float edge = Mathf.Min(d, dy + soft, height - dy);
        return Mathf.Clamp01(edge / soft);
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CalculateLightRadius))]
internal static class PumpkinVisionPatch
{
    public static void Postfix(NetworkedPlayerInfo player, ref float __result)
    {
        var ph = PumpkinHead.Instance;
        if (ph == null || player == null || !player.Object) return;
        if (ph.Wearing(player.Object)) __result *= ph.VisionFactor;
    }
}
