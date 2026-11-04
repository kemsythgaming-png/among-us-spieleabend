using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Neutral (Seelensammler): kann nicht selbst toeten. Er sammelt Leichen ein (sie verschwinden) und muss sie an einen
/// Impostor "verfuettern" - dafuer geht er zu einem Spieler und fuettert; ist es ein Impostor, zaehlt es. Genug
/// gefuetterte Leichen = Sieg. Bei jedem Tod bekommt er eine kurze, anonyme Info.
/// </summary>
public sealed class SoulCollector : CustomRole
{
    public override string ConfigSection => "Seelensammler";
    public override string Name => Lang.T("Seelensammler", "Soul Collector");
    public override Color Color => new Color32(90, 150, 120, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override string IntroText => Lang.T("Füttere die Impostor", "Feed the impostors");
    public override string Description => Lang.T("Sammle Leichen (F), füttere\nsie einem Impostor (G).", "Collect bodies (F), feed them\nto an impostor (G).");
    public override string LongDescription => Lang.T(
        "Du bist neutral und kannst nicht selbst töten. Mit Taste F sammelst du eine Leiche ein (sie verschwindet). Mit Taste G fütterst du sie dem Spieler neben dir - ist es ein Impostor, zählt es; sonst nicht. Fütterst du genug Leichen an Impostor, gewinnst du. Bei jedem Tod bekommst du eine kurze, anonyme Info.",
        "You are neutral and can't kill. Press F to collect a body (it disappears). Press G to feed it to the player next to you - if it's an impostor it counts, otherwise not. Feed enough bodies to impostors and you win. On every death you get a short, anonymous hint.");

    private const byte ActionCollect = 0, ActionFeed = 1, ActionFed = 2;
    private const float Range = 1.2f;

    private RoleOption toWin;
    private readonly HashSet<byte> carrying = new();  // Host: Sammler, die gerade eine Leiche tragen
    private readonly Dictionary<byte, int> fed = new();
    private bool myCarrying;

    protected override void Setup()
    {
        toWin = AddOption("ToWin", "Fütterungen zum Sieg|Feeds to win", 2f, 1f, 6f, 1f);

        AddButton(new RoleButton
        {
            Label = "Einsammeln|Collect",
            Icon = "eat",
            NeedsTarget = false,
            Cooldown = () => 5f,
            StartCooldown = 5f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && !myCarrying && NearbyBody() != null,
            OnClick = _ => { var b = NearbyBody(); if (b) SendToAll(ActionCollect, b.ParentId); },
        });

        AddButton(new RoleButton
        {
            Label = "Füttern|Feed",
            Icon = "bite",
            NeedsTarget = false,
            Cooldown = () => 3f,
            StartCooldown = 5f,
            Hotkey = KeyCode.G,
            Visible = () => myCarrying,
            CanUse = () => !InMeeting && myCarrying && NearbyPlayer() != null,
            OnClick = _ => { var p = NearbyPlayer(); if (p) SendToHost(ActionFeed, p.PlayerId); },
        });
    }

    public override void ResetState() { carrying.Clear(); fed.Clear(); myCarrying = false; }

    private static DeadBody NearbyBody()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !IsAlive(local) || local.inVent) return null;
        Vector2 me = local.GetTruePosition();
        return Object.FindObjectsOfType<DeadBody>().Where(b => b && Vector2.Distance(me, b.TruePosition) <= Range)
            .OrderBy(b => Vector2.Distance(me, b.TruePosition)).FirstOrDefault();
    }

    private static PlayerControl NearbyPlayer()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !IsAlive(local)) return null;
        Vector2 me = local.GetTruePosition();
        return PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p && p != local && IsAlive(p) && !p.inVent && Vector2.Distance(me, p.GetTruePosition()) <= Range)
            .OrderBy(p => Vector2.Distance(me, p.GetTruePosition())).FirstOrDefault();
    }

    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        if (cause == DeathCause.Exile) return;
        if (IsLocalRole && IsAlive(PlayerControl.LocalPlayer))
            Notifier.Show($"<color={ColorHex}>{Lang.T("Du spürst einen Tod ...", "You sense a death ...")}</color>", 2.5f);
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionCollect && data.Length >= 1 && RoleRegistry.Has(sender, this))
        {
            foreach (var body in Object.FindObjectsOfType<DeadBody>())
                if (body && body.ParentId == data[0]) Object.Destroy(body.gameObject);
            if (AmHost) carrying.Add(sender.PlayerId);
            if (sender == PlayerControl.LocalPlayer) { myCarrying = true; Sfx.Play(Sfx.TaskComplete, 0.6f); }
        }
        else if (action == ActionFeed && AmHost && data.Length >= 1 && RoleRegistry.Has(sender, this))
        {
            if (!carrying.Contains(sender.PlayerId)) return;
            var target = FindPlayer(data[0]);
            bool success = IsAlive(target) && IsImpostor(target);
            if (success)
            {
                carrying.Remove(sender.PlayerId);
                fed.TryGetValue(sender.PlayerId, out int n);
                fed[sender.PlayerId] = ++n;
                SendToAll(ActionFed, sender.PlayerId, (byte)Mathf.Min(253, n));
                if (n >= Mathf.RoundToInt(toWin.Value)) CustomWin.Trigger(this, new[] { sender });
            }
            else SendToAll(ActionFed, sender.PlayerId, 254);
        }
        else if (action == ActionFed && data.Length >= 2)
        {
            if (FindPlayer(data[0]) != PlayerControl.LocalPlayer) return;
            if (data[1] == 254) Notifier.Show($"<color=#9AA0AE>{Lang.T("Kein Impostor - daneben.", "Not an impostor - no luck.")}</color>", 3f);
            else { myCarrying = false; Notifier.Show($"<color={ColorHex}>{Lang.T("Gefüttert!", "Fed!")} {data[1]}/{Mathf.RoundToInt(toWin.Value)}</color>", 3f); }
        }
    }
}
