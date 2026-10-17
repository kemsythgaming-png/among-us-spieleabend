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

    private RoleOption cooldown, maxPiles, rustleAfter, hiddenAfter, lifetime;
    private const float RustleEvery = 8f, ShakeTime = 1.2f;
    private readonly HashSet<byte> hiddenBodies = new();

    private sealed class Pile
    {
        public byte Owner;
        public Vector2 Pos;
        public GameObject Visual;
        public float NextRustleSound;
        public float ShakeUntil;
        public float Radius;
        public float Created;
    }

    // bei allen Clients (jeder berechnet Sichtbarkeit selbst - alle kennen alle Positionen)
    private readonly List<Pile> piles = new();
    private readonly Dictionary<byte, float> enteredAt = new();
    private readonly Dictionary<byte, float> leftAt = new();
    private readonly HashSet<byte> hiddenByUs = new();
    private bool localFaded;

    private static AudioClip rustleClip;

    protected override void Setup()
    {
        Instance = this;
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 20f, 5f, 60f, 2.5f, "s");
        maxPiles = AddOption("MaxPilesTotal", "Laubhaufen auf der Map|Piles on the map", 5f, 1f, 10f, 1f);
        rustleAfter = AddOption("RustleAfter", "Raschelt nach|Rustles after", 5f, 2f, 15f, 1f, "s");
        lifetime = AddOption("Lifetime", "Laub verschwindet nach|Leaves vanish after", 60f, 15f, 180f, 15f, "s");
        hiddenAfter = AddOption("HiddenAfter", "Unsichtbar auf Admin/Kameras danach|Hidden on admin/cameras after", 10f, 0f, 30f, 1f, "s");

        AddButton(new RoleButton
        {
            Label = "Laub|Leaves",
            Icon = "leaves",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            Hotkey = KeyCode.F,
            CanUse = () => !PlayerControl.LocalPlayer.inVent,
            // Jeder Haufen ist anders gross - die Groesse geht mit, damit alle denselben Haufen sehen
            OnClick = _ => SendToAll(ActionPlace, WorldMarker.Concat(RandomSize(), WorldMarker.Encode(PlayerControl.LocalPlayer.GetTruePosition()))),
        });
    }

    public override void ResetState()
    {
        ShowAll();
        FadeLocal(false);
        hiddenBodies.Clear();
        foreach (var p in piles)
            if (p.Visual) Object.Destroy(p.Visual);
        piles.Clear();
        enteredAt.Clear();
        leftAt.Clear();
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionPlace || data.Length < 5 || !RoleRegistry.Has(sender, this)) return;

        // Hoechstens so viele Haufen auf der ganzen Map - beim naechsten verschwindet der aelteste
        while (piles.Count >= maxPiles.Int)
        {
            var old = piles[0];
            piles.RemoveAt(0);
            if (old.Visual) Object.Destroy(old.Visual);
        }

        float size = Mathf.Clamp(data[0], 30, 200) / 100f;
        var pos = WorldMarker.Decode(data, 1);
        var visual = WorldMarker.Spawn("leaves", pos + new Vector2(0f, 0.1f), size, "LeafPile");
        piles.Add(new Pile { Owner = sender.PlayerId, Pos = pos, Visual = visual, Radius = PileRadius * size / 0.55f, Created = Time.time });
        if (sender == PlayerControl.LocalPlayer) Sfx.Play(RustleClip(), 0.7f);
    }

    /// <summary>Laubhaufen an dieser Stelle, oder null.</summary>
    private Pile PileAtPos(Vector2 pos) => piles.FirstOrDefault(pile => Vector2.Distance(pos, pile.Pos) <= pile.Radius);

    /// <summary>
    /// Leichen im Laub sieht und meldet nur, wer selbst im selben Haufen steht.
    /// </summary>
    internal bool CanReport(PlayerControl reporter, DeadBody body)
    {
        if (!body) return true;
        var pile = PileAtPos(body.TruePosition);
        return pile == null || PileAt(reporter) == pile;
    }

    private void UpdateBodies(Pile localPile)
    {
        var stillHidden = new HashSet<byte>();
        foreach (var body in Object.FindObjectsOfType<DeadBody>())
        {
            if (!body) continue;
            var pile = InMeeting ? null : PileAtPos(body.TruePosition);
            bool hide = pile != null && pile != localPile;
            if (hide) stillHidden.Add(body.ParentId);
            if (hide == hiddenBodies.Contains(body.ParentId)) continue;
            foreach (var r in body.GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = !hide;
        }
        hiddenBodies.Clear();
        foreach (var id in stillHidden) hiddenBodies.Add(id);
    }

    private Pile PileAt(PlayerControl p)
    {
        if (!IsAlive(p) || p.inVent) return null;
        Vector2 pos = p.GetTruePosition();
        return piles.FirstOrDefault(pile => Vector2.Distance(pos, pile.Pos) <= pile.Radius);
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

        // Laub verschwindet nach einer Weile
        for (int i = piles.Count - 1; i >= 0; i--)
        {
            if (Time.time - piles[i].Created < lifetime.Value) continue;
            if (piles[i].Visual) Object.Destroy(piles[i].Visual);
            piles.RemoveAt(i);
        }

        if (piles.Count == 0 && hiddenByUs.Count == 0 && hiddenBodies.Count == 0) return;
        var localPile = PileAt(local);
        UpdateBodies(localPile);
        if (InMeeting)
        {
            ShowAll();
            FadeLocal(false);
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

            // Selbst im Laub: man sieht sich durchsichtig (wie der Phantom) und bekommt einen Hinweis
            if (p == local)
            {
                FadeLocal(pile != null);
                continue;
            }

            // Sichtbarkeit: im Laub unsichtbar fuer andere; auf Kameras auch kurz danach
            if (!IsAlive(p)) continue;
            // Wer im selben Haufen steckt, sieht die anderen darin
            bool hide = (pile != null && pile != localPile) || (watchingCams && HiddenFromSurveillance(p));
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
            // Steckt jemand zu lange drin: alle 8 Sekunden einmal kurz wackeln und rascheln
            if (rustling.Contains(pile) && t >= pile.NextRustleSound)
            {
                pile.NextRustleSound = t + RustleEvery;
                pile.ShakeUntil = t + ShakeTime;
                if (Vector2.Distance(me, pile.Pos) <= HearRange) Sfx.Play(RustleClip(), 0.6f);
            }
            bool shakes = t < pile.ShakeUntil;
            pile.Visual.transform.localRotation = Quaternion.Euler(0f, 0f, shakes ? Mathf.Sin(t * 25f) * 8f : 0f);
        }
    }

    /// <summary>Den eigenen Spieler halb durchsichtig zeichnen, solange er im Laub steckt.</summary>
    private void FadeLocal(bool fade)
    {
        if (fade == localFaded) return;
        localFaded = fade;
        var local = PlayerControl.LocalPlayer;
        if (local && local.cosmetics) local.cosmetics.SetPhantomRoleAlpha(fade ? 0.35f : 1f);
    }

    /// <summary>Meist normal (40-85 %), manchmal ein riesiger Haufen (130-170 %).</summary>
    private static byte RandomSize() =>
        (byte)(Random.value < 0.12f ? Random.Range(130, 171) : Random.Range(40, 86));

    /// <summary>
    /// Rascheln (selbst erzeugt, kein Spiel-Sound passt): helles Rauschen ohne Bass, dessen Lautstaerke schnell und
    /// unregelmaessig schwankt - wie Blaetter, die aneinander reiben. Weiche Uebergaenge, keine harten Knackser.
    /// </summary>
    private static AudioClip RustleClip()
    {
        if (rustleClip) return rustleClip;
        const int rate = 44100;
        int length = (int)(rate * 0.9f);
        var samples = new float[length];
        var rng = new System.Random(4711);

        float low = 0f, high = 0f, env = 0f, target = 0f;
        int nextTarget = 0;
        for (int i = 0; i < length; i++)
        {
            // Rauschen: Bass raus (minus Tiefpass), ganz oben etwas weicher (zweiter Tiefpass)
            float noise = (float)rng.NextDouble() * 2f - 1f;
            low += (noise - low) * 0.02f;
            high += ((noise - low) - high) * 0.5f;

            // Alle 10-35 ms ein neues Lautstaerke-Ziel, weich angefahren
            if (i >= nextTarget)
            {
                nextTarget = i + rng.Next(rate / 100, rate * 35 / 1000);
                double r = rng.NextDouble();
                target = (float)(r * r);
            }
            env += (target - env) * 0.004f;

            float fade = Mathf.Min(1f, i / (rate * 0.08f)) * Mathf.Min(1f, (length - i) / (rate * 0.25f));
            samples[i] = Mathf.Clamp(high * env * fade * 1.6f, -1f, 1f);
        }
        rustleClip = AudioClip.Create("Spieleabend_Rustle2", length, 1, rate, false);
        rustleClip.SetData(samples, 0);
        rustleClip.hideFlags = HideFlags.HideAndDontSave;
        return rustleClip;
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

/// <summary>Melden: Eine Leiche im Laub kann nur melden, wer selbst im selben Haufen steht.</summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdReportDeadBody))]
internal static class LeafReportPatch
{
    public static bool Prefix(PlayerControl __instance, NetworkedPlayerInfo target)
    {
        var ls = LeafSpreader.Instance;
        if (ls == null || target == null) return true; // Notfallknopf: immer erlaubt
        foreach (var body in Object.FindObjectsOfType<DeadBody>())
            if (body && body.ParentId == target.PlayerId && !ls.CanReport(__instance, body)) return false;
        return true;
    }
}
