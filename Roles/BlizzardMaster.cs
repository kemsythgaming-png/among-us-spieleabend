using System;
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

    public override void ResetState() { Until = 0f; }
    public override void OnMeetingStart() { Until = 0f; }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionBlizzard || data.Length < 8 || !RoleRegistry.Has(sender, this)) return;
        Center = new Vector2(BitConverter.ToSingle(data, 0), BitConverter.ToSingle(data, 4));
        Radius = radius.Value;
        Factor = vision.Value;
        Until = Time.time + duration.Value;
    }

    internal static bool Affects(PlayerControl p) =>
        p && Time.time < Until && Vector2.Distance(p.GetTruePosition(), Center) <= Radius;
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
