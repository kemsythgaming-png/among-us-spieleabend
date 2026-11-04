using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Crew (Seer): prueft einen Spieler in der Naehe. Danach ist dessen Name - nur fuer den Seer - gruen (gut) oder
/// rot (boese/neutral), auf der Map und im Meeting. Nur einmal pro Runde; nach jedem Meeting gibt es wieder einen
/// Versuch und die vorher gefaerbten Namen sind wieder normal.
/// </summary>
public sealed class Seer : CustomRole
{
    public override string Name => Lang.T("Seer", "Seer");
    public override Color Color => new Color32(97, 178, 108, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Blicke in die Seelen", "Look into their souls");
    public override string Description =>
        Lang.T("Prüfe einen Spieler (F):\nName wird grün oder rot.", "Check a player (F):\nname turns green or red.");
    public override string LongDescription =>
        Lang.T("Du bist Crew. Mit Taste F prüfst du einen Spieler in der Nähe - einmal pro Runde.\n\nDanach ist sein Name (nur für dich) grün, wenn er gut ist, oder rot, wenn er böse ist. Neutrale (Jester, Arsonist ...) zeigen sich auch rot - du weißt also nicht, ob Impostor oder Neutraler. Nach jedem Meeting hast du wieder einen Versuch, und die alte Färbung ist weg.",
                "You are crew. Press F to check a nearby player - once per round.\n\nAfterwards their name is green (good) or red (evil), only for you. Neutrals (Jester, Arsonist ...) also show red - so you can't tell an impostor from a neutral. After every meeting you get another check and the old coloring is gone.");

    private static readonly Color Good = new Color32(91, 227, 91, 255);
    private static readonly Color Evil = new Color32(255, 25, 25, 255);

    private RoleOption cooldown;
    private int uses = 1;

    /// <summary>Nur beim Seer selbst: gepruefte Spieler und ob boese.</summary>
    private readonly Dictionary<byte, bool> results = new();

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 15f, 5f, 60f, 5f, "s");

        AddButton(new RoleButton
        {
            Label = "Prüfen|Check",
            Icon = "check",
            Cooldown = () => cooldown.Value,
            Hotkey = KeyCode.F,
            UsesLeft = () => uses,
            CanUse = () => uses > 0,
            TargetFilter = p => !results.ContainsKey(p.PlayerId),
            OnClick = Check,
        });
    }

    public override void ResetState() { results.Clear(); uses = 1; }

    // Nach dem Meeting: neuer Versuch, alte Faerbung weg.
    public override void OnMeetingEnd(PlayerControl exiled) { results.Clear(); uses = 1; }

    private void Check(PlayerControl target)
    {
        if (!target || target.Data == null || uses <= 0) return;
        results[target.PlayerId] = IsImpostor(target) || IsNeutral(target);
        uses = 0;
        Sfx.Play(Sfx.Investigate);
    }

    public override Color? GetNameColor(PlayerControl viewer, PlayerControl target)
    {
        if (!RoleRegistry.Has(viewer, this) || !results.TryGetValue(target.PlayerId, out bool evil)) return null;
        return evil ? Evil : Good;
    }
}
