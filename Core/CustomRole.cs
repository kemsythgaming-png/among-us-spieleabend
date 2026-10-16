using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace Spieleabend.Core;

public enum RoleTeam
{
    /// <summary>Wird an Spieler verteilt, die vom Spiel normaler Crewmate bekommen haben.</summary>
    Crewmate,

    /// <summary>Wird an Spieler verteilt, die vom Spiel normaler Impostor bekommen haben.</summary>
    Impostor,

    /// <summary>
    /// Eigenes Team mit eigenem Sieg (z. B. Jester). Wird an normale Crewmates verteilt,
    /// zaehlt fuers Spiel also als Crewmate, aber die Aufgaben zaehlen nicht fuer den Crew-Sieg.
    /// </summary>
    Neutral,

    /// <summary>Anpassung (z. B. Lover, Flash) - kommt zusaetzlich zur normalen Rolle obendrauf.</summary>
    Modifier,
}

/// <summary>Wie ein Kill abgefangen wurde (Medic-Schild, Bodyguard ...).</summary>
public enum KillIntercept
{
    /// <summary>Nicht abgefangen - der Kill passiert normal.</summary>
    None,

    /// <summary>Abgefangen - die Rolle hat selbst entschieden, was passiert.</summary>
    Handled,
}

/// <summary>Woran ein Spieler gestorben ist.</summary>
public enum DeathCause
{
    Kill,
    Exile,

    /// <summary>Ohne Leiche (Guesser, Liebeskummer ...).</summary>
    NoBody,
}

/// <summary>
/// Basisklasse fuer alle eigenen Rollen. Neue Rolle = neue Klasse im Ordner Roles, die hiervon erbt.
/// Sie wird automatisch gefunden, verteilt, synchronisiert und bekommt ihre Einstellungen im F1-Fenster.
/// </summary>
public abstract class CustomRole
{
    /// <summary>Anzeigename in der aktuellen Sprache (z. B. Lang.T("Hexe", "Witch")).</summary>
    public abstract string Name { get; }

    /// <summary>Abschnitt in der Config-Datei - fest, unabhaengig von der Sprache. Nicht mehr aendern, sonst gehen Einstellungen verloren.</summary>
    public virtual string ConfigSection => GetType().Name;

    public abstract Color Color { get; }

    /// <summary>An welches Vanilla-Team die Rolle verteilt wird (oder Modifier).</summary>
    public abstract RoleTeam Team { get; }

    /// <summary>Kurzer Satz im Intro unter dem Rollennamen.</summary>
    public abstract string IntroText { get; }

    /// <summary>Erklaerung in der Aufgabenliste.</summary>
    public abstract string Description { get; }

    /// <summary>Ausfuehrliche Erklaerung fuer das Rollen-Info-Fenster (F2). Standard: Description.</summary>
    public virtual string LongDescription => Description;

    /// <summary>Standard-Anzahl, bevor der Host etwas einstellt.</summary>
    public virtual int DefaultCount => 0;

    /// <summary>Wie viele Spieler eine "Anzahl 1" bekommt (Lover: 2).</summary>
    public virtual int PlayersPerCount => 1;

    /// <summary>Nur Modifier: darf auch an Impostor verteilt werden?</summary>
    public virtual bool ModifierAllowsImpostors => true;

    /// <summary>Nur Modifier: darf dieser Spieler (mit dieser Hauptrolle, oder null) die Anpassung bekommen?</summary>
    public virtual bool CanReceiveModifier(PlayerControl player, CustomRole mainRole) => ModifierAllowsImpostors || !IsImpostor(player);

    /// <summary>Zaehlen die Aufgaben dieser Rolle fuer den Crew-Aufgabensieg? Standard: nicht bei Neutralen.</summary>
    public virtual bool TasksCount => Team != RoleTeam.Neutral;

    public bool IsModifier => Team == RoleTeam.Modifier;

    /// <summary>
    /// Geisterrolle: wird nicht zu Spielbeginn verteilt, sondern beim Tod (Anzahl + Chance wie bei jeder Rolle).
    /// Kommt zusaetzlich zur bisherigen Rolle dazu, wie eine Anpassung.
    /// </summary>
    public virtual bool IsGhostRole => false;

    /// <summary>Nur Geisterrollen: darf dieser gerade gestorbene Spieler den Geist bekommen?</summary>
    public virtual bool CanHaunt(PlayerControl dead) => false;

