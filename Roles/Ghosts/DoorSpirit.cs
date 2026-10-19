using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Ghosts;

/// <summary>Geisterrolle: kann die naechstgelegene Tuer wieder oeffnen (z. B. nach einer Tuer-Sabotage).</summary>
public sealed class DoorSpirit : CustomRole
{
    public override string ConfigSection => "Tuergeist";
    public override string Name => Lang.T("Türgeist", "Door Spirit");
    public override Color Color => new Color32(150, 170, 200, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override bool IsGhostRole => true;
    public override bool CanHaunt(PlayerControl dead) => !IsImpostor(dead) && !IsNeutral(dead);
    public override string IntroText => Lang.T("Mach die Tür auf", "Open the door");
    public override string Description => Lang.T("Geist: Öffne die nächste Tür (F).", "Ghost: open the nearest door (F).");
    public override string LongDescription => Lang.T(
        "Geisterrolle für tote Crewmates. Schweb an eine Tür und drück F: Die nächste Tür geht wieder auf - praktisch, wenn die Impostor Türen zugesperrt haben.",
        "Ghost role for dead crewmates. Float to a door and press F: the nearest door opens again - handy when impostors have closed doors.");

    private const byte ActionOpen = 0;
    private RoleOption cooldown;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 20f, 5f, 60f, 2.5f, "s");

        AddButton(new RoleButton
        {
            Label = "Tür öffnen|Open door",
            Icon = "door",
            NeedsTarget = false,
            UsableWhenDead = true,
            Visible = () => !IsAlive(PlayerControl.LocalPlayer),
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => NearestClosed() != null, // nur aktiv, wenn eine Tür zu ist
            OnClick = _ =>
            {
                var door = NearestClosed();
                if (door) SendToAll(ActionOpen, (byte)door.Id);
            },
        });
    }

    /// <summary>Nächste GESCHLOSSENE Tür.</summary>
    private static OpenableDoor NearestClosed()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !ShipStatus.Instance || ShipStatus.Instance.AllDoors == null) return null;
        Vector2 me = local.GetTruePosition();
        return ShipStatus.Instance.AllDoors.ToArray()
            .Where(d => d && !d.IsOpen)
            .OrderBy(d => Vector2.Distance(me, d.transform.position))
            .FirstOrDefault();
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionOpen || data.Length < 1 || !RoleRegistry.Has(sender, this) || !ShipStatus.Instance) return;
        foreach (var d in ShipStatus.Instance.AllDoors)
            if (d && d.Id == data[0]) { d.SetDoorway(true); break; }
        if (sender == PlayerControl.LocalPlayer) Sfx.Play(Sfx.TaskComplete, 0.6f);
    }
}
