using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor: Knopf "BOO!". Beim Druecken erscheinen bei ALLEN Spielern ausser den BOOs selbst ein paar Kleckse auf dem
/// Bildschirm (wie Mario-Geister), die Stueck fuer Stueck wieder verschwinden. Kein Kill - nur Sicht-Chaos.
/// </summary>
public sealed class Boo : CustomRole
{
    public override string ConfigSection => "Boo";
    public override string Name => "BOO";
    public override Color Color => new Color32(170, 120, 215, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Erschreck die Crew", "Spook the crew");
    public override string Description => Lang.T("Knopf: Kleckse auf allen Bildschirmen\naußer deinem.", "Button: blots on every screen\nbut yours.");
    public override string LongDescription => Lang.T(
        "Impostor: Mit dem Knopf \"BOO!\" klatschen bei allen anderen Spielern Kleckse auf den Bildschirm (wie kleine Geister), die kurz die Sicht stören und dann verschwinden. Du selbst (und andere BOOs) seht nichts davon. Du lädst alle 30 s einen Einsatz auf und kannst bis zu 3 Stacks ansammeln (Zahl am Knopf). Kein Schaden - nur Chaos.",
        "Impostor: the \"BOO!\" button splatters blots onto every other player's screen (like little ghosts) that briefly block their view and then fade. You (and other BOOs) see nothing. You charge one use every 30 s and can store up to 3 stacks (number on the button). No damage - just chaos.");

    private const byte ActionBoo = 0;
    private const int MaxStacks = 3;
    private RoleOption cooldown;
    private RoleButton button;
    private static Sprite booSprite;

    private int stacks;
    private float nextCharge;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Nachlade-Zeit je Stack|Recharge per stack", 30f, 5f, 90f, 2.5f, "s");

        button = AddButton(new RoleButton
        {
            Label = "BOO!|BOO!",
            GameIcon = _ => BooSprite(),
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,       // Radial zeigt die 30-s-Aufladung
            StartCooldown = 0f,
            UsesLeft = () => stacks,               // bis zu 3 Stacks sichtbar am Knopf
            CanUse = () => stacks > 0,
            OnClick = _ =>
            {
                if (stacks <= 0) return;
                stacks--;
                SendToAll(ActionBoo, (byte)Random.Range(12, 25));
            },
        });
    }

    public override void ResetState() { stacks = 0; nextCharge = Time.time + cooldown.Value; BooOverlay.Reset(); }

    /// <summary>In der Aufgabenliste oben links: aktuelle Stacks (bis 3).</summary>
    public override string GetTaskNote(PlayerControl local) =>
        $"BOO-Stacks: {stacks}/{MaxStacks} ({Lang.T("lädt alle", "charges every")} {Mathf.RoundToInt(cooldown.Value)}s)";

    /// <summary>Stacks laden mit der Zeit auf (alle 30 s einer, bis 3). Startet leer.</summary>
    public override void Update()
    {
        if (!IsLocalRole) return;
        if (stacks < MaxStacks && Time.time >= nextCharge)
        {
            stacks++;
            if (stacks < MaxStacks) nextCharge = Time.time + cooldown.Value;
        }
        // Radial zeigt die Aufladung nur, wenn gerade kein Stack da ist (sonst blockiert es das Klicken).
        if (button != null) button.Timer = stacks == 0 ? Mathf.Max(0f, nextCharge - Time.time) : 0f;
    }


