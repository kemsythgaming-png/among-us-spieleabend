using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Modifiers;

/// <summary>
/// Anpassung: Wird der Traeger als ERSTER Spieler getoetet (nicht rausgevotet), gewinnt er sofort allein.
/// Sobald irgendein anderer Spieler vorher gestorben ist, ist die Chance vorbei und seine normale Rolle zaehlt.
/// </summary>
public sealed class Martyr : CustomRole
{
    public override string ConfigSection => "Maertyrer";
    public override string Name => Lang.T("Märtyrer", "Martyr");
    public override Color Color => new Color32(190, 40, 60, 255);
    public override RoleTeam Team => RoleTeam.Modifier;
    public override string IntroText => Lang.T("Stirb als Erster", "Die first");
    public override string Description => Lang.T("Wirst du als Erster getötet,\ngewinnst du allein.", "If you are killed first,\nyou win alone.");
    public override string LongDescription => Lang.T(
        "Anpassung: Wirst du als ERSTER Spieler der Runde getötet, gewinnst du sofort allein. Ein Rauswurf im Meeting zählt nicht - nur ein echter Tod (Kill, Guesser ...). Stirbt vor dir schon jemand anderes, ist die Chance vorbei und nur deine normale Rolle zählt. Deine Rolle bleibt dir ansonsten erhalten.",
        "Modifier: if you are the FIRST player of the round to be killed, you instantly win alone. Being voted out in a meeting does not count - only a real death (kill, guesser ...). If someone else dies before you, the chance is gone and only your normal role counts. Otherwise you keep your normal role.");

    // Host: ist in dieser Runde schon jemand gestorben? (dann kann niemand mehr "als Erster" gewinnen)
    private bool anyDeathYet;

    protected override void Setup() { }

    public override void ResetState() => anyDeathYet = false;

    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        if (!AmHost || !victim) return;

        bool firstDeath = !anyDeathYet;
        anyDeathYet = true;

        // Nur der allererste Tod, und nur ein echter Tod (kein Rauswurf), und nur fuer einen Traeger
        if (!firstDeath || cause == DeathCause.Exile) return;
        if (!RoleRegistry.Has(victim, this)) return;

        CustomWin.Trigger(this, new[] { victim });
    }
}
