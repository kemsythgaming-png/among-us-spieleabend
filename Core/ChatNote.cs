using System.Linq;

namespace Spieleabend.Core;

/// <summary>
/// Schreibt Aenderungen an den Rollen-Einstellungen in den Chat (unten links), damit ALLE in der Lobby direkt sehen,
/// was gerade geaendert wurde. Nur der Host aendert Einstellungen; er zeigt den Hinweis selbst an und schickt ihn an alle.
/// </summary>
internal static class ChatNote
{
    public static void OptionChanged(RoleOption option)
    {
        string text = Format(option);
        if (text == null) return;
        Display(PlayerControl.LocalPlayer, text); // der Host sieht es selbst
        RoleRpc.SendChatNote(text);               // alle anderen bekommen es per Nachricht
    }

    /// <summary>Hinweis unten links anzeigen (wie die Standard-Meldung "# Impostors set to 2") - auf jedem Client.</summary>
    internal static void Display(PlayerControl source, string text)
    {
        try
        {
            var hud = HudManager.Instance;
            if (!hud || !hud.Notifier || string.IsNullOrEmpty(text)) return;
            hud.Notifier.AddDisconnectMessage(text);
        }
        catch { /* Notifier evtl. noch nicht bereit - dann eben kein Hinweis */ }
    }

    private static string Format(RoleOption option)
    {
        if (!LobbyBehaviour.Instance) return null;
        // Gehoert die Option zu einer Mod-Rolle? Dann die ganze Rolle zusammenfassen ("Vampir: 1x, 70%").
        var role = RoleRegistry.Roles.FirstOrDefault(r => r.Options.Contains(option));
        return role != null
            ? $"{role.Name}: {role.Count.Int}x, {role.Chance.Int}%"
            : $"{option.Name}: {option.Display}";
    }
}
