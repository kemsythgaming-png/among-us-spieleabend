using HarmonyLib;

namespace Spieleabend.Core;

/// <summary>Klicks auf unsere Fenster nicht an Spiel-Buttons darunter weitergeben (z. B. Spielerkarten im Meeting).</summary>
[HarmonyPatch(typeof(PassiveButton), nameof(PassiveButton.ReceiveClickDown))]
internal static class PassiveButtonClickDownPatch
{
    public static bool Prefix() => !Ui.IsMouseBlocked();
}

[HarmonyPatch(typeof(PassiveButton), nameof(PassiveButton.ReceiveClickUp))]
internal static class PassiveButtonClickUpPatch
{
    public static bool Prefix() => !Ui.IsMouseBlocked();
}
