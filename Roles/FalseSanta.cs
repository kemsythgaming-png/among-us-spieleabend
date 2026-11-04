using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor (Falscher Weihnachtsmann): gibt einem Spieler fuer kurze Zeit das Aussehen und den Namen eines anderen.
/// Auswahl wie beim Shapeshifter - erst wen verkleiden, dann als wen. Nach Ablauf (oder Meeting) wieder normal.
/// </summary>
public sealed class FalseSanta : CustomRole
{
    public override string ConfigSection => "FalscherWeihnachtsmann";
    public override string Name => Lang.T("Falscher Weihnachtsmann", "False Santa");
    public override Color Color => new Color32(210, 40, 50, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Wer ist wer?", "Who is who?");
    public override string Description => Lang.T("Gib jemandem das Aussehen\neines anderen (F).", "Give someone another's\nlook (F).");
    public override string LongDescription => Lang.T(
        "Impostor: Mit Taste F wählst du nacheinander zwei Spieler - der erste bekommt für 10 Sekunden Aussehen und Namen des zweiten. Danach (oder nach einem Meeting) ist alles wieder normal. So stiftest du Verwirrung. Deinen normalen Kill hast du zusätzlich.",
        "Impostor: press F to pick two players one after another - the first gets the second's look and name for 10 seconds. Afterwards (or after a meeting) everything is normal again. Great for confusion. You also keep your normal kill.");

    private const byte ActionDisguise = 0;

    private const int MaxUses = 2;

    private RoleOption cooldown, duration;
    private RoleButton button;
    private ShapeshifterMinigame menu;
    private byte firstId = byte.MaxValue;
    private int uses = MaxUses;
    private float nextRecharge;

    private sealed class Disg { public NetworkedPlayerInfo.PlayerOutfit Outfit; public PlayerOutfitType Type; public float Until; }
    private readonly Dictionary<byte, Disg> active = new();

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 40f, 15f, 120f, 5f, "s");
        duration = AddOption("Duration", "Dauer|Duration", 10f, 3f, 30f, 1f, "s");

