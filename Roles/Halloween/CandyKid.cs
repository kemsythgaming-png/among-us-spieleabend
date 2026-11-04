using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Halloween;

/// <summary>
/// Crew (Halloween): Nur das Kind sieht Bonbons auf der Map. Hat es alle gesammelt, kommt der Zuckerrausch:
/// es wird schneller und bekommt einen Schild gegen einen Angriff.
/// </summary>
public sealed class CandyKid : CustomRole
{
    public override string ConfigSection => "SuessigkeitenKind";
    public override string Name => Lang.T("Süßigkeiten-Kind", "Candy Kid");
    public override Color Color => new Color32(255, 120, 200, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Süßes oder Saures!", "Trick or treat!");
    public override string Description => Lang.T("Sammle alle Bonbons -\nnur du siehst sie!", "Collect all candy -\nonly you can see it!");
    public override string LongDescription => Lang.T(
        "Halloween-Crew. In versteckten Ecken der Map liegen Bonbons, die nur du siehst. Auf der Map liegt immer 1 weniger als nötig - jede erledigte Aufgabe lässt 1 weiteres erscheinen. Hast du genug, kommt der Zuckerrausch: Du bist für den Rest der Runde schneller.\n\nDanach gibt dir jedes weitere eingesammelte Bonbon einen Schild (Ladung). Mit Taste F aktivierst du einen: 15 Sekunden lang kannst du nicht getötet werden. Ein Kill-Versuch scheitert dabei einfach - der Impostor landet nur auf Abklingzeit. Nach dem Schild musst du kurz warten, bevor du den nächsten aktivieren kannst. Bonbons sammeln geht jederzeit.",
        "Halloween crew. Candy is hidden in corners of the map and only you can see it. There is always 1 fewer than needed on the map - each completed task spawns one more. Once you have enough, you get a sugar rush: you are faster for the rest of the game.\n\nAfter that, every further candy you collect gives you a shield (charge). Press F to activate one: for 15 seconds you can't be killed. A kill attempt simply fails - the impostor just goes on cooldown. After the shield you must wait briefly before you can activate the next one. Collecting candy is always possible.");

    private const byte ActionAddStack = 0, ActionShieldOn = 1;
    private const float CollectRange = 0.85f;
    private const float CandyScale = 0.35f;

    private RoleOption candies, rushSpeed, rushShield, shieldDuration, relock;

    // alle Clients: gesammelte Schilde (Ladungen) und der aktive 15s-Schild je Kind
    private readonly Dictionary<byte, int> stacks = new();
    private readonly Dictionary<byte, float> shieldUntil = new();

    // lokal beim Kind
    private readonly List<GameObject> candyObjects = new();
    private System.IntPtr spawnedFor;
    private int collected, total;
    private int lastTasksDone;
    private bool rushing;
    private RoleButton shieldButton;

    internal static CandyKid Instance { get; private set; }

    private static Sprite candySprite;

    private int Stacks(byte id) => stacks.TryGetValue(id, out var n) ? n : 0;
    private bool ActiveShield(PlayerControl p) => p && shieldUntil.TryGetValue(p.PlayerId, out var t) && Time.time < t;

    /// <summary>Hat dieser Spieler GERADE einen aktiven Zucker-Schild? (für andere Rollen, z. B. die Banane)</summary>
    internal static bool HasShield(PlayerControl p) => Instance != null && Instance.ActiveShield(p);

    /// <summary>Ist target gerade durch einen aktiven Schild geschützt? Blockt auch harmlose Effekte (nur während der 15 s).</summary>
    internal static bool Absorb(PlayerControl target) => HasShield(target);

    protected override void Setup()
    {
        Instance = this;
        candies = AddOption("Candies", "Bonbons bis Zuckerrausch|Candies until sugar rush", 8f, 3f, 20f, 1f);
        rushSpeed = AddOption("RushSpeed", "Zuckerrausch-Tempo|Sugar rush speed", 1.2f, 1.1f, 2f, 0.05f, "x");
        rushShield = AddToggle("RushShield", "Schilde sammeln (nach dem Rausch)|Collect shields (after rush)", true);
        shieldDuration = AddOption("ShieldTime", "Schild-Dauer|Shield duration", 15f, 5f, 30f, 1f, "s");
        relock = AddOption("Relock", "Wartezeit bis nächster Schild|Cooldown to next shield", 5f, 0f, 30f, 1f, "s");

        shieldButton = AddButton(new RoleButton
        {
            Label = "Zucker-Schild|Sugar shield",
            Icon = "shield",
            NeedsTarget = false,
            Cooldown = () => shieldDuration.Value + relock.Value, // Radial = Aktivzeit + Sperre
            StartCooldown = 0f,
            UsesLeft = () => StacksLocal(),
            Visible = () => rushing && (StacksLocal() > 0 || (shieldButton != null && shieldButton.Timer > 0.05f)), // auch sichtbar, solange Aktiv/Sperre läuft
            CanUse = () => rushing && StacksLocal() > 0 && IsAlive(PlayerControl.LocalPlayer),
            Hotkey = KeyCode.F,
            OnClick = _ => Activate(),
        });
    }

    private int StacksLocal() => PlayerControl.LocalPlayer ? Stacks(PlayerControl.LocalPlayer.PlayerId) : 0;

    private void Activate()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || StacksLocal() <= 0 || ActiveShield(local)) return;
        SendToAll(ActionShieldOn, local.PlayerId); // verbraucht 1 Ladung und startet den 15s-Schutz
    }

    public override void ResetState()
    {
        // Freeplay: wird bei jeder (Dummy-)Rollenänderung aufgerufen. Solange DASSELBE Schiff läuft, Fortschritt behalten.
        if (ShipStatus.Instance && spawnedFor == ShipStatus.Instance.Pointer) return;

        foreach (var c in candyObjects)
            if (c) Object.Destroy(c);
        candyObjects.Clear();
        spawnedFor = System.IntPtr.Zero;
        collected = total = 0;
        lastTasksDone = 0;
        stacks.Clear();
        shieldUntil.Clear();
        if (shieldButton != null) shieldButton.Timer = 0f;
        rushing = false;
    }

    public override float SpeedMultiplier(PlayerControl local) => rushing && IsLocalRole && IsAlive(local) ? rushSpeed.Value : 1f;

    public override void Update()
    {
        var local = PlayerControl.LocalPlayer;
        if (!IsLocalRole || !ShipStatus.Instance) return;

        if (!IsAlive(local))
        {
            foreach (var c in candyObjects)
                if (c) c.SetActive(false);
            return;
        }

        if (spawnedFor != ShipStatus.Instance.Pointer) SpawnCandies();
        CheckTasks(local); // auch nach dem Rausch: neue Bonbons = Schilde


        bool inMeeting = InMeeting;
        Vector2 me = local.GetTruePosition();
        float t = Time.time;
        for (int i = candyObjects.Count - 1; i >= 0; i--)
        {
            var c = candyObjects[i];
            if (!c)
            {
                candyObjects.RemoveAt(i);
                continue;
            }
            c.SetActive(!inMeeting);
            var basePos = c.transform.parent ? c.transform.position : c.transform.position;
            c.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2f + i) * 15f);

            if (!inMeeting && Vector2.Distance(me, c.transform.position) <= CollectRange) Collect(i);
        }
    }

    private void SpawnCandies()
    {
        foreach (var c in candyObjects)
            if (c) Object.Destroy(c);
        candyObjects.Clear();
        spawnedFor = ShipStatus.Instance.Pointer;

        // Neues Schiff = frische Runde: Fortschritt/Schilde zurücksetzen.
        stacks.Clear();
        shieldUntil.Clear();
        if (shieldButton != null) shieldButton.Timer = 0f;
        rushing = false;

        // Immer 1 weniger als nötig liegt auf der Map - das letzte (und weitere) kommen über erledigte Aufgaben dazu.
        var spots = PickSpots(Mathf.Max(1, candies.Int - 1));
        total = candies.Int;   // benötigte Anzahl für den Zuckerrausch (Anzeige x/total)
        collected = 0;
        lastTasksDone = CompletedTasks(PlayerControl.LocalPlayer); // ab jetzt zählen neue Aufgaben
        var sprite = ButtonIcons.Get("candy", null);
        foreach (var pos in spots)
        {
            var go = new GameObject("Spieleabend_Candy");
            go.transform.SetParent(ShipStatus.Instance.transform, true);
            go.transform.position = new Vector3(pos.x, pos.y, pos.y / 1000f - 0.01f);
            go.transform.localScale = new Vector3(CandyScale, CandyScale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite ? sprite : CandySprite();
            candyObjects.Add(go);
        }
    }

    /// <summary>
    /// Wählt bis zu count Bonbon-Stellen aus: zufällig gemischt, mind. 3 Einheiten Abstand zu schon liegenden Bonbons,
    /// höchstens 2 pro Raum (Gänge ohne Grenze). Reicht das nicht, wird über zufällige Vents aufgefüllt.
    /// </summary>
    private List<Vector2> PickSpots(int count)
    {
        var existing = candyObjects.Where(c => c).Select(c => (Vector2)c.transform.position).ToList();
        var roomCount = new Dictionary<int, int>();
        foreach (var e in existing) { int r = RoomIndex(e); if (r >= 0) roomCount[r] = roomCount.TryGetValue(r, out var n) ? n + 1 : 1; }

        var result = new List<Vector2>();
        bool Ok(Vector2 s) => existing.All(e => (e - s).sqrMagnitude >= 9f) && result.All(e => (e - s).sqrMagnitude >= 9f);
        void TakeRoom(Vector2 s) { int r = RoomIndex(s); if (r >= 0) roomCount[r] = roomCount.TryGetValue(r, out var n) ? n + 1 : 1; }
        bool RoomFree(Vector2 s) { int r = RoomIndex(s); return r < 0 || (roomCount.TryGetValue(r, out var n) ? n : 0) < 2; }

        // Kandidaten zufällig mischen (nicht immer dieselben "besten" Ecken -> sonst landet alles z. B. in der Cafeteria).
        foreach (var s in FindHidingSpots(Mathf.Max(count * 4, count + 10)).OrderBy(_ => Random.value))
        {
            if (result.Count >= count) break;
            if (Ok(s) && RoomFree(s)) { result.Add(s); TakeRoom(s); }
        }

        // Notfall: über zufällige Vents auffüllen.
        if (result.Count < count)
        {
            var ship = ShipStatus.Instance;
            var mask = Constants.ShipAndObjectsMask;
            if (ship && ship.AllVents != null)
                foreach (var v in ship.AllVents.ToArray().Where(x => x).OrderBy(_ => Random.value))
                {
                    if (result.Count >= count) break;
                    Vector2 basePos = (Vector2)v.transform.position + (Vector2)v.Offset;
                    for (int tryi = 0; tryi < 8; tryi++)
                    {
                        float ang = Random.Range(0f, Mathf.PI * 2f);
                        Vector2 p = basePos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(0.6f, 1.6f);
                        if (Physics2D.OverlapCircle(p, 0.4f, mask)) continue;
                        if (!Ok(p) || !RoomFree(p)) continue;
                        result.Add(p); TakeRoom(p);
                        break;
                    }
                }
        }
        return result;
    }

    /// <summary>Index des Raums, in dem die Stelle liegt (oder -1 für Gang/Flur).</summary>
    private static int RoomIndex(Vector2 pos)
    {
        var ship = ShipStatus.Instance;
        if (!ship || ship.AllRooms == null) return -1;
        for (int i = 0; i < ship.AllRooms.Count; i++)
        {
            var r = ship.AllRooms[i];
            if (r && r.roomArea && r.roomArea.OverlapPoint(pos)) return i;
        }
        return -1;
    }

    /// <summary>
    /// Verstecke: Von sicher begehbaren Punkten (Vents, Start- und Dummy-Punkte) aus werden Strahlen in zufaellige
    /// Richtungen geschickt. Wo ein Strahl auf eine Wand trifft, kommt das Bonbon kurz davor hin - also an den Rand
    /// des Raums und garantiert innerhalb der Map. Bevorzugt werden Stellen weit weg von Aufgaben und Vents (Ecken).
    /// </summary>
    private static List<Vector2> FindHidingSpots(int count)
    {
        var ship = ShipStatus.Instance;
        var mask = Constants.ShipAndObjectsMask;

        var anchors = new List<Vector2>();
        foreach (var vent in ship.AllVents)
            if (vent) anchors.Add(vent.transform.position + vent.Offset);
        if (ship.DummyLocations != null)
            foreach (var t in ship.DummyLocations)
                if (t) anchors.Add(t.position);
        anchors.Add(ship.InitialSpawnCenter);
        anchors.Add(ship.MeetingSpawnCenter);

        var busy = new List<Vector2>(anchors);
        foreach (var console in ship.AllConsoles)
            if (console) busy.Add(console.transform.position);

        var candidates = new List<(Vector2 Pos, float Score)>();
        foreach (var anchor in anchors)
        {
            if (Physics2D.OverlapCircle(anchor, 0.1f, mask)) continue; // Startpunkt selbst muss frei sein
            for (int i = 0; i < 36; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var hit = Physics2D.Raycast(anchor, dir, 12f, mask);
                if (!hit.collider || hit.distance < 1.6f) continue;               // keine Wand in Reichweite / zu nah

                var pos = anchor + dir * (hit.distance - 1.0f);                     // deutlich vor der Wand (nicht in der Wand)
                if (Physics2D.OverlapCircle(pos, 0.55f, mask)) continue;           // genug Abstand zu Wand/Moebeln
                float score = busy.Min(x => (x - pos).sqrMagnitude);                // weit weg von Aufgaben = Ecke
                candidates.Add((pos, score));
            }
        }

        var chosen = new List<Vector2>();
        foreach (var c in candidates.OrderByDescending(c => c.Score))
        {
            if (chosen.Count >= count) break;
            if (chosen.Any(x => (x - c.Pos).sqrMagnitude < 16f)) continue;        // mindestens 4 Einheiten Abstand
            chosen.Add(c.Pos);
        }
        foreach (var c in candidates.OrderByDescending(c => c.Score))             // notfalls enger auffuellen
        {
            if (chosen.Count >= count) break;
            if (!chosen.Any(x => (x - c.Pos).sqrMagnitude < 4f)) chosen.Add(c.Pos);
        }
        return chosen;
    }

    /// <summary>Anzahl erledigter (echter, nicht Sabotage-)Aufgaben des Spielers.</summary>
    private static int CompletedTasks(PlayerControl p)
    {
        int done = 0;
        if (p && p.myTasks != null)
            foreach (var t in p.myTasks)
                if (t && !t.TryCast<SabotageTask>() && t.IsComplete) done++;
        return done;
    }

    /// <summary>
    /// Nach jeder frisch erledigten Aufgabe erscheint 1 neuer Bonbon an einer sicheren versteckten Stelle (nie in der Wand).
    /// Er zaehlt mit - so kommt das Kind schneller zum Zuckerrausch.
    /// </summary>
    private void CheckTasks(PlayerControl local)
    {
        int done = CompletedTasks(local);
        if (done <= lastTasksDone) { lastTasksDone = done; return; }

        int add = done - lastTasksDone;
        lastTasksDone = done;

        var spots = PickSpots(add);
        var sprite = ButtonIcons.Get("candy", null);
        for (int i = 0; i < add && i < spots.Count; i++)
        {
            var go = new GameObject("Spieleabend_TaskCandy");
            go.transform.SetParent(ShipStatus.Instance.transform, true);
            go.transform.position = new Vector3(spots[i].x, spots[i].y, spots[i].y / 1000f - 0.01f);
            go.transform.localScale = new Vector3(CandyScale, CandyScale, 1f);
            go.AddComponent<SpriteRenderer>().sprite = sprite ? sprite : CandySprite();
            candyObjects.Add(go);
        }
        Notifier.Show($"<color={ColorHex}>{Lang.T("Ein neuer Bonbon ist aufgetaucht!", "A new candy appeared!")}</color>", 3f);
    }

    private void Collect(int index)
    {
        var c = candyObjects[index];
        candyObjects.RemoveAt(index);
        ProtectionVisuals.ShowBurst(PlayerControl.LocalPlayer, Color);
        Object.Destroy(c);

        if (!rushing)
        {
            collected++;
            if (collected < total)
            {
                Sfx.Play(Sfx.TaskUpdate);
                Notifier.Show($"<color={ColorHex}>{Lang.T("Bonbon", "Candy")} {collected}/{total}</color>", 2f);
                return;
            }
            // Zuckerrausch erreicht: ab jetzt schneller; jedes weitere Bonbon gibt einen Schild.
            rushing = true;
            Sfx.Play(Sfx.TaskComplete);
            Notifier.Show($"<color={ColorHex}>{Lang.T("Zuckerrausch! Du bist schneller.", "Sugar rush! You are faster.")}{(rushShield.Bool ? Lang.T(" Weitere Bonbons = Schilde.", " More candy = shields.") : "")}</color>", 5f);
            return;
        }

        // Nach dem Rausch: jedes Bonbon = eine Schild-Ladung (an alle, damit auch der Host sie kennt)
        if (rushShield.Bool)
        {
            var l = PlayerControl.LocalPlayer;
            if (l) SendToAll(ActionAddStack, l.PlayerId);
        }
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (data.Length == 0) return;
        var local = PlayerControl.LocalPlayer;

        if (action == ActionAddStack)
        {
            stacks[data[0]] = Stacks(data[0]) + 1;
            if (local && local.PlayerId == data[0]) { Sfx.Play(Sfx.TaskUpdate); Notifier.Show($"<color={ColorHex}>{Lang.T("Schild gesammelt", "Shield collected")} ({Stacks(data[0])})</color>", 2f); }
        }
        else if (action == ActionShieldOn)
        {
            stacks[data[0]] = Mathf.Max(0, Stacks(data[0]) - 1);   // Aktivierung verbraucht eine Ladung
            shieldUntil[data[0]] = Time.time + shieldDuration.Value;
            if (local && local.PlayerId == data[0])
            {
                ProtectionVisuals.ShowBurst(local, Color);
                Sfx.Play(Sfx.Protect);
                if (shieldButton != null) shieldButton.Timer = shieldDuration.Value + relock.Value; // Radial/Label: erst aktiv, dann Sperre
                Notifier.Show($"<color={ColorHex}>{Lang.T($"Schild aktiv - {shieldDuration.Int}s unverwundbar!", $"Shield active - invulnerable for {shieldDuration.Int}s!")}</color>", 4f);
            }
        }
    }

    public override KillIntercept InterceptKill(PlayerControl killer, PlayerControl target)
    {
        if (killer == target || !target || !ActiveShield(target)) return KillIntercept.None;
        // Kein "Schild weg" - der Kill scheitert nur sichtbar, der Killer-Cooldown startet neu. Der Schutz bleibt die 15s.
        try { killer.RpcMurderPlayer(target, false); } catch { }
        return KillIntercept.Handled;
    }

    public override IEnumerable<(PlayerControl Target, Color Color)> GetProtectionBubbles(PlayerControl viewer)
    {
        if (viewer && ActiveShield(viewer)) yield return (viewer, Color);
    }

    public override string GetNameMark(PlayerControl viewer, PlayerControl target)
    {
        if (viewer != target || !IsLocalRole) return null;
        if (!rushing && total > 0) return $"<color={ColorHex}>[{collected}/{total}]</color>";
        int s = Stacks(target.PlayerId);
        if (rushing && s > 0) return $"<color={ColorHex}>[{s} Schild]</color>";
        return null;
    }

    private static Sprite CandySprite()
    {
        if (candySprite) return candySprite;
        const int W = 96, H = 48;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        var outline = new Color(0.12f, 0.08f, 0.12f, 1f);
        var pink = new Color(1f, 0.45f, 0.75f, 1f);
        var white = new Color(1f, 0.95f, 0.98f, 1f);
        var wrap = new Color(1f, 0.85f, 0.3f, 1f);
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            Color c = Color.clear;
            float dx = x - 47.5f, dy = y - 23.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            // Bonbon-Kugel mit Streifen
            if (d <= 18f)
            {
                c = Mathf.Repeat((dx + dy) / 8f, 2f) < 1f ? pink : white;
                if (d > 15.5f) c = outline;
            }
            // Papier links/rechts (Dreiecke)
            float wx = Mathf.Abs(dx) - 16f;
            if (c.a == 0f && wx > 0f && wx < 28f && Mathf.Abs(dy) <= wx * 0.75f + 3f)
                c = Mathf.Abs(dy) > wx * 0.75f || wx > 25f ? outline : wrap;
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        candySprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 140f);
        candySprite.hideFlags = HideFlags.HideAndDontSave;
        return candySprite;
    }
}
