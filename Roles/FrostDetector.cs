using System;
using System.Collections.Generic;
using System.Text;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Crew (Detektor): setzt einen unsichtbaren Erkennungs-Bereich auf die Karte. Benutzt dort jemand eine Faehigkeit
/// (Rollen-Knopf), merkt sich der Detektor, WELCHE Faehigkeit das war - und bekommt im naechsten Meeting eine
/// Chat-Info darueber (nur er). Wer es war, erfaehrt er nicht.
/// </summary>
public sealed class FrostDetector : CustomRole
{
    public override string ConfigSection => "Detektor";
    public override string Name => Lang.T("Detektor", "Detector");
    public override Color Color => new Color32(140, 220, 240, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Spuren im Schnee", "Traces in the snow");
    public override string Description => Lang.T("Setz einen Bereich (F). Faehigkeiten\ndarin siehst du im Meeting.", "Place a zone (F). Abilities\ninside show up in the meeting.");
    public override string LongDescription => Lang.T(
        "Du bist Crew. Mit Taste F setzt du an deiner Stelle einen unsichtbaren Erkennungs-Bereich (nur du siehst den Ring). Benutzt dort jemand eine Fähigkeit, merkt sich dein Detektor, welche es war. Im nächsten Meeting bekommst du eine Chat-Info, welche Fähigkeiten in deinem Bereich benutzt wurden - aber nicht, wer es war. Der Bereich bleibt, bis du ihn neu setzt.",
        "You are crew. Press F to place an invisible detection zone at your spot (only you see the ring). If someone uses an ability there, your detector remembers which one. In the next meeting you get a chat note which abilities were used in your zone - but not who did it. The zone stays until you place a new one.");

    private const byte ActionPing = 0;

    internal static FrostDetector Instance { get; private set; }
    private static bool hooked;

    private RoleOption cooldown, radius;

    // nur beim Detektor: aktueller Bereich + gemerkte Faehigkeiten (Name) seit dem letzten Meeting
    private bool hasZone;
    private Vector2 zone;
    private readonly List<string> seen = new();
    private GameObject ring;

    protected override void Setup()
    {
        Instance = this;
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 20f, 5f, 60f, 5f, "s");
        radius = AddOption("Radius", "Bereichsgröße|Zone size", 3.5f, 1.5f, 8f, 0.5f);

        if (!hooked)
        {
            RoleButton.Used += role => Instance?.OnAbilityUsed(role);
            hooked = true;
        }

        AddButton(new RoleButton
        {
            Label = "Bereich|Zone",
            GameIcon = _ => ScanSprite(),
            Icon = "radar",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 5f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer),
            OnClick = _ => PlaceZone(),
        });
    }

    public override void ResetState()
    {
        Instance = this;
        hasZone = false;
        seen.Clear();
        if (ring) Object.Destroy(ring);
        ring = null;
    }

    private void PlaceZone()
    {
        var local = PlayerControl.LocalPlayer;
        if (!IsAlive(local)) return;
        zone = local.GetTruePosition();
        hasZone = true;
        Sfx.Play(Sfx.TaskUpdate, 0.5f);
    }

    /// <summary>Laeuft beim benutzenden Spieler: Ort + Name der benutzten Faehigkeit anonym an alle melden.</summary>
    private void OnAbilityUsed(CustomRole role)
    {
        if (role == this || role == null) return; // das Setzen des Bereichs zaehlt nicht
        var local = PlayerControl.LocalPlayer;
        if (!IsAlive(local)) return;
        var pos = local.GetTruePosition();
        var name = Encoding.UTF8.GetBytes(role.Name ?? "?");
        var data = new byte[8 + name.Length];
        BitConverter.GetBytes(pos.x).CopyTo(data, 0);
        BitConverter.GetBytes(pos.y).CopyTo(data, 4);
        name.CopyTo(data, 8);
        SendToAll(ActionPing, data);
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionPing || data.Length < 8) return;
        if (!IsLocalRole || !hasZone) return; // nur der Detektor mit gesetztem Bereich wertet aus
        var pos = new Vector2(BitConverter.ToSingle(data, 0), BitConverter.ToSingle(data, 4));
        if (Vector2.Distance(pos, zone) > radius.Value) return;
        string name = data.Length > 8 ? Encoding.UTF8.GetString(data, 8, data.Length - 8) : "?";
        seen.Add(name);
        if (seen.Count > 50) seen.RemoveAt(0);
    }

    public override void OnMeetingStart()
    {
        if (!IsLocalRole) return;
        if (hasZone && seen.Count > 0)
        {
            // doppelte Faehigkeiten zusammenfassen (z. B. "Biss ×2")
            var counts = new Dictionary<string, int>();
            foreach (var s in seen) { counts.TryGetValue(s, out int n); counts[s] = n + 1; }
            var parts = new List<string>();
            foreach (var kv in counts) parts.Add(kv.Value > 1 ? $"{kv.Key} ×{kv.Value}" : kv.Key);
            ChatNote.Message($"<color={ColorHex}>{Lang.T("Detektor - im Bereich benutzt:", "Detector - used in zone:")} {string.Join(", ", parts)}</color>");
        }
        else if (hasZone)
        {
            ChatNote.Message($"<color=#9AA0AE>{Lang.T("Detektor - im Bereich wurde nichts benutzt.", "Detector - nothing used in zone.")}</color>");
        }
        seen.Clear();
    }

    public override void Update()
    {
        // Ring des Bereichs nur fuer den Detektor zeigen
        bool show = IsLocalRole && hasZone && IsAlive(PlayerControl.LocalPlayer) && !InMeeting && ShipStatus.Instance;
        if (show)
        {
            if (!ring)
            {
                ring = new GameObject("Spieleabend_DetectorZone");
                ring.transform.SetParent(ShipStatus.Instance.transform, false);
                var sr = ring.AddComponent<SpriteRenderer>();
                sr.sprite = Shapes.Ring();
                sr.color = new Color(Color.r, Color.g, Color.b, 0.35f);
            }
            ring.SetActive(true);
            float d = radius.Value; // halbierter Ring (vorher Durchmesser = radius×2)
            ring.transform.position = new Vector3(zone.x, zone.y, zone.y / 1000f + 0.1f);
            ring.transform.localScale = new Vector3(d, d, 1f);
        }
        else if (ring) ring.SetActive(false);
    }

    // ---------- Eigenes Scanner-Symbol (konzentrische Ringe mit Punkt) ----------
    private static Sprite scanSprite;
    private static Sprite ScanSprite()
    {
        if (scanSprite) return scanSprite;
        const int S = 96;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        var col = new Color(0.6f, 0.9f, 1f, 1f);
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float r = (new Vector2(x - S / 2f, y - S / 2f)).magnitude / (S / 2f);
            bool ring1 = Mathf.Abs(r - 0.9f) < 0.08f;
            bool ring2 = Mathf.Abs(r - 0.55f) < 0.08f;
            bool dot = r < 0.16f;
            tex.SetPixel(x, y, (ring1 || ring2 || dot) ? col : Color.clear);
        }
        tex.Apply();
        scanSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        scanSprite.hideFlags = HideFlags.HideAndDontSave;
        return scanSprite;
    }
}
