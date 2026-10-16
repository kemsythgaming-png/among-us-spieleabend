using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>Impostor: alle sehen fuer kurze Zeit gleich aus (grau, ohne Namen und Kosmetik).</summary>
public sealed class Camouflager : CustomRole
{
    public override string Name =>
        Lang.T("Camouflager", "Camouflager");
    public override Color Color => new Color32(140, 140, 140, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText =>
        Lang.T("Lass alle gleich aussehen", "Make everyone look the same");
    public override string Description =>
        Lang.T("Tarne alle Spieler kurz (F).", "Camouflage everyone briefly (F).");
    public override string LongDescription =>
        Lang.T("Du bist Impostor. Mit Taste F sehen alle für ein paar Sekunden gleich aus: grau, ohne Namen und Hut.\n\nPerfekt für einen unerkannten Kill. Ein Meeting beendet die Tarnung.", "You are an impostor. Press F and everyone looks the same for a few seconds: grey, no name, no hat.\n\nPerfect for an unseen kill. A meeting ends the camouflage.");

    private const byte ActionCamo = 0;

    private RoleOption cooldown, duration;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 30f, 10f, 60f, 5f, "s");
        duration = AddOption("Duration", "Dauer|Duration", 10f, 5f, 20f, 1f, "s");

        AddButton(new RoleButton
        {
            Label = "Tarnen|Camo",
            Icon = "camo",
            Cooldown = () => cooldown.Value,
            Duration = () => duration.Value,
            Hotkey = KeyCode.F,
            NeedsTarget = false,
            OnClick = _ => SendToAll(ActionCamo),
        });
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionCamo && RoleRegistry.Has(sender, this) && IsAlive(sender)) {
            Camouflage.Start(duration.Value);
            Sfx.Play(Sfx.Shapeshift); // alle hoeren, dass die Tarnung beginnt
        }
    }
}
