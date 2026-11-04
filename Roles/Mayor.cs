using System.Collections.Generic;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Crew (Mayor): Startet mit 1 Stimme. Nutzt er seine Stimme in einem Meeting NICHT (Skip oder gar nicht wählen),
/// bekommt er im nächsten Meeting +1 Stimme - setzt er seine geballte Stimme ein (wählt einen Spieler), zählt sie
/// entsprechend mehrfach und fällt danach wieder auf 1 zurück. Nach oben begrenzt durch die Max-Stimmen-Option.
/// </summary>
public sealed class Mayor : CustomRole
{
    public override string Name =>
        Lang.T("Mayor", "Mayor");
    public override Color Color => new Color32(112, 79, 168, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText =>
        Lang.T("Deine Stimme hat Gewicht", "Your vote counts more");
    public override string Description =>
        Lang.T("Spar deine Stimme - sie wird\njedes Meeting stärker.", "Save your vote - it grows\nevery meeting.");
    public override string LongDescription =>
        Lang.T("Du bist Crew. Du startest mit 1 Stimme. Wählst du in einem Meeting NICHT (Skip oder gar nicht abstimmen), bekommst du im nächsten Meeting eine Stimme mehr - so sammelst du an (z. B. im 3. Meeting 3 Stimmen). Wählst du dann einen Spieler, zählt deine Stimme entsprechend mehrfach und fällt danach wieder auf 1. Die Anzeige zeigt nur eine Stimme, gezählt werden aber alle.",
                "You are crew. You start with 1 vote. If you do NOT vote in a meeting (skip or abstain), you gain one more vote next meeting - so it adds up (e.g. 3 votes in the 3rd meeting). When you then vote for a player, your vote counts that many times and drops back to 1. The display shows one vote, but all are counted.");

    internal static Mayor Instance { get; private set; }

    /// <summary>Obergrenze der angesammelten Stimmen.</summary>
    internal RoleOption Votes { get; private set; }

    // Host-autoritativ: aktuelle Stimmkraft je Mayor-Spieler (Standard 1).
    private readonly Dictionary<byte, int> power = new();

    protected override void Setup()
    {
        Instance = this;
        Votes = AddOption("Votes", "Max-Stimmen|Max votes", 5f, 2f, 10f, 1f);
    }

    public override void ResetState() => power.Clear();

    internal int GetPower(byte id) => power.TryGetValue(id, out var v) ? Mathf.Max(1, v) : 1;
    internal void SetPower(byte id, int v) => power[id] = Mathf.Clamp(v, 1, Votes.Int);
}

/// <summary>
/// Beim Auszaehlen (Host): die angesammelte Stimmkraft des Mayors auf sein Ziel legen und seine Stimmkraft
/// anpassen - genutzt (einen Spieler gewählt) -> zurück auf 1; nicht genutzt (Skip/keine Wahl) -> +1.
/// </summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CalculateVotes))]
internal static class MayorVotePatch
{
    public static void Postfix(MeetingHud __instance, Il2CppSystem.Collections.Generic.Dictionary<byte, int> __result)
    {
        var mayor = Mayor.Instance;
        if (mayor == null || __result == null || !RoleRegistry.HasAssignments) return;

        foreach (var state in __instance.playerStates)
        {
            if (!state) continue;
            var voter = CustomRole.FindPlayer((byte)state.PlayerId);
            if (!CustomRole.IsAlive(voter) || !RoleRegistry.Has(voter, mayor)) continue;

            byte id = (byte)state.PlayerId;
            int p = mayor.GetPower(id);

            byte votedFor = (byte)state.VotedForId;
            bool used = state.DidVote && __result.ContainsKey(votedFor); // echten Spieler gewählt = genutzt (Skip zählt nicht)
            if (used)
            {
                __result[votedFor] = __result[votedFor] + p - 1;
                mayor.SetPower(id, 1);          // nach dem Einsatz wieder bei 1
            }
            else
            {
                mayor.SetPower(id, p + 1);        // nicht genutzt -> nächstes Meeting eine mehr
            }
        }
    }
}
