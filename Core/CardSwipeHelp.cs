using HarmonyLib;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Hilfe fuer die Aufgabe "Karte durchziehen": Wer es nach X Fehlversuchen (Einstellung im Reiter "Spiel")
/// immer noch nicht geschafft hat, bekommt die Aufgabe trotzdem abgehakt.
/// Ein Fehlversuch = die rote Lampe geht an.
/// </summary>
[HarmonyPatch(typeof(CardSlideGame), nameof(CardSlideGame.Update))]
internal static class CardSwipeHelpPatch
{
    private static System.IntPtr game;
    private static int fails;
    private static bool redWasOn;

    public static void Postfix(CardSlideGame __instance)
    {
        int needed = GeneralOptions.CardSwipeHelp != null ? GeneralOptions.CardSwipeHelp.Int : 0;
        if (needed <= 0 || !__instance || !__instance.redLight) return;

        if (__instance.Pointer != game) // neues Fenster
        {
            game = __instance.Pointer;
            redWasOn = false;
            // Fehlversuche zaehlen ueber mehrere Versuche hinweg, aber nur fuer dieselbe Aufgabe
        }

        bool redOn = IsLit(__instance.redLight.color, __instance.gray);
        if (redOn && !redWasOn)
        {
            fails++;
            Plugin.Logger.LogInfo($"Karte durchziehen: Fehlversuch {fails}/{needed}");
            if (fails >= needed && __instance.MyNormTask && !__instance.MyNormTask.IsComplete)
            {
                fails = 0;
                __instance.MyNormTask.NextStep();
                __instance.Close();
                return;
            }
        }
        redWasOn = redOn;
    }

    private static bool IsLit(Color light, Color gray) =>
        Mathf.Abs(light.r - gray.r) + Mathf.Abs(light.g - gray.g) + Mathf.Abs(light.b - gray.b) > 0.15f && light.r > light.g;

    /// <summary>Neue Runde: Zaehler zuruecksetzen.</summary>
    internal static void Reset() => fails = 0;
}
