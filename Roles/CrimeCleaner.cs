using System.Linq;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor: kann eine Leiche in Reichweite wegputzen - sie verschwindet bei allen und kann nicht mehr gemeldet
/// werden. Praktisch, um nach einem Kill die Spuren zu beseitigen. Den normalen Kill behaelt er zusaetzlich.
/// </summary>
public sealed class CrimeCleaner : CustomRole
{
    public override string ConfigSection => "Tatortreiniger";
    public override string Name => Lang.T("Tatortreiniger", "Janitor");
    public override Color Color => new Color32(150, 120, 80, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Keine Leiche, kein Problem", "No body, no problem");
    public override string Description => Lang.T("Putz Leichen weg (F),\ndann kann niemand sie melden.", "Clean up bodies (F),\nso nobody can report them.");
    public override string LongDescription => Lang.T(
        "Du bist Impostor. Stehst du neben einer Leiche, putzt du sie mit Taste F weg - sie verschwindet bei allen und kann nicht mehr gemeldet werden. So beseitigst du nach einem Kill die Spuren. Deinen normalen Kill hast du zusätzlich.",
        "You are impostor. Standing next to a body, press F to clean it away - it disappears for everyone and can no longer be reported. So you remove the traces after a kill. You also keep your normal kill.");

    private const float CleanRange = 1.2f;
    private const byte ActionClean = 0;

    private RoleOption cooldown;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 30f, 5f, 60f, 2.5f, "s");

        AddButton(new RoleButton
        {
            Label = "Leiche wegputzen|Clean body",
            GameIcon = _ => ShovelSprite(), // Grabschaufel
            Icon = "extinguish",
            NeedsTarget = false,
            SharesKillCooldown = true, // teilt sich die Abklingzeit mit dem Kill-Knopf
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && NearbyBody() != null,
            OnClick = _ =>
            {
                var body = NearbyBody();
                if (body) SendToAll(ActionClean, body.ParentId);
            },
        });
    }

    private static DeadBody NearbyBody()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !IsAlive(local) || local.inVent) return null;
        Vector2 me = local.GetTruePosition();
        return Object.FindObjectsOfType<DeadBody>().Where(b => b && Vector2.Distance(me, b.TruePosition) <= CleanRange)
            .OrderBy(b => Vector2.Distance(me, b.TruePosition)).FirstOrDefault();
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionClean || data.Length < 1 || !RoleRegistry.Has(sender, this) || !IsAlive(sender) || InMeeting) return;

        // Leiche verschwindet bei allen
        bool found = false;
        foreach (var body in Object.FindObjectsOfType<DeadBody>())
        {
            if (!body || body.ParentId != data[0]) continue;
            Object.Destroy(body.gameObject);
            found = true;
        }
        if (!found) return; // schon weggeputzt

        // Das Pet des Opfers verschwindet mit (sonst laeuft es ohne Leiche weiter herum)
        var owner = FindPlayer(data[0]);
        if (owner && owner.cosmetics && owner.cosmetics.currentPet)
            owner.cosmetics.currentPet.gameObject.SetActive(false);

        if (sender == PlayerControl.LocalPlayer)
        {
            Sfx.Play(Sfx.TaskComplete, 0.6f);
            Notifier.Show($"<color={ColorHex}>{Lang.T("Leiche weggeputzt.", "Body cleaned.")}</color>", 2f);
        }
    }

    // ---------- Eigenes Symbol: Grabschaufel (Stiel + Blatt) ----------
    private static Sprite shovelSprite;
    private static Sprite ShovelSprite()
    {
        if (shovelSprite) return shovelSprite;
        const int S = 96;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) tex.SetPixel(x, y, Color.clear);
        var handle = new Color(0.6f, 0.42f, 0.24f, 1f);
        var metal = new Color(0.78f, 0.8f, 0.86f, 1f);

        // Diagonaler Stiel (unten-links nach oben-rechts)
        for (int t = 0; t < 52; t++)
        {
            int cx = 30 + t, cy = 44 + t;
            for (int w = -4; w <= 4; w++)
            {
                int xx = cx + w; if (xx >= 0 && xx < S && cy >= 0 && cy < S) tex.SetPixel(xx, cy, handle);
            }
        }
        // T-Griff oben rechts
        for (int x = 70; x < 90; x++) for (int y = 86; y < 92; y++) tex.SetPixel(x, y, handle);
        // Schaufelblatt unten links (abgerundetes Trapez, Spitze unten)
        for (int y = 6; y < 44; y++)
        for (int x = 8; x < 52; x++)
        {
            float nx = (x - 30f) / 22f;
            float top = 44f, bot = 6f;
            float wob = 1f - (y - bot) / (top - bot);          // unten schmaler
            if (Mathf.Abs(nx) <= 1f - 0.35f * wob && y < 42) tex.SetPixel(x, y, metal);
        }
        tex.Apply();
        shovelSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        shovelSprite.hideFlags = HideFlags.HideAndDontSave;
        return shovelSprite;
    }
}
