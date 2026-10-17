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

/// <summary>Waehrend man im Suchfeld tippt, bewegt sich die Spielfigur nicht (WASD wuerde sonst mitlaufen).</summary>
[HarmonyLib.HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.Update))]
[HarmonyLib.HarmonyPriority(HarmonyLib.Priority.First)]
internal static class TypingBlocksMovementPatch
{
    public static bool Prefix() => !Ui.IsTyping;
}
