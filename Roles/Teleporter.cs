using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Crew: öffnet die echte Schiffs-Karte und teleportiert sich per Klick an die angeklickte Stelle.
/// Landet immer auf einem begehbaren Punkt (nie in einer Wand). Danach läuft eine Abklingzeit.
/// Die eigentliche Karten-Logik liegt in <see cref="MapTeleport"/> (auch vom Gambler genutzt).
/// </summary>
public sealed class Teleporter : CustomRole
{
    public override string Name => Lang.T("Teleporter", "Teleporter");
    public override Color Color => new Color32(90, 205, 180, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Sei im Nu woanders", "Be elsewhere in a blink");
    public override string Description => Lang.T("Öffne die Karte (F) und\nklick dein Ziel an.", "Open the map (F) and\nclick your target.");
    public override string LongDescription => Lang.T(
        "Du bist Crew. Mit Taste F öffnet sich die Karte. Klick auf eine Stelle - du teleportierst sofort dorthin. Du landest immer auf einem begehbaren Punkt, nie in einer Wand. Danach läuft eine Abklingzeit.",
        "You are crew. Press F to open the map. Click a spot to teleport there instantly. You always land on a walkable point, never inside a wall. Then a cooldown runs.");

    private RoleOption cooldown;
    private RoleButton button;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 45f, 10f, 120f, 5f, "s");

        button = AddButton(new RoleButton
        {
            Label = "Teleportieren|Teleport",
            Icon = "tp",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value, // echte Länge (sonst NaN in der Radial-Anzeige -> Button beim 1. Mal unsichtbar)
            StartCooldown = 5f,
            Hotkey = KeyCode.F,
            CanUse = () => !MapTeleport.Active && !InMeeting,
            OnClick = _ => OpenMap(),
        });
    }

    public override void ResetState() { if (MapTeleport.Active) MapTeleport.Cancel(); }

    public override void OnMeetingStart() { if (MapTeleport.Active) MapTeleport.Cancel(); }

    private void OpenMap()
    {
        if (!MapTeleport.Open()) return;
        if (button != null) button.Timer = 0f; // Abklingzeit startet erst beim Teleport, nicht beim Karte-Öffnen
        Notifier.Show($"<color={ColorHex}>{Lang.T("Klick auf die Karte, um dich zu teleportieren.", "Click the map to teleport.")}</color>", 4f);
    }

    public override void Update()
    {
        if (!IsLocalRole) return;
        if (MapTeleport.Update() && button != null) button.Timer = cooldown.Value; // teleportiert -> Abklingzeit starten
    }
}