    /// <summary>Freeplay-Test: ein BOO-Dummy löst seine Kleckse direkt beim lokalen Spieler aus (kein Kill).</summary>
    public override bool FreeplayAbility(PlayerControl attacker, PlayerControl target)
    {
        BooOverlay.Trigger(Random.Range(12, 25));
        return true;
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionBoo || data.Length < 1) return;
        int count = Mathf.Clamp(data[0], 3, 6);
        var local = PlayerControl.LocalPlayer;
        if (local && !RoleRegistry.Has(local, this)) BooOverlay.Trigger(count); // alle ausser BOO
    }

    /// <summary>Kleines Geister-Symbol fuer den Knopf (weisser Geist mit Augen) - im Code erzeugt.</summary>
    private static Sprite BooSprite()
    {
        if (booSprite) return booSprite;
        const int S = 96;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            var c = new Vector2(x - S / 2f, y - S / 2f) / (S / 2f);
            // Kopf (Kreis) + wackeliger Saum unten
            float body = c.magnitude;
            bool inBody = c.y > -0.1f ? body < 0.85f : Mathf.Abs(c.x) < 0.82f && c.y > -0.9f + 0.12f * Mathf.Sin(c.x * 14f);
            Color col = Color.clear;
            if (inBody)
            {
                col = new Color(0.96f, 0.96f, 1f, 0.97f);
                // zwei Augen + Mund
                if ((c - new Vector2(-0.28f, 0.18f)).magnitude < 0.14f) col = new Color(0.1f, 0.1f, 0.15f, 1f);
                if ((c - new Vector2(0.28f, 0.18f)).magnitude < 0.14f) col = new Color(0.1f, 0.1f, 0.15f, 1f);
                if ((c - new Vector2(0f, -0.22f)).magnitude < 0.12f) col = new Color(0.1f, 0.1f, 0.15f, 1f);
            }
            tex.SetPixel(x, y, col);
        }
        tex.Apply();
        booSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        booSprite.hideFlags = HideFlags.HideAndDontSave;
        return booSprite;
    }
}

/// <summary>
/// Bildschirm-Kleckse der BOO-Rolle: liegen als halbdurchsichtige dunkle Flecken ueber dem Bild und verschwinden
/// nach und nach. Wird von SettingsWindow.OnGUI gezeichnet (laeuft bei jedem Client, egal welche Rolle).
/// </summary>
internal static class BooOverlay
{
    private sealed class Blot { public float X, Y, Size, Start, Life; }

    private static readonly List<Blot> blots = new();
    private static Texture2D tex;

    public static void Trigger(int count)
    {
        float w = Ui.ScreenWidth, h = 1080f;
        for (int i = 0; i < count; i++)
            blots.Add(new Blot
            {
                X = Random.Range(0.08f, 0.92f) * w,
                Y = Random.Range(0.1f, 0.9f) * h,
                Size = Random.Range(430f, 740f),
                Start = Time.time,
                Life = Random.Range(3f, 6f),
            });
    }

    public static void Reset() => blots.Clear();

    private static GUIStyle blotStyle;

    public static void Draw()
    {
        if (blots.Count == 0) return;
        if (CustomRole.InMeeting) { blots.Clear(); return; }
        EnsureTex();
        // WICHTIG (IL2CPP): GUI.DrawTexture und GUI.color sind wegoptimiert ("Method unstripping failed") und
        // haben das Fenster jeden Frame abstürzen lassen - die Kleckse erschienen NIE. Deshalb über einen
        // Style-Hintergrund mit GUI.Box zeichnen. Die weiche Kante steckt schon in der Textur (Alpha pro Pixel).
        if (blotStyle == null) { blotStyle = new GUIStyle(Ui.Small); blotStyle.normal.background = tex; }
        float t = Time.time;
        for (int i = blots.Count - 1; i >= 0; i--)
        {
            var b = blots[i];
            float age = t - b.Start;
            if (age >= b.Life) { blots.RemoveAt(i); continue; }
            GUI.Box(new Rect(b.X - b.Size / 2f, b.Y - b.Size / 2f, b.Size, b.Size), GUIContent.none, blotStyle);
        }
    }

    private static void EnsureTex()
    {
        if (tex) return;
        const int S = 128;
        tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var dark = new Color(0.07f, 0.05f, 0.12f);
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            var c = new Vector2(x - (S - 1) / 2f, y - (S - 1) / 2f) / (S / 2f);
            // unregelmaessiger Klecks: Radius wackelt mit dem Winkel
            float ang = Mathf.Atan2(c.y, c.x);
            float wob = 0.78f + 0.12f * Mathf.Sin(ang * 5f) + 0.06f * Mathf.Sin(ang * 9f + 1.3f);
            float d = c.magnitude / wob;
            float a = Mathf.Clamp01(1f - d);
            a = Mathf.SmoothStep(0f, 1f, a * 1.3f);
            tex.SetPixel(x, y, new Color(dark.r, dark.g, dark.b, a));
        }
        tex.Apply();
    }
}
