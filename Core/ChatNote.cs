using System.Linq;
using AmongUs.GameOptions;

namespace Spieleabend.Core;

/// <summary>
/// Zeigt Aenderungen an den Einstellungen unten links an - im selben Stil und mit demselben Ton wie das Spiel, wenn man
/// eine Standard-Einstellung aendert (nicht wie eine "Spieler hat den Raum verlassen"-Meldung). Nur der Host aendert;
/// er zeigt es selbst an und schickt es an alle.
/// </summary>
internal static class ChatNote
{
    public static void OptionChanged(RoleOption option)
    {
        string text = Format(option);
        if (text != null) Send(text);
    }

    /// <summary>Freie Meldung (z. B. "Alle Chancen: zufällig").</summary>
    public static void Message(string text)
    {
        if (!string.IsNullOrEmpty(text) && LobbyBehaviour.Instance) Send(text);
    }

    private static void Send(string text)
    {
        Display(PlayerControl.LocalPlayer, text); // der Host sieht es selbst
        RoleRpc.SendChatNote(text);               // alle anderen bekommen es per Nachricht
    }

    /// <summary>Hinweis unten links anzeigen - wie eine Standard-Einstellungsaenderung (Ton inklusive).</summary>
    internal static void Display(PlayerControl source, string text)
    {
        try
        {
            var hud = HudManager.Instance;
            if (!hud || !hud.Notifier || string.IsNullOrEmpty(text)) return;
            hud.Notifier.AddSettingsChangeMessage(StringNames.None, text, true, RoleTypes.Crewmate);
        }
        catch { /* Notifier evtl. noch nicht bereit - dann eben kein Hinweis */ }
    }

    private static string Format(RoleOption option)
    {
        if (!LobbyBehaviour.Instance) return null;
        var role = RoleRegistry.Roles.FirstOrDefault(r => r.Options.Contains(option));
        return role != null
            ? $"{role.Name}: {role.Count.Int}x, {role.Chance.Int}%"
            : $"{option.Name}: {option.Display}";
    }
}
