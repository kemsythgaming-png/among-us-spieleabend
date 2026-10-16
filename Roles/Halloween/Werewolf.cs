using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Halloween;

/// <summary>Impostor: verwandelt sich kurz in einen Wolf - schneller, sieht weiter, kuerzere Kill-Abklingzeit. Alle hoeren das Heulen.</summary>
public sealed class Werewolf : CustomRole
{
    public override string ConfigSection => "Werwolf";
    public override string Name => Lang.T("Werwolf", "Werewolf");
    public override Color Color => new Color32(150, 95, 55, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Der Vollmond ruft", "The full moon calls");
    public override string Description => Lang.T("Verwandle dich (F): schneller,\nweitere Sicht, schnelleres Töten.", "Transform (F): faster,\nwider vision, quicker kills.");
    public override string LongDescription => Lang.T(
        "Halloween-Impostor. Mit Taste F wirst du für ein paar Sekunden zum Wolf: Du bist schneller, siehst weiter und deine Kill-Abklingzeit ist kürzer.\n\nAber Vorsicht: Bei jeder Verwandlung hören alle ein Heulen.",
        "Halloween impostor. Press F to become a wolf for a few seconds: you are faster, see further and your kill cooldown is shorter.\n\nBut careful: everyone hears a howl whenever you transform.");

    private const byte ActionHowl = 0;

    internal static Werewolf Instance { get; private set; }

    private RoleOption cooldown, duration, speed, vision, wolfKillCooldown, howl;
    private RoleButton button;

    /// <summary>Ist der lokale Spieler gerade ein Wolf?</summary>
    internal bool LocalTransformed => button != null && button.EffectActive && IsLocalRole && IsAlive(PlayerControl.LocalPlayer);

    internal float VisionFactor => vision.Value;

    protected override void Setup()
    {
        Instance = this;
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 30f, 10f, 60f, 2.5f, "s");
        duration = AddOption("Duration", "Dauer als Wolf|Wolf duration", 10f, 3f, 20f, 1f, "s");
        speed = AddOption("Speed", "Wolf-Tempo|Wolf speed", 1.3f, 1f, 2f, 0.05f, "x");
        vision = AddOption("Vision", "Wolf-Sicht|Wolf vision", 1.5f, 1f, 3f, 0.1f, "x");
        wolfKillCooldown = AddOption("KillCooldown", "Kill-Abklingzeit als Wolf|Kill cooldown as wolf", 10f, 2.5f, 30f, 2.5f, "s");
        howl = AddToggle("Howl", "Alle hören das Heulen|Everyone hears the howl", true);

        button = AddButton(new RoleButton
        {
            Label = "Verwandeln|Transform",
            Icon = "claw",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            Duration = () => duration.Value,
            Hotkey = KeyCode.F,
            OnClick = _ => SendToAll(ActionHowl),
        });
    }

    public override float SpeedMultiplier(PlayerControl local) => LocalTransformed ? speed.Value : 1f;

    public override void Update()
    {
        // Als Wolf ist die Kill-Abklingzeit hoechstens so lang wie eingestellt
        var local = PlayerControl.LocalPlayer;
        if (LocalTransformed && local.killTimer > wolfKillCooldown.Value) local.SetKillTimer(wolfKillCooldown.Value);
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionHowl || !RoleRegistry.Has(sender, this)) return;
        if (sender == PlayerControl.LocalPlayer)
        {
            Sfx.Play(Sfx.Creepy);
            return;
        }
        if (!howl.Bool) return;
        Sfx.Play(Sfx.Creepy, 0.6f);
        Notifier.Show($"<color={ColorHex}>{Lang.T("Ein Heulen hallt durch die Nacht ...", "A howl echoes through the night ...")}</color>", 3f);
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CalculateLightRadius))]
internal static class WerewolfVisionPatch
{
    public static void Postfix(NetworkedPlayerInfo player, ref float __result)
    {
        var wolf = Werewolf.Instance;
        if (wolf == null || player == null || player.Object != PlayerControl.LocalPlayer) return;
        if (wolf.LocalTransformed) __result *= wolf.VisionFactor;
    }
}
