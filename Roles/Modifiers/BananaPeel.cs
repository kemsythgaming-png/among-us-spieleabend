using System;
using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles.Modifiers;

/// <summary>
/// Anpassung (alle Teams): Knopf, mit dem man jederzeit eine Bananenschale ablegt. Wer darueberlaeuft, rutscht aus und
/// ist 2 s langsamer (ueber seinem Kopf blitzt "BANANA!" auf). Alle sehen die Schale, aber nicht, wer sie gelegt hat.
/// Hoechstens 2 Schalen je Spieler, alte verschwinden nach 15 s. Kein Schaden, kein Kill - nur Chaos.
/// </summary>
public sealed class BananaPeel : CustomRole
{
    public override string ConfigSection => "Bananenschale";
    public override string Name => "Banana";
    public override Color Color => new Color32(245, 220, 70, 255);
    public override RoleTeam Team => RoleTeam.Modifier;
    public override string IntroText => Lang.T("Leg sie aufs Glatteis", "Lay a slippery trap");
    public override string Description => Lang.T("Leg Bananenschalen - wer drauftritt, rutscht aus.", "Drop banana peels - whoever steps on one slips.");
    public override string LongDescription => Lang.T(
        "Anpassung für alle Teams: Mit dem Knopf legst du an deiner Stelle eine Bananenschale ab. Der nächste Spieler, der darüberläuft, rutscht aus und ist 2 Sekunden langsamer. Alle sehen die Schale, aber nicht, wer sie gelegt hat.\n\nHöchstens 2 Schalen gleichzeitig, alte verschwinden nach 15 Sekunden. Kein Schaden, kein Kill - nur um Fluchtwege zu sabotieren.",
        "Modifier for all teams: the button drops a banana peel at your spot. The next player who walks over it slips and is slowed for 2 seconds. Everyone sees the peel, but not who placed it.\n\nAt most 2 peels at once, old ones vanish after 15 seconds. No damage, no kills - just to sabotage escape routes.");

    private const byte ActionPlace = 0, ActionTrip = 1;
    private const float PeelLife = 15f, SlipTime = 2f, SlipFactor = 0.45f, TripRadius = 0.5f, MaxPeels = 2, ArmDelay = 2.5f;

    private RoleOption cooldown;

    private sealed class Peel { public byte Placer; public byte Seq; public GameObject Obj; public float SpawnTime; public float ArmAt; }

