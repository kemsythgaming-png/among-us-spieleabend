using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Ghosts;

/// <summary>Geisterrolle: kann genau die Tür, an der man steht, auf/zu machen. Zugehaltene Türen bleiben zu (per Patch),
/// bis der Türgeist sie wieder öffnet oder ein Meeting kommt - kein Flackern, und es trifft immer nur EINE Tür.</summary>
public sealed class DoorSpirit : CustomRole
{
    public override string ConfigSection => "Tuergeist";
    public override string Name => Lang.T("Türgeist", "Door Spirit");
    public override Color Color => new Color32(150, 170, 200, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override bool IsGhostRole => true;
    public override bool CanHaunt(PlayerControl dead) => !IsImpostor(dead) && !IsNeutral(dead);
    public override string IntroText => Lang.T("Mach die Tür auf", "Open the door");
    public override string Description => Lang.T("Geist: die Tür bei dir auf/zu (F).", "Ghost: the door next to you open/close (F).");
    public override string LongDescription => Lang.T(
        "Geisterrolle für tote Crewmates. Stell dich in eine Tür und drück F: Genau diese eine Tür geht zu. Sie bleibt 7-10 Sekunden ruhig zu (kein Flackern) und geht dann von selbst wieder auf - oder früher, wenn du sie erneut anklickst. Beim Meeting gehen alle wieder auf. Danach eine kurze Abklingzeit.",
        "Ghost role for dead crewmates. Stand in a door and press F: exactly that one door closes. It stays quietly closed for 7-10 seconds (no flicker) and then reopens on its own - or earlier if you click it again. At the meeting all doors reopen. Then a short cooldown.");

    private const byte ActionToggle = 0;
    private const float DoorCooldown = 22f; // fest 22 s
    private const float Reach = 2.2f;       // nur die Tür, in der man wirklich steht

    // Index (in ShipStatus.AllDoors) der zugehaltenen Türen -> Zeitpunkt, zu dem sie von selbst wieder aufgehen (nur Host wertet aus).
    internal static readonly Dictionary<int, float> Held = new();
    // true, während der Türgeist selbst eine Tür legitim öffnet (sonst blockiert der Patch jedes Öffnen).
    internal static bool AllowOpen;

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
            CanUse = () => NearestDoorIndex() >= 0,
            OnClick = _ =>
            {
                int idx = NearestDoorIndex();
                if (idx < 0 || !ShipStatus.Instance) return;
                var all = ShipStatus.Instance.AllDoors;
                if (all == null || idx >= all.Count || !all[idx]) return;
                // Nach dem TATSÄCHLICHEN Türzustand schalten: offene Tür -> zu, geschlossene Tür -> auf
                // (so lässt sich auch eine vom Impostor sabotierte/geschlossene Tür wieder öffnen).
                bool wantOpen = !all[idx].IsOpen;
                SendToAll(ActionToggle, (byte)idx, (byte)(wantOpen ? 1 : 0));
            },
        });
    }

    /// <summary>Index der nächsten Tür in Reichweite, oder -1.</summary>
    private static int NearestDoorIndex()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !ShipStatus.Instance || ShipStatus.Instance.AllDoors == null) return -1;
        Vector2 me = local.GetTruePosition();
        var all = ShipStatus.Instance.AllDoors;
        int best = -1; float bestD = Reach;
        for (int i = 0; i < all.Count; i++)
        {
            var d = all[i];
            if (!d) continue;
            float dist = Vector2.Distance(me, d.transform.position);
            if (dist <= bestD) { bestD = dist; best = i; }
        }
        return best;
    }

    public override void ResetState() => ReopenAllAndClear();
    public override void OnMeetingStart() => ReopenAllAndClear();

    /// <summary>Host: zugehaltene Türen nach 7-10 s wieder öffnen (für alle, via RPC).</summary>
    public override void Update()
    {
        if (!AmHost || Held.Count == 0 || InMeeting) return;
        float now = Time.time;
        foreach (var idx in Held.Where(kv => now >= kv.Value).Select(kv => kv.Key).ToList())
            SendToAll(ActionToggle, (byte)idx, 1); // aufmachen
    }

    private void ReopenAllAndClear()
    {
        if (ShipStatus.Instance && ShipStatus.Instance.AllDoors != null)
        {
            var all = ShipStatus.Instance.AllDoors;
            AllowOpen = true;
            foreach (var i in Held.Keys) if (i >= 0 && i < all.Count && all[i]) all[i].SetDoorway(true);
            AllowOpen = false;
        }
        Held.Clear();
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionToggle || data.Length < 2 || !RoleRegistry.Has(sender, this) || !ShipStatus.Instance) return;
        int idx = data[0];
        bool open = data[1] == 1;
        var all = ShipStatus.Instance.AllDoors;
        if (all == null || idx < 0 || idx >= all.Count || !all[idx]) return;

        if (open)
        {
            Held.Remove(idx);
            AllowOpen = true;
            all[idx].SetDoorway(true);
            AllowOpen = false;
        }
        else
        {
            Held[idx] = Time.time + Random.Range(7f, 10f); // nach 7-10 s automatisch wieder auf
            all[idx].SetDoorway(false);
        }
        if (sender == PlayerControl.LocalPlayer) Sfx.Play(Sfx.TaskComplete, 0.6f);
    }

    /// <summary>Hält der Türgeist diese Tür gerade zu?</summary>
    internal static bool IsHeld(OpenableDoor door)
    {
        if (Held.Count == 0 || !door || !ShipStatus.Instance || ShipStatus.Instance.AllDoors == null) return false;
        var all = ShipStatus.Instance.AllDoors;
        for (int i = 0; i < all.Count; i++)
            if (all[i] == door) return Held.ContainsKey(i);
        return false;
    }
}

/// <summary>Zugehaltene Türen lassen sich vom Spiel nicht wieder öffnen (nur der Türgeist selbst macht sie auf).
/// So bleibt die Tür ruhig zu, statt nach ihrem Timer kurz auf- und sofort wieder zuzuspringen.</summary>
[HarmonyPatch(typeof(PlainDoor), nameof(PlainDoor.SetDoorway))]
internal static class DoorSpiritHoldPatch
{
    public static bool Prefix(PlainDoor __instance, bool open)
    {
        // Schließen immer erlauben; Öffnen nur, wenn der Türgeist es selbst auslöst.
        if (open && !DoorSpirit.AllowOpen && DoorSpirit.IsHeld(__instance)) return false;
        return true;
    }
}
