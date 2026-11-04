using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Ghosts;

/// <summary>Geisterrolle: kann die naechstgelegene Tuer wieder oeffnen (z. B. nach einer Tuer-Sabotage).</summary>
public sealed class DoorSpirit : CustomRole
{
    public override string ConfigSection => "Tuergeist";
    public override string Name => Lang.T("Türgeist", "Door Spirit");
    public override Color Color => new Color32(150, 170, 200, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override bool IsGhostRole => true;
    public override bool CanHaunt(PlayerControl dead) => !IsImpostor(dead) && !IsNeutral(dead);
    public override string IntroText => Lang.T("Mach die Tür auf", "Open the door");
    public override string Description => Lang.T("Geist: nächste Tür auf/zu (F).", "Ghost: open/close nearest door (F).");
    public override string LongDescription => Lang.T(
        "Geisterrolle für tote Crewmates. Schweb an eine Tür und drück F: Die nächste Tür geht auf, wenn sie zu ist - oder zu, wenn sie offen ist. Danach eine kurze Abklingzeit.",
        "Ghost role for dead crewmates. Float to a door and press F: the nearest door opens if closed - or closes if open. Then a short cooldown.");

    private const byte ActionToggle = 0;
    private const float DoorCooldown = 12f; // fest 12 s

    // Türen, die der Türgeist ZUHALTEN will (auf allen Clients). Das ist die Wahrheit fürs Umschalten.
    private readonly HashSet<byte> closed = new();
    // Wann wir eine Tür zuletzt nachgeschlossen haben (gegen Flackern, falls das Spiel sie wieder aufmacht).
    private readonly Dictionary<byte, float> lastReclose = new();

    protected override void Setup()
    {
        AddButton(new RoleButton
        {
            Label = "Tür auf/zu|Toggle door",
            Icon = "door",
            NeedsTarget = false,
            UsableWhenDead = true,
            Visible = () => !IsAlive(PlayerControl.LocalPlayer),
            Cooldown = () => DoorCooldown,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => NearestDoor() != null, // aktiv, wenn eine Tür in der Nähe ist
            OnClick = _ =>
            {
                var door = NearestDoor();
                if (!door) return;
                // Umschalten nach EIGENER Absicht, nicht nach door.IsOpen (das flackert sonst):
                // steht diese Tür auf unserer Zuhalte-Liste -> aufmachen, sonst zumachen.
                bool wantOpen = closed.Contains((byte)door.Id);
                SendToAll(ActionToggle, (byte)door.Id, (byte)(wantOpen ? 1 : 0)); // 1 = öffnen, 0 = schließen
            },
        });
    }

    private const float Reach = 2.2f;      // nur die Tür, in der man wirklich steht
    private const float RecloseGap = 0.6f; // nicht öfter als alle 0,6 s nachschließen (kein Flackern)

    /// <summary>Nächste Tür in Reichweite (egal ob offen oder zu). null, wenn keine nah genug ist.</summary>
    private static OpenableDoor NearestDoor()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !ShipStatus.Instance || ShipStatus.Instance.AllDoors == null) return null;
        Vector2 me = local.GetTruePosition();
        return ShipStatus.Instance.AllDoors.ToArray()
            .Where(d => d && Vector2.Distance(me, d.transform.position) <= Reach) // nur direkt neben einer Tür
            .OrderBy(d => Vector2.Distance(me, d.transform.position))
            .FirstOrDefault();
    }

    public override void ResetState() { closed.Clear(); lastReclose.Clear(); }
    public override void OnMeetingStart() { ReopenAll(); closed.Clear(); lastReclose.Clear(); }

    /// <summary>Alle zugehaltenen Türen vor dem Meeting wieder aufmachen (sonst blieben sie zu).</summary>
    private void ReopenAll()
    {
        if (!ShipStatus.Instance || ShipStatus.Instance.AllDoors == null) return;
        foreach (var d in ShipStatus.Instance.AllDoors)
            if (d && closed.Contains((byte)d.Id)) d.SetDoorway(true);
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionToggle || data.Length < 2 || !RoleRegistry.Has(sender, this) || !ShipStatus.Instance) return;
        bool open = data[1] == 1;
        byte id = data[0];
        if (open) { closed.Remove(id); lastReclose.Remove(id); } else { closed.Add(id); lastReclose[id] = Time.time; }
        // genau DIESE eine Tür schalten
        foreach (var d in ShipStatus.Instance.AllDoors)
            if (d && (byte)d.Id == id) { d.SetDoorway(open); break; }
        if (sender == PlayerControl.LocalPlayer) Sfx.Play(Sfx.TaskComplete, 0.6f);
    }

    /// <summary>
    /// Zugehaltene Türen zu lassen, falls das Spiel sie wieder öffnet - aber nur gedrosselt (kein Flackern)
    /// und nur die tatsächlich betroffene Tür, nie alle.
    /// </summary>
    public override void Update()
    {
        if (closed.Count == 0 || !ShipStatus.Instance || ShipStatus.Instance.AllDoors == null) return;
        float t = Time.time;
        foreach (var d in ShipStatus.Instance.AllDoors)
        {
            if (!d || !closed.Contains((byte)d.Id) || !d.IsOpen) continue; // nur wenn das Spiel sie geöffnet hat
            if (lastReclose.TryGetValue((byte)d.Id, out var last) && t - last < RecloseGap) continue; // Drossel
            d.SetDoorway(false);
            lastReclose[(byte)d.Id] = t;
        }
    }
}
