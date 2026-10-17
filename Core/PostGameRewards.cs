using HarmonyLib;

namespace Spieleabend.Core;

/// <summary>
/// Belohnungs-Bildschirm nach dem Spiel (XP, Pods, Cosmicube-Punkte) ueberspringen, wenn im Reiter "Spiel"
/// die Belohnungen ausgeschaltet sind. Dann gibt es nach dem Spiel kein "Geld" und keine Cube-Punkte-Anzeige.
/// </summary>
[HarmonyPatch(typeof(EndGameNavigation), nameof(EndGameNavigation.ShowProgression))]
internal static class PostGameRewardsPatch
{
    public static bool Prefix(EndGameNavigation __instance)
    {
        if (GeneralOptions.PostGameRewards == null || GeneralOptions.PostGameRewards.Bool) return true;
        // Statt des Fortschritts gleich die normalen Knoepfe zeigen (Zurueck zur Lobby / Beenden)
        __instance.ShowDefaultNavigation();
        return false;
    }
}
