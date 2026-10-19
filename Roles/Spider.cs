using System;
using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor: spannt (zufaellig grosse) Spinnennetze. Wer hindurchlaeuft, ist kurz langsam; mit kleiner Chance toetet
/// das Netz ihn - auch andere Impostor. Netze bleiben eine Weile liegen.
/// </summary>
public sealed class Spider : CustomRole
{
    public override string ConfigSection => "Spinne";
    public override string Name => Lang.T("Spinne", "Spider");
    public override Color Color => new Color32(130, 100, 170, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Spinn dein Netz", "Spin your web");
    public override string Description => Lang.T("Leg Spinnennetze (F). Wer durchläuft,\nist langsam - manchmal tödlich.", "Place webs (F). Who walks through\nis slowed - sometimes deadly.");
    public override string LongDescription => Lang.T(
        "Impostor: Mit dem Knopf spannst du ein zufällig großes Spinnennetz über einen Weg. Wer hindurchläuft, ist 3 Sekunden langsam; mit kleiner Chance tötet das Netz ihn - auch andere Impostor. Das Netz bleibt 15 Sekunden liegen. Du selbst (und andere Spinnen) sind immun.",
        "Impostor: place a random-sized web. Whoever walks through is slowed for 3 seconds; with a small chance the web kills them - including other impostors. The web stays 15 seconds. You (and other spiders) are immune.");

    private const byte ActionWeb = 0, ActionTrip = 1;
    private const float SlowFactor = 0.4f;
    private static Sprite webSprite;

    private RoleOption cooldown, webLife, slowTime, killChance;

    private sealed class Web { public Vector2 Pos; public float Radius; public float Until; public GameObject Obj; }
    private readonly List<Web> webs = new();
    private float slowUntil, nextTrip;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 30f, 10f, 90f, 2.5f, "s");
        webLife = AddOption("WebLife", "Netz bleibt|Web lasts", 15f, 5f, 30f, 1f, "s");
        slowTime = AddOption("SlowTime", "Verlangsamung|Slow", 3f, 1f, 8f, 0.5f, "s");
        killChance = AddOption("KillChance", "Tödlich-Chance|Deadly chance", 10f, 0f, 100f, 5f, "%");

        AddButton(new RoleButton
        {
            Label = "Spinnennetz|Web",
            Icon = "web",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            OnClick = _ =>
            {
                var local = PlayerControl.LocalPlayer;
                if (!local) return;
                var pos = local.GetTruePosition();
                byte r = (byte)UnityEngine.Random.Range(9, 20); // Radius * 10 (0.9 - 1.9)
                var data = new byte[9];
                data[0] = r;
                BitConverter.GetBytes(pos.x).CopyTo(data, 1);
                BitConverter.GetBytes(pos.y).CopyTo(data, 5);
                SendToAll(ActionWeb, data);
            },
        });
    }

    public override void ResetState()
    {
        foreach (var w in webs) if (w.Obj) Object.Destroy(w.Obj);
        webs.Clear();
        slowUntil = nextTrip = 0f;
    }

    public override void OnMeetingStart() => ResetState();

    public override float SpeedMultiplier(PlayerControl local) =>
        slowUntil > Time.time && IsAlive(local) && !InMeeting ? SlowFactor : 1f;

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionWeb && data.Length >= 9)
        {
            float radius = data[0] / 10f;
            var pos = new Vector2(BitConverter.ToSingle(data, 1), BitConverter.ToSingle(data, 5));
            var go = new GameObject("Spieleabend_Web");
            if (ShipStatus.Instance) go.transform.SetParent(ShipStatus.Instance.transform, true);
            go.transform.position = new Vector3(pos.x, pos.y, pos.y / 1000f + 0.02f);
            go.transform.localScale = Vector3.one * (radius * 2f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f)); // jedes Netz anders ausgerichtet
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = WebSprite();
            sr.color = new Color(0.92f, 0.94f, 0.98f, UnityEngine.Random.Range(0.55f, 0.8f));
            webs.Add(new Web { Pos = pos, Radius = radius, Until = Time.time + webLife.Value, Obj = go });
        }
        else if (action == ActionTrip && AmHost && data.Length >= 1)
        {
            // Host wuerfelt, ob das Netz toetet
            var victim = FindPlayer(data[0]);
            if (!IsAlive(victim) || InMeeting) return;
            if (UnityEngine.Random.Range(0, 100) >= killChance.Int) return;
            KillSystem.ForceKill(victim, victim); // Selbst-Kill: Leiche bleibt an Ort, niemand wird teleportiert
        }
    }

    public override void Update()
    {
        float t = Time.time;
        for (int i = webs.Count - 1; i >= 0; i--)
        {
            if (t >= webs[i].Until || !webs[i].Obj)
            {
                if (webs[i].Obj) Object.Destroy(webs[i].Obj);
                webs.RemoveAt(i);
            }
        }

        var local = PlayerControl.LocalPlayer;
        if (!local || !IsAlive(local) || InMeeting || t < nextTrip) return;
        if (RoleRegistry.Has(local, this)) return; // Spinnen sind immun
        Vector2 me = local.GetTruePosition();
        foreach (var w in webs)
        {
            if (Vector2.Distance(me, w.Pos) > w.Radius) continue;
            slowUntil = t + slowTime.Value;
            nextTrip = t + slowTime.Value;
            Sfx.Play(Sfx.Fail);
            SendToHost(ActionTrip, local.PlayerId);
            break;
        }
    }

    /// <summary>Einfaches Spinnennetz (Speichen + Ringe) - im Code erzeugt.</summary>
    private static Sprite WebSprite()
    {
        if (webSprite) return webSprite;
        const int S = 128; float c = (S - 1) / 2f;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            var v = new Vector2(x - c, y - c) / c;
            float d = v.magnitude;
            float ang = Mathf.Atan2(v.y, v.x);
            // Speichen (8) + konzentrische Ringe
            float spokes = Mathf.Abs(Mathf.Sin(ang * 4f));
            float rings = Mathf.Abs(Mathf.Sin(d * 10f));
            float line = Mathf.Max(spokes > 0.96f ? 1f : 0f, rings > 0.9f ? 1f : 0f);
            float a = d < 1f ? line * 0.9f : 0f;
            tex.SetPixel(x, y, new Color(0.9f, 0.9f, 0.95f, a));
        }
        tex.Apply();
        webSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        webSprite.hideFlags = HideFlags.HideAndDontSave;
        return webSprite;
    }
}
