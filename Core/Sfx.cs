using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Geraeusche fuer Rollen - es werden nur Original-Sounds des Spiels verwendet, damit alles passt.
/// Abgespielt wird immer nur lokal (beim Spieler, der den Sound hoeren soll).
/// </summary>
public static class Sfx
{
    public static void Play(AudioClip clip, float volume = 1f)
    {
        if (!clip || !SoundManager.Instance) return;
        try
        {
            if (!Constants.ShouldPlaySfx()) return;
            SoundManager.Instance.PlaySound(clip, false, volume, null);
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogWarning($"Sound konnte nicht abgespielt werden: {e.Message}");
        }
    }

    private static RoleBehaviour VanillaRole(RoleTypes type) =>
        RoleManager.Instance ? RoleManager.Instance.AllRoles.ToArray().FirstOrDefault(r => r && r.Role == type) : null;

    /// <summary>Normaler Kill-Sound.</summary>
    public static AudioClip Kill => PlayerControl.LocalPlayer ? PlayerControl.LocalPlayer.KillSfx : null;

    /// <summary>Schutz-Sound (Guardian Angel).</summary>
    public static AudioClip Protect => VanillaRole(RoleTypes.GuardianAngel)?.UseSound;

    /// <summary>Verwandeln (Shapeshifter).</summary>
    public static AudioClip Shapeshift => VanillaRole(RoleTypes.Shapeshifter)?.UseSound;

    /// <summary>Verschwinden (Phantom).</summary>
    public static AudioClip Vanish => VanillaRole(RoleTypes.Phantom)?.UseSound;

    /// <summary>Wieder auftauchen (Phantom).</summary>
    public static AudioClip Appear => VanillaRole(RoleTypes.Phantom)?.TryCast<PhantomRole>()?.appearSound;

    /// <summary>Verhoer (Detective).</summary>
    public static AudioClip Investigate => VanillaRole(RoleTypes.Detective)?.TryCast<DetectiveRole>()?.interrogateSFX;

    /// <summary>Abbruch/Fehlschlag (Tracker).</summary>
    public static AudioClip Fail => VanillaRole(RoleTypes.Tracker)?.TryCast<TrackerRole>()?.cancelSound;

    /// <summary>Unheimlicher Ton (Impostor entdeckt).</summary>
    public static AudioClip Creepy => PlayerControl.LocalPlayer && PlayerControl.LocalPlayer.MyPhysics ? PlayerControl.LocalPlayer.MyPhysics.ImpostorDiscoveredSound : null;

    public static AudioClip TaskComplete => HudManager.InstanceExists ? HudManager.Instance.TaskCompleteSound : null;
    public static AudioClip TaskUpdate => HudManager.InstanceExists ? HudManager.Instance.TaskUpdateSound : null;
    public static AudioClip Alarm => ShipStatus.Instance ? ShipStatus.Instance.SabotageSound : null;
    public static AudioClip Splash => ShipStatus.Instance ? ShipStatus.Instance.VentEnterSound : null;
    public static AudioClip Warning => HudManager.InstanceExists && HudManager.Instance.Chat ? HudManager.Instance.Chat.warningSound : null;
}
