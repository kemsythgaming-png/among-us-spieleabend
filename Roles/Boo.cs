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
        "Impostor: Mit dem Knopf \"BOO!\" klatschen bei allen anderen Spielern 3-6 Kleckse auf den Bildschirm (wie kleine Geister), die kurz die Sicht stören und dann Stück für Stück wieder verschwinden. Du selbst (und andere BOOs) seht nichts davon. Es gibt keinen Schaden - nur Chaos.",
        "Impostor: the \"BOO!\" button splatters 3-6 blots onto every other player's screen (like little ghosts) that briefly block their view and then fade away one by one. You (and other BOOs) see nothing. No damage - just chaos.");

    private const byte ActionBoo = 0;
    private RoleOption cooldown;
    private static Sprite booSprite;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 30f, 10f, 90f, 2.5f, "s");

        AddButton(new RoleButton
        {
            Label = "BOO!|BOO!",
            GameIcon = _ => BooSprite(),
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 15f,
            OnClick = _ => SendToAll(ActionBoo, (byte)Random.Range(3, 7)), // 3-6 Kleckse
        });
    }

    public override void ResetState() => BooOverlay.Reset();

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
                Size = Random.Range(260f, 470f),
                Start = Time.time,
                Life = Random.Range(3f, 6f),
            });
    }

    public static void Reset() => blots.Clear();

    public static void Draw()
    {
        if (blots.Count == 0) return;
        if (CustomRole.InMeeting) { blots.Clear(); return; }
        EnsureTex();
        float t = Time.time;
        var prev = GUI.color;
        for (int i = blots.Count - 1; i >= 0; i--)
        {
            var b = blots[i];
            float age = t - b.Start;
            if (age >= b.Life) { blots.RemoveAt(i); continue; }
            float k = age / b.Life;
            float alpha = k < 0.15f ? k / 0.15f : 1f - (k - 0.15f) / 0.85f; // kurz rein, dann langsam raus
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha) * 0.9f);
            GUI.DrawTexture(new Rect(b.X - b.Size / 2f, b.Y - b.Size / 2f, b.Size, b.Size), tex);
        }
        GUI.color = prev;
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
