using System;
using AmongUs.Data;
using BepInEx.Configuration;

namespace Spieleabend.Core;

/// <summary>
/// Sprache des Mods (Deutsch/Englisch), pro Spieler einstellbar - nicht vom Host synchronisiert.
/// Standard Deutsch; umstellbar auf Englisch oder "Automatisch" (= Sprache des Spiels).
/// Texte schreibt man als Lang.T("Deutsch", "English") oder - fuer Optionen und Buttons - als "Deutsch|English".
/// </summary>
public static class Lang
{
    private static ConfigEntry<int> setting; // 0 = Deutsch, 1 = English, 2 = automatisch

    internal static void Bind(ConfigFile config)
    {
        setting = config.Bind("Sprache", "Language", 0, "0 = Deutsch, 1 = English, 2 = automatisch (Sprache des Spiels)");
    }

    public static bool German => (setting?.Value ?? 0) switch
    {
        0 => true,
        1 => false,
        _ => GameIsGerman(),
    };

    private static bool GameIsGerman()
    {
        try { return DataManager.Settings.Language.CurrentLanguage == SupportedLangs.German; }
        catch { return true; }
    }

    public static string T(string de, string en) => German ? de : en;

    /// <summary>"Deutsch|English" -> passender Teil (ohne | bleibt der Text, wie er ist).</summary>
    public static string Pick(string both)
    {
        if (string.IsNullOrEmpty(both)) return both;
        int i = both.IndexOf('|');
        return i < 0 ? both : German ? both.Substring(0, i) : both.Substring(i + 1);
    }

    /// <summary>Die Spracheinstellung als Zeile im F1-Fenster (jeder Spieler fuer sich, immer aenderbar).</summary>
    internal sealed class Setting : ISetting
    {
        public string Name => T("Sprache (nur für dich)", "Language (just for you)");
        public bool IsToggle => false;
        public bool Bool => false;
        public bool AlwaysEditable => true;

        public string Display => setting.Value switch
        {
            0 => "Deutsch",
            1 => "English",
            _ => T("Automatisch", "Automatic"),
        };

        public void Change(int direction)
        {
            setting.Value = (setting.Value + direction + 3) % 3;
            RoleUI.Reset(); // Aufgabenliste usw. neu aufbauen
        }
    }

    internal static readonly Setting LanguageSetting = new();
}
