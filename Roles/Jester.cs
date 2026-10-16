using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>Neutral: gewinnt allein, wenn er rausgevotet wird. Seine Aufgaben zaehlen nicht fuer die Crew.</summary>
public sealed class Jester : CustomRole
{
    public override string Name =>
        Lang.T("Jester", "Jester");
    public override Color Color => new Color32(236, 98, 165, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override string IntroText =>
        Lang.T("Lass dich rausvoten", "Get yourself voted out");
    public override string Description =>
        Lang.T("Lass dich rausvoten,\ndann gewinnst du allein.", "Get voted out\nto win alone.");
    public override string LongDescription =>
        Lang.T("Du bist neutral. Dein Ziel: im Meeting rausgevotet werden!\n\nWirk verdächtig, aber nicht zu offensichtlich. Fliegst du raus, gewinnst du sofort allein. Wirst du getötet, hast du verloren.", "You are neutral. Your goal: get voted out in a meeting!\n\nAct suspicious, but not too obvious. If you are ejected, you win alone instantly. If you get killed, you lose.");
    public override int DefaultCount => 1;

    private RoleOption revealOnExile;

    protected override void Setup()
    {
        revealOnExile = AddToggle("RevealOnExile", "Beim Rauswurf als Jester zeigen|Reveal as Jester when ejected", true);
    }

    public override string GetExileText(PlayerControl player) =>
        revealOnExile.Bool && player.Data != null ? player.Data.PlayerName + Lang.T(" war der Jester.", " was the Jester.") : null;

    public override void OnExiled(PlayerControl player)
    {
        if (AmongUsClient.Instance.AmHost) CustomWin.Trigger(this, new[] { player });
    }
}
