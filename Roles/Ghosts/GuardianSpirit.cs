using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Ghosts;

/// <summary>Crew-Geist: nagelt Vents unsichtbar zu - niemand kommt eine Weile hinein, heraus oder hindurch.</summary>
public sealed class GuardianSpirit : CustomRole
{
    public override string ConfigSection => "Waechtergeist";
    public override string Name => Lang.T("Wächtergeist", "Guardian Spirit");
    public override Color Color => new Color32(170, 200, 140, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override bool IsGhostRole => true;
    public override bool CanHaunt(PlayerControl dead) => !IsImpostor(dead) && !IsNeutral(dead);
    public override string IntroText => Lang.T("Kein Impostor kommt hier durch", "No impostor gets through here");
    public override string Description => Lang.T("Geist: Schweb an ein Vent und\nnagle es unsichtbar zu (F).", "Ghost: float to a vent and\nboard it up invisibly (F).");
    public override string LongDescription => Lang.T(
        "Geisterrolle für tote Crewmates. Schweb an ein Vent und drück F: Es ist eine Weile zu - niemand kann hinein, heraus oder durch die Gänge dorthin.\n\nMan sieht es dem Vent nicht an. Ein Impostor merkt es erst, wenn er es benutzen will.",
        "Ghost role for dead crewmates. Float to a vent and press F: it is closed for a while - nobody can get in, out or travel to it.\n\nThe vent looks normal. An impostor only notices when trying to use it.");

    private const byte ActionBlock = 0;
    private const float Reach = 1.3f;

    internal static GuardianSpirit Instance { get; private set; }

    private RoleOption cooldown, duration;

    // bei allen Clients: Vent-Id -> zu bis
    private readonly Dictionary<int, float> blockedUntil = new();

    protected override void Setup()
    {
        Instance = this;
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 30f, 10f, 90f, 2.5f, "s");
        duration = AddOption("Duration", "Vent zu für|Vent closed for", 20f, 5f, 60f, 5f, "s");

        AddButton(new RoleButton
        {
            Label = "Zunageln|Board up",
            Icon = "boards",
            NeedsTarget = false,
            UsableWhenDead = true,
            Visible = () => !IsAlive(PlayerControl.LocalPlayer),
            Cooldown = () => cooldown.Value,
            Duration = () => duration.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => NearestVent() != null,
            OnClick = _ =>
            {
                var vent = NearestVent();
                if (vent) SendToAll(ActionBlock, (byte)vent.Id);
            },
        });
    }

    public override void ResetState() => blockedUntil.Clear();

    public override void OnMeetingStart() => blockedUntil.Clear();

    private static Vent NearestVent()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !ShipStatus.Instance) return null;
        Vector2 me = local.GetTruePosition();
        return ShipStatus.Instance.AllVents.Where(v => v && Vector2.Distance(me, v.transform.position) <= Reach)
            .OrderBy(v => Vector2.Distance(me, v.transform.position)).FirstOrDefault();
    }

    internal bool IsBlocked(Vent vent) =>
        vent && blockedUntil.TryGetValue(vent.Id, out float until) && until > Time.time;

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionBlock || data.Length < 1 || !RoleRegistry.Has(sender, this)) return;
        blockedUntil[data[0]] = Time.time + duration.Value;
        if (sender == PlayerControl.LocalPlayer)
        {
            Sfx.Play(Sfx.TaskComplete, 0.6f);
            Notifier.Show($"<color={ColorHex}>{Lang.T("Vent zugenagelt!", "Vent boarded up!")}</color>", 2.5f);
        }
    }
}

/// <summary>Zugenagelte Vents: nicht benutzbar (hinein und heraus).</summary>
[HarmonyPatch(typeof(Vent), nameof(Vent.CanUse))]
internal static class GuardianVentUsePatch
{
    public static void Postfix(Vent __instance, ref bool canUse, ref bool couldUse)
    {
        var gs = GuardianSpirit.Instance;
        if (gs == null || !gs.IsBlocked(__instance)) return;
        canUse = false;
        couldUse = false;
    }
}

/// <summary>Zugenagelte Vents: auch durch die Gaenge kommt man nicht hinein.</summary>
[HarmonyPatch(typeof(Vent), nameof(Vent.TryMoveToVent))]
internal static class GuardianVentMovePatch
{
    public static bool Prefix(Vent otherVent, ref string error, ref bool __result)
    {
        var gs = GuardianSpirit.Instance;
        if (gs == null || !gs.IsBlocked(otherVent)) return true;
        error = "";
        __result = false;
        return false;
    }
}
