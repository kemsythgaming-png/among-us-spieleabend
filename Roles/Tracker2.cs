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
    private bool leftFoot;
    private static Sprite printSprite;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 50f, 20f, 90f, 5f, "s");
        duration = AddOption("Duration", "Dauer|Duration", 30f, 10f, 60f, 5f, "s");

        AddButton(new RoleButton
        {
            Label = "Fußspuren|Footprints",
            Icon = "radar",
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
        activeUntil = nextStep = 0f;
    }

    public override void Update()
    {
        float t = Time.time;

        // alte Spuren ausblenden
        for (int i = prints.Count - 1; i >= 0; i--)
        {
            var (go, born) = prints[i];
            float k = (t - born) / 4f;
            if (!go || k >= 1f) { if (go) Object.Destroy(go); prints.RemoveAt(i); continue; }
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr) { var c = sr.color; sr.color = new Color(c.r, c.g, c.b, (1f - k) * 0.8f); }
        }

        var local = PlayerControl.LocalPlayer;
        if (!IsLocalRole || !IsAlive(local) || InMeeting || t > activeUntil || t < nextStep) return;
        nextStep = t + 0.35f;

        leftFoot = !leftFoot; // links/rechts abwechseln
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!p || p == local || !IsAlive(p) || p.inVent || !p.cosmetics || !p.cosmetics.nameText.gameObject.activeInHierarchy) continue;
            Vector2 pos = p.GetTruePosition();

            // Laufrichtung aus der letzten Position
            float angle = 90f;
            bool moved = lastPos.TryGetValue(p.PlayerId, out var prev) && (pos - prev).sqrMagnitude > 0.0004f;
            if (moved) angle = Mathf.Atan2(pos.y - prev.y, pos.x - prev.x) * Mathf.Rad2Deg;
            lastPos[p.PlayerId] = pos;
            if (!moved) continue; // nur Stapfen setzen, wenn sich jemand bewegt

            // Fußstapfen leicht seitlich versetzt (links/rechts)
            Vector2 side = new Vector2(-Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad)) * (leftFoot ? 0.11f : -0.11f);
            Vector2 fp = pos + new Vector2(0f, -0.3f) + side;
            Color col = PlayerColor(p);
            var go = new GameObject("Spieleabend_Footprint");
            if (ShipStatus.Instance) go.transform.SetParent(ShipStatus.Instance.transform, true);
            go.transform.position = new Vector3(fp.x, fp.y, fp.y / 1000f - 0.02f);
            go.transform.localScale = Vector3.one * 0.22f;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PrintSprite();
            sr.color = new Color(col.r, col.g, col.b, 0.85f);
            prints.Add((go, t));
        }
    }

    private static Color PlayerColor(PlayerControl p)
    {
        try { return Palette.PlayerColors[p.Data.DefaultOutfit.ColorId]; }
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