    /// <summary>Kommt zusaetzlich zur Hauptrolle dazu (Anpassung oder Geisterrolle).</summary>
    public bool IsExtra => IsModifier || IsGhostRole;

    public byte Id { get; internal set; }
    public RoleOption Count { get; private set; }
    public RoleOption Chance { get; private set; }
    public List<RoleOption> Options { get; } = new();
    public List<RoleButton> Buttons { get; } = new();

    public string ColorHex => "#" + ColorUtility.ToHtmlStringRGB(Color);
    /// <summary>
    /// Farbe des Rollennamens (Intro, eigener Name, Listen) wie im echten Spiel: Impostor-Rollen rot, Crew-Rollen tuerkis.
    /// Neutrale und Anpassungen behalten ihre eigene Farbe. Effekte (Umrandung, Schutzblasen ...) nutzen weiter Color.
    /// </summary>
    public Color NameColor => Team switch
    {
        RoleTeam.Impostor => ImpostorRed,
        RoleTeam.Crewmate => CrewBlue,
        _ => Color,
    };

    public static readonly Color ImpostorRed = new Color32(255, 25, 25, 255);
    public static readonly Color CrewBlue = new Color32(140, 255, 255, 255);

    public string NameHex => "#" + ColorUtility.ToHtmlStringRGB(NameColor);
    public string ColoredName => $"<color={NameHex}>{Name}</color>";

    /// <summary>Hat der lokale Spieler diese Rolle (oder diesen Modifier)?</summary>
    public bool IsLocalRole => RoleRegistry.Has(PlayerControl.LocalPlayer, this);

    private ConfigFile config;

    internal void Init(ConfigFile cfg)
    {
        config = cfg;
        Count = AddOption("Count", "Anzahl|Count", DefaultCount, 0, 15, 1);
        Chance = AddOption("Chance", "Wahrscheinlichkeit|Chance", 100, 0, 100, 10, "%");
        Setup();
    }

    /// <summary>Hier eigene Optionen (AddOption/AddToggle) und Buttons (AddButton) anlegen.</summary>
    protected abstract void Setup();

    // ---------- Ereignisse (laufen auf allen Clients, ausser anders angegeben) ----------

    /// <summary>Ein Spieler hat diese Rolle bekommen.</summary>
    public virtual void OnAssigned(PlayerControl player) { }

    /// <summary>Alles zuruecksetzen (Spielende, Lobby).</summary>
    public virtual void ResetState() { }

    /// <summary>
    /// Tempo-Faktor fuer den lokalen Spieler (1 = normal). Alle Faktoren werden multipliziert (siehe SpeedControl),
    /// darum hier NICHT selbst MyPhysics.Speed setzen.
    /// </summary>
    public virtual float SpeedMultiplier(PlayerControl local) => 1f;

    /// <summary>Jedes Frame waehrend einer Runde (fuer lokale Effekte).</summary>
    public virtual void Update() { }

    /// <summary>Ein Meeting beginnt.</summary>
    public virtual void OnMeetingStart() { }

    /// <summary>Ein Meeting ist vorbei (nach der Rauswurf-Animation). exiled = rausgevoteter Spieler oder null (Skip/Gleichstand).</summary>
    public virtual void OnMeetingEnd(PlayerControl exiled) { }

    /// <summary>Jedes Frame im Meeting, nur fuer den lokalen Spieler mit dieser Rolle (z. B. Knoepfe auf Spielerkarten).</summary>
    public virtual void OnMeetingUpdate(MeetingHud meeting) { }

    /// <summary>Symbol (Name aus Assets/Icons) auf der Meeting-Karte von target, das viewer sieht - z. B. Fluch. null = keins.</summary>
    public virtual string GetMeetingIcon(PlayerControl viewer, PlayerControl target) => null;

    /// <summary>Symbol hinter dem Namen von target auf der Map (ausserhalb von Meetings), das viewer sieht. null = keins.</summary>
    public virtual string GetNameIcon(PlayerControl viewer, PlayerControl target) => null;

    /// <summary>Eigenes Fenster zeichnen (IMGUI, Koordinaten fuer 1080p). Nur fuer den lokalen Spieler mit dieser Rolle.</summary>
    public virtual void DrawGUI() { }

