using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>Anpassung: laeuft schneller.</summary>
public sealed class Flash : CustomRole
{
    public override string Name =>
        Lang.T("Flash", "Flash");
    public override Color Color => new Color32(255, 230, 50, 255);
    public override RoleTeam Team => RoleTeam.Modifier;
    public override string IntroText =>
        Lang.T("Du bist schneller als alle", "You are faster than everyone");
    public override string Description =>
        Lang.T("Du läufst schneller.", "You move faster.");
    public override string LongDescription =>
        Lang.T("Anpassung: Du läufst schneller als alle anderen. Deine Rolle bleibt.", "Modifier: you move faster than everyone else. Your role stays.");

    private RoleOption speed;

    protected override void Setup()
    {
        speed = AddOption("Speed", "Tempo|Speed", 1.25f, 1.1f, 2f, 0.05f, "x");
    }

    public override float SpeedMultiplier(PlayerControl local) => IsLocalRole && IsAlive(local) ? speed.Value : 1f;
}
