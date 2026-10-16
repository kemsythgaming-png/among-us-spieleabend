using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>Crew: seine Stimme zaehlt mehrfach.</summary>
public sealed class Mayor : CustomRole
{
    public override string Name =>
        Lang.T("Mayor", "Mayor");
    public override Color Color => new Color32(112, 79, 168, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText =>
        Lang.T("Deine Stimme hat Gewicht", "Your vote counts more");
    public override string Description =>
        Lang.T("Deine Stimme zählt mehrfach.", "Your vote counts multiple times.");
    public override string LongDescription =>
        Lang.T("Du bist Crew. Deine Stimme im Meeting zählt mehrfach (einstellbar).\n\nDie Anzeige zeigt nur eine Stimme, gezählt werden aber alle.", "You are crew. Your vote in meetings counts multiple times (configurable).\n\nThe display only shows one vote, but all of them are counted.");

    internal static Mayor Instance { get; private set; }

    internal RoleOption Votes { get; private set; }

    protected override void Setup()
    {
        Instance = this;
        Votes = AddOption("Votes", "Stimmen|Votes", 2f, 2f, 5f, 1f);
    }
}

/// <summary>Beim Auszaehlen (Host) bekommt die Stimme des Mayors Extra-Gewicht.</summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CalculateVotes))]
internal static class MayorVotePatch
{
    public static void Postfix(MeetingHud __instance, Il2CppSystem.Collections.Generic.Dictionary<byte, int> __result)
    {
        var mayor = Mayor.Instance;
        if (mayor == null || __result == null || !RoleRegistry.HasAssignments) return;

        foreach (var state in __instance.playerStates)
        {
            if (!state || !state.DidVote) continue;
            var voter = CustomRole.FindPlayer((byte)state.PlayerId);
            if (!CustomRole.IsAlive(voter) || !RoleRegistry.Has(voter, mayor)) continue;

            byte votedFor = (byte)state.VotedForId;
            if (!__result.ContainsKey(votedFor)) continue;
            __result[votedFor] = __result[votedFor] + mayor.Votes.Int - 1;
        }
    }
}
