using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Halloween;

/// <summary>Impostor: laesst Nebel aufziehen - alle ausser den Impostorn sehen kurz fast nichts.</summary>
public sealed class FogMaker : CustomRole
{
    public override string ConfigSection => "Nebelmacher";
    public override string Name => Lang.T("Nebelmacher", "Fog Maker");
    public override Color Color => new Color32(130, 140, 165, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Im Nebel sind alle gleich", "In the fog, everyone is the same");
    public override string Description => Lang.T("Lass Nebel aufziehen (F) -\ndie Crew sieht kaum etwas.", "Summon fog (F) -\nthe crew can barely see.");
    public override string LongDescription => Lang.T(
        "Halloween-Impostor. Mit Taste F zieht für ein paar Sekunden dichter Nebel auf: Alle außer den Impostorn sehen nur noch ganz wenig.\n\nAnders als die Licht-Sabotage muss niemand etwas reparieren - der Nebel verschwindet von selbst. Im Schein einer Laterne sieht man trotzdem.",
        "Halloween impostor. Press F and thick fog rolls in for a few seconds: everyone except the impostors can barely see.\n\nUnlike the lights sabotage nobody has to fix anything - the fog fades by itself. In the light of a lantern you can still see.");

    private const byte ActionFog = 0;

    internal static FogMaker Instance { get; private set; }

    private RoleOption cooldown, duration, factor;
    private float fogUntil;

    internal bool FogActive => fogUntil > Time.time && !InMeeting;
    internal float Factor => factor.Value;

    protected override void Setup()
    {
        Instance = this;
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 35f, 10f, 90f, 2.5f, "s");
        duration = AddOption("Duration", "Nebel-Dauer|Fog duration", 8f, 3f, 20f, 1f, "s");
        factor = AddOption("Vision", "Sicht im Nebel|Vision in fog", 0.35f, 0.1f, 0.8f, 0.05f, "x");

        AddButton(new RoleButton
        {
            Label = "Nebel|Fog",
            Icon = "fog",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            Duration = () => duration.Value,
            Hotkey = KeyCode.F,
            OnClick = _ => SendToAll(ActionFog),
        });
    }

    public override void ResetState() => fogUntil = 0f;

    public override void OnMeetingStart() => fogUntil = 0f;

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionFog || !RoleRegistry.Has(sender, this)) return;
        fogUntil = Time.time + duration.Value;

        var local = PlayerControl.LocalPlayer;
        if (sender == local) Sfx.Play(Sfx.Vanish);
        else if (IsAlive(local) && !IsImpostor(local))
        {
            Sfx.Play(Sfx.Creepy, 0.5f);
            Notifier.Show($"<color={ColorHex}>{Lang.T("Dichter Nebel zieht auf ...", "Thick fog rolls in ...")}</color>", 3f);
        }
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CalculateLightRadius))]
internal static class FogVisionPatch
{
    public static void Postfix(NetworkedPlayerInfo player, ref float __result)
    {
        var fog = FogMaker.Instance;
        if (fog == null || !fog.FogActive || player == null || player.IsDead) return;
        if (player.Role && player.Role.IsImpostor) return;
        __result *= fog.Factor;
    }
}
