using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Halloween;

/// <summary>
/// Impostor: legt Laubhaufen aus. Wer darin steht, ist unsichtbar - jeder, auch die Crew. Bleibt jemand zu lange drin,
/// raschelt es. Wer im Laub war, taucht kurz nicht auf Admin-Karte und Kameras auf.
/// </summary>
public sealed class LeafSpreader : CustomRole
{
    public override string ConfigSection => "Laubstreuer";
    public override string Name => Lang.T("Laubstreuer", "Leaf Spreader");
    public override Color Color => new Color32(205, 110, 35, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Im Laub sieht dich keiner", "Nobody sees you in the leaves");
    public override string Description => Lang.T("Lege Laubhaufen aus (F).\nWer drin steht, ist unsichtbar.", "Place leaf piles (F).\nWhoever stands in one is invisible.");
    public override string LongDescription => Lang.T(
        "Halloween-Impostor mit normalem Kill. Mit Taste F legst du dort, wo du stehst, einen Laubhaufen aus (alle sehen ihn).\n\nWer im Laub steht, ist für die anderen unsichtbar - das gilt für jeden, auch für die Crew. Bleibt jemand zu lange drin, raschelt es: Alle in der Nähe hören es und sehen das Laub wackeln. Wer im Laub war, taucht danach kurz nicht auf Admin-Karte und Kameras auf.",
        "Halloween impostor with a normal kill. Press F to place a leaf pile where you stand (everyone can see it).\n\nWhoever stands in the leaves is invisible to the others - that applies to everyone, crew included. Stay too long and the leaves rustle: everyone nearby hears it and sees the pile shake. Whoever was in the leaves briefly doesn't show up on the admin map and cameras.");

    private const byte ActionPlace = 0;
    private const float PileRadius = 0.75f, HearRange = 6f;

    internal static LeafSpreader Instance { get; private set; }

    private RoleOption cooldown, maxPiles, rustleAfter, hiddenAfter;

    private sealed class Pile
    {
        public byte Owner;
        public Vector2 Pos;
        public GameObject Visual;
        public float NextRustleSound;
    }

    // bei allen Clients (jeder berechnet Sichtbarkeit selbst - alle kennen alle Positionen)
    private readonly List<Pile> piles = new();
    private readonly Dictionary<byte, float> enteredAt = new();
    private readonly Dictionary<byte, float> leftAt = new();
    private readonly HashSet<byte> hiddenByUs = new();

    protected override void Setup()
    {
        Instance = this;
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 20f, 5f, 60f, 2.5f, "s");
        maxPiles = AddOption("MaxPiles", "Laubhaufen gleichzeitig|Piles at once", 3f, 1f, 6f, 1f);
        rustleAfter = AddOption("RustleAfter", "Raschelt nach|Rustles after", 5f, 2f, 15f, 1f, "s");
        hiddenAfter = AddOption("HiddenAfter", "Unsichtbar auf Admin/Kameras danach|Hidden on admin/cameras after", 10f, 0f, 30f, 1f, "s");

        AddButton(new RoleButton
        {
            Label = "Laub|Leaves",
            Icon = "leaves",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            Hotkey = KeyCode.F,
            CanUse = () => !PlayerControl.LocalPlayer.inVent,
            OnClick = _ => SendToAll(ActionPlace, WorldMarker.Encode(PlayerControl.LocalPlayer.GetTruePosition())),
        });
    }

    public override void ResetState()
    {
        ShowAll();
        foreach (var p in piles)
            if (p.Visual) Object.Destroy(p.Visual);
        piles.Clear();
        enteredAt.Clear();
        leftAt.Clear();
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionPlace || data.Length < 4 || !RoleRegistry.Has(sender, this)) return;

        // Zu viele? Der aelteste Haufen dieses Laubstreuers verschwindet
        var mine = piles.Where(p => p.Owner == sender.PlayerId).ToList();
        while (mine.Count >= maxPiles.Int)
        {
            var old = mine[0];
            mine.RemoveAt(0);
            piles.Remove(old);
            if (old.Visual) Object.Destroy(old.Visual);
        }

