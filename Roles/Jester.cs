using System.Linq;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>Neutral: gewinnt allein, wenn er rausgevotet wird. Seine Aufgaben zaehlen nicht fuer die Crew. Kann (einstellbar) venten.</summary>
public sealed class Jester : CustomRole
{
    public override string Name =>
        Lang.T("Jester", "Jester");
    public override Color Color => new Color32(236, 98, 165, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override string IntroText =>
        Lang.T("Lass dich rausvoten", "Get yourself voted out");
    public override string Description =>
        Lang.T("Lass dich rausvoten,\ndann gewinnst du allein. Du kannst venten (V).", "Get voted out\nto win alone. You can vent (V).");
    public override string LongDescription =>
        Lang.T("Du bist neutral. Dein Ziel: im Meeting rausgevotet werden!\n\nWirk verdächtig, aber nicht zu offensichtlich - zum Beispiel, indem du wie ein Impostor durch die Vents kriechst (Taste V, einstellbar). Fliegst du raus, gewinnst du sofort allein. Wirst du getötet, hast du verloren.", "You are neutral. Your goal: get voted out in a meeting!\n\nAct suspicious, but not too obvious - for example by crawling through the vents like an impostor (key V, configurable). If you are ejected, you win alone instantly. If you get killed, you lose.");
    public override int DefaultCount => 1;

    internal static Jester Instance { get; private set; }

    private RoleOption revealOnExile, canVent;

    protected override void Setup()
    {
        Instance = this;
        revealOnExile = AddToggle("RevealOnExile", "Beim Rauswurf als Jester zeigen|Reveal as Jester when ejected", true);
        canVent = AddToggle("CanVent", "Kann venten|Can vent", true);

        AddButton(new RoleButton
        {
            Label = "Venten|Vent",
            Icon = "vent",
            NeedsTarget = false,
            UsableInVent = true,
            Cooldown = () => 0f,
            StartCooldown = 0f,
            Hotkey = KeyCode.V,
            Visible = () => canVent.Bool,
            CanUse = () => VentToUse() != null,
            OnClick = _ => VentToUse()?.Use(),
        });
    }

    private Vent shownArrowsFor;

    /// <summary>Im Vent die Pfeile zu den Nachbar-Vents zeigen (macht das Spiel sonst nur fuer Impostor/Engineer).</summary>
    public override void Update()
    {
        var local = PlayerControl.LocalPlayer;
        Vent current = null;
        if (IsLocalRole && IsAlive(local) && local.inVent && ShipStatus.Instance && !InMeeting)
        {
            Vector2 me = local.GetTruePosition();
            current = ShipStatus.Instance.AllVents.Where(v => v).OrderBy(v => Vector2.Distance(me, v.transform.position)).FirstOrDefault();
        }
        if (current == shownArrowsFor) return;
        if (shownArrowsFor) shownArrowsFor.SetButtons(false);
        if (current) current.SetButtons(true);
        shownArrowsFor = current;
    }

    public override void ResetState() => shownArrowsFor = null;

    /// <summary>Darf dieser Spieler venten?</summary>
    internal bool Vents(PlayerControl p) => canVent.Bool && RoleRegistry.Has(p, this) && IsAlive(p);

    /// <summary>Das Vent in Reichweite (bzw. das, in dem man gerade steckt).</summary>
    private static Vent VentToUse()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !ShipStatus.Instance) return null;
        Vector2 me = local.GetTruePosition();
        var vent = ShipStatus.Instance.AllVents.Where(v => v).OrderBy(v => Vector2.Distance(me, v.transform.position)).FirstOrDefault();
        if (!vent) return null;
        vent.CanUse(local.Data, out bool canUse, out _);
        return canUse ? vent : null;
    }

    public override string GetExileText(PlayerControl player) =>
        revealOnExile.Bool && player.Data != null ? player.Data.PlayerName + Lang.T(" war der Jester.", " was the Jester.") : null;

    public override void OnExiled(PlayerControl player)
    {
        if (AmongUsClient.Instance.AmHost) CustomWin.Trigger(this, new[] { player });
    }
}

/// <summary>Vents fuer den Jester benutzbar machen (wie fuer Impostor: Reichweite und freie Sicht zum Vent).</summary>
[HarmonyPatch(typeof(Vent), nameof(Vent.CanUse))]
internal static class JesterVentPatch
{
    public static void Postfix(Vent __instance, NetworkedPlayerInfo pc, ref bool canUse, ref bool couldUse, ref float __result)
    {
        var jester = Jester.Instance;
        var player = pc != null ? pc.Object : null;
        if (jester == null || !player || !jester.Vents(player)) return;

        Vector2 me = player.GetTruePosition();
        Vector2 vent = __instance.transform.position;
        float dist = Vector2.Distance(me, vent);
        couldUse = player.CanMove || player.inVent;
        canUse = couldUse && dist <= __instance.UsableDistance
                 && !PhysicsHelpers.AnythingBetween(player.Collider, me, vent, Constants.ShipOnlyMask, false);
        __result = dist;
    }
}
