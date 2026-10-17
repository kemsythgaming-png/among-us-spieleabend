using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>Crew: prueft Spieler und sieht danach dauerhaft, ob sie gut, boese oder neutral sind.</summary>
public sealed class Seer : CustomRole
{
    public override string Name =>
        Lang.T("Seer", "Seer");
    public override Color Color => new Color32(97, 178, 108, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText =>
        Lang.T("Blicke in die Seelen", "Look into their souls");
    public override string Description =>
        Lang.T("Prüfe Spieler (F): gut oder böse.", "Check players (F): good or evil.");
    public override string LongDescription =>
        Lang.T("Du bist Crew. Mit Taste F prüfst du einen Spieler in der Nähe.\n\nDanach steht an seinem Namen, ob er [Gut] oder [Böse] ist. Neutrale (Jester, Arsonist ...) zeigen sich auch als [Böse] - du weißt also nicht, ob es ein Impostor oder ein Neutraler ist. Nur du siehst das.", "You are crew. Press F to check a nearby player.\n\nAfterwards their name shows [Good] or [Evil]. Neutrals (Jester, Arsonist ...) also show as [Evil] - so you can't tell an impostor from a neutral. Only you can see it.");

    private RoleOption cooldown;

    /// <summary>Nur beim Seer selbst: gepruefte Spieler und das Ergebnis.</summary>
    private readonly Dictionary<byte, string> results = new();

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 25f, 5f, 60f, 5f, "s");

        AddButton(new RoleButton
        {
            Label = "Prüfen|Check",
            Icon = "check",
            Cooldown = () => cooldown.Value,
            Hotkey = KeyCode.F,
            TargetFilter = p => !results.ContainsKey(p.PlayerId),
            OnClick = Check,
        });
    }

    public override void ResetState() => results.Clear();

    private void Check(PlayerControl target)
    {
        string result;
        if (IsImpostor(target) || IsNeutral(target)) result = $"<color=#FF1919>{Lang.T("[Böse]", "[Evil]")}</color>";
        else result = $"<color=#5BE35B>{Lang.T("[Gut]", "[Good]")}</color>";

        results[target.PlayerId] = result;
        Sfx.Play(Sfx.Investigate);
        Notifier.Show($"{target.Data.PlayerName}: {result}");
    }

    public override string GetNameMark(PlayerControl viewer, PlayerControl target) =>
        RoleRegistry.Has(viewer, this) && results.TryGetValue(target.PlayerId, out var r) ? r : null;
}
