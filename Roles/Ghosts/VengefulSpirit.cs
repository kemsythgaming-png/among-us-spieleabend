using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Ghosts;

/// <summary>Impostor-Geist: laesst alle Crewmates im selben Raum kurz vor Kaelte erstarren.</summary>
public sealed class VengefulSpirit : CustomRole
{
    public override string ConfigSection => "Rachegeist";
    public override string Name => Lang.T("Rachegeist", "Vengeful Spirit");
    public override Color Color => new Color32(150, 220, 255, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override bool IsGhostRole => true;
    public override bool CanHaunt(PlayerControl dead) => IsImpostor(dead);
    public override string IntroText => Lang.T("Auch tot hilfst du deinem Team", "Even dead you help your team");
    public override string Description => Lang.T("Geist: Lass alle Crewmates im Raum\nerstarren (F).", "Ghost: freeze all crewmates\nin the room (F).");
    public override string LongDescription => Lang.T(
        "Geisterrolle für tote Impostor. Schwebe in einen Raum und drück F: Alle lebenden Crewmates und Neutralen in diesem Raum erstarren ein paar Sekunden vor Kälte.\n\nIm Gang (außerhalb von Räumen) trifft es alle in deiner Nähe.",
        "Ghost role for dead impostors. Float into a room and press F: all living crewmates and neutrals in that room freeze in the cold for a few seconds.\n\nIn a hallway (outside rooms) it hits everyone near you.");

    private const byte ActionFreeze = 0;
    private const float HallwayRadius = 4f;
    private static readonly Color Ice = new Color32(150, 220, 255, 255);

    private RoleOption cooldown, freezeTime;
    private float frozenUntil;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 40f, 15f, 90f, 2.5f, "s");
        freezeTime = AddOption("FreezeTime", "Erstarren|Freeze time", 3f, 1f, 8f, 0.5f, "s");

        AddButton(new RoleButton
        {
            Label = "Erstarren|Freeze",
            Icon = "frost",
            NeedsTarget = false,
            UsableWhenDead = true,
            Visible = () => !IsAlive(PlayerControl.LocalPlayer),
            Cooldown = () => cooldown.Value,
            StartCooldown = 15f,
            Hotkey = KeyCode.F,
            OnClick = _ => Freeze(),
        });
    }

    public override void ResetState() => frozenUntil = 0f;

    public override void OnMeetingStart() => frozenUntil = 0f;

    public override float SpeedMultiplier(PlayerControl local) => frozenUntil > Time.time && !InMeeting ? 0f : 1f;

    private void Freeze()
    {
        var local = PlayerControl.LocalPlayer;
        Vector2 me = local.GetTruePosition();
        var room = ShipStatus.Instance ? ShipStatus.Instance.AllRooms.FirstOrDefault(r => r && r.roomArea && r.roomArea.OverlapPoint(me)) : null;

        var targets = new List<byte>();
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!IsAlive(p) || IsImpostor(p) || p.inVent) continue;
            Vector2 pos = p.GetTruePosition();
            bool hit = room ? room.roomArea.OverlapPoint(pos) : Vector2.Distance(me, pos) <= HallwayRadius;
            if (hit) targets.Add(p.PlayerId);
        }

        Sfx.Play(Sfx.Creepy);
        if (targets.Count == 0)
        {
            Notifier.Show($"<color={ColorHex}>{Lang.T("Niemand hier zum Einfrieren.", "Nobody here to freeze.")}</color>", 2.5f);
            return;
        }
        SendToAll(ActionFreeze, targets.ToArray());
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionFreeze || !RoleRegistry.Has(sender, this) || IsAlive(sender) || InMeeting) return;
        var local = PlayerControl.LocalPlayer;
        foreach (var id in data)
        {
            var p = FindPlayer(id);
            if (!IsAlive(p)) continue;
            ProtectionVisuals.ShowBurst(p, Ice);
            if (p != local) continue;
            frozenUntil = Time.time + freezeTime.Value;
            Sfx.Play(Sfx.Creepy);
            Notifier.Show($"<color={ColorHex}>{Lang.T("Eiskalte Geisterhände halten dich fest!", "Ice-cold ghost hands hold you tight!")}</color>", freezeTime.Value);
        }
    }
}
