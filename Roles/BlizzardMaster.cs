using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor (Schneesturm-Bändiger): loest einen Schneesturm aus. Fuer 7 Sekunden sehen alle Spieler im betroffenen
/// Bereich deutlich weniger weit. Cooldown 40 s.
/// </summary>
public sealed class BlizzardMaster : CustomRole
{
    public override string ConfigSection => "Schneesturmbaendiger";
    public override string Name => Lang.T("Schneesturm-Bändiger", "Blizzard Master");
    public override Color Color => new Color32(170, 200, 230, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Weißes Chaos", "White chaos");
    public override string Description => Lang.T("Löse einen Schneesturm aus (F):\nkurze Sicht im Bereich.", "Start a blizzard (F):\nshort vision in the area.");
    public override string LongDescription => Lang.T(
        "Impostor: Mit Taste F löst du einen Schneesturm aus. Für 7 Sekunden sehen alle Spieler im betroffenen Bereich deutlich weniger weit. Danach läuft eine Abklingzeit (40 s).",
        "Impostor: press F to start a blizzard. For 7 seconds all players in the affected area see much less far. Then a cooldown runs (40 s).");

    private const byte ActionBlizzard = 0;

    private RoleOption cooldown, duration, radius, vision;

    internal static float Until;
    internal static Vector2 Center;
    internal static float Radius;
    internal static float Factor = 0.4f;
    internal static byte CasterId = byte.MaxValue; // der Bändiger selbst bleibt verschont

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 40f, 15f, 120f, 5f, "s");
        duration = AddOption("Duration", "Dauer|Duration", 7f, 3f, 15f, 1f, "s");
        radius = AddOption("Radius", "Bereich|Area", 6f, 3f, 15f, 0.5f);
        vision = AddOption("Vision", "Restsicht|Vision left", 0.4f, 0.1f, 0.8f, 0.05f, "x");

        AddButton(new RoleButton
        {
            Label = "Schneesturm|Blizzard",
            Icon = "frost",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer),
            OnClick = _ =>
            {
                var local = PlayerControl.LocalPlayer;
                if (!local) return;
                var pos = local.GetTruePosition();
                var data = new byte[8];
                BitConverter.GetBytes(pos.x).CopyTo(data, 0);
                BitConverter.GetBytes(pos.y).CopyTo(data, 4);
                SendToAll(ActionBlizzard, data);
            },
        });
    }

    public override void ResetState() { Until = 0f; CasterId = byte.MaxValue; }
    public override void OnMeetingStart() { Until = 0f; }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionBlizzard || data.Length < 8 || !RoleRegistry.Has(sender, this)) return;
        if (sender == null || sender.isDummy) return; // ein Dummy löst keinen echten Schneesturm aus
        Center = new Vector2(BitConverter.ToSingle(data, 0), BitConverter.ToSingle(data, 4));
        Radius = radius.Value;
        Factor = vision.Value;
        CasterId = sender.PlayerId;
        Until = Time.time + duration.Value;
    }

    /// <summary>Freeplay: ein Dummy löst den Schneesturm an seiner Position aus (lokal, kein Netzwerk).</summary>
    public override bool FreeplayAbility(PlayerControl user, PlayerControl target)
    {
        if (!user) return false;
        Center = user.GetTruePosition();
        Radius = radius.Value;
        Factor = vision.Value;
        CasterId = user.PlayerId; // der Dummy ist der Bändiger -> du (als Ziel) bekommst den Sturm
        Until = Time.time + duration.Value;
        return true;
    }

    // Schnee + verringerte Sicht gelten für ALLE Spieler (nicht nur im Umkreis) - außer dem Bändiger selbst.
    internal static bool Active => Time.time < Until;
    internal static bool Affects(PlayerControl p) => p && Active && p.PlayerId != CasterId;
    internal static bool LocalIsCaster => PlayerControl.LocalPlayer && PlayerControl.LocalPlayer.PlayerId == CasterId;
}

/// <summary>Schnee über den Bildschirm ALLER Spieler, solange der Schneesturm läuft (wie das BOO-Overlay gezeichnet).</summary>
internal static class BlizzardOverlay
{
    private sealed class Flake { public float X, Y, Vy, Size, Phase, Sway, SwayFreq; }
    private sealed class Big { public float X, Y, Size, Born, Life; }
    private static Flake[] flakes;
    private static readonly List<Big> bigs = new();
    private static float nextBig;
    private static GUIStyle style;
    private static readonly Dictionary<int, GUIStyle> bigStyles = new();

