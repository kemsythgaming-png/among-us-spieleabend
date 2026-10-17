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
        "Halloween-Crew. In versteckten Ecken der Map liegen Bonbons, die nur du siehst. Lauf drüber, um sie einzusammeln. Bei jeder Sabotage springen 2-3 Extra-Bonbons an den Reparatur-Stellen heraus - die zählen mit.\n\nHast du genug, bekommst du den Zuckerrausch: Du bist für den Rest der Runde schneller und ein Schild schützt dich vor dem nächsten Angriff.",
        "Halloween crew. Candy is hidden in corners of the map and only you can see it. Walk over it to collect it. Every sabotage drops 2-3 extra candies at the repair spots - they count too.\n\nOnce you have enough, you get a sugar rush: you are faster for the rest of the game and a shield protects you from the next attack.");

    private const byte ActionRush = 0, ActionShieldUsed = 1;
    private const float CollectRange = 0.85f;
    private const float CandyScale = 0.35f;

    private RoleOption candies, rushSpeed, rushShield;

    // lokal beim Kind
    private readonly List<GameObject> candyObjects = new();
    private System.IntPtr spawnedFor;
    private int collected, total;
    private bool rushing;
    private readonly HashSet<System.IntPtr> seenSabotages = new();

    // bei allen: wer hat (noch) einen Zucker-Schild
    private readonly HashSet<byte> shielded = new();

    private static Sprite candySprite;

    protected override void Setup()
    {
        candies = AddOption("Candies", "Bonbons|Candies", 8f, 3f, 20f, 1f);
        rushSpeed = AddOption("RushSpeed", "Zuckerrausch-Tempo|Sugar rush speed", 1.3f, 1.1f, 2f, 0.05f, "x");
        rushShield = AddToggle("RushShield", "Zuckerrausch-Schild|Sugar rush shield", true);
    }

    public override void ResetState()
    {
        foreach (var c in candyObjects)
            if (c) Object.Destroy(c);
        candyObjects.Clear();
        spawnedFor = System.IntPtr.Zero;
        collected = total = 0;
        shielded.Clear();
        seenSabotages.Clear();

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
        if (!rushing) CheckSabotages(local);


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

        var spots = FindHidingSpots(candies.Int);
        total = spots.Count;
        collected = 0;
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

                var pos = anchor + dir * (hit.distance - 0.8f);                     // ein Stueck vor der Wand (nicht in der Wand)
                if (Physics2D.OverlapCircle(pos, 0.45f, mask)) continue;           // genug Abstand zu Wand/Moebeln
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

    /// <summary>
    /// Neue Sabotage: 2-3 Extra-Bonbons springen an zufaelligen Reparatur-Stellen heraus. Sie zaehlen normal mit -
    /// so kommt das Kind schneller zum Zuckerrausch, wenn es zu den Sabotagen rennt.
    /// </summary>
    private void CheckSabotages(PlayerControl local)
    {
        foreach (var task in local.myTasks)
        {
            if (!task || !task.TryCast<SabotageTask>()) continue;
            if (!seenSabotages.Add(task.Pointer)) continue;

            var consoles = ShipStatus.Instance.AllConsoles.Where(c => c && c.TaskTypes != null && c.TaskTypes.Contains(task.TaskType)).ToList();
            if (consoles.Count == 0) continue;
            int count = Random.Range(2, 4);
            var sprite = ButtonIcons.Get("candy", null);
            for (int i = 0; i < count; i++)
            {
                var console = consoles[Random.Range(0, consoles.Count)];
                if (!TryFreeSpotNear(console.transform.position, out var pos)) continue; // kein freier Platz -> lieber kein Bonbon als eins in der Wand
                var go = new GameObject("Spieleabend_SabotageCandy");
                go.transform.SetParent(ShipStatus.Instance.transform, true);
                go.transform.position = new Vector3(pos.x, pos.y, pos.y / 1000f - 0.01f);
                go.transform.localScale = new Vector3(CandyScale, CandyScale, 1f);
                go.AddComponent<SpriteRenderer>().sprite = sprite ? sprite : CandySprite();
                candyObjects.Add(go);
            }
            ProtectionVisuals.ShowBurst(local, Color);
            Notifier.Show($"<color={ColorHex}>{Lang.T("Sabotage! Bei der Reparatur liegen Extra-Bonbons.", "Sabotage! Extra candy is waiting at the repair spots.")}</color>", 4f);
        }
    }

    /// <summary>Freier Platz neben einer Konsole (nicht in Waenden oder Moebeln). false = kein freier Platz gefunden.</summary>
    private static bool TryFreeSpotNear(Vector2 center, out Vector2 spot)
    {
        var mask = Constants.ShipAndObjectsMask;
        for (int i = 0; i < 30; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            var pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(0.6f, 1.3f);
            // Platz frei (nicht in Wand/Moebel) UND keine Wand zwischen Konsole und Platz (sonst liegt das Bonbon hinter der Wand)
            if (!Physics2D.OverlapCircle(pos, 0.35f, mask) && !PhysicsHelpers.AnythingBetween(center, pos, mask, false))
            {
                spot = pos;
                return true;
            }
        }
        spot = center;
        return false;
    }

    private void Collect(int index)
    {
        var c = candyObjects[index];
        candyObjects.RemoveAt(index);
        ProtectionVisuals.ShowBurst(PlayerControl.LocalPlayer, Color);
        Object.Destroy(c);
        collected++;

        if (collected < total)
        {
            Sfx.Play(Sfx.TaskUpdate);
            Notifier.Show($"<color={ColorHex}>{Lang.T("Bonbon", "Candy")} {collected}/{total}</color>", 2f);
            return;
        }

        Sfx.Play(Sfx.TaskComplete);
        Notifier.Show($"<color={ColorHex}>{Lang.T("Zuckerrausch! Du bist schneller", "Sugar rush! You are faster")}{(rushShield.Bool ? Lang.T(" und geschützt!", " and shielded!") : "!")}</color>", 5f);
        rushing = true;
        SendToAll(ActionRush);

        // Restliche Bonbons (z. B. Extra-Bonbons aus Sabotagen) braucht es nicht mehr
        foreach (var rest in candyObjects)
            if (rest) Object.Destroy(rest);
        candyObjects.Clear();
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionRush && RoleRegistry.Has(sender, this) && rushShield.Bool) shielded.Add(sender.PlayerId);
        else if (action == ActionShieldUsed && data.Length > 0)
        {
            shielded.Remove(data[0]);
            var local = PlayerControl.LocalPlayer;
            if (local && local.PlayerId == data[0])
            {
                ProtectionVisuals.ShowBurst(local, Color);
                Sfx.Play(Sfx.Protect);
            }
        }
    }

    public override KillIntercept InterceptKill(PlayerControl killer, PlayerControl target)
    {
        if (!shielded.Contains(target.PlayerId) || killer == target) return KillIntercept.None;
        killer.RpcMurderPlayer(target, false);
        SendToAll(ActionShieldUsed, target.PlayerId);
        return KillIntercept.Handled;
    }

    public override IEnumerable<(PlayerControl Target, Color Color)> GetProtectionBubbles(PlayerControl viewer)
    {
        if (viewer && shielded.Contains(viewer.PlayerId)) yield return (viewer, Color);
    }

    public override string GetNameMark(PlayerControl viewer, PlayerControl target) =>
        viewer == target && IsLocalRole && total > 0 && !rushing ? $"<color={ColorHex}>[{collected}/{total}]</color>" : null;

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
