using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Modifiers;

/// <summary>
/// Anpassung (nur Crew): Hat der Spieler alle seine Aufgaben erledigt, wird er automatisch zum Sheriff
/// (garantiert - anders als die allgemeine Sheriff-Chance nach Aufgaben).
/// </summary>
public sealed class Deputy : CustomRole
{
    public override string ConfigSection => "Hilfssheriff";
    public override string Name => Lang.T("Hilfssheriff", "Deputy");
    public override Color Color => new Color32(210, 170, 70, 255);
    public override RoleTeam Team => RoleTeam.Modifier;

    /// <summary>Nur normale Crew ohne eigene Mod-Rolle (sonst würde die Hauptrolle überschrieben).</summary>
    public override bool CanReceiveModifier(PlayerControl player, CustomRole mainRole) =>
        !IsImpostor(player) && mainRole == null;

    public override string IntroText => Lang.T("Mach dir den Stern verdient", "Earn your badge");
    public override string Description => Lang.T("Schaffst du alle Aufgaben,\nwirst du zum Sheriff.", "Finish all tasks\nto become the Sheriff.");
    public override string LongDescription => Lang.T(
        "Anpassung (nur Crew): Erledige alle deine Aufgaben - dann wirst du automatisch zum Sheriff und bekommst einen Kill-Button, um einen Impostor zu erschießen (triffst du einen Unschuldigen, stirbst du selbst). Deine Rolle bleibt sonst normale Crew.",
        "Modifier (crew only): finish all your tasks - then you automatically become the Sheriff and get a kill button to shoot an impostor (if you hit an innocent, you die yourself). Otherwise you are a normal crewmate.");

    /// <summary>Host: schon zum Sheriff gemacht (einmal pro Spieler).</summary>
    private readonly HashSet<byte> promoted = new();

    protected override void Setup() { }

    public override void ResetState() => promoted.Clear();

    /// <summary>Host: hat ein Hilfssheriff alle Aufgaben erledigt, wird er garantiert zum Sheriff.</summary>
    public override void Update()
    {
        if (!AmHost || !RoleRegistry.HasAssignments || InMeeting) return; // läuft auch im Freeplay (zum Testen)
        var sheriff = RoleRegistry.Roles.OfType<Sheriff>().FirstOrDefault();
        if (sheriff == null) return;

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!IsAlive(p) || promoted.Contains(p.PlayerId)) continue;
            if (!RoleRegistry.Has(p, this) || RoleRegistry.GetRole(p) != null) continue; // nur Hilfssheriffs ohne Hauptrolle
            var tasks = p.Data?.Tasks;
            if (tasks == null || tasks.Count == 0 || tasks.ToArray().Any(t => !t.Complete)) continue;

            promoted.Add(p.PlayerId);
            RoleRegistry.ChangeRole(p, sheriff);
        }
    }
}
