using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Crew: Steht der Heiler an einer Leiche, kann er sich selbst opfern, um den Toten wiederzubeleben. Der Heiler
/// stirbt sofort (ohne Leiche); nach ein paar Sekunden steht der Geopferte wieder auf. Ein Meeting dazwischen
/// bricht die Wiederbelebung ab.
/// </summary>
public sealed class Healer : CustomRole
{
    public override string ConfigSection => "Heiler";
    public override string Name => Lang.T("Heiler", "Healer");
    public override Color Color => new Color32(120, 230, 150, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Ein Leben für ein Leben", "A life for a life");
    public override string Description => Lang.T("Erst alle Aufgaben, dann opfere\ndich an einer Leiche (F).", "Finish all tasks, then sacrifice\nyourself at a body (F).");
    public override string LongDescription => Lang.T(
        "Du bist Crew. Stehst du an einer Leiche, opferst du mit Taste F dein eigenes Leben: Du stirbst sofort (ohne Leiche), und nach wenigen Sekunden steht der Tote wieder auf - an der Stelle seiner Leiche. Beginnt vorher ein Meeting, ist die Wiederbelebung verloren. Du weißt nicht, welche Rolle du zurückholst - auch ein Impostor kann es sein.",
        "You are crew. Standing at a body, press F to sacrifice your own life: you die at once (no body), and after a few seconds the dead player rises again - at the spot of their body. If a meeting starts first, the revive is lost. You don't know which role you bring back - it could be an impostor too.");

    private const float BodyRange = 1.2f;
    private const byte ActionStart = 0, ActionRevive = 1;

    private RoleOption delay;

    // Host: ausstehende Wiederbelebungen (Ziel-Id -> Zeitpunkt)
    private readonly Dictionary<byte, float> pending = new();

    protected override void Setup()
    {
        delay = AddOption("Delay", "Wartezeit|Delay", 5f, 2f, 15f, 1f, "s");

        AddButton(new RoleButton
        {
            Label = "Opfern|Sacrifice",
            Icon = "cat",
            NeedsTarget = false,
            Cooldown = () => 1f,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer) && AllTasksDone(PlayerControl.LocalPlayer) && NearbyBody() != null,
            OnClick = _ =>
            {
                var body = NearbyBody();
                if (body) SendToAll(ActionStart, body.ParentId);
            },
        });
    }

    public override void ResetState() => pending.Clear();

    public override void OnMeetingStart() => pending.Clear(); // Wiederbelebung bricht ab

    /// <summary>Hat der Spieler alle echten Aufgaben erledigt? (Sabotagen zaehlen nicht; keine Aufgaben = ja)</summary>
    private static bool AllTasksDone(PlayerControl p)
    {
        if (!p || p.myTasks == null) return true;
        // Nur echte Crew-Aufgaben zählen. Sabotagen und die eingeblendete Rollen-Textzeile
        // (ImportantTextTask) sind keine erledigbaren Aufgaben.
        foreach (var t in p.myTasks)
            if (t && t.TryCast<NormalPlayerTask>() != null && !t.IsComplete) return false;
        return true;
    }

    private static DeadBody NearbyBody()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !IsAlive(local) || local.inVent) return null;
        Vector2 me = local.GetTruePosition();
        return Object.FindObjectsOfType<DeadBody>().Where(b => b && Vector2.Distance(me, b.TruePosition) <= BodyRange)
            .OrderBy(b => Vector2.Distance(me, b.TruePosition)).FirstOrDefault();
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (data.Length < 1) return;

        if (action == ActionStart)
        {
            if (!AmHost || !RoleRegistry.Has(sender, this) || !IsAlive(sender)) return;
            byte targetId = data[0];
            // Leiche muss (noch) existieren
            if (!Object.FindObjectsOfType<DeadBody>().Any(b => b && b.ParentId == targetId)) return;
            KillSystem.KillNoBody(sender);           // der Heiler opfert sich sofort
            pending[targetId] = Time.time + delay.Value;
            return;
        }

        if (action == ActionRevive)
        {
            // kommt vom Host: Toten wiederbeleben und seine Leiche entfernen
            if (AmongUsClient.Instance && sender && sender.OwnerId != AmongUsClient.Instance.HostId) return;
            byte targetId = data[0];
            var target = FindPlayer(targetId);
            if (!target) return;

            Vector2 pos = target.GetTruePosition();
            foreach (var body in Object.FindObjectsOfType<DeadBody>())
                if (body && body.ParentId == targetId) { pos = body.TruePosition; Object.Destroy(body.gameObject); }

            target.Revive();
            if (AmHost)
            {
                bool wasImp = target.Data != null && target.Data.Role && target.Data.Role.IsImpostor;
                target.RpcSetRole(wasImp ? RoleTypes.Impostor : RoleTypes.Crewmate, true);
                if (target.NetTransform) target.NetTransform.SnapTo(pos);
                // Falls der Tote beim Sterben eine Geisterrolle (z. B. Spukgestalt) bekommen hat: wieder entfernen.
                var ghost = RoleRegistry.GetGhostRole(target);
                if (ghost != null) RoleRegistry.RemoveRole(target, ghost);
            }
            ProtectionVisuals.ShowBurst(target, Color);
            if (target == PlayerControl.LocalPlayer) { Sfx.Play(Sfx.Protect); Notifier.Show($"<color={ColorHex}>{Lang.T("Du wurdest wiederbelebt!", "You were revived!")}</color>", 5f); }
        }
    }

    public override void Update()
    {
        if (!AmHost || pending.Count == 0) return;
        float now = Time.time;
        foreach (var pair in pending.Where(p => now >= p.Value).ToList())
        {
            pending.Remove(pair.Key);
            var target = FindPlayer(pair.Key);
            if (target && target.Data != null && !target.Data.Disconnected) SendToAll(ActionRevive, pair.Key);
        }
    }
}
