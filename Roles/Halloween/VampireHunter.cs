using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Halloween;

/// <summary>Crew: jagt Monster mit Holzpfloecken. Trifft er Vampir, Hexe oder Zombie, stirbt das Monster - sonst ist nur der Pflock weg.</summary>
public sealed class VampireHunter : CustomRole
{
    public override string ConfigSection => "Vampirjäger"; // fest, damit Einstellungen erhalten bleiben
    public override string Name =>
        Lang.T("Vampirjäger", "Vampire Hunter");
    public override Color Color => new Color32(205, 170, 120, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText =>
        Lang.T("Jage die Monster der Nacht", "Hunt the monsters of the night");
    public override string Description =>
        Lang.T("Pfähle Monster (F):\nVampir, Hexe, Zombie.", "Stake monsters (F):\nVampire, Witch, Zombie.");
    public override string LongDescription =>
        Lang.T("Halloween-Crew. Du hast nur wenige Pflöcke (Taste F).\n\nTriffst du Vampir, Hexe oder Zombie, stirbt das Monster. Bei allen anderen ist nur der Pflock weg - du stirbst nicht.", "Halloween crew. You only have a few stakes (key F).\n\nHit a Vampire, Witch or Zombie and the monster dies. Anyone else: only the stake is gone - you don't die.");

    private const byte ActionStake = 0, ActionResult = 1;

    private RoleOption stakes, showResult;

    private int stakesLeft;
    private readonly Dictionary<byte, int> stakesUsed = new(); // beim Host

    protected override void Setup()
    {
        stakes = AddOption("Stakes", "Pflöcke pro Spiel|Stakes per game", 2f, 1f, 5f, 1f);
        showResult = AddToggle("ShowResult", "Erfährt, ob es ein Monster war|Learns if it was a monster", true);

        AddButton(new RoleButton
        {
            Label = "Pflock|Stake",
            Icon = "stake",
            Cooldown = () => 1f,      // keine echte Abklingzeit - begrenzt sind nur die Pfloecke
            StartCooldown = 1f,
            Hotkey = KeyCode.F,
            CanUse = () => stakesLeft > 0,
            UsesLeft = () => stakesLeft,
            OnClick = target =>
            {
                stakesLeft--;
                SendToHost(ActionStake, target.PlayerId);
            },
        });
    }

    public override void ResetState() => stakesUsed.Clear();

    public override void OnAssigned(PlayerControl player)
    {
        if (player == PlayerControl.LocalPlayer) stakesLeft = stakes.Int;
    }

    private static bool IsMonster(PlayerControl p) => RoleRegistry.GetRole(p) is Vampire or Witch or Zombie;

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionResult && data.Length >= 2)
        {
            if (sender && data[0] == PlayerControl.LocalPlayer.PlayerId && showResult.Bool)
            {
                if (data[1] != 1) Sfx.Play(Sfx.Fail);
                Notifier.Show(data[1] == 1 ? $"<color={ColorHex}>{Lang.T("Getroffen - das war ein Monster!", "Hit - that was a monster!")}</color>" : Lang.T("Kein Monster - dein Pflock ist verloren.", "No monster - your stake is lost."));
            }
            return;
        }

        if (action != ActionStake || data.Length < 1 || !AmHost) return;
        if (!RoleRegistry.Has(sender, this) || !IsAlive(sender) || InMeeting) return;

        stakesUsed.TryGetValue(sender.PlayerId, out int used);
        if (used >= stakes.Int) return;
        stakesUsed[sender.PlayerId] = used + 1;

        var target = FindPlayer(data[0]);
        if (!IsAlive(target)) return;

        bool monster = IsMonster(target);
        if (monster) KillSystem.TryKill(sender, target);
        SendToAll(ActionResult, sender.PlayerId, (byte)(monster ? 1 : 0));
    }
}
