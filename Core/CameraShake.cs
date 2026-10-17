using HarmonyLib;

namespace Spieleabend.Core;

/// <summary>
/// Kamera-Wackeln (Screen Shake) abschalten, solange die Option "Kamera-Wackeln" aus ist (Standard in einer neuen Lobby).
/// Vor jedem FollowerCamera.Update wird der Wackel-Betrag auf 0 gesetzt, damit die Kamera ruhig bleibt - egal, wer
/// das Wackeln ausgeloest hat (Sabotage, Effekte ...). Ist die Option an, bleibt alles wie im Original.
/// </summary>
[HarmonyPatch(typeof(FollowerCamera), nameof(FollowerCamera.Update))]
internal static class FollowerCameraShakePatch
{
    public static void Prefix(FollowerCamera __instance)
    {
        if (!__instance) return;
        if (GeneralOptions.CameraShake == null || GeneralOptions.CameraShake.Bool) return;
        __instance.shakeAmount = 0f;
        __instance.shakePeriod = 0f;
    }
}
