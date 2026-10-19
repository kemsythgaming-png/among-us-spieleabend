using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Modifiers;

/// <summary>Anpassung: ein Knopf, mit dem man von ueberall ein Notfall-Meeting einberufen kann.</summary>
public sealed class ButtonBarry : CustomRole
{
    public override string ConfigSection => "ButtonBarry";
    public override string Name => Lang.T("Button Barry", "Button Barry");
    public override Color Color => new Color32(230, 70, 70, 255);
    public override RoleTeam Team => RoleTeam.Modifier;
    public override string IntroText => Lang.T("Der Knopf ist immer dabei", "The button is always with you");
    public override string Description => Lang.T("Ruf von überall ein Meeting (F).", "Call a meeting from anywhere (F).");
    public override string LongDescription => Lang.T(
        "Anpassung: Du hast einen eigenen Notfall-Knopf und kannst von überall ein Meeting einberufen - du musst nicht zum Tisch laufen. Es gelten die normalen Notfall-Regeln (Anzahl/Abklingzeit). Deine Rolle bleibt.",
        "Modifier: you carry your own emergency button and can call a meeting from anywhere - no need to walk to the table. Normal emergency rules apply (count/cooldown). Your role stays.");

    private RoleOption cooldown;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 25f, 10f, 60f, 5f, "s");

        AddButton(new RoleButton
        {
            Label = "Notfall|Emergency",
            Icon = "ebutton",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 15f,
            CanUse = () => !InMeeting,
            OnClick = _ =>
            {
                var local = PlayerControl.LocalPlayer;
                if (local && IsAlive(local)) local.CmdReportDeadBody(null); // null = Notfall-Meeting
            },
        });
    }
}
