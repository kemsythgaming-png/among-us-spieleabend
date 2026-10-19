using System.Collections.Generic;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Modifiers;

/// <summary>Anpassung: Links und rechts (optional auch oben/unten) sind vertauscht.</summary>
public sealed class Drunk : CustomRole
{
    public override string Name => Lang.T("Betrunken", "Drunk");
    public override Color Color => new Color32(200, 150, 60, 255);
    public override RoleTeam Team => RoleTeam.Modifier;
    public override string IntroText => Lang.T("Hicks!", "Hic!");
    public override string Description => Lang.T("Links und rechts sind vertauscht!", "Left and right are swapped!");
    public override string LongDescription => Lang.T(
        "Anpassung: Du bist betrunken - links und rechts sind vertauscht (je nach Einstellung auch oben und unten). Deine Rolle bleibt.",
        "Modifier: you are drunk - left and right are swapped (depending on the settings also up and down). Your role stays.");

    internal static Drunk Instance { get; private set; }
    internal RoleOption SwapVertical { get; private set; }

    protected override void Setup()
    {
        Instance = this;
        SwapVertical = AddToggle("SwapVertical", "Auch oben/unten vertauscht|Also swap up/down", false);
    }
}

/// <summary>
/// Vertauscht die Tastatur-Steuerung: Die Tastatur speichert ihre Richtung im Feld "del".
/// Vor dem Update wird der echte Wert wiederhergestellt, danach gespiegelt - so wird nie doppelt gespiegelt.
/// </summary>
[HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.Update))]
internal static class DrunkKeyboardPatch
{
    private static Vector2 raw;
    private static bool flipped;

    public static void Prefix(KeyboardJoystick __instance)
    {
        if (flipped) __instance.del = raw;
        flipped = false;
    }

    public static void Postfix(KeyboardJoystick __instance)
    {
        raw = __instance.del;
        var local = PlayerControl.LocalPlayer;
        if (!local || local.Data == null || local.Data.IsDead || !ShipStatus.Instance) return;

        var drunk = Drunk.Instance;
        bool byRole = drunk != null && RoleRegistry.HasAssignments && drunk.IsLocalRole;
        bool byBierdalf = Bierdalf.Instance?.DrunkNow(local) ?? false; // Bierdalf macht kurzzeitig betrunken
        bool byPotion = Spieleabend.Roles.PotionBrewer.Instance?.ReverseNow(local) ?? false; // Kopfueber-Trank
        bool byRotate = Spieleabend.Roles.PotionBrewer.Instance?.RotateNow(local) ?? false; // Zappeltrank: um 90° gedreht
        if (!byRole && !byBierdalf && !byPotion && !byRotate) return;

        // Zappeltrank hat Vorrang und dreht die Steuerung um 90° (statt nur zu spiegeln)
        if (byRotate && !byRole && !byBierdalf && !byPotion)
        {
            __instance.del = new Vector2(-raw.y, raw.x);
            flipped = true;
            return;
        }

        bool vert = byRole && drunk.SwapVertical.Bool;
        __instance.del = new Vector2(-raw.x, vert ? -raw.y : raw.y);
        flipped = true;
    }
}

/// <summary>Anpassung: kleiner und etwas schneller (Gegenstueck zum Giant).</summary>
public sealed class Mini : CustomRole
{
    public override string Name => "Mini";
    public override Color Color => new Color32(255, 200, 230, 255);
    public override RoleTeam Team => RoleTeam.Modifier;
    public override string IntroText => Lang.T("Klein, aber flink", "Small but quick");
    public override string Description => Lang.T("Du bist kleiner und schneller.", "You are smaller and faster.");
    public override string LongDescription => Lang.T(
        "Anpassung: Du bist für alle sichtbar kleiner und etwas schneller. Deine Rolle bleibt.",
        "Modifier: you are visibly smaller and a bit faster. Your role stays.");

    private RoleOption size, speed;
    private readonly Dictionary<byte, Vector3> baseScales = new();

    protected override void Setup()
    {
        size = AddOption("Size", "Größe|Size", 0.7f, 0.5f, 0.9f, 0.05f, "x");
        speed = AddOption("Speed", "Tempo|Speed", 1.15f, 1f, 1.5f, 0.05f, "x");
    }

    public override void Update()
    {
        foreach (var p in Players)
        {
            if (!p) continue;
            if (!baseScales.ContainsKey(p.PlayerId)) baseScales[p.PlayerId] = p.transform.localScale;
            var b = baseScales[p.PlayerId];
            p.transform.localScale = new Vector3(b.x * size.Value, b.y * size.Value, b.z);
        }

    }

    public override float SpeedMultiplier(PlayerControl local) => IsLocalRole && IsAlive(local) ? speed.Value : 1f;

    public override void ResetState()
    {
        foreach (var pair in baseScales)
        {
            var p = FindPlayer(pair.Key);
            if (p) p.transform.localScale = pair.Value;
        }
        baseScales.Clear();

    }
}
