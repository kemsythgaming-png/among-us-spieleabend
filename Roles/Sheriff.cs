using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>Crewmate mit Schuss: Impostor getroffen -> Impostor stirbt, sonst stirbt der Sheriff selbst.</summary>
public sealed class Sheriff : CustomRole
{
    public override string Name =>
        Lang.T("Sheriff", "Sheriff");
    public override Color Color => new Color32(248, 205, 70, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText =>
        Lang.T("Erschieße die Impostor", "Shoot the impostors");
    public override string Description =>
        Lang.T("Erschieße Impostor (Q).\nFalsch geschossen = du stirbst!", "Shoot impostors (Q).\nWrong target = you die!");
    public override string LongDescription =>
        Lang.T("Du bist Crew mit einer Waffe (Taste Q).\n\nTriffst du einen Impostor, stirbt er. Triffst du einen Crewmate, stirbst du selbst - schieß also nur, wenn du sicher bist.\n\nMedic-Schild und Guardian Angel schützen dein Ziel.", "You are crew with a gun (key Q).\n\nShoot an impostor and they die. Shoot a crewmate and YOU die - only shoot when you are sure.\n\nA Medic shield or Guardian Angel protects your target.");
    public override int DefaultCount => 1;

    private const byte ActionShoot = 0;

    private RoleOption cooldown;
    private RoleOption misfireKillsTarget;
    private RoleOption canKillNeutrals;

    protected override void Setup()
    {
        cooldown = AddOption("KillCooldown", "Abklingzeit|Cooldown", 30f, 10f, 60f, 2.5f, "s");
        misfireKillsTarget = AddToggle("MisfireKillsTarget", "Fehlschuss tötet auch Ziel|Misfire also kills target", false);
        canKillNeutrals = AddToggle("CanKillNeutrals", "Darf Neutrale erschießen|Can shoot neutrals", false);

        AddButton(new RoleButton
        {
            Label = "Schießen|Shoot",
            Icon = "shoot",
            Cooldown = () => cooldown.Value,
            Hotkey = KeyCode.Q,
            OnClick = target => SendToHost(ActionShoot, target.PlayerId),
        });
    }

    // Laeuft nur beim Host: er entscheidet, wer stirbt.
    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionShoot || data.Length < 1 || !AmongUsClient.Instance.AmHost) return;
        if (RoleRegistry.GetRole(sender) != this || !IsAlive(sender)) return;
        if (MeetingHud.Instance || ExileController.Instance) return;

        var target = FindPlayer(data[0]);
        if (!IsAlive(target) || target == sender) return;

        bool neutralHit = canKillNeutrals.Bool && IsNeutral(target);
        if (IsImpostor(target) || neutralHit)
        {
            KillSystem.TryKill(sender, target); // Medic-Schild, Guardian Angel usw. gelten
            return;
        }

        if (misfireKillsTarget.Bool) KillSystem.TryKill(sender, target);
        KillSystem.ForceKill(sender, sender);
    }

    // ---------- Spielregel: Sheriff-Chance nach allen Aufgaben ----------

    /// <summary>Spieler, fuer die schon gewuerfelt wurde (beim Host, einmal pro Runde).</summary>
    private readonly System.Collections.Generic.HashSet<byte> rolledForTasks = new();

    public override void ResetState() => rolledForTasks.Clear();

    public override void OnAssigned(PlayerControl player)
    {
        if (player == PlayerControl.LocalPlayer && RoleRegistry.IsRoleChange)
        {
            Notifier.Show($"<color={ColorHex}>{Lang.T("Alle Aufgaben erledigt - du bist jetzt Sheriff!", "All tasks done - you are the Sheriff now!")}</color>", 6f);
            Sfx.Play(Sfx.TaskComplete);
        }
    }

    // Beim Host: hat ein normaler Crewmate alle Aufgaben erledigt, wird mit der eingestellten Chance ein Sheriff draus
    public override void Update()
    {
        int chance = GeneralOptions.TaskSheriffChance.Int;
        if (!AmHost || chance <= 0 || InMeeting || FreeplayTest.IsFreeplay) return;

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!IsAlive(p) || rolledForTasks.Contains(p.PlayerId)) continue;
            if (!p.Data.Role || p.Data.Role.Role != AmongUs.GameOptions.RoleTypes.Crewmate || RoleRegistry.GetRole(p) != null) continue;
            var tasks = p.Data.Tasks;
            if (tasks == null || tasks.Count == 0 || tasks.ToArray().Any(t => !t.Complete)) continue;

            rolledForTasks.Add(p.PlayerId);
            if (Random.Range(0, 100) < chance) RoleRegistry.ChangeRole(p, this);
        }
    }
}
