using System;
using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>Crew: sieht fuer eine Weile die (farbigen) Fussspuren der anderen Spieler.</summary>
public sealed class Tracker2 : CustomRole
{
    public override string ConfigSection => "Spurensucher";
    public override string Name => Lang.T("Spurensucher", "Tracker");
    public override Color Color => new Color32(120, 190, 120, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Folge den Spuren", "Follow the tracks");
    public override string Description => Lang.T("Sieh die Fußspuren der anderen (F).", "See everyone's footprints (F).");
    public override string LongDescription => Lang.T(
        "Du bist Crew. Mit dem Knopf siehst du 30 Sekunden lang die Fußspuren aller anderen Spieler - in der Farbe des jeweiligen Spielers. So verfolgst du, wer wo langgelaufen ist. Nur du siehst sie.",
        "You are crew. The button shows the footprints of all other players for 30 seconds - in each player's color. Track who went where. Only you see them.");

    private RoleOption cooldown, duration;
    private float activeUntil, nextStep;
    private readonly List<(GameObject Go, float Born)> prints = new();
    private readonly Dictionary<byte, Vector2> lastPos = new();
    private readonly Dictionary<int, Vector2> lastPosFig = new();
    private static Sprite printSprite;

    /// <summary>
    /// Andere Rollen können hier Figuren anmelden, die sie spawnen (z. B. Dummy-Spawner-Läufer), damit der
    /// Spurensucher auch von ihnen Fußspuren sieht. Jede Quelle liefert die aktuell lebenden Figuren
    /// (eindeutige Id, Position, Farbe).
    /// </summary>
    public static readonly List<Func<IEnumerable<(int Id, Vector2 Pos, Color Col)>>> FigureSources = new();

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 50f, 20f, 90f, 5f, "s");
        duration = AddOption("Duration", "Dauer|Duration", 30f, 10f, 60f, 5f, "s");

        AddButton(new RoleButton
        {
            Label = "Fußspuren|Footprints",
            Icon = "radar",
            GameIcon = _ => ProcIcons.Footprints(),
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 15f,
            Hotkey = KeyCode.F,
            OnClick = _ => activeUntil = Time.time + duration.Value,
        });
    }

    public override void ResetState()
    {
        foreach (var (go, _) in prints) if (go) Object.Destroy(go);
        prints.Clear();
        lastPos.Clear();
        lastPosFig.Clear();
        activeUntil = nextStep = 0f;
    }

    public override void Update()
    {
        float t = Time.time;

        // alte Spuren ausblenden
        for (int i = prints.Count - 1; i >= 0; i--)
        {
            var (go, born) = prints[i];
            float k = (t - born) / 2f; // nach ~2 s verschwinden
            if (!go || k >= 1f) { if (go) Object.Destroy(go); prints.RemoveAt(i); continue; }
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr) { var c = sr.color; sr.color = new Color(c.r, c.g, c.b, (1f - k) * 0.8f); }
        }

        var local = PlayerControl.LocalPlayer;
        if (!IsLocalRole || !IsAlive(local) || InMeeting || t > activeUntil || t < nextStep) return;
        nextStep = t + 0.35f;

        // Alle Spieler - jetzt AUCH der Spurensucher selbst, damit man die eigenen Spuren sieht.
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!p || !IsAlive(p) || p.inVent || !p.cosmetics || !p.cosmetics.nameText.gameObject.activeInHierarchy) continue;
            Vector2 pos = p.GetTruePosition();
            bool has = lastPos.TryGetValue(p.PlayerId, out var prev);
            TryPrint(pos, prev, has, PlayerColor(p), t);
            lastPos[p.PlayerId] = pos;
        }

        // Von anderen Rollen gespawnte Figuren (z. B. Dummy-Spawner-Läufer) hinterlassen ebenfalls Spuren.
        foreach (var src in FigureSources)
        {
            IEnumerable<(int Id, Vector2 Pos, Color Col)> figs = null;
            try { figs = src(); } catch { }
            if (figs == null) continue;
            foreach (var f in figs)
            {
                int key = 100000 + f.Id;
                bool has = lastPosFig.TryGetValue(key, out var prev);
                TryPrint(f.Pos, prev, has, Camouflage.Active ? Gray : f.Col, t);
                lastPosFig[key] = f.Pos;
            }
        }
    }

    private static readonly Color Gray = new Color(0.55f, 0.55f, 0.6f, 1f);

    /// <summary>Setzt einen Fußabdruck direkt unter die Figur, in Laufrichtung gedreht (nur wenn sie sich bewegt hat).</summary>
    private void TryPrint(Vector2 pos, Vector2 prev, bool hasPrev, Color col, float t)
    {
        if (!hasPrev || (pos - prev).sqrMagnitude <= 0.0004f) return; // nur bei Bewegung
        float angle = Mathf.Atan2(pos.y - prev.y, pos.x - prev.x) * Mathf.Rad2Deg;
        Vector2 fp = pos + new Vector2(0f, -0.25f); // direkt unter die Figur (mittig, kein Links/Rechts-Versatz)
        var go = new GameObject("Spieleabend_Footprint");
        if (ShipStatus.Instance) go.transform.SetParent(ShipStatus.Instance.transform, true);
        go.transform.position = new Vector3(fp.x, fp.y, fp.y / 1000f - 0.02f);
        go.transform.localScale = Vector3.one * 0.22f;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, angle + 90f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PrintSprite();
        sr.color = new Color(col.r, col.g, col.b, 0.85f);
        prints.Add((go, t));
    }

    private static Color PlayerColor(PlayerControl p)
    {
        if (Camouflage.Active) return Gray; // getarnt -> man erkennt die Farbe nicht, Spuren grau
        try
        {
            // AKTUELLE Farbe (ändert sich z. B. durch Verkleidung/Tausch), nicht die Standardfarbe
            var type = p.CurrentOutfitType;
            int colorId = p.Data.Outfits != null && p.Data.Outfits.ContainsKey(type)
                ? p.Data.Outfits[type].ColorId
                : p.Data.DefaultOutfit.ColorId;
            return Palette.PlayerColors[colorId];
        }
        catch { return Color.white; }
    }

    /// <summary>Kleiner Fußabdruck (Sohle + Ballen), zeigt nach oben (+y).</summary>
    private static Sprite PrintSprite()
    {
        if (printSprite) return printSprite;
        const int S = 32;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        bool In(float x, float y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            return dx * dx + dy * dy <= 1f;
        }
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            bool sole = In(x, y, 15.5f, 13f, 5.5f, 9f);   // Sohle/Ferse
            bool ball = In(x, y, 15.5f, 24f, 4.3f, 4.5f);  // Ballen oben
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, sole || ball ? 1f : 0f));
        }
        tex.Apply();
        printSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        printSprite.hideFlags = HideFlags.HideAndDontSave;
        return printSprite;
    }
}
