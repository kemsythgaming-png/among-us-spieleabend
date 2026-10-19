using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Crew: verpasst einem Spieler per Knopf einen zufaelligen Trank-Effekt. Gewinnt mit der Crew.
/// 14 Traenke: Schnecke, Zeitschleim, Hexenbesen, Kopfueber, Kicher, Schrumpf, Riese, Nebel, Adlerauge,
/// Geisteressenz (durchsichtig), Camo, Fledermaus, Zappel (gedrehte Steuerung), Kompass-Chaos.
/// </summary>
public sealed class PotionBrewer : CustomRole
{
    public override string ConfigSection => "Trankbrauer";
    public override string Name => Lang.T("Trankbrauer", "Potion Brewer");
    public override Color Color => new Color32(150, 90, 200, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Ein Schluck Chaos", "A sip of chaos");
    public override string Description => Lang.T("Wirf einen Zufallstrank (F).", "Throw a random potion (F).");
    public override string LongDescription => Lang.T(
        "Du bist Crew und gewinnst mit der Crew. Mit dem Knopf wählst du einen Spieler (nicht dich selbst) und löst einen zufälligen von 14 Trank-Effekten aus - z. B. langsam, eingefroren, schnell, verdrehte Steuerung, geschrumpft, riesig, Nebelsicht, Adlerauge, durchsichtig, getarnt, Fledermaus-Blindheit, gedrehte Steuerung oder ein falscher Kompass. Jeder Effekt hält nur kurz.",
        "You are crew and win with the crew. The button targets a player (not yourself) and triggers one of 14 random potion effects - e.g. slow, frozen, fast, reversed controls, shrunk, giant, foggy vision, eagle eye, transparent, camouflaged, bat blindness, spun controls or a false compass. Each effect is short.");

    internal static PotionBrewer Instance { get; private set; }

    private const byte ActionPotion = 0;

    private enum Potion
    {
        Slow, Freeze, Speed, Reverse, Giggle,       // Welle 1
        Shrink, Grow, Fog, Eagle, Transparent,      // Welle 2
        Camo, Bat, Rotate, Compass,                 // Welle 2
    }

    // Dauer je Trank (gleiche Reihenfolge wie das Enum)
    private static readonly float[] Durations =
    {
        10f, 3f, 8f, 8f, 8f,
        10f, 10f, 10f, 10f, 8f,
        8f, 10f, 8f, 10f,
    };

    private const int PotionCount = 14;

    private RoleOption cooldown;
    private readonly Dictionary<byte, (Potion P, float Until)> effects = new();
    private float nextGiggle, nextBat;

    // Zustand fuer optische Effekte (auf allen Clients): damit wir sauber zuruecksetzen koennen
    private readonly Dictionary<byte, Vector3> scaleBase = new();
    private readonly HashSet<byte> faded = new();
    private readonly Dictionary<byte, PlayerOutfitType> camoSaved = new();
    private readonly Dictionary<byte, float> compassAngle = new();

    // falscher Kompass-Pfeil (nur lokal)
    private GameObject compassArrow;
    private static Sprite arrowSprite;

    protected override void Setup()
    {
        Instance = this;
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 25f, 10f, 60f, 2.5f, "s");

        AddButton(new RoleButton
        {
            Label = "Zufallstrank|Random potion",
            Icon = "cauldron",
            NeedsTarget = true,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            OnClick = target =>
            {
                if (!target) return;
                byte p = (byte)UnityEngine.Random.Range(0, PotionCount);
                SendToAll(ActionPotion, target.PlayerId, p);
            },
        });
    }

    public override void ResetState()
    {
        effects.Clear();
        RestoreAllVisuals();
        compassAngle.Clear();
        if (compassArrow) Object.Destroy(compassArrow);
        compassArrow = null;
    }

