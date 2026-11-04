using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Crew: tauscht die Positionen von ZWEI Gewaehlten. Ueber ein Menue (wie beim Shapeshifter) waehlt man nacheinander
/// zwei Ziele; danach stehen beide am Platz des anderen. Waehlbar sind lebende Spieler und Leichen - keine Geister.
/// Eine Leiche kann also mitgetauscht werden.
/// </summary>
public sealed class Swapper : CustomRole
{
    public override string ConfigSection => "Tauscher";
    public override string Name => Lang.T("Tauscher", "Swapper");
    public override Color Color => new Color32(120, 90, 220, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Platzwechsel!", "Switch places!");
    public override string Description => Lang.T("Tausche die Positionen\nvon zwei Zielen (F).", "Swap the positions\nof two targets (F).");
    public override string LongDescription => Lang.T(
        "Du bist Crew. Mit Taste F öffnest du ein Menü und wählst nacheinander zwei Ziele - danach stehen beide am Platz des anderen. Wählbar sind lebende Spieler und Leichen (keine Geister); eine Leiche lässt sich also mittauschen. So kannst du zum Beispiel jemanden aus der Gefahr holen oder eine Leiche verschieben.",
        "You are crew. Press F to open a menu and pick two targets one after another - then both stand at the other's spot. You can pick living players and bodies (no ghosts); so a body can be swapped too. Use it to pull someone out of danger or to move a body.");

    private const byte ActionSwap = 0;

    private RoleOption cooldown;
    private ShapeshifterMinigame menu;

    // erster Pick (Id + ob es eine Leiche ist); byte.MaxValue = noch nichts gewaehlt
    private byte firstId = byte.MaxValue;
    private bool firstIsBody;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 40f, 10f, 120f, 5f, "s");