    /// <summary>Irgendein Spieler ist gestorben (killer kann null sein).</summary>
    public virtual void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause) { }

    /// <summary>Ein Spieler mit DIESER Rolle wurde rausgevotet (nach der Rauswurf-Animation).</summary>
    public virtual void OnExiled(PlayerControl player) { }

    /// <summary>Irgendein Spieler wurde rausgevotet (nach der Rauswurf-Animation).</summary>
    public virtual void OnAnyExiled(PlayerControl player) { }

    /// <summary>Optional: eigener Text in der Rauswurf-Animation (null = normaler Text).</summary>
    public virtual string GetExileText(PlayerControl player) => null;

    /// <summary>
    /// Nur beim Host: ein Kill soll passieren. Die Rolle kann ihn abfangen (Schild, Leibwache ...).
    /// Bei Handled passiert der normale Kill nicht.
    /// </summary>
    public virtual KillIntercept InterceptKill(PlayerControl killer, PlayerControl target) => KillIntercept.None;

    /// <summary>
    /// Markierung am Namen von target, die der lokale Spieler (viewer) sieht - z. B. ein Herz beim Lover.
    /// Wird fuer ALLE Rollen aufgerufen; die Rolle prueft selbst, ob viewer etwas sehen darf. null = nichts.
    /// </summary>
    public virtual string GetNameMark(PlayerControl viewer, PlayerControl target) => null;

    /// <summary>
    /// Schutzblasen, die der lokale Spieler (viewer) sehen soll - z. B. Medic-Schild. Die Rolle entscheidet, wer was sieht.
    /// </summary>
    public virtual IEnumerable<(PlayerControl Target, Color Color)> GetProtectionBubbles(PlayerControl viewer) =>
        System.Array.Empty<(PlayerControl, Color)>();

    /// <summary>
    /// Empfaengt Aktionen, die mit SendToHost/SendToAll geschickt wurden.
    /// Bei SendToHost laeuft das nur beim Host - dort Entscheidungen treffen (z. B. wer stirbt).
    /// </summary>
    public virtual void OnAction(PlayerControl sender, byte action, byte[] data) { }

    // ---------- Helfer fuer Rollen ----------

    protected RoleOption AddOption(string key, string name, float def, float min, float max, float step, string suffix = "")
    {
        var option = new RoleOption(config, ConfigSection, key, name, def, min, max, step, suffix, false);
        Options.Add(option);
        return option;
    }

    protected RoleOption AddToggle(string key, string name, bool def)
    {
        var option = new RoleOption(config, ConfigSection, key, name, def ? 1f : 0f, 0f, 1f, 1f, "", true);
        Options.Add(option);
        return option;
    }

    protected RoleButton AddButton(RoleButton button)
    {
        button.Owner = this;
        Buttons.Add(button);
        return button;
    }

    /// <summary>Schickt eine Aktion nur an den Host (der Host ruft OnAction auf).</summary>
    protected void SendToHost(byte action, params byte[] data) => RoleRpc.SendAction(this, action, data, toHostOnly: true);

    /// <summary>Schickt eine Aktion an alle, inklusive einem selbst.</summary>
    protected void SendToAll(byte action, params byte[] data) => RoleRpc.SendAction(this, action, data, toHostOnly: false);

    /// <summary>Alle Spieler mit dieser Rolle.</summary>
    public IEnumerable<PlayerControl> Players => RoleRegistry.PlayersWith(this);

    public static bool AmHost => AmongUsClient.Instance && AmongUsClient.Instance.AmHost;

    public static PlayerControl FindPlayer(byte playerId)
    {
        foreach (var p in PlayerControl.AllPlayerControls)
            if (p && p.PlayerId == playerId) return p;
        return null;
    }

    /// <summary>Gilt als Impostor (echt, oder Test-Impostor-Dummy im Freeplay).</summary>
    public static bool IsImpostor(PlayerControl player) =>
        player && player.Data != null && ((player.Data.Role && player.Data.Role.IsImpostor) || FreeplayTest.IsFakeImpostor(player));

    public static bool IsNeutral(PlayerControl player) => RoleRegistry.GetRole(player)?.Team == RoleTeam.Neutral;

    public static bool IsAlive(PlayerControl player) =>
        player && player.Data != null && !player.Data.IsDead && !player.Data.Disconnected;

    public static bool InMeeting => MeetingHud.Instance || ExileController.Instance;
}
