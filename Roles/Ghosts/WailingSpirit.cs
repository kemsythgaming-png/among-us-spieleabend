using System;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles.Ghosts;

/// <summary>
/// Geisterrolle: löst wie der Noisemaker einen Alarm aus - alle lebenden Spieler hören ihn und ein Pfeil zeigt
/// ein paar Sekunden lang zu der Stelle, von der der Alarm kam. Wiederholbar mit langer Abklingzeit.
/// </summary>
public sealed class WailingSpirit : CustomRole
{
    public override string ConfigSection => "Heulgeist";
    public override string Name => Lang.T("Heulgeist", "Wailing Spirit");
    public override Color Color => new Color32(150, 180, 230, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override bool IsGhostRole => true;
    public override bool CanHaunt(PlayerControl dead) => !IsImpostor(dead) && !IsNeutral(dead);
    public override string IntroText => Lang.T("Ein Heulen als Alarm", "A wail as an alarm");
    public override string Description => Lang.T("Geist: Löse einen Alarm aus (F).\nEin Pfeil zeigt kurz zu dir.", "Ghost: trigger an alarm (F).\nAn arrow briefly points to you.");
    public override string LongDescription => Lang.T(
        "Geisterrolle für tote Crewmates. Mit Taste F löst du einen Alarm aus - wie beim Noisemaker hören ihn alle lebenden Spieler und ein roter Pfeil zeigt ein paar Sekunden zu der Stelle, von der der Alarm kam. Dazwischen liegt eine lange Abklingzeit (mind. 40 Sekunden).",
        "Ghost role for dead crewmates. Press F to trigger an alarm - like the Noisemaker, every living player hears it and a red arrow points for a few seconds to where it came from. There is a long cooldown between alarms (at least 40 seconds).");

    private const byte ActionWail = 0;
    private RoleOption cooldown;

    internal static WailingSpirit Instance { get; private set; }

    private Vector2 alarmPos;
    private float alarmUntil;
    private GameObject arrow;
    private static Sprite arrowSprite;

    /// <summary>Toter Heulgeist bekommt keine Rollen-Infos.</summary>
    internal bool HidesRoles(PlayerControl viewer) => RoleRegistry.Has(viewer, this);

    protected override void Setup()
    {
        Instance = this;
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 90f, 60f, 200f, 5f, "s");

        AddButton(new RoleButton
        {
            Label = "Alarm|Alarm",
            Icon = "radar",
            NeedsTarget = false,
            UsableWhenDead = true,
            Visible = () => !IsAlive(PlayerControl.LocalPlayer),
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            OnClick = _ =>
            {
                var local = PlayerControl.LocalPlayer;
                if (!local) return;
                var pos = local.GetTruePosition();
                var data = new byte[8];
                BitConverter.GetBytes(pos.x).CopyTo(data, 0);
                BitConverter.GetBytes(pos.y).CopyTo(data, 4);
                SendToAll(ActionWail, data);
            },
        });
    }

    public override void ResetState()
    {
        alarmUntil = 0f;
        if (arrow) Object.Destroy(arrow);
        arrow = null;
    }

    public override void OnMeetingStart()
    {
        alarmUntil = 0f;
        if (arrow) arrow.SetActive(false);
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionWail || data.Length < 8 || !RoleRegistry.Has(sender, this)) return;
        alarmPos = new Vector2(BitConverter.ToSingle(data, 0), BitConverter.ToSingle(data, 4));

        // Alle hoeren ein "BOOO" und sehen den Pfeil - Lebende UND Geister, genau gleich
        alarmUntil = Time.time + 6f;
        Sfx.Play(BooClip(), 0.9f);
        Notifier.Show($"<color={ColorHex}>{Lang.T("BOO! Ein Pfeil zeigt zur Quelle.", "BOO! An arrow points to the source.")}</color>", 3f);

        // Heulender Geist an der Stelle, von der es kam (alle sehen es)
        var ghost = new GameObject("Spieleabend_WailGhost");
        if (ShipStatus.Instance) ghost.transform.SetParent(ShipStatus.Instance.transform, true);
        ghost.transform.position = new Vector3(alarmPos.x, alarmPos.y, alarmPos.y / 1000f - 0.5f);
        ghost.transform.localScale = Vector3.one * 0.7f;
        var gsr = ghost.AddComponent<SpriteRenderer>();
        gsr.sprite = ButtonIcons.Get("ghost", null);
        gsr.color = new Color(1f, 1f, 1f, 0.55f);
        Object.Destroy(ghost, 5f);
    }

    /// <summary>Tiefes, abfallendes "BOOO" (selbst erzeugt - es gibt keinen Boo-Clip im Spiel).</summary>
    private static AudioClip booClip;
    private static AudioClip BooClip()
    {
        if (booClip) return booClip;
        const int rate = 44100;
        int length = (int)(rate * 0.55f);
        var samples = new float[length];
        double phase = 0;
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)length;
            float freq = Mathf.Lerp(330f, 200f, t);                // heller: von 330 Hz nach 200 Hz ("BUH")
            float vib = 1f + 0.03f * Mathf.Sin(t * 70f);           // leichtes Vibrato
            phase += 2.0 * Mathf.PI * freq * vib / rate;
            float tone = Mathf.Sin((float)phase) + 0.25f * Mathf.Sin((float)phase * 2f);
            float env = Mathf.Min(1f, t / 0.08f) * Mathf.Min(1f, (1f - t) / 0.3f);
            samples[i] = Mathf.Clamp(tone * env * 0.65f, -1f, 1f);
        }
        booClip = AudioClip.Create("Spieleabend_Boo", length, 1, rate, false);
        booClip.SetData(samples, 0);
        booClip.hideFlags = HideFlags.HideAndDontSave;
        return booClip;
    }

    public override void Update()
    {
        var local = PlayerControl.LocalPlayer;
        bool active = local && !InMeeting && Time.time < alarmUntil; // auch Geister sehen den Pfeil
        if (!active)
        {
            if (arrow) arrow.SetActive(false);
            return;
        }
        if (!arrow) arrow = CreateArrow(local);
        arrow.SetActive(true);

        Vector2 dir = alarmPos - local.GetTruePosition();
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        arrow.transform.localPosition = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * 1.0f, Mathf.Sin(angle * Mathf.Deg2Rad) * 1.0f + 0.1f, -0.05f);
        arrow.transform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
        // große, rote, pulsierende Pfeile
        float s = 1f + 0.18f * Mathf.Sin(Time.time * 8f);
        arrow.transform.localScale = new Vector3(s, s, 1f) * 1.1f;
    }

    private static GameObject CreateArrow(PlayerControl local)
    {
        var go = new GameObject("Spieleabend_WailArrow");
        go.transform.SetParent(local.transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ArrowSprite();
        sr.color = new Color(1f, 0.25f, 0.2f, 0.95f);
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
            tex.SetPixel(x, y, !inside ? Color.clear : edge ? new Color(0.15f, 0.05f, 0.05f, 1f) : Color.white);
        }
        tex.Apply();
        arrowSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 200f);
        arrowSprite.hideFlags = HideFlags.HideAndDontSave;
        return arrowSprite;
    }
}

/// <summary>Der tote Heulgeist kann keinen Chat benutzen (Senden wird blockiert).</summary>
[HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
internal static class WailingSpiritChatPatch
{
    public static bool Prefix()
    {
        var ws = WailingSpirit.Instance;
        var local = PlayerControl.LocalPlayer;
        if (ws != null && local && RoleRegistry.Has(local, ws)) return false; // kein Senden als Heulgeist
        return true;
    }
}