    // bei allen Clients: alle liegenden Schalen (Schluessel = Platzierer<<8 | Seq)
    private readonly Dictionary<int, Peel> peels = new();
    private byte nextSeq;                 // lokaler Zaehler des Platzierers
    private float slipUntil;              // lokaler Spieler rutscht bis dahin
    private readonly List<(GameObject Go, float Until)> bananaTexts = new();
    private static Sprite peelSprite;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 25f, 5f, 60f, 2.5f, "s");

        AddButton(new RoleButton
        {
            Label = "Bananenschale|Banana peel",
            GameIcon = _ => PeelSprite(),
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            OnClick = _ =>
            {
                var local = PlayerControl.LocalPlayer;
                if (!local) return;
                var pos = local.GetTruePosition();
                byte seq = nextSeq++;
                var data = new byte[10];
                data[0] = local.PlayerId;
                data[1] = seq;
                BitConverter.GetBytes(pos.x).CopyTo(data, 2);
                BitConverter.GetBytes(pos.y).CopyTo(data, 6);
                SendToAll(ActionPlace, data);
            },
        });
    }

    public override void ResetState()
    {
        foreach (var p in peels.Values) if (p.Obj) Object.Destroy(p.Obj);
        peels.Clear();
        foreach (var (go, _) in bananaTexts) if (go) Object.Destroy(go);
        bananaTexts.Clear();
        slipUntil = 0f;
        nextSeq = 0;
    }

    public override float SpeedMultiplier(PlayerControl local) =>
        Time.time < slipUntil && IsAlive(local) ? SlipFactor : 1f;

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionPlace && data.Length >= 10)
        {
            byte placer = data[0], seq = data[1];
            float x = BitConverter.ToSingle(data, 2), y = BitConverter.ToSingle(data, 6);
            SpawnPeel(placer, seq, new Vector2(x, y));
        }
        else if (action == ActionTrip && data.Length >= 3)
        {
            byte tripper = data[0], placer = data[1], seq = data[2];
            RemovePeel(Key(placer, seq));
            ShowBanana(FindPlayer(tripper));
        }
    }

    private static int Key(byte placer, byte seq) => (placer << 8) | seq;

    private void SpawnPeel(byte placer, byte seq, Vector2 pos)
    {
        int key = Key(placer, seq);
        RemovePeel(key);

        var go = new GameObject("Spieleabend_BananaPeel");
        if (ShipStatus.Instance) go.transform.SetParent(ShipStatus.Instance.transform, true);
        go.transform.position = new Vector3(pos.x, pos.y, pos.y / 1000f - 0.01f); // genau an der Stelle, an der man steht
        go.transform.localScale = Vector3.one * 0.6f;
        go.AddComponent<SpriteRenderer>().sprite = PeelSprite();
        peels[key] = new Peel { Placer = placer, Seq = seq, Obj = go, SpawnTime = Time.time, ArmAt = Time.time + ArmDelay };

        // Hoechstens 2 je Spieler: aelteste entfernen
        var mine = peels.Values.Where(p => p.Placer == placer).OrderBy(p => p.SpawnTime).ToList();
        while (mine.Count > MaxPeels)
        {
            RemovePeel(Key(mine[0].Placer, mine[0].Seq));
            mine.RemoveAt(0);
        }
    }

    private void RemovePeel(int key)
    {
        if (peels.TryGetValue(key, out var p))
        {
            if (p.Obj) Object.Destroy(p.Obj);
            peels.Remove(key);
        }
    }

    public override void Update()
    {
        float t = Time.time;

        // Alte Schalen verschwinden
        foreach (var key in peels.Keys.ToList())
            if (t - peels[key].SpawnTime > PeelLife) RemovePeel(key);

        // "BANANA!"-Texte ausblenden
        for (int i = bananaTexts.Count - 1; i >= 0; i--)
        {
            if (bananaTexts[i].Until <= t || !bananaTexts[i].Go)
            {
                if (bananaTexts[i].Go) Object.Destroy(bananaTexts[i].Go);
                bananaTexts.RemoveAt(i);
            }
        }

        // Rutscht der lokale Spieler gerade ueber eine Schale?
        var local = PlayerControl.LocalPlayer;
        if (!local || !IsAlive(local) || InMeeting || t < slipUntil || !local.CanMove) return;
        Vector2 me = local.GetTruePosition();
        foreach (var p in peels.Values)
        {
            if (!p.Obj || t < p.ArmAt) continue; // erst nach kurzer Zeit "scharf" (Platzierer rutscht nicht sofort selbst)
            if (Vector2.Distance(me, p.Obj.transform.position) > TripRadius) continue;
            slipUntil = t + SlipTime;
            Sfx.Play(Sfx.Fail);
            SendToAll(ActionTrip, local.PlayerId, p.Placer, p.Seq);
            break;
        }
    }

    /// <summary>"BANANA!" kurz ueber dem Kopf eines Spielers (bei allen sichtbar).</summary>
    private void ShowBanana(PlayerControl who)
    {
        if (!who || !who.cosmetics || !who.cosmetics.nameText) return;
        try
        {
            var src = who.cosmetics.nameText;
            var go = Object.Instantiate(src.gameObject, src.transform.parent);
            go.name = "Spieleabend_Banana";
            foreach (var tr in go.GetComponents<TextTranslatorTMP>()) Object.Destroy(tr);
            var tmp = go.GetComponent<TMPro.TextMeshPro>();
            tmp.text = "BANANA!";
            tmp.color = new Color(1f, 0.85f, 0.1f);
            tmp.fontSize = src.fontSize * 1.3f;
            go.transform.localPosition = src.transform.localPosition + new Vector3(0f, 0.55f, -0.1f);
            go.SetActive(true);
            bananaTexts.Add((go, Time.time + 1.5f));
        }
        catch (Exception e) { Plugin.Logger.LogWarning($"BANANA-Text: {e.Message}"); }
    }

    /// <summary>Banane (dicke gelbe Krumme mit braunen Enden) - im Code erzeugt.</summary>
    private static Sprite PeelSprite()
    {
        if (peelSprite) return peelSprite;
        const int S = 96;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        var yellow = new Color(0.98f, 0.84f, 0.12f);
        var darker = new Color(0.80f, 0.63f, 0.07f);
        var brown = new Color(0.42f, 0.28f, 0.10f);
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            var c = new Vector2(x - S / 2f, y - S / 2f) / (S / 2f);
            // Dicke untere Sichel = Bananenform (zwei versetzte Kreise, nur die untere Haelfte)
            float outer = (c - new Vector2(0f, 0.15f)).magnitude;
            float inner = (c - new Vector2(0f, 0.62f)).magnitude;
            bool inside = outer < 0.9f && inner > 0.6f && c.y < 0.45f;
            Color col = Color.clear;
            if (inside)
            {
                float edge = Mathf.Min(Mathf.Min(0.9f - outer, inner - 0.6f), 0.45f - c.y);
                bool tip = Mathf.Abs(c.x) > 0.46f;         // braune Enden
                col = tip ? brown : (edge < 0.07f ? darker : yellow);
            }
            tex.SetPixel(x, y, col);
        }
        tex.Apply();
        peelSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        peelSprite.hideFlags = HideFlags.HideAndDontSave;
        return peelSprite;
    }
}