        AddButton(new RoleButton
        {
            Label = "Tauschen|Swap",
            Icon = "admin",
            GameIcon = _ => SwapSprite(), // eigenes Zwei-Pfeile-Symbol (nur für den Tauscher)
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && !Minigame.Instance && IsAlive(PlayerControl.LocalPlayer),
            OnClick = _ => { firstId = byte.MaxValue; OpenMenu(); },
        });
    }

    public override void ResetState()
    {
        firstId = byte.MaxValue;
        if (menu) Object.Destroy(menu.gameObject);
        menu = null;
    }

    public override void OnMeetingStart()
    {
        firstId = byte.MaxValue;
        if (menu) { try { menu.Close(); } catch { } menu = null; }
    }

    // ---------- Auswahl-Menue (Shapeshifter-Stil) ----------

    private struct Entry { public byte Id; public bool IsBody; public NetworkedPlayerInfo Data; }

    private static ShapeshifterMinigame MenuPrefab()
    {
        if (!RoleManager.Instance) return null;
        foreach (var role in RoleManager.Instance.AllRoles)
        {
            var ss = role ? role.TryCast<ShapeshifterRole>() : null;
            if (ss && ss.ShapeshifterMenu) return ss.ShapeshifterMenu;
        }
        return null;
    }

    private static List<Entry> Targets()
    {
        var list = new List<Entry>();
        // lebende Spieler
        foreach (var p in PlayerControl.AllPlayerControls.ToArray())
            if (IsAlive(p) && p.Data != null) list.Add(new Entry { Id = p.PlayerId, IsBody = false, Data = p.Data });
        // Leichen (keine Geister als Person)
        foreach (var body in Object.FindObjectsOfType<DeadBody>())
        {
            if (!body) continue;
            var owner = FindPlayer(body.ParentId);
            if (owner && owner.Data != null) list.Add(new Entry { Id = body.ParentId, IsBody = true, Data = owner.Data });
        }
        return list;
    }

    private void OpenMenu()
    {
        try
        {
            var prefab = MenuPrefab();
            var local = PlayerControl.LocalPlayer;
            if (!prefab || !Camera.main || Minigame.Instance || !IsAlive(local) || InMeeting) return;

            menu = Object.Instantiate(prefab, Camera.main.transform, false);
            menu.transform.localPosition = new Vector3(0f, 0f, -50f);
            menu.Begin(null);
            Populate();
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogWarning($"Tauscher-Menü: {e.Message}");
            if (menu) Object.Destroy(menu.gameObject);
            menu = null;
        }
    }

    /// <summary>Panels (neu) befüllen - ohne das Menü zu schließen (für den zweiten Pick).</summary>
    private void Populate()
    {
        if (!menu) return;
        foreach (var panel in menu.potentialVictims)
            if (panel) Object.Destroy(panel.gameObject);
        menu.potentialVictims.Clear();

        var targets = Targets();
        for (int i = 0; i < targets.Count; i++)
        {
            var entry = targets[i];
            if (firstId != byte.MaxValue && entry.Id == firstId && entry.IsBody == firstIsBody) continue; // ersten nicht nochmal
            var panel = Object.Instantiate(menu.PanelPrefab, menu.transform);
            panel.transform.localPosition = new Vector3(menu.XStart + i % 3 * menu.XOffset, menu.YStart + i / 3 * menu.YOffset, -1f);
            panel.SetPlayer(i, entry.Data, (System.Action)(() => Pick(entry)));
            menu.potentialVictims.Add(panel);
        }
    }

    private void CloseMenu()
    {
        if (menu) { try { menu.Close(); } catch { } }
        menu = null;
    }

    private void Pick(Entry entry)
    {
        var local = PlayerControl.LocalPlayer;
        if (!IsAlive(local) || InMeeting) { CloseMenu(); firstId = byte.MaxValue; return; }

        if (firstId == byte.MaxValue)
        {
            // erster Gewaehlter gemerkt - Menü OFFEN lassen und nur neu befüllen (Close+Reopen klappt nicht, da Minigame.Instance noch gesetzt ist)
            firstId = entry.Id;
            firstIsBody = entry.IsBody;
            Populate();
            return;
        }

        if (entry.Id == firstId && entry.IsBody == firstIsBody) { CloseMenu(); firstId = byte.MaxValue; return; } // derselbe -> abbrechen
        CloseMenu();
        SendToAll(ActionSwap, firstId, (byte)(firstIsBody ? 1 : 0), entry.Id, (byte)(entry.IsBody ? 1 : 0));
        firstId = byte.MaxValue;
    }

    // ---------- Positionstausch ----------

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionSwap || data.Length < 4 || !RoleRegistry.Has(sender, this)) return;
        byte aId = data[0]; bool aBody = data[1] != 0;
        byte bId = data[2]; bool bBody = data[3] != 0;

        if (!PosOf(aId, aBody, out Vector2 pa) || !PosOf(bId, bBody, out Vector2 pb)) return;

        // Lebende Spieler versetzt der Host (netzwerk-synchron); Leichen bewegt jeder Client selbst
        if (AmHost)
        {
            if (!aBody) SnapLiving(aId, pb);
            if (!bBody) SnapLiving(bId, pa);
        }
        if (aBody) MoveBody(aId, pb);
        if (bBody) MoveBody(bId, pa);

        var local = PlayerControl.LocalPlayer;
        if (local && (local.PlayerId == aId || local.PlayerId == bId) && (!aBody || !bBody))
            Sfx.Play(Sfx.TaskUpdate, 0.6f);
    }

    private static bool PosOf(byte id, bool isBody, out Vector2 pos)
    {
        pos = Vector2.zero;
        if (isBody)
        {
            foreach (var body in Object.FindObjectsOfType<DeadBody>())
                if (body && body.ParentId == id) { pos = body.TruePosition; return true; }
            return false;
        }
        var p = FindPlayer(id);
        if (!IsAlive(p)) return false;
        pos = p.GetTruePosition();
        return true;
    }

    private static void SnapLiving(byte id, Vector2 pos)
    {
        var p = FindPlayer(id);
        if (IsAlive(p) && p.NetTransform) p.NetTransform.SnapTo(pos);
    }

    private static void MoveBody(byte id, Vector2 pos)
    {
        foreach (var body in Object.FindObjectsOfType<DeadBody>())
            if (body && body.ParentId == id)
                body.transform.position = new Vector3(pos.x, pos.y, body.transform.position.z);
    }

    // ---------- Eigenes Zwei-Pfeile-Symbol (auch vom Vertauscher genutzt) ----------
    private static Sprite swapSprite;
    internal static Sprite SwapSprite()
    {
        if (swapSprite) return swapSprite;
        const int W = 96, H = 96;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) tex.SetPixel(x, y, Color.clear);
        var c = Color.white;

        void Shaft(int cy, int x0, int x1, int th)
        {
            for (int yy = cy - th / 2; yy <= cy + th / 2; yy++)
            for (int xx = x0; xx <= x1; xx++)
                if (xx >= 0 && xx < W && yy >= 0 && yy < H) tex.SetPixel(xx, yy, c);
        }
        void HeadRight(int tipX, int cy, int size)
        {
            for (int d = 0; d <= size; d++)
            for (int yy = cy - d; yy <= cy + d; yy++)
            { int xx = tipX - d; if (xx >= 0 && xx < W && yy >= 0 && yy < H) tex.SetPixel(xx, yy, c); }
        }
        void HeadLeft(int tipX, int cy, int size)
        {
            for (int d = 0; d <= size; d++)
            for (int yy = cy - d; yy <= cy + d; yy++)
            { int xx = tipX + d; if (xx >= 0 && xx < W && yy >= 0 && yy < H) tex.SetPixel(xx, yy, c); }
        }

        // Oberer Pfeil zeigt nach rechts, unterer nach links (zwei Pfeile = Tausch)
        Shaft(60, 16, 68, 9); HeadRight(84, 60, 16);
        Shaft(36, 28, 80, 9); HeadLeft(12, 36, 16);

        tex.Apply();
        swapSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 110f);
        swapSprite.hideFlags = HideFlags.HideAndDontSave;
        return swapSprite;
    }
}