        button = AddButton(new RoleButton
        {
            Label = "Verkleiden|Disguise",
            Icon = "camo",
            NeedsTarget = false,
            Cooldown = () => 0.5f,              // Öffnen kostet keine Abklingzeit - die kommt erst beim echten Einsatz
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            UsesLeft = () => uses,              // 2 Verkleidungen pro Runde
            CanUse = () => uses > 0 && !InMeeting && !Minigame.Instance && IsAlive(PlayerControl.LocalPlayer),
            OnClick = _ => { firstId = byte.MaxValue; OpenMenu(); },
        });
    }

    public override void ResetState() { firstId = byte.MaxValue; uses = MaxUses; nextRecharge = 0f; RestoreAll(); if (menu) Object.Destroy(menu.gameObject); menu = null; }
    public override void OnMeetingStart() { firstId = byte.MaxValue; RestoreAll(); if (menu) { try { menu.Close(); } catch { } menu = null; } }
    public override void OnMeetingEnd(PlayerControl exiled) => uses = MaxUses; // pro Runde wieder 2

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
        catch (System.Exception e) { Plugin.Logger.LogWarning($"Santa-Menü: {e.Message}"); if (menu) Object.Destroy(menu.gameObject); menu = null; }
    }

    /// <summary>Panels neu befüllen, OHNE das Menü zu schließen (für den zweiten Pick - Close+Reopen klappt nicht).</summary>
    private void Populate()
    {
        if (!menu) return;
        foreach (var panel in menu.potentialVictims) if (panel) Object.Destroy(panel.gameObject);
        menu.potentialVictims.Clear();

        var players = PlayerControl.AllPlayerControls.ToArray().Where(p => IsAlive(p) && p.Data != null).ToList();
        for (int i = 0; i < players.Count; i++)
        {
            var target = players[i];
            if (firstId != byte.MaxValue && target.PlayerId == firstId) continue;
            var panel = Object.Instantiate(menu.PanelPrefab, menu.transform);
            panel.transform.localPosition = new Vector3(menu.XStart + i % 3 * menu.XOffset, menu.YStart + i / 3 * menu.YOffset, -1f);
            panel.SetPlayer(i, target.Data, (System.Action)(() => Pick(target)));
            menu.potentialVictims.Add(panel);
        }
    }

    private void CloseMenu()
    {
        if (menu) { try { menu.Close(); } catch { } }
        menu = null;
    }

    /// <summary>Überschrift über der Auswahl - zeigt, wofür man gerade wählt.</summary>
    public override void DrawGUI()
    {
        if (!menu) return;
        string title = firstId == byte.MaxValue
            ? Lang.T("Wen verkleiden?", "Who to disguise?")
            : Lang.T("Als wen soll er aussehen?", "Look like whom?");
        var style = new GUIStyle(GUI.skin.label) { fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = new Color(0f, 0f, 0f, 0.6f);
        GUI.Label(new Rect(Ui.ScreenWidth / 2f - 497f, 143f, 1000f, 60f), title, style); // Schatten
        style.normal.textColor = Color;
        GUI.Label(new Rect(Ui.ScreenWidth / 2f - 500f, 140f, 1000f, 60f), title, style);
    }

    private void Pick(PlayerControl target)
    {
        if (!IsAlive(PlayerControl.LocalPlayer) || InMeeting || !target) { CloseMenu(); firstId = byte.MaxValue; return; }
        if (firstId == byte.MaxValue)
        {
            // Erster gewählt (wird verkleidet) - Menü OFFEN lassen und neu befüllen für den zweiten
            firstId = target.PlayerId;
            Populate();
            return;
        }
        if (target.PlayerId == firstId) { CloseMenu(); firstId = byte.MaxValue; return; } // derselbe -> abbrechen
        CloseMenu();
        SendToAll(ActionDisguise, firstId, target.PlayerId);
        firstId = byte.MaxValue;
        uses = Mathf.Max(0, uses - 1);                          // Einsatz verbraucht einen Stack
        if (uses < MaxUses && nextRecharge <= Time.time) nextRecharge = Time.time + cooldown.Value; // Nachladen starten
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionDisguise || data.Length < 2 || !RoleRegistry.Has(sender, this)) return;
        var target = FindPlayer(data[0]);
        var model = FindPlayer(data[1]);
        if (!target || !model || target.Data == null || model.Data == null) return;

        if (!active.ContainsKey(target.PlayerId))
            active[target.PlayerId] = new Disg { Outfit = target.Data.DefaultOutfit, Type = target.CurrentOutfitType };
        active[target.PlayerId].Until = Time.time + duration.Value;

        var m = model.Data.DefaultOutfit;
        var outfit = new NetworkedPlayerInfo.PlayerOutfit
        {
            ColorId = m.ColorId, HatId = m.HatId, SkinId = m.SkinId, VisorId = m.VisorId,
            PetId = m.PetId, NamePlateId = m.NamePlateId, PlayerName = m.PlayerName,
        };
        target.RawSetOutfit(outfit, PlayerOutfitType.MushroomMixup);
    }

    public override void Update()
    {
        // Stacks über den Cooldown nachladen (bis MaxUses)
        if (IsLocalRole && uses < MaxUses && Time.time >= nextRecharge)
        {
            uses++;
            if (uses < MaxUses) nextRecharge = Time.time + cooldown.Value;
        }

        if (active.Count == 0) return;
        float now = Time.time;
        foreach (var pair in active.Where(kv => now >= kv.Value.Until).ToList())
        {
            active.Remove(pair.Key);
            var p = FindPlayer(pair.Key);
            if (p && p.Data != null) p.RawSetOutfit(pair.Value.Outfit ?? p.Data.DefaultOutfit, PlayerOutfitType.Default);
        }
    }

    private void RestoreAll()
    {
        foreach (var pair in active)
        {
            var p = FindPlayer(pair.Key);
            if (p && p.Data != null) p.RawSetOutfit(pair.Value.Outfit ?? p.Data.DefaultOutfit, PlayerOutfitType.Default);
        }
        active.Clear();
    }
}
