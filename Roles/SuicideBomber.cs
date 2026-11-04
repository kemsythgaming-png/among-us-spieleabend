using System;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor: sprengt sich selbst in die Luft. Alle in einem Radius sterben - er selbst auch. Einmalige, heftige
/// Aktion, um mehrere Spieler auf einmal mitzunehmen.
/// </summary>
public sealed class SuicideBomber : CustomRole
{
    public override string ConfigSection => "Selbstmordbomber";
    public override string Name => Lang.T("Selbstmordbomber", "Suicide Bomber");
    public override Color Color => new Color32(230, 90, 40, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Reiß alle mit", "Take them all with you");
    public override string Description => Lang.T("Sprenge dich (F) - alle in der\nNähe sterben, du auch.", "Blow yourself up (F) - everyone\nnearby dies, you too.");
    public override string LongDescription => Lang.T(
        "Impostor: Mit Taste F sprengst du dich in die Luft. Jeder im Radius stirbt - auch du selbst und andere Impostor. Eine einmalige, heftige Aktion. Deinen normalen Kill hast du bis dahin zusätzlich.",
        "Impostor: press F to blow yourself up. Everyone in the radius dies - including you and other impostors. A one-time, heavy move. Until then you also keep your normal kill.");

    private const byte ActionBoom = 0;

    private RoleOption radius;

    protected override void Setup()
    {
        radius = AddOption("Radius", "Radius|Radius", 2.5f, 1f, 6f, 0.5f);

        AddButton(new RoleButton
        {
            Label = "Sprengen|Detonate",
            Icon = "ignite",
            NeedsTarget = false,
            Cooldown = () => 10f,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer),
            OnClick = _ =>
            {
                var local = PlayerControl.LocalPlayer;
                if (!local) return;
                var pos = local.GetTruePosition();
                var data = new byte[8];
                BitConverter.GetBytes(pos.x).CopyTo(data, 0);
                BitConverter.GetBytes(pos.y).CopyTo(data, 4);
                SendToAll(ActionBoom, data);
            },
        });
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionBoom || data.Length < 8 || !RoleRegistry.Has(sender, this)) return;
        var pos = new Vector2(BitConverter.ToSingle(data, 0), BitConverter.ToSingle(data, 4));
        float r = radius.Value;

        int willDie = 0;
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!IsAlive(p) || InMeeting || p.inVent) continue;
            if (Vector2.Distance(p.GetTruePosition(), pos) > r) continue;
            willDie++;
            ProtectionVisuals.ShowBurst(p, Color);
            if (AmHost) KillSystem.ForceKill(p, p); // Selbst-Kill: trifft jeden im Radius, auch Impostor
        }
        if (willDie > 0) Sfx.Play(Sfx.Kill, 0.7f);
    }
}
