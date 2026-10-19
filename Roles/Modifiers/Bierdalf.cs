using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Modifiers;

/// <summary>
/// Anpassung: Knopf, der einem anderen Spieler fuer kurze Zeit den Betrunken-Effekt verpasst (Steuerung vertauscht).
/// </summary>
public sealed class Bierdalf : CustomRole
{
    public override string ConfigSection => "Bierdalf";
    public override string Name => "Bierdalf";
    public override Color Color => new Color32(210, 160, 60, 255);
    public override RoleTeam Team => RoleTeam.Modifier;
    public override string IntroText => Lang.T("Prost!", "Cheers!");
    public override string Description => Lang.T("Mach andere kurz betrunken (F).", "Make others briefly drunk (F).");
    public override string LongDescription => Lang.T(
        "Anpassung: Mit dem Knopf verpasst du einem Spieler in der Nähe für 8 Sekunden den Betrunken-Effekt (Steuerung vertauscht). Deine Rolle bleibt.",
        "Modifier: the button gives a nearby player the drunk effect (controls swapped) for 8 seconds. Your role stays.");

    internal static Bierdalf Instance { get; private set; }

    private const byte ActionDrunk = 0;
    private RoleOption cooldown, duration;

    // bei allen Clients: wer ist bis wann betrunken
    private readonly Dictionary<byte, float> affected = new();

    protected override void Setup()
    {
        Instance = this;
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 20f, 5f, 60f, 2.5f, "s");
        duration = AddOption("Duration", "Dauer|Duration", 8f, 3f, 15f, 1f, "s");

        AddButton(new RoleButton
        {
            Label = "Prost!|Cheers!",
            Icon = "camo",
            NeedsTarget = true,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            OnClick = target => { if (target) SendToAll(ActionDrunk, target.PlayerId); },
        });
    }

    public override void ResetState() => affected.Clear();

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionDrunk || data.Length < 1) return;
        affected[data[0]] = Time.time + duration.Value;
        var local = PlayerControl.LocalPlayer;
        if (local && local.PlayerId == data[0])
        {
            Sfx.Play(Sfx.Creepy, 0.5f);
            Notifier.Show($"<color={ColorHex}>{Lang.T("Hicks! Dir ist ganz schwummrig ...", "Hic! You feel dizzy ...")}</color>", duration.Value);
        }
    }

    /// <summary>Ist dieser Spieler gerade durch Bierdalf betrunken?</summary>
    internal bool DrunkNow(PlayerControl p) => p && affected.TryGetValue(p.PlayerId, out var t) && t > Time.time;
}