        var pos = WorldMarker.Decode(data, 0);
        piles.Add(new Pile { Owner = sender.PlayerId, Pos = pos, Visual = WorldMarker.Spawn("leaves", pos + new Vector2(0f, 0.1f), 0.55f, "LeafPile") });
        if (sender == PlayerControl.LocalPlayer) Sfx.Play(Sfx.Splash, 0.5f);
    }

    private Pile PileAt(PlayerControl p)
    {
        if (!IsAlive(p) || p.inVent) return null;
        Vector2 pos = p.GetTruePosition();
        return piles.FirstOrDefault(pile => Vector2.Distance(pos, pile.Pos) <= PileRadius);
    }

    /// <summary>Wird gerade auf Admin-Karte und Kameras versteckt (im Laub oder kurz danach)?</summary>
    internal bool HiddenFromSurveillance(PlayerControl p)
    {
        if (!p || piles.Count == 0 && leftAt.Count == 0) return false;
        if (enteredAt.ContainsKey(p.PlayerId)) return true;
        return leftAt.TryGetValue(p.PlayerId, out float t) && Time.time - t < hiddenAfter.Value;
    }

    public override void Update()
    {
        var local = PlayerControl.LocalPlayer;
        if (piles.Count == 0 && hiddenByUs.Count == 0) return;
        if (InMeeting)
        {
            ShowAll();
            return;
        }

        // Kameras auf allen Maps: SurveillanceMinigame, PlanetSurveillanceMinigame, FungleSurveillanceMinigame
        bool watchingCams = Minigame.Instance && Minigame.Instance.GetIl2CppType().Name.Contains("Surveillance");
        float t = Time.time;
        var rustling = new HashSet<Pile>();

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!p || p.Data == null) continue;
            var pile = PileAt(p);

            // Zeit im Laub merken
            if (pile != null)
            {
                if (!enteredAt.ContainsKey(p.PlayerId)) enteredAt[p.PlayerId] = t;
                else if (t - enteredAt[p.PlayerId] >= rustleAfter.Value) rustling.Add(pile);
            }
            else if (enteredAt.Remove(p.PlayerId)) leftAt[p.PlayerId] = t;

            // Sichtbarkeit: im Laub unsichtbar fuer andere; auf Kameras auch kurz danach
            if (p == local || !IsAlive(p)) continue;
            bool hide = pile != null || (watchingCams && HiddenFromSurveillance(p));
            if (hide && !hiddenByUs.Contains(p.PlayerId) && !p.inVent)
            {
                hiddenByUs.Add(p.PlayerId);
                p.Visible = false;
            }
            else if (!hide && hiddenByUs.Remove(p.PlayerId) && !p.inVent) p.Visible = true;
        }

        // Rascheln: Laub wackelt fuer alle, in der Naehe hoert man es
        Vector2 me = local ? local.GetTruePosition() : Vector2.zero;
        foreach (var pile in piles)
        {
            if (!pile.Visual) continue;
            bool shakes = rustling.Contains(pile);
            pile.Visual.transform.localRotation = Quaternion.Euler(0f, 0f, shakes ? Mathf.Sin(t * 25f) * 8f : 0f);
            if (!shakes || t < pile.NextRustleSound || Vector2.Distance(me, pile.Pos) > HearRange) continue;
            pile.NextRustleSound = t + 1.5f;
            Sfx.Play(Sfx.Splash, 0.35f);
        }
    }

    private void ShowAll()
    {
        foreach (var id in hiddenByUs)
        {
            var p = FindPlayer(id);
            if (IsAlive(p) && !p.inVent) p.Visible = true;
        }
        hiddenByUs.Clear();
    }
}

/// <summary>Admin-Karte: Wer im Laub ist oder kurz davor war, wird nicht mitgezaehlt (Collider kurz aus).</summary>
[HarmonyPatch(typeof(MapCountOverlay), nameof(MapCountOverlay.Update))]
internal static class LeafAdminPatch
{
    public static void Prefix(out List<Collider2D> __state)
    {
        __state = null;
        var ls = LeafSpreader.Instance;
        if (ls == null) return;
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!p || !p.Collider || !p.Collider.enabled || !ls.HiddenFromSurveillance(p)) continue;
            __state ??= new List<Collider2D>();
            p.Collider.enabled = false;
            __state.Add(p.Collider);
        }
    }

    public static void Postfix(List<Collider2D> __state)
    {
        if (__state == null) return;
        foreach (var c in __state)
            if (c) c.enabled = true;
    }
}
