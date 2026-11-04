using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Crew: sammelt Coins durch erledigte Aufgaben (1-3 je Aufgabe). Ab genug Coins dreht er am Automaten und gewinnt
/// eine zufällige, einmal nutzbare Fähigkeit aus einer festen Auswahl.
/// </summary>
public sealed class Gambler : CustomRole
{
    public override string ConfigSection => "Gambler";
    public override string Name => "Gambler";
    public override Color Color => new Color32(240, 195, 70, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Dein Glück an der Walze", "Your luck on the reels");
    public override string Description => Lang.T("Aufgaben geben Coins. Dreh am\nAutomaten (F) für eine Fähigkeit.", "Tasks give coins. Spin the\nmachine (F) for an ability.");
    public override string LongDescription => Lang.T(
        "Du bist Crew. Jede erledigte Aufgabe gibt dir 1-3 Coins (Zahl am Automaten-Knopf). Hast du genug Coins, drehst du mit F am Spielautomaten - 3 Walzen mit Symbolen:\n\n- 2 gleiche Symbole: du bekommst diese Fähigkeit 1x.\n- 3 gleiche Symbole: du bekommst sie sogar 2x.\n- alles verschieden: Pech, nichts.\n\nFähigkeiten: Schild, Sheriff-Schuss, Radar, Feuerwehrmann (Sabotage löschen), Teleport oder ein Trank (wie beim Trankbrauer). Mit Taste C nutzt du die gewonnene Fähigkeit.",
        "You are crew. Each finished task gives 1-3 coins (shown on the machine button). With enough coins, press F to spin the slot machine - 3 reels of symbols:\n\n- 2 matching symbols: you get that ability once.\n- 3 matching symbols: you get it twice.\n- all different: no luck, nothing.\n\nAbilities: shield, sheriff shot, radar, firefighter (fix the sabotage), teleport or a potion (like the Potion Brewer). Press C to use the won ability.");

    internal static Gambler Instance { get; private set; }

    private const byte ActionShoot = 0, ActionShield = 1, ActionShieldBreak = 2;

    private enum Ability { Schild, Schuss, Radar, Feuerwehr, Teleport, Trank }
    private static readonly (string De, string En, string Icon)[] Abilities =
    {
        ("Schild", "Shield", "shield"),
        ("Sheriff-Schuss", "Sheriff shot", "shoot"),
        ("Radar", "Radar", "radar"),
        ("Feuerwehr", "Firefighter", "extinguish"),
        ("Teleport", "Teleport", "tp"),
        ("Trank", "Potion", "cauldron"),
    };

    private bool pendingTeleport; // Teleport-Fähigkeit: Karte offen, Verbrauch erst nach dem Teleport

    private RoleOption spinCost, spinCooldown;
    private RoleButton slotButton, useButton;

    private int coins;
    private int won = -1;          // gewonnene Fähigkeit (-1 = keine)
    private int wonUses;           // wie oft noch nutzbar (2 bei Dreier-Treffer, 1 bei Zweier)
    private bool spinning;
    private int lastWonShown = -1;              // welches Symbol der Nutzen-Knopf gerade zeigt
    private float spinStart, resultShownUntil, nextTick;
    private readonly int[] reels = new int[3];  // Ergebnis der 3 Walzen
    private const float SpinTime = 2.4f;

    // Effektzustände
    private bool shieldActive;                    // lokal: für die Blase
    private readonly HashSet<byte> shielded = new(); // Host: wer ist gerade geschützt
    private float radarUntil;
    private GameObject radarArrow;
    private static Sprite arrowSprite;

    protected override void Setup()
    {
        Instance = this;
        spinCost = AddOption("SpinCost", "Kosten pro Dreh|Cost per spin", 3f, 1f, 20f, 1f);
        spinCooldown = AddOption("SpinCooldown", "Abklingzeit nach dem Dreh|Cooldown after a spin", 20f, 0f, 60f, 5f, "s");

        slotButton = AddButton(new RoleButton
        {
            Label = "Automat|Slot machine",
            Icon = "slot",
            NeedsTarget = false,
            Cooldown = () => spinCooldown.Value,     // nach jedem Dreh eine Abklingzeit
            StartCooldown = 3f,
            UsesLeft = () => coins,                 // Coin-Zahl am Knopf
            CanUse = () => coins >= spinCost.Int && won < 0 && !spinning,
            Hotkey = KeyCode.F,
            OnClick = _ => StartSpin(),
        });

        useButton = AddButton(new RoleButton
        {
            Label = "Nutzen|Use",
            Icon = "shield",                                   // Startbild; beim Gewinn via SetIconStatic auf die Fähigkeit gewechselt
            NeedsTarget = false,
            Cooldown = () => 1f,               // >0, sonst NaN in der Radial-Anzeige -> Knopf wirkt leer/fehlt
            StartCooldown = 1f,
            UsesLeft = () => wonUses,          // wie oft die Fähigkeit noch geht (1 oder 2)
            Visible = () => won >= 0,
            CanUse = () => AbilityReady(),     // nur aktiv, wenn die Fähigkeit gerade wirklich nutzbar ist
            Hotkey = KeyCode.C,
            OnClick = _ => UseAbility(),
        });
    }

    public override void OnAssigned(PlayerControl player)
    {
        // Im Practise gleich ein paar Coins, damit man die Slot-Machine sofort testen kann
        if (player == PlayerControl.LocalPlayer && FreeplayTest.IsFreeplay) coins = spinCost.Int * 3;
    }

    public override void ResetState()
    {
        coins = 0; won = -1; wonUses = 0; spinning = false; pendingTeleport = false;
        shieldActive = false; shielded.Clear();
        radarUntil = 0f;
        if (radarArrow) Object.Destroy(radarArrow);
        radarArrow = null;
    }

    internal void AddCoins(int n) { coins += n; }

    private void StartSpin()
    {
        if (coins < spinCost.Int || won >= 0 || spinning) return;
        coins -= spinCost.Int;
        spinning = true;
        spinStart = Time.time;
        // Jede Walze zufällig - wie an einem echten Automaten
        for (int i = 0; i < reels.Length; i++) reels[i] = Random.Range(0, Abilities.Length);
        Sfx.Play(Sfx.TaskUpdate, 0.8f);
    }

    /// <summary>Treffer auswerten: 3 gleich = Fähigkeit mit 2 Nutzungen, 2 gleich = 1 Nutzung, sonst Pech (nichts).</summary>
    private void Evaluate()
    {
        if (reels[0] == reels[1] && reels[1] == reels[2]) { won = reels[0]; wonUses = 2; }       // Jackpot: 3 gleiche -> 2x
        else if (reels[0] == reels[1] || reels[0] == reels[2]) { won = reels[0]; wonUses = 1; }    // 2 gleiche (Walze 0 dabei)
        else if (reels[1] == reels[2]) { won = reels[1]; wonUses = 1; }                            // 2 gleiche (Walze 1+2)
        else { won = -1; wonUses = 0; }                                                            // Pech: alle verschieden
    }

    public override void Update()
    {
        // Teleport-Fähigkeit: Karte offen -> auf den Klick warten, dann erst verbrauchen
        if (pendingTeleport)
        {
            if (MapTeleport.Update()) { pendingTeleport = false; ConsumeWon(); }
            else if (!MapTeleport.Active) pendingTeleport = false; // Karte ohne Teleport geschlossen -> Fähigkeit bleibt
        }

        // Dreh fertig -> Treffer auswerten
        if (spinning && Time.time >= spinStart + SpinTime)
        {
            spinning = false;
            Evaluate();
            resultShownUntil = Time.time + 2f;
            // Keine Text-Meldung mehr - das Ergebnis sieht man am Automaten und am Nutzen-Knopf.
            Sfx.Play(won >= 0 ? Sfx.TaskComplete : Sfx.TaskUpdate, won >= 0 ? 0.9f : 0.5f);
        }
        if (useButton != null && won >= 0)
        {
            useButton.Label = Abilities[won].De + "|" + Abilities[won].En;
            if (lastWonShown != won) { useButton.SetIconStatic(Abilities[won].Icon); lastWonShown = won; } // Bild sicher setzen (Knopf-Neuaufbau)
        }
        if (won < 0) lastWonShown = -1;

        // Tick-Geräusch während des Drehens
        if (spinning && Time.time >= nextTick) { nextTick = Time.time + 0.12f; Sfx.Play(Sfx.TaskUpdate, 0.2f); }

        UpdateRadar();
    }

    /// <summary>Ist die gewonnene Fähigkeit gerade wirklich nutzbar? (sonst bleibt der Nutzen-Knopf grau)</summary>
    private bool AbilityReady()
    {
        if (won < 0) return false;
        var local = PlayerControl.LocalPlayer;
        if (!IsAlive(local)) return false;
        return (Ability)won switch
        {
            Ability.Schuss => Nearest(local, 2.0f) != null,
            Ability.Trank => Nearest(local, 2.5f) != null,
            Ability.Feuerwehr => Firefighter.SabotageActive(),
            _ => true, // Schild, Radar, Teleport: überall nutzbar
        };
    }

    private void UseAbility()
    {
        if (won < 0) return;
        var local = PlayerControl.LocalPlayer;
        if (!local) return;

        switch ((Ability)won)
        {
            case Ability.Schild:
                shieldActive = true;
                SendToHost(ActionShield);
                Sfx.Play(Sfx.Protect);
                Notifier.Show($"<color={ColorHex}>{Lang.T("Schild aktiv - der nächste Kill prallt ab.", "Shield active - the next kill is blocked.")}</color>", 4f);
                break;
            case Ability.Schuss:
                var shot = Nearest(local, 2.0f);
                if (shot) SendToHost(ActionShoot, shot.PlayerId);
                else { Notifier.Show(Lang.T("Niemand in der Nähe.", "Nobody nearby."), 2f); return; }
                break;
            case Ability.Radar:
                radarUntil = Time.time + 6f;
                Sfx.Play(Sfx.Investigate);
                break;
            case Ability.Feuerwehr:
                if (!Firefighter.SabotageActive()) { Notifier.Show(Lang.T("Keine Sabotage aktiv.", "No sabotage active."), 2f); return; }
                Firefighter.FixSabotages();
                Notifier.Show($"<color={ColorHex}>{Lang.T("Sabotage gelöscht!", "Sabotage fixed!")}</color>", 3f);
                break;
            case Ability.Teleport:
                if (!MapTeleport.Open()) return; // Karte ging nicht auf -> nicht verbrauchen
                pendingTeleport = true;
                Notifier.Show($"<color={ColorHex}>{Lang.T("Klick auf die Karte, um dich zu teleportieren.", "Click the map to teleport.")}</color>", 4f);
                return; // Verbrauch erst nach dem Teleport (siehe Update)
            case Ability.Trank:
                var drinker = Nearest(local, 2.5f);
                if (!drinker) { Notifier.Show(Lang.T("Niemand in der Nähe.", "Nobody nearby."), 2f); return; }
                PotionBrewer.Instance?.CastRandom(drinker);
                Sfx.Play(Sfx.Investigate);
                break;
        }
        ConsumeWon();
    }

    /// <summary>Eine Nutzung der gewonnenen Fähigkeit verbrauchen (bei Dreier-Treffer bleibt sie noch ein 2. Mal).</summary>
    private void ConsumeWon()
    {
        if (--wonUses <= 0) won = -1;
    }

    private PlayerControl Nearest(PlayerControl local, float range)
    {
        Vector2 me = local.GetTruePosition();
        return PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p && p != local && IsAlive(p) && Vector2.Distance(me, p.GetTruePosition()) <= range)
            .OrderBy(p => Vector2.Distance(me, p.GetTruePosition()))
            .FirstOrDefault();
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionShoot && AmHost && data.Length >= 1)
        {
            var victim = FindPlayer(data[0]);
            if (!IsAlive(victim) || !IsAlive(sender)) return;
            if (IsImpostor(victim)) KillSystem.TryKill(sender, victim);     // Treffer: normale Kill-Animation
            else KillSystem.ForceKill(sender, sender);                      // daneben: der Gambler stirbt
        }
        else if (action == ActionShield && AmHost)
        {
            if (RoleRegistry.Has(sender, this)) shielded.Add(sender.PlayerId);
        }
        else if (action == ActionShieldBreak && data.Length >= 1)
        {
            var g = FindPlayer(data[0]);
            if (g == PlayerControl.LocalPlayer) shieldActive = false;
            if (g) ProtectionVisuals.ShowBurst(g, Color);
            if (g == PlayerControl.LocalPlayer) Sfx.Play(Sfx.Protect);
        }
    }

    public override KillIntercept InterceptKill(PlayerControl killer, PlayerControl target)
    {
        if (!AmHost || !target || !shielded.Contains(target.PlayerId)) return KillIntercept.None;
        shielded.Remove(target.PlayerId);
        SendToAll(ActionShieldBreak, target.PlayerId);
        return KillIntercept.Handled;
    }

    public override IEnumerable<(PlayerControl Target, Color Color)> GetProtectionBubbles(PlayerControl viewer)
    {
        if (viewer && shieldActive && RoleRegistry.Has(viewer, this)) yield return (viewer, Color);
    }

    // ---------- Radar-Pfeil ----------
    private void UpdateRadar()
    {
        var local = PlayerControl.LocalPlayer;
        bool active = IsLocalRole && IsAlive(local) && !InMeeting && Time.time < radarUntil;
        if (!active)
        {
            if (radarArrow) radarArrow.SetActive(false);
            return;
        }
        var near = Nearest(local, 999f);
        if (!near) { if (radarArrow) radarArrow.SetActive(false); return; }

        if (!radarArrow) radarArrow = CreateArrow(local);
        radarArrow.SetActive(true);
        Vector2 dir = near.GetTruePosition() - local.GetTruePosition();
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        radarArrow.transform.localPosition = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * 0.8f, Mathf.Sin(angle * Mathf.Deg2Rad) * 0.8f + 0.1f, -0.05f);
        radarArrow.transform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    private static GameObject CreateArrow(PlayerControl local)
    {
        var go = new GameObject("Spieleabend_GamblerRadar");
        go.transform.SetParent(local.transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ArrowSprite();
        sr.color = new Color(1f, 0.85f, 0.3f, 0.95f);
        go.transform.localScale = Vector3.one * 0.7f;
        return go;
    }

    private static Sprite ArrowSprite()
    {
        if (arrowSprite) return arrowSprite;
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float half = (S - 1 - y) * 0.45f;
            float dx = Mathf.Abs(x - (S - 1) / 2f);
            bool inside = y > 8 && dx <= half;
            bool edge = inside && (dx > half - 4f || y < 13);
            tex.SetPixel(x, y, !inside ? Color.clear : edge ? new Color(0.2f, 0.12f, 0.02f, 1f) : Color.white);
        }
        tex.Apply();
        arrowSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 200f);
        arrowSprite.hideFlags = HideFlags.HideAndDontSave;
        return arrowSprite;
    }

    // ---------- Slot-Machine-Anzeige (IMGUI): schnell wechselnde Symbole, gestaffelt anhaltend ----------
    // Bilder gehen hier NUR über einen Box-Style mit normal.background (GUI.DrawTexture ist im IL2CPP wegoptimiert).
    private static int Mod(int a, int n) => ((a % n) + n) % n;

    private static GUIStyle titleStyle, statusStyle, cellStyle;
    private static Texture2D cellTex;
    private static readonly Dictionary<string, GUIStyle> iconStyles = new();
    private static readonly RectOffset Zero = new RectOffset(); // Felder sind standardmäßig 0 (IL2CPP hat keinen 4-Arg-Ctor)

    /// <summary>Box-Style, der nur ein Bild zeichnet (von Ui.Panel abgeleitet, Rand 0 -> randloses Bild).</summary>
    private static GUIStyle BoxWithImage(Texture2D tex)
    {
        var s = new GUIStyle(Ui.Panel) { border = Zero, padding = Zero, overflow = Zero, margin = Zero };
        s.normal.background = tex;
        return s;
    }

    private static GUIStyle IconStyle(string name)
    {
        if (iconStyles.TryGetValue(name, out var s) && s != null) return s;
        s = BoxWithImage(ButtonIcons.Texture(name));
        iconStyles[name] = s;
        return s;
    }

    public override void DrawGUI()
    {
        if (!IsLocalRole) return;
        if (!spinning && Time.time > resultShownUntil) return;

        float cx = Ui.ScreenWidth / 2f, cy = 480f;
        var panel = new Rect(cx - 235f, cy - 165f, 470f, 330f);

        GUI.Box(panel, GUIContent.none, Ui.Panel); // dunkles Gehäuse (bewährter Box-Style)

        titleStyle ??= new GUIStyle(Ui.Title) { alignment = TextAnchor.MiddleCenter, fontSize = 30 };
        GUI.Label(new Rect(panel.x, panel.y + 12f, panel.width, 40f), $"<color=#FFD94D>{Lang.T("Spielautomat", "Slot machine")}</color>", titleStyle);

        if (!cellTex)
        {
            cellTex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            cellTex.SetPixel(0, 0, new Color(0.95f, 0.95f, 0.98f, 1f));
            cellTex.Apply();
        }
        cellStyle ??= BoxWithImage(cellTex);

        float cell = 108f, gap = 16f, totalW = cell * 3 + gap * 2, startX = cx - totalW / 2f, reelY = cy - cell / 2f + 14f;
        float tt = spinning ? Time.time - spinStart : SpinTime;

        for (int i = 0; i < 3; i++)
        {
            var reel = new Rect(startX + i * (cell + gap), reelY, cell, cell);
            GUI.Box(reel, GUIContent.none, cellStyle); // helle Walzen-Fläche

            float stop = SpinTime * (0.42f + i * 0.19f); // Walze i hält gestaffelt
            bool locked = tt >= stop;
            // Während des Drehens schnell durch die Symbole blättern, dann auf dem eigenen Ergebnis stehen bleiben
            int idx = locked ? reels[i] : Mod((int)(tt / (0.07f + i * 0.015f)), Abilities.Length);
            GUI.Box(new Rect(reel.x + 12f, reel.y + 12f, cell - 24f, cell - 24f), GUIContent.none, IconStyle(Abilities[idx].Icon));
        }

        statusStyle ??= new GUIStyle(Ui.Title) { alignment = TextAnchor.MiddleCenter, fontSize = 26 };
        bool allLocked = tt >= SpinTime * (0.42f + 2 * 0.19f);
        string col, txt;
        if (!allLocked) { col = "#FFFFFF"; txt = Lang.T("Dreht ...", "Spinning ..."); }
        else if (won >= 0) { col = "#66FF80"; txt = $"{Lang.T("Gewonnen", "Won")}: {Abilities[won].De}{(wonUses > 1 ? " (2x)" : "")}"; }
        else { col = "#FF8080"; txt = Lang.T("Pech - nichts", "No luck"); }
        GUI.Label(new Rect(panel.x, panel.yMax - 52f, panel.width, 40f), $"<color={col}>{txt}</color>", statusStyle);
    }
}

/// <summary>Jede erledigte Aufgabe gibt dem Gambler 1-3 Coins.</summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask), typeof(uint))]
internal static class GamblerCoinPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        var g = Gambler.Instance;
        if (g == null || !__instance || __instance != PlayerControl.LocalPlayer) return;
        if (!RoleRegistry.Has(__instance, g)) return;
        g.AddCoins(Random.Range(1, 4)); // 1-3
    }
}
