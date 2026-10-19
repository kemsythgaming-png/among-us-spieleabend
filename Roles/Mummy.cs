using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Neutral: wickelt einen Spieler ein. Er bekommt die echte Camouflage-Optik (grau, ohne Namen - genau wie beim
/// Camouflager) auf seinem eigenen Körper, kann sich nicht mehr bewegen und nichts melden. So steht er wie eine
/// leblose Tarn-Figur herum.
/// </summary>
public sealed class Mummy : CustomRole
{
    public override string ConfigSection => "Mumie";
    public override string Name => Lang.T("Mumie", "Mummy");
    public override Color Color => new Color32(220, 210, 180, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override string IntroText => Lang.T("Wickel sie ein", "Wrap them up");
    public override string Description => Lang.T("Wickle einen Spieler ein (F):\ngetarnt + bewegungslos.", "Wrap a player (F):\ncamouflaged + frozen.");
    public override string LongDescription => Lang.T(
        "Neutral: Mit dem Knopf wickelst du einen Spieler in der Nähe ein. Er sieht dann aus wie beim Camouflager (grau, ohne Namen), kann sich nicht mehr bewegen und nichts melden oder abstimmen - er steht wie eine leblose Tarn-Figur herum.",
        "Neutral: the button wraps a nearby player. They look like the Camouflager's effect (gray, nameless), can't move and can't report or vote - they stand around like a lifeless camouflage figure.");

    internal static Mummy Instance { get; private set; }

    private const byte ActionWrap = 0;
    private RoleOption cooldown;
    private readonly Dictionary<byte, PlayerOutfitType> wrapped = new(); // Id -> gespeicherte Outfit-Art

    protected override void Setup()
    {
        Instance = this;
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 30f, 10f, 90f, 2.5f, "s");

        AddButton(new RoleButton
        {
            Label = "Einwickeln|Wrap",
            Icon = "tp",
            NeedsTarget = true,
            SharesKillCooldown = true,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            OnClick = target => { if (target) SendToAll(ActionWrap, target.PlayerId); },
        });
    }

    public override void ResetState() => RestoreAll();

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionWrap || data.Length < 1 || !RoleRegistry.Has(sender, this)) return;
        var target = FindPlayer(data[0]);
        if (!IsAlive(target) || target.Data == null) return;

        if (!wrapped.ContainsKey(data[0])) wrapped[data[0]] = target.CurrentOutfitType;
        target.RawSetOutfit(GrayOutfit(), PlayerOutfitType.MushroomMixup);

        var local = PlayerControl.LocalPlayer;
        if (local && local.PlayerId == data[0])
        {
            Sfx.Play(Sfx.Creepy, 0.6f);
            Notifier.Show($"<color={ColorHex}>{Lang.T("Du bist eingewickelt - keine Bewegung, kein Melden!", "You are wrapped up - no moving, no reporting!")}</color>", 6f);
        }
    }

    public override void Update()
    {
        // Tarnung erzwingen (falls das Spiel das Outfit zwischendurch zurücksetzt) + Namen der Eingewickelten ausblenden
        foreach (var id in wrapped.Keys.ToArray())
        {
            var p = FindPlayer(id);
            if (!IsAlive(p) || p.Data == null) { wrapped.Remove(id); continue; }
            if (p.CurrentOutfitType != PlayerOutfitType.MushroomMixup)
                p.RawSetOutfit(GrayOutfit(), PlayerOutfitType.MushroomMixup);
            if (p.cosmetics && p.cosmetics.nameText && p.cosmetics.nameText.text.Length > 0)
                p.cosmetics.nameText.text = ""; // namenlos (auch wenn das Spiel den Namen neu setzt)
        }

        // Die Mumie selbst hat keinen Kill- und keinen Vent-Knopf
        var local = PlayerControl.LocalPlayer;
        if (IsLocalRole && IsAlive(local))
        {
            var hud = HudManager.InstanceExists ? HudManager.Instance : null;
            if (hud)
            {
                if (hud.KillButton) hud.KillButton.ToggleVisible(false);
                if (hud.ImpostorVentButton) hud.ImpostorVentButton.ToggleVisible(false);
            }
        }

        // Host: Sieg, wenn alle anderen eingewickelt (oder tot) sind
        if (AmHost && RoleRegistry.HasAssignments && !InMeeting)
        {
            var contenders = PlayerControl.AllPlayerControls.ToArray()
                .Where(p => IsAlive(p) && !RoleRegistry.Has(p, this) && !wrapped.ContainsKey(p.PlayerId)).ToList();
            var mummies = Players.Where(IsAlive).ToList();
            if (contenders.Count == 0 && mummies.Count > 0) CustomWin.Trigger(this, mummies);
        }
    }

    public override float SpeedMultiplier(PlayerControl local) =>
        IsWrapped(local) && IsAlive(local) && !InMeeting ? 0f : 1f;

    internal bool IsWrapped(PlayerControl p) => p && wrapped.ContainsKey(p.PlayerId);

    private void RestoreAll()
    {
        foreach (var pair in wrapped)
        {
            var p = FindPlayer(pair.Key);
            if (p) RestoreOutfit(p, pair.Value);
        }
        wrapped.Clear();
    }

    private static void RestoreOutfit(PlayerControl p, PlayerOutfitType type)
    {
        if (!p || p.Data == null) return;
        NetworkedPlayerInfo.PlayerOutfit outfit = null;
        if (p.Data.Outfits.ContainsKey(type)) outfit = p.Data.Outfits[type];
        if (outfit == null) { outfit = p.Data.DefaultOutfit; type = PlayerOutfitType.Default; }
        p.RawSetOutfit(outfit, type);
    }

    private static NetworkedPlayerInfo.PlayerOutfit GrayOutfit() => new()
    {
        ColorId = 15,
        HatId = "hat_NoHat",
        SkinId = "skin_None",
        VisorId = "visor_EmptyVisor",
        PetId = "pet_EmptyPet",
        NamePlateId = "nameplate_NoPlate",
        PlayerName = "",
    };
}

/// <summary>Eingewickelte Spieler können kein Meeting einberufen / keine Leiche melden.</summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdReportDeadBody))]
internal static class MummyNoReportPatch
{
    public static bool Prefix(PlayerControl __instance)
    {
        var m = Mummy.Instance;
        if (m == null || !m.IsWrapped(__instance)) return true;
        if (__instance == PlayerControl.LocalPlayer)
            Notifier.Show($"<color={m.ColorHex}>{Lang.T("Eingewickelt - du kannst nichts melden.", "Wrapped up - you can't report.")}</color>", 2f);
        return false;
    }
}

/// <summary>Im Meeting gelten eingewickelte Spieler als tot (durchgestrichen, keine Stimme).</summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
internal static class MummyMeetingDeadPatch
{
    public static void Postfix(MeetingHud __instance)
    {
        var m = Mummy.Instance;
        if (m == null) return;
        foreach (var state in __instance.playerStates)
        {
            if (!state) continue;
            var p = CustomRole.FindPlayer((byte)state.PlayerId);
            if (m.IsWrapped(p)) state.SetDead(true);
        }
    }
}

/// <summary>Eingewickelte Spieler können nicht abstimmen.</summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CmdCastVote))]
internal static class MummyVotePatch
{
    public static bool Prefix(byte playerId)
    {
        var m = Mummy.Instance;
        return m == null || !m.IsWrapped(CustomRole.FindPlayer(playerId));
    }
}
