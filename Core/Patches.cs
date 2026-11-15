using HarmonyLib;
using Hazel;

namespace Spieleabend.Core;

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
internal static class HandleRpcPatch
{
    public static void Postfix(PlayerControl __instance, byte callId, MessageReader reader)
    {
        RoleRpc.Handle(__instance, callId, reader);
    }
}

/// <summary>Wenn das Intro startet, sind alle Vanilla-Rollen verteilt -> Host verteilt die eigenen Rollen.</summary>
[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
internal static class IntroBeginPatch
{
    public static void Prefix()
    {
        Spieleabend.Roles.TaskTracker.Reset();
        if (AmongUsClient.Instance.AmHost && !FreeplayTest.IsFreeplay)
        {
            // Draft-Modus: NICHT jetzt verteilen - erst nach dem Intro wählt jeder seine Rolle (DraftMode.Finalize verteilt dann).
            if (!DraftMode.On)
            {
                RoleRegistry.AssignAsHost();

                // Neutrale Rollen bekommen keine Aufgaben
                foreach (var p in PlayerControl.AllPlayerControls)
                {
                    if (!p || p.Data == null) continue;
                    var r = RoleRegistry.GetRole(p);
                    if (r != null && r.Team == RoleTeam.Neutral && !r.KeepsTasks)
                        try { p.Data.RpcSetTasks(new byte[0]); } catch { }
                }
            }
            FirstDeathShield.AssignAsHost();
            RandomLooks.AssignAsHost(); // zufällige Namen/Farben/Dekorationen (je nach Option)
        }
        // Freeplay: beim Abspielen des Intros die Looks (nach F1-Option) lokal anwenden
        else if (FreeplayTest.IsFreeplay)
        {
            try { RandomLooks.RunFreeplay(); } catch (System.Exception e) { Plugin.Logger.LogWarning($"Freeplay-Looks (Intro): {e.Message}"); }
        }
    }
}

/// <summary>Beim Rollen-Intro (auch online) sofort unseren Text setzen (Rolle + Anpassung + Beschreibung); der
/// Frame-weise Override in SettingsWindow hält ihn danach aktuell.</summary>
[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginCrewmate))]
internal static class IntroCrewTextPatch
{
    public static void Postfix(IntroCutscene __instance) { try { RoleUI.ApplyIntroOverride(__instance); } catch { } }
}

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginImpostor))]
internal static class IntroImpTextPatch
{
    public static void Postfix(IntroCutscene __instance) { try { RoleUI.ApplyIntroOverride(__instance); } catch { } }
}

[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
internal static class HudUpdatePatch
{
    public static void Postfix(HudManager __instance)
    {
        if (!PlayerControl.LocalPlayer) return;
        CustomWin.FinalizePending(); // gesammelte Sieger (mehrere gleichzeitig) beenden das Spiel einmal
        RoleRpc.UpdateOptionSync();
        DraftMode.HostTick(); // Host: Draft starten/beenden
        DraftMode.Update();   // während des Drafts Bewegung sperren
        FreeplayTest.Update();
        ButtonManager.Update(__instance);
        RoleUI.Update();
        HalloweenTheme.Update();
        PrototypeRoom.Update();
        MapExtension.Update();
        ExtraVents.Update();
        ProtectionVisuals.Update();
        FireEffect.Update();
        Emotes.Update();
        Spieleabend.Roles.Halloween.Undead.TickEffects(); // aufsteigende Seelen-Kugeln (Untoter) animieren
        HudButtons.Update(__instance);

        if (ShipStatus.Instance) // auch ohne Mod-Rollen (z. B. Sheriff-Chance nach Aufgaben)
        {
            SpeedControl.Update();
            Camouflage.Update();
            GhostRoles.Update();
            GhostRoles.SuppressHauntButton();
            foreach (var role in RoleRegistry.Roles)
            {
                try { role.Update(); }
                catch (System.Exception e) { Plugin.Logger.LogError($"{role.Name}.Update: {e.Message}"); }
            }

            // Crew-/Neutral-Mod-Rollen haben nie den Vanilla-Kill/Vent-Knopf (falls die Impostor-Basis aus der Vorrunde haengt)
            var lp = PlayerControl.LocalPlayer;
            var mr = RoleRegistry.HasAssignments ? RoleRegistry.GetRole(lp) : null;
            if (mr != null && mr.Team != RoleTeam.Impostor)
            {
                if (__instance.KillButton) __instance.KillButton.ToggleVisible(false);
                if (__instance.ImpostorVentButton) __instance.ImpostorVentButton.ToggleVisible(false);
            }
        }
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
internal static class MeetingUpdatePatch
{
    public static void Postfix(MeetingHud __instance)
    {
        RoleUI.UpdateMeeting(__instance);
        FreeplayTest.MeetingVoteFollow(__instance);
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
internal static class GameEndPatch
{
    public static void Postfix()
    {
        CustomWin.BuildWinners();
        FirstDeathShield.OnGameEnd();
        RoleRegistry.Reset();
    }
}

[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
internal static class LobbyStartPatch
{
    public static void Postfix()
    {
        RoleRegistry.Reset();
        CustomWin.Reset();
        FirstDeathShield.ResetActive();
        RoleRpc.MarkOptionsDirty();
    }
}

[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
internal static class MainMenuStartPatch
{
    public static void Postfix()
    {
        RoleRegistry.Reset();
        CustomWin.Reset();
        RoleRpc.ClearSynced();
    }
}
