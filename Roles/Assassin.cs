using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor: markiert eine Stelle und kann spaeter per Knopf dorthin zurueckspringen. Praktisch, um nach einem Kill
/// sofort wieder ein Alibi aufzusuchen. Normaler Kill bleibt.
/// </summary>
public sealed class Assassin : CustomRole
{
    public override string ConfigSection => "Assassine";
    public override string Name => Lang.T("Assassine", "Assassin");
    public override Color Color => new Color32(150, 40, 70, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Schlag zu und verschwinde", "Strike and vanish");
    public override string Description => Lang.T("Punkt setzen (F),\nspäter zurückspringen (G).", "Set a point (F),\njump back later (G).");
    public override string LongDescription => Lang.T(
        "Impostor: Mit Taste F markierst du deine aktuelle Stelle. Mit Taste G springst du jederzeit dorthin zurück (kurze Abklingzeit). So killst du woanders und bist sofort wieder an deinem Alibi-Platz. Deinen normalen Kill hast du zusätzlich.",
        "Impostor: press F to mark your current spot. Press G to jump back there anytime (short cooldown). So you kill elsewhere and are instantly back at your alibi spot. You also keep your normal kill.");

    private RoleOption cooldown, setCooldown;
    private Vector2 mark;
    private bool hasMark;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit Rücksprung|Return cooldown", 20f, 5f, 60f, 5f, "s");
        setCooldown = AddOption("SetCooldown", "Abklingzeit Punkt setzen|Set-point cooldown", 45f, 5f, 90f, 5f, "s");

        AddButton(new RoleButton
        {
            Label = "Punkt setzen|Set point",
            GameIcon = _ => PinSprite(), // eigenes Markierungs-Symbol (Fähnchen) statt des Radar-Icons
            Icon = "radar",
            NeedsTarget = false,
            Cooldown = () => setCooldown.Value,
            StartCooldown = 5f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer),
            OnClick = _ =>
            {
                var local = PlayerControl.LocalPlayer;
                if (!local) return;
                mark = local.GetTruePosition();
                hasMark = true;
                Notifier.Show($"<color={ColorHex}>{Lang.T("Punkt gesetzt.", "Point set.")}</color>", 2f);
            },
        });

        AddButton(new RoleButton
        {
            Label = "Zurückspringen|Jump back",
            Icon = "tp",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 5f,
            Hotkey = KeyCode.G,
            Visible = () => hasMark,
            CanUse = () => hasMark && !InMeeting && IsAlive(PlayerControl.LocalPlayer),
            OnClick = _ =>
            {
                var local = PlayerControl.LocalPlayer;
                if (local && hasMark && local.NetTransform) { local.NetTransform.SnapTo(mark); Sfx.Play(Sfx.TaskUpdate, 0.6f); }
            },
        });
    }

    public override void ResetState() => hasMark = false;

    // ---------- Eigenes Markierungs-Symbol (Fähnchen auf Mast) ----------
    private static Sprite pinSprite;
    private static Sprite PinSprite()
    {
        if (pinSprite) return pinSprite;
        const int S = 96;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) tex.SetPixel(x, y, Color.clear);
        var pole = new Color(0.85f, 0.85f, 0.9f, 1f);
        var flag = new Color(0.9f, 0.25f, 0.4f, 1f);
        // Mast
        for (int y = 12; y < 84; y++) for (int x = 34; x < 40; x++) tex.SetPixel(x, y, pole);
        // Fähnchen (Dreieck nach rechts, oben)
        for (int y = 56; y < 82; y++)
        {
            int w = (82 - y); // oben breit
            for (int x = 40; x < 40 + w + 14; x++) if (x < S) tex.SetPixel(x, y, flag);
        }
        // Fuß
        for (int x = 26; x < 48; x++) { tex.SetPixel(x, 12, pole); tex.SetPixel(x, 13, pole); }
        tex.Apply();
        pinSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        pinSprite.hideFlags = HideFlags.HideAndDontSave;
        return pinSprite;
    }
}
