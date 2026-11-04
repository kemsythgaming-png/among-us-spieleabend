using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>Impostor: kann sich per Knopf fuer eine Weile schneller machen (Sprint). Normaler Kill bleibt.</summary>
public sealed class FastImpostor : CustomRole
{
    public override string ConfigSection => "SchnellerImpostor";
    public override string Name => Lang.T("Sprinter", "Sprinter");
    public override Color Color => new Color32(255, 120, 60, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Keiner läuft dir davon", "No one outruns you");
    public override string Description => Lang.T("Sprint an/aus schalten (F).", "Toggle sprint on/off (F).");
    public override string LongDescription => Lang.T(
        "Impostor: Mit Taste F schaltest du deinen Sprint an und wieder aus - solange er an ist, läufst du deutlich schneller (ideal zum Jagen oder Flüchten). Deinen normalen Kill hast du zusätzlich.",
        "Impostor: press F to toggle your sprint on and off - while on, you move much faster (great for chasing or escaping). You also keep your normal kill.");

    private RoleOption speed;
    private bool boostOn;

    protected override void Setup()
    {
        speed = AddOption("Speed", "Tempo|Speed", 1.5f, 1.1f, 2.5f, 0.1f, "x");

        AddButton(new RoleButton
        {
            Label = "Sprint|Sprint",
            GameIcon = _ => FootSprite(), // schneller Fuß statt Blitz
            Icon = "bolt",
            NeedsTarget = false,
            Cooldown = () => 0.3f, // nur kurze Klicksperre -> an/aus
            StartCooldown = 0f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer),
            OnClick = _ => { boostOn = !boostOn; Sfx.Play(Sfx.TaskUpdate, 0.6f); },
        });
    }

    public override void ResetState() => boostOn = false;
    public override void OnMeetingStart() => boostOn = false;

    public override float SpeedMultiplier(PlayerControl local) =>
        IsLocalRole && IsAlive(local) && boostOn && !InMeeting ? speed.Value : 1f;

    // ---------- Eigenes Symbol: laufender Fuß mit Tempo-Strichen ----------
    private static Sprite footSprite;
    private static Sprite FootSprite()
    {
        if (footSprite) return footSprite;
        const int S = 96;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) tex.SetPixel(x, y, Color.clear);
        var foot = new Color(0.95f, 0.9f, 0.85f, 1f);
        var speedL = new Color(1f, 0.6f, 0.3f, 0.85f);

        // Fuß (Sohle + Spann) - grob als zwei Ellipsen/Bereiche rechts im Bild
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            // Sohle (liegende Ellipse unten)
            var sole = new Vector2((x - 60f) / 30f, (y - 30f) / 10f);
            // Spann/Ferse (aufsteigend hinten)
            var heel = new Vector2((x - 44f) / 12f, (y - 44f) / 18f);
            if (sole.sqrMagnitude < 1f || heel.sqrMagnitude < 1f) tex.SetPixel(x, y, foot);
        }
        // Tempo-Striche links
        void Dash(int yy, int x0, int x1) { for (int x = x0; x <= x1; x++) for (int dy = -2; dy <= 2; dy++) { int yv = yy + dy; if (x >= 0 && x < S && yv >= 0 && yv < S) tex.SetPixel(x, yv, speedL); } }
        Dash(44, 4, 26); Dash(30, 4, 22); Dash(20, 8, 24);
        tex.Apply();
        footSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        footSprite.hideFlags = HideFlags.HideAndDontSave;
        return footSprite;
    }
}
