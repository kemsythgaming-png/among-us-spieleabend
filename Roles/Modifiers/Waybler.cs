using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Modifiers;

/// <summary>
/// Anpassung: Wird GENAU dieser Spieler im allerersten Rauswurf des Spiels rausgevotet, gewinnt er allein (wie ein
/// einmaliger Jester). Sobald ein anderer als Erster rausgevotet wurde, verliert die Anpassung ihre Wirkung.
/// </summary>
public sealed class Waybler : CustomRole
{
    public override string ConfigSection => "Waybler";
    public override string Name => "Waybler";
    public override Color Color => new Color32(120, 205, 130, 255);
    public override RoleTeam Team => RoleTeam.Modifier;
    public override bool ModifierAllowsImpostors => false; // nur Crew/Neutral - ein Impostor-Jester waere seltsam
    public override string IntroText => Lang.T("Flieg als Erster raus", "Be the first voted out");
    public override string Description => Lang.T("Wirst du als Erster rausgevotet,\ngewinnst du. Danach wirkungslos.", "If you're the first voted out,\nyou win. Then no effect.");
    public override string LongDescription => Lang.T(
        "Anpassung: Wirst DU im allerersten Rauswurf des Spiels rausgevotet, gewinnst du sofort allein. Sobald aber ein anderer als Erster rausgevotet wurde, verliert die Anpassung ihre Wirkung - du spielst dann einfach deine normale Rolle weiter.",
        "Modifier: if YOU are the very first player voted out, you win alone instantly. But once someone else is voted out first, the modifier loses its effect - you just keep playing your normal role.");

    private bool firstExileDone;

    protected override void Setup() { }

    public override void ResetState() => firstExileDone = false;

    public override void OnAnyExiled(PlayerControl player)
    {
        if (!AmHost || firstExileDone) return;
        firstExileDone = true; // nur der allererste Rauswurf zaehlt
        if (player && RoleRegistry.Has(player, this)) CustomWin.Trigger(this, new[] { player });
    }
}

/// <summary>
/// Anpassung: Sobald irgendeine Sabotage aktiv ist, flitzt der Hamster deutlich schneller. Danach wieder normal.
/// </summary>
public sealed class Hamster : CustomRole
{
    public override string ConfigSection => "Hamster";
    public override string Name => "Hamster";
    public override Color Color => new Color32(214, 170, 110, 255);
    public override RoleTeam Team => RoleTeam.Modifier;
    public override string IntroText => Lang.T("Ab ins Laufrad!", "Hit the wheel!");
    public override string Description => Lang.T("Bei Sabotage wirst du schnell.", "During sabotage you get fast.");
    public override string LongDescription => Lang.T(
        "Anpassung: Sobald irgendeine Sabotage aktiv ist, flitzt du wie ein Hamster im Laufrad - deutlich schneller. Ist die Sabotage behoben, bist du wieder normal schnell. Deine Rolle bleibt.",
        "Modifier: whenever any sabotage is active, you zip around like a hamster in a wheel - much faster. Once the sabotage is fixed you are back to normal. Your role stays.");

    private RoleOption speed;

    protected override void Setup()
    {
        speed = AddOption("Speed", "Tempo bei Sabotage|Speed during sabotage", 1.6f, 1.1f, 2.5f, 0.1f, "x");
    }

    public override float SpeedMultiplier(PlayerControl local) =>
        IsLocalRole && IsAlive(local) && SabotageActive() ? speed.Value : 1f;

    /// <summary>Ist gerade irgendeine Sabotage aktiv?</summary>
    private static bool SabotageActive()
    {
        var ship = ShipStatus.Instance;
        if (!ship || ship.Systems == null) return false;
        try
        {
            if (!ship.Systems.ContainsKey(SystemTypes.Sabotage)) return false;
            var sys = ship.Systems[SystemTypes.Sabotage].TryCast<SabotageSystemType>();
            return sys != null && sys.AnyActive;
        }
        catch { return false; }
    }
}