    private static void Reseed(Flake f, float w, float h, bool initial)
    {
        f.X = UnityEngine.Random.Range(0f, w);
        f.Y = initial ? UnityEngine.Random.Range(0f, h) : -UnityEngine.Random.Range(10f, 80f);
        f.Vy = UnityEngine.Random.Range(200f, 520f);        // jede Flocke anders schnell
        f.Size = UnityEngine.Random.Range(8f, 30f);
        f.Phase = UnityEngine.Random.Range(0f, 10f);
        f.Sway = UnityEngine.Random.Range(10f, 85f);
        f.SwayFreq = UnityEngine.Random.Range(0.7f, 2.1f);  // unterschiedliches Pendeln
    }

    public static void Draw()
    {
        if (!BlizzardMaster.Active || CustomRole.InMeeting || BlizzardMaster.LocalIsCaster)
        {
            if (flakes != null) { flakes = null; bigs.Clear(); } // für den nächsten Sturm frisch
            return;
        }
        float w = Ui.ScreenWidth, h = 1080f, dt = Mathf.Min(Time.deltaTime, 0.05f), t = Time.time;
        if (style == null) { style = new GUIStyle(Ui.Small); style.normal.background = FlakeTex(0.9f); }
        if (flakes == null || flakes.Length == 0)
        {
            flakes = new Flake[460]; // noch dichterer Schneesturm
            for (int i = 0; i < flakes.Length; i++) { flakes[i] = new Flake(); Reseed(flakes[i], w, h, true); }
            nextBig = t + 1.5f;
        }

        foreach (var f in flakes)
        {
            f.Y += f.Vy * dt;
            if (f.Y > h + f.Size) Reseed(f, w, h, false); // beim Neustart ALLES neu -> fällt immer unterschiedlich
            float x = f.X + Mathf.Sin(t * f.SwayFreq + f.Phase) * f.Sway;
            GUI.Box(new Rect(x, f.Y, f.Size, f.Size), GUIContent.none, style);
        }

        // Ab und zu eine große Flocke, die auf dem Bildschirm hängen bleibt und sich langsam auflöst.
        if (t >= nextBig)
        {
            bigs.Add(new Big { X = UnityEngine.Random.Range(0f, w - 150f), Y = UnityEngine.Random.Range(0f, h * 0.65f), Size = UnityEngine.Random.Range(80f, 150f), Born = t, Life = UnityEngine.Random.Range(3.5f, 6f) });
            nextBig = t + UnityEngine.Random.Range(1.2f, 2.8f);
        }
        for (int i = bigs.Count - 1; i >= 0; i--)
        {
            var b = bigs[i];
            float age = t - b.Born;
            if (age >= b.Life) { bigs.RemoveAt(i); continue; }
            float k = age / b.Life;
            float a = (1f - k) * 0.8f;              // langsam ausblenden
            float sz = b.Size * (1f - k * 0.25f);   // + leicht schrumpfen = "auflösen"
            GUI.Box(new Rect(b.X, b.Y, sz, sz), GUIContent.none, BigStyle(a));
        }
    }

    private static GUIStyle BigStyle(float alpha)
    {
        int k = Mathf.Clamp(Mathf.RoundToInt(alpha * 8f), 0, 8);
        if (bigStyles.TryGetValue(k, out var s) && s != null) return s;
        s = new GUIStyle { normal = { background = FlakeTex(k / 8f * 0.8f) } };
        bigStyles[k] = s;
        return s;
    }

    private static Texture2D FlakeTex(float maxAlpha)
    {
        const int S = 48;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            var c = new Vector2(x - (S - 1) / 2f, y - (S - 1) / 2f) / (S / 2f);
            float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - c.magnitude)) * maxAlpha;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return tex;
    }
}

/// <summary>Sicht aller Spieler im Schneesturm-Bereich verringern.</summary>
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CalculateLightRadius))]
internal static class BlizzardVisionPatch
{
    public static void Postfix(NetworkedPlayerInfo player, ref float __result)
    {
        if (player == null || player.IsDead || !player.Object) return;
        if (BlizzardMaster.Affects(player.Object)) __result *= BlizzardMaster.Factor;
    }
}
