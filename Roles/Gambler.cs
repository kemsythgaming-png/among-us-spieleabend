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
        "Du bist Crew. Jede erledigte Aufgabe gibt dir 1-3 Coins (Zahl am Automaten-Knopf). Hast du genug Coins, drehst du mit F am Spielautomaten - du gewinnst eine zufällige, EINMAL nutzbare Fähigkeit: Schild, Sheriff-Schuss, Radar, Prüfer, Enttarnen oder Tempo. Mit Taste C nutzt du die gewonnene Fähigkeit.",
        "You are crew. Each finished task gives 1-3 coins (shown on the machine button). With enough coins, press F to spin the slot machine - you win a random, ONE-USE ability: shield, sheriff shot, radar, inspect, reveal or speed. Press C to use the won ability.");

    internal static Gambler Instance { get; private set; }

    private const byte ActionShoot = 0, ActionShield = 1, ActionShieldBreak = 2;

    private enum Ability { Schild, Schuss, Radar, Pruefer, Enttarnen, Tempo }
    private static readonly (string De, string En, string Icon)[] Abilities =
    {
        ("Schild", "Shield", "shield"),
        ("Sheriff-Schuss", "Sheriff shot", "shoot"),
        ("Radar", "Radar", "radar"),
        ("Prüfer", "Inspect", "check"),
        ("Enttarnen", "Reveal", "guess"),
        ("Tempo", "Speed", "bolt"),
    };

    private RoleOption spinCost;
    private RoleButton slotButton, useButton;

    private int coins;
    private int won = -1;          // gewonnene Fähigkeit (-1 = keine)
    private bool spinning;
    private float spinStart, resultShownUntil, nextTick;
    private int reelResult;
    private const float SpinTime = 2.4f;

    // Effektzustände
    private bool shieldActive;                    // lokal: für die Blase
    private readonly HashSet<byte> shielded = new(); // Host: wer ist gerade geschützt
    private float speedUntil, radarUntil;
    private GameObject radarArrow;
    private static Sprite arrowSprite;

    protected override void Setup()
    {
        Instance = this;
        spinCost = AddOption("SpinCost", "Kosten pro Dreh|Cost per spin", 5f, 1f, 20f, 1f);

        slotButton = AddButton(new RoleButton
        {
            Label = "Automat|Slot machine",
            Icon = "slot",
            NeedsTarget = false,
            Cooldown = () => 2f,
            StartCooldown = 3f,
            UsesLeft = () => coins,                 // Coin-Zahl am Knopf
            CanUse = () => coins >= spinCost.Int && won < 0 && !spinning,
            Hotkey = KeyCode.F,
            OnClick = _ => StartSpin(),
        });

        useButton = AddButton(new RoleButton
        {
            Label = "Nutzen|Use",
            Icon = "shield",
            IconFunc = () => won >= 0 ? Abilities[won].Icon : "shield",
            NeedsTarget = false,
            Cooldown = () => 0f,
            Visible = () => won >= 0,
            CanUse = () => won >= 0,
            Hotkey = KeyCode.C,
            OnClick = _ => UseAbility(),
        });
    }

    public override void ResetState()
    {
        coins = 0; won = -1; spinning = false;
        shieldActive = false; shielded.Clear();
        speedUntil = radarUntil = 0f;
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
        reelResult = Random.Range(0, Abilities.Length);
        Sfx.Play(Sfx.TaskUpdate, 0.8f);
    }

    public override void Update()
    {
        // Dreh fertig -> Gewinn festlegen
        if (spinning && Time.time >= spinStart + SpinTime)
        {
            spinning = false;
            won = reelResult;
            resultShownUntil = Time.time + 1.6f;
            Sfx.Play(Sfx.TaskComplete, 0.9f);
            Notifier.Show($"<color={ColorHex}>{Lang.T("Gewonnen", "Won")}: {Abilities[won].De}</color>", 4f);
        }
        if (useButton != null && won >= 0) useButton.Label = Abilities[won].De + "|" + Abilities[won].En;

        // Tick-Geräusch während des Drehens
        if (spinning && Time.time >= nextTick) { nextTick = Time.time + 0.12f; Sfx.Play(Sfx.TaskUpdate, 0.2f); }

        UpdateRadar();
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
            case Ability.Pruefer:
                var insp = Nearest(local, 2.5f);
                if (insp) Notifier.Show($"<color={ColorHex}>{insp.Data.PlayerName}: {TaskTracker.Count(insp.PlayerId)} {Lang.T("Aufgaben", "tasks")}</color>", 4f);
                else return;
                break;
            case Ability.Enttarnen:
                var rev = Nearest(local, 2.5f);
                if (rev) Notifier.Show($"{rev.Data.PlayerName}: {RoleUI.RoleLabel(rev)}", 5f);
                else return;
                break;
            case Ability.Tempo:
                speedUntil = Time.time + 8f;
                Sfx.Play(Sfx.Investigate);
                break;
        }
        won = -1; // verbraucht
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
            if (IsImpostor(victim)) KillSystem.ForceKill(victim, victim);   // Treffer: Impostor stirbt
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

    public override float SpeedMultiplier(PlayerControl local) =>
        IsLocalRole && IsAlive(local) && Time.time < speedUntil ? 1.5f : 1f;

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

    // ---------- Slot-Machine-Anzeige (IMGUI): scrollende Walzen, gestaffelt anhaltend ----------
    private static int Mod(int a, int n) => ((a % n) + n) % n;

    public override void DrawGUI()
    {
        if (!IsLocalRole) return;
        if (!spinning && Time.time > resultShownUntil) return;

        float cx = Ui.ScreenWidth / 2f, cy = 480f;
        var panel = new Rect(cx - 235f, cy - 165f, 470f, 330f);
        var prev = GUI.color;

        // Gehäuse + goldener Rahmen
        GUI.color = new Color(0.14f, 0.11f, 0.16f, 0.96f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color(0.92f, 0.76f, 0.26f, 1f);
        GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 6f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(panel.x, panel.yMax - 6f, panel.width, 6f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(panel.x, panel.y, 6f, panel.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(panel.xMax - 6f, panel.y, 6f, panel.height), Texture2D.whiteTexture);

        var title = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        GUI.color = new Color(1f, 0.85f, 0.3f, 1f);
        GUI.Label(new Rect(panel.x, panel.y + 10f, panel.width, 36f), Lang.T("Spielautomat", "Slot machine"), title);

        float cell = 108f, gap = 16f, totalW = cell * 3 + gap * 2, startX = cx - totalW / 2f, reelY = cy - cell / 2f + 14f;
        float tt = spinning ? Time.time - spinStart : SpinTime;

        void DrawIcon(Rect r, int idx)
        {
            var tex = ButtonIcons.Texture(Abilities[idx].Icon);
            if (tex) { GUI.color = Color.white; GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit); }
        }

        for (int i = 0; i < 3; i++)
        {
            var reel = new Rect(startX + i * (cell + gap), reelY, cell, cell);
            GUI.color = Color.white;
            GUI.DrawTexture(reel, Texture2D.whiteTexture); // heller Walzen-Hintergrund

            float stop = SpinTime * (0.42f + i * 0.19f); // Walze i hält gestaffelt
            bool locked = tt >= stop;

            GUI.BeginGroup(reel);
            if (locked)
            {
                DrawIcon(new Rect(10f, 10f, cell - 20f, cell - 20f), reelResult);
            }
            else
            {
                float period = 0.07f + i * 0.015f;
                float phase = tt / period;
                int idx = Mod((int)phase, Abilities.Length);
                int nxt = Mod((int)phase + 1, Abilities.Length);
                float slide = phase - Mathf.Floor(phase);
                DrawIcon(new Rect(10f, 10f - slide * cell, cell - 20f, cell - 20f), idx);
                DrawIcon(new Rect(10f, 10f + (1f - slide) * cell, cell - 20f, cell - 20f), nxt);
            }
            GUI.EndGroup();
        }

        var style = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        bool allLocked = tt >= SpinTime * (0.42f + 2 * 0.19f);
        GUI.color = !allLocked ? new Color(1f, 1f, 1f, 0.85f) : new Color(0.4f, 1f, 0.5f, 1f);
        string txt = !allLocked ? Lang.T("Dreht ...", "Spinning ...") : $"{Lang.T("Gewonnen", "Won")}: {Abilities[Mathf.Max(0, reelResult)].De}";
        GUI.Label(new Rect(panel.x, panel.yMax - 50f, panel.width, 38f), txt, style);
        GUI.color = prev;
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