    public override void OnMeetingStart()
    {
        // Optische Effekte enden mit dem Meeting (sonst z. B. getarnt/durchsichtig im Meeting)
        effects.Clear();
        RestoreAllVisuals();
        if (compassArrow) compassArrow.SetActive(false);
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionPotion || data.Length < 2) return;
        var p = (Potion)Mathf.Clamp(data[1], 0, PotionCount - 1);
        effects[data[0]] = (p, Time.time + Durations[(int)p]);
        if (p == Potion.Compass) compassAngle[data[0]] = UnityEngine.Random.Range(0f, 360f);

        var local = PlayerControl.LocalPlayer;
        if (local && local.PlayerId == data[0])
        {
            Sfx.Play(Sfx.Creepy, 0.5f);
            Notifier.Show($"<color={ColorHex}>{Lang.T("Du spürst einen seltsamen Trank ...", "You feel a strange potion ...")}</color>", Durations[(int)p]);
        }
    }

    private bool Has(PlayerControl p, Potion potion) =>
        p && effects.TryGetValue(p.PlayerId, out var e) && e.P == potion && e.Until > Time.time;

    // ---------- Tempo ----------

    public override float SpeedMultiplier(PlayerControl local)
    {
        if (!IsAlive(local) || InMeeting) return 1f;
        if (Has(local, Potion.Freeze)) return 0f;
        if (Has(local, Potion.Slow)) return 0.4f;
        if (Has(local, Potion.Speed)) return 1.6f;
        return 1f;
    }

    /// <summary>Fuer den Betrunken-Patch: ist der Spieler gerade durch den Kopfueber-Trank verdreht?</summary>
    internal bool ReverseNow(PlayerControl p) => Has(p, Potion.Reverse);

    /// <summary>Fuer den Betrunken-Patch: ist die Steuerung gerade um 90° gedreht (Zappeltrank)?</summary>
    internal bool RotateNow(PlayerControl p) => Has(p, Potion.Rotate);

    /// <summary>Fuer den Sicht-Patch: Faktor fuer die Sichtweite (1 = normal).</summary>
    internal float VisionFactor(PlayerControl p)
    {
        if (Has(p, Potion.Bat)) return 0.12f;
        if (Has(p, Potion.Fog)) return 0.3f;
        if (Has(p, Potion.Eagle)) return 2.2f;
        return 1f;
    }

    // ---------- jeden Frame ----------

    public override void Update()
    {
        var local = PlayerControl.LocalPlayer;

        // Geraeusche
        if (Has(local, Potion.Giggle) && Time.time > nextGiggle)
        {
            nextGiggle = Time.time + 1.4f;
            Sfx.Play(Sfx.Creepy, 0.4f);
        }
        if (Has(local, Potion.Bat) && Time.time > nextBat)
        {
            nextBat = Time.time + 0.8f;
            Sfx.Play(Sfx.Creepy, 0.25f);
        }

        ApplyVisuals();
        ApplyCompass(local);
    }

    /// <summary>Groesse, Durchsichtigkeit und Tarnung fuer ALLE Spieler (jeder Client sieht dasselbe).</summary>
    private void ApplyVisuals()
    {
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!p) continue;
            byte id = p.PlayerId;

            // Groesse (Schrumpf/Riese)
            bool shrink = Has(p, Potion.Shrink), grow = Has(p, Potion.Grow);
            if (shrink || grow)
            {
                if (!scaleBase.ContainsKey(id)) scaleBase[id] = p.transform.localScale;
                var b = scaleBase[id];
                float f = shrink ? 0.55f : 1.6f;
                p.transform.localScale = new Vector3(b.x * f, b.y * f, b.z);
            }
            else if (scaleBase.TryGetValue(id, out var baseScale))
            {
                p.transform.localScale = baseScale;
                scaleBase.Remove(id);
            }

            // Durchsichtig (Geisteressenz)
            bool fade = Has(p, Potion.Transparent);
            if (fade && p.cosmetics) { p.cosmetics.SetPhantomRoleAlpha(0.3f); faded.Add(id); }
            else if (faded.Remove(id) && p.cosmetics) p.cosmetics.SetPhantomRoleAlpha(1f);

            // Tarnung (Camo) - einzelner Spieler wird grau/namenlos
            bool camo = Has(p, Potion.Camo);
            if (camo && !camoSaved.ContainsKey(id) && p.Data != null)
            {
                camoSaved[id] = p.CurrentOutfitType;
                p.RawSetOutfit(GrayOutfit(), PlayerOutfitType.MushroomMixup);
            }
            else if (!camo && camoSaved.TryGetValue(id, out var savedType))
            {
                RestoreOutfit(p, savedType);
                camoSaved.Remove(id);
            }
        }
    }

    /// <summary>Kompass-Chaos: ein falscher Pfeil zeigt in eine zufaellige Richtung (nur beim Betroffenen).</summary>
    private void ApplyCompass(PlayerControl local)
    {
        bool want = Has(local, Potion.Compass) && IsAlive(local) && !InMeeting;
        if (!want)
        {
            if (compassArrow) compassArrow.SetActive(false);
            return;
        }
        if (!compassArrow) compassArrow = CreateArrow(local);
        compassArrow.SetActive(true);

        float angle = compassAngle.TryGetValue(local.PlayerId, out var a) ? a : 0f;
        // leichtes Zittern, damit er glaubhaft "sucht"
        angle += Mathf.Sin(Time.time * 1.7f) * 12f;
        compassArrow.transform.localPosition = new Vector3(
            Mathf.Cos(angle * Mathf.Deg2Rad) * 0.75f,
            Mathf.Sin(angle * Mathf.Deg2Rad) * 0.75f + 0.1f, -0.05f);
        compassArrow.transform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    private void RestoreAllVisuals()
    {
        foreach (var pair in scaleBase)
        {
            var p = FindPlayer(pair.Key);
            if (p) p.transform.localScale = pair.Value;
        }
        scaleBase.Clear();

        foreach (var id in faded.ToList())
        {
            var p = FindPlayer(id);
            if (p && p.cosmetics) p.cosmetics.SetPhantomRoleAlpha(1f);
        }
        faded.Clear();

        foreach (var pair in camoSaved.ToList())
        {
            var p = FindPlayer(pair.Key);
            if (p) RestoreOutfit(p, pair.Value);
        }
        camoSaved.Clear();
    }

    private static void RestoreOutfit(PlayerControl p, PlayerOutfitType type)
    {
        if (!p || p.Data == null) return;
        NetworkedPlayerInfo.PlayerOutfit outfit = null;
        if (p.Data.Outfits.ContainsKey(type)) outfit = p.Data.Outfits[type];
        if (outfit == null) { outfit = p.Data.DefaultOutfit; type = PlayerOutfitType.Default; }
        p.RawSetOutfit(outfit, type);
    }

    private static NetworkedPlayerInfo.PlayerOutfit GrayOutfit() => new()
    {
        ColorId = 15,
        HatId = "hat_NoHat",
        SkinId = "skin_None",
        VisorId = "visor_EmptyVisor",
        PetId = "pet_EmptyPet",
        NamePlateId = "nameplate_NoPlate",
        PlayerName = "",
    };

    private static GameObject CreateArrow(PlayerControl local)
    {
        var go = new GameObject("Spieleabend_FalseCompass");
        go.transform.SetParent(local.transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ArrowSprite();
        sr.color = new Color32(150, 90, 200, 255);
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
}

/// <summary>Sichtweite fuer die Nebel-, Fledermaus- und Adlerauge-Traenke anpassen.</summary>
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CalculateLightRadius))]
internal static class PotionVisionPatch
{
    public static void Postfix(NetworkedPlayerInfo player, ref float __result)
    {
        var pb = PotionBrewer.Instance;
        if (pb == null || !RoleRegistry.HasAssignments || player == null || player.IsDead) return;
        var pc = player.Object;
        if (!pc) return;
        __result *= pb.VisionFactor(pc);
    }
}
