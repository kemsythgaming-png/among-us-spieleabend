using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Halloween;

/// <summary>
/// Neutral: infiziert andere, die dadurch selbst zu Zombies werden.
/// Die Zombies gewinnen gemeinsam, wenn alle Lebenden ausser den Impostorn Zombies sind.
/// </summary>
public sealed class Zombie : CustomRole
{
    public override string Name =>
        Lang.T("Zombie", "Zombie");
    public override Color Color => new Color32(110, 170, 60, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override string IntroText =>
        Lang.T("Gehirne ... Gehirne ...", "Brains ... brains ...");
    public override string Description =>
        Lang.T("Infiziere alle (F) -\nsie werden auch Zombies.", "Infect everyone (F) -\nthey become zombies too.");
    public override string LongDescription =>
        Lang.T("Halloween-Rolle, neutral. Mit Taste F infizierst du Spieler - sie verlieren ihre Rolle und werden selbst Zombies.\n\nIhr gewinnt, wenn alle Lebenden Zombies sind. Je nach Einstellung sind Impostor immun.", "Halloween role, neutral. Press F to infect players - they lose their role and become zombies too.\n\nYou win when all living players are zombies. Depending on the settings impostors are immune.");

    private const byte ActionInfect = 0;

    private RoleOption cooldown, impostorsInfectable;

    internal static Zombie Instance { get; private set; }

    protected override void Setup()
    {
        Instance = this;
        cooldown = AddOption("Cooldown", "Infizier-Abklingzeit|Infect cooldown", 25f, 10f, 60f, 2.5f, "s");
        impostorsInfectable = AddToggle("ImpostorsInfectable", "Impostor infizierbar|Impostors can be infected", true);

        AddButton(new RoleButton
        {
            Label = "Infizieren|Infect",
            Icon = "infect",
            Cooldown = () => cooldown.Value,
            Hotkey = KeyCode.F,
            TargetFilter = p => RoleRegistry.GetRole(p) != this,
            OnClick = target =>
            {
                Sfx.Play(Sfx.Creepy);
                SendToHost(ActionInfect, target.PlayerId);
            },
        });
    }

    public override void OnAssigned(PlayerControl player)
    {
        // Neu infiziert (Rollenwechsel waehrend der Runde, nicht die Verteilung am Anfang)
        if (player == PlayerControl.LocalPlayer && RoleRegistry.IsRoleChange)
        {
            Notifier.Show($"<color={ColorHex}>{Lang.T("Du wurdest infiziert - du bist jetzt ein Zombie!", "You got infected - you are a zombie now!")}</color>");
            Sfx.Play(Sfx.Creepy);
        }
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionInfect || data.Length < 1 || !AmHost) return;
        if (RoleRegistry.GetRole(sender) != this || !IsAlive(sender) || InMeeting) return;

        var target = FindPlayer(data[0]);
        if (!IsAlive(target) || RoleRegistry.GetRole(target) == this) return;
        if (IsImpostor(target) && !impostorsInfectable.Bool) return; // Impostor immun (Einstellung)
        if (target.protectedByGuardianId >= 0 || KillSystem.Intercept(sender, target)) return;

        // Ein infizierter Impostor verliert seine Impostor-Rolle (kein Kill-Button mehr, zaehlt nicht mehr als Impostor)
        if (FreeplayTest.IsFakeImpostor(target)) FreeplayTest.RemoveFakeImpostor(target);
        else if (IsImpostor(target)) target.RpcSetRole(RoleTypes.Crewmate, true);

        RoleRegistry.ChangeRole(target, this);
    }

    public override void Update()
    {
        if (!AmHost || InMeeting || CustomWin.Active) return;

        var alive = PlayerControl.AllPlayerControls.ToArray().Where(IsAlive).ToList();
        var zombies = alive.Where(p => RoleRegistry.GetRole(p) == this).ToList();
        if (zombies.Count == 0) return;

        // Gewonnen, wenn alle Lebenden Zombies sind (bei immunen Impostorn: alle ausser den Impostorn)
        var others = alive.Where(p => !zombies.Contains(p));
        bool won = impostorsInfectable.Bool ? !others.Any() : others.All(IsImpostor);
        if (won)
            CustomWin.Trigger(this, PlayerControl.AllPlayerControls.ToArray().Where(p => RoleRegistry.GetRole(p) == this));
    }

    public override string GetNameMark(PlayerControl viewer, PlayerControl target) =>
        RoleRegistry.GetRole(viewer) == this && RoleRegistry.GetRole(target) == this && viewer != target
            ? $"<color={ColorHex}>[Zombie]</color>"
            : null;

    /// <summary>Leben Zombies und noch andere Nicht-Zombies? Dann darf das Spiel noch nicht enden (ausser durch Aufgaben/Impostor).</summary>
    internal bool BlocksCrewWin()
    {
        if (!impostorsInfectable.Bool || !RoleRegistry.HasAssignments) return false;
        var alive = PlayerControl.AllPlayerControls.ToArray().Where(IsAlive).ToList();
        bool zombiesAlive = alive.Any(p => RoleRegistry.GetRole(p) == this);
        bool impostorsAlive = alive.Any(IsImpostor);
        bool othersAlive = alive.Any(p => RoleRegistry.GetRole(p) != this);
        bool tasksDone = GameData.Instance && GameData.Instance.TotalTasks > 0 && GameData.Instance.CompletedTasks >= GameData.Instance.TotalTasks;
        // Kein Impostor mehr, aber Zombies und andere leben noch: Das Spiel wuerde sonst sofort als Crew-Sieg enden.
        return zombiesAlive && othersAlive && !impostorsAlive && !tasksDone;
    }
}

/// <summary>
/// Wenn alle Impostor zu Zombies wurden, wuerde das Spiel sofort "Crew gewinnt" melden.
/// Solange noch Zombies und andere leben, geht das Spiel weiter (Crew muss die Zombies loswerden oder Aufgaben schaffen).
/// </summary>
[HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
internal static class ZombieEndCriteriaPatch
{
    public static bool Prefix() => !(Zombie.Instance != null && Zombie.Instance.BlocksCrewWin());
}
