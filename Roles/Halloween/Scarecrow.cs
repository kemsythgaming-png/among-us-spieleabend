using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Halloween;

/// <summary>
/// Impostor: stellt eine Kopie eines Spielers (mit Name) auf die Map. Wer ihr zu nahe kommt, erstarrt kurz vor Angst.
/// Die Kopie verschwindet beim naechsten Meeting.
/// </summary>
public sealed class Scarecrow : CustomRole
{
    public override string ConfigSection => "VogelscheucheImp"; // nicht "Vogelscheuche" - das war die entfernte Crew-Rolle
    public override string Name => Lang.T("Vogelscheuche", "Scarecrow");
    public override Color Color => new Color32(190, 140, 70, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Wer steht denn da?", "Who's standing there?");
    public override string Description => Lang.T("Stelle eine Kopie eines Spielers auf (F).\nWer ihr nahe kommt, erstarrt.", "Place a copy of a player (F).\nWhoever gets close freezes.");
    public override string LongDescription => Lang.T(
        "Halloween-Impostor mit normalem Kill. Mit Taste F wählst du einen Spieler - eine täuschend echte Kopie von ihm (mit Name) steht dann dort, wo du stehst.\n\nWer der Kopie zu nahe kommt, erstarrt ein paar Sekunden vor Angst. So kannst du auch Alibis kaputtmachen. Die Kopie verschwindet beim nächsten Meeting.",
        "Halloween impostor with a normal kill. Press F and choose a player - a deceptively real copy of them (with name) then stands where you are.\n\nWhoever gets too close to the copy freezes in fear for a few seconds. You can also ruin alibis this way. The copy disappears at the next meeting.");

    private const byte ActionPlace = 0;
    private const float FearRange = 1.2f;

    private RoleOption cooldown, fearTime, maxCopies;
    private RoleButton button;

    private sealed class Copy
    {
        public int Id;
        public Vector2 Pos;
        public GameObject Visual;
    }

    // bei allen Clients
    private readonly List<Copy> copies = new();
    private int nextId;

    // lokal
    private bool choosing;
    private readonly HashSet<int> scaredBy = new();
    private float frozenUntil;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 25f, 10f, 60f, 2.5f, "s");
        fearTime = AddOption("FearTime", "Erstarren|Freeze time", 3f, 1f, 8f, 0.5f, "s");
        maxCopies = AddOption("MaxCopies", "Kopien gleichzeitig|Copies at once", 1f, 1f, 3f, 1f);

        button = AddButton(new RoleButton
        {
            Label = "Vogelscheuche|Scarecrow",
            Icon = "scarecrow",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            Hotkey = KeyCode.F,
            CanUse = () => !PlayerControl.LocalPlayer.inVent,
            OnClick = _ =>
            {
                // Erst waehlen - die Abklingzeit startet beim Aufstellen
                button.Timer = 0f;
                choosing = true;
            },
        });
    }

    public override void ResetState()
    {
        ClearCopies();
        choosing = false;
        frozenUntil = 0f;
        nextId = 0;
    }

    public override void OnMeetingStart()
    {
        ClearCopies();
        choosing = false;
        frozenUntil = 0f;
    }

    private void ClearCopies()
    {
        foreach (var c in copies)
            if (c.Visual) Object.Destroy(c.Visual);
        copies.Clear();
        scaredBy.Clear();
    }

    public override float SpeedMultiplier(PlayerControl local) => frozenUntil > Time.time && !InMeeting ? 0f : 1f;

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionPlace || data.Length < 5 || !RoleRegistry.Has(sender, this)) return;
        var model = FindPlayer(data[0]);
        if (!model || model.Data == null) return;

        // Pro Vogelscheuche nur so viele Kopien wie erlaubt - die aelteste verschwindet
        var mine = copies.Where(c => c.Id >> 8 == sender.PlayerId).ToList();
        while (mine.Count >= maxCopies.Int)
        {
            var old = mine[0];
            mine.RemoveAt(0);
            copies.Remove(old);
            if (old.Visual) Object.Destroy(old.Visual);
        }

        var pos = WorldMarker.Decode(data, 1);
        var copy = new Copy { Id = sender.PlayerId << 8 | (nextId++ & 0xFF), Pos = pos };
        copy.Visual = CreateCopy(model, pos);
        copies.Add(copy);
        if (sender == PlayerControl.LocalPlayer) Sfx.Play(Sfx.Shapeshift);
    }

    /// <summary>Kopie des Spielers: dieselbe Spielfigur wie im Intro, mit Outfit und Name.</summary>
    private static GameObject CreateCopy(PlayerControl model, Vector2 pos)
    {
        var hud = HudManager.Instance;
        if (!hud || !hud.IntroPrefab || !hud.IntroPrefab.PlayerPrefab) return null;

        var poolable = Object.Instantiate(hud.IntroPrefab.PlayerPrefab, ShipStatus.Instance ? ShipStatus.Instance.transform : null);
        poolable.name = "Spieleabend_ScarecrowCopy";
        poolable.gameObject.SetActive(true);
        poolable.UpdateFromPlayerOutfit(model.Data.DefaultOutfit, PlayerMaterial.MaskType.None, false, false, null, true);
        poolable.SetName(model.Data.PlayerName);
        poolable.ToggleName(true);
        poolable.SetFlipX(model.cosmetics && model.cosmetics.FlipX);

        // So gross wie eine echte Spielfigur
        var realBody = model.cosmetics && model.cosmetics.currentBodySprite != null ? model.cosmetics.currentBodySprite.BodySprite : null;
        var copyBody = poolable.cosmetics && poolable.cosmetics.currentBodySprite != null ? poolable.cosmetics.currentBodySprite.BodySprite : null;
        float scale = 0.5f;
        if (realBody && copyBody && copyBody.bounds.size.y > 0.01f)
            scale = poolable.transform.localScale.y * realBody.bounds.size.y / copyBody.bounds.size.y;
        poolable.transform.localScale = new Vector3(scale, scale, 1f);

        // Fuesse auf die Position, Tiefe wie bei Spielern
        var offset = model.transform.position - (Vector3)model.GetTruePosition();
        poolable.transform.position = new Vector3(pos.x + offset.x, pos.y + offset.y, (pos.y + offset.y) / 1000f);
        return poolable.gameObject;
    }

    public override void Update()
    {
        var local = PlayerControl.LocalPlayer;
        if (copies.Count == 0 || !IsAlive(local) || InMeeting || IsImpostor(local) || local.inVent) return;

        Vector2 me = local.GetTruePosition();
        foreach (var c in copies)
        {
            if (scaredBy.Contains(c.Id) || Vector2.Distance(me, c.Pos) > FearRange) continue;
            scaredBy.Add(c.Id);
            frozenUntil = Time.time + fearTime.Value;
            Sfx.Play(Sfx.Creepy);
            Notifier.Show($"<color={ColorHex}>{Lang.T("Du erstarrst vor Angst!", "You freeze in fear!")}</color>", fearTime.Value);
        }
    }

    // ---------- Auswahl, wen man kopiert ----------

    public override void DrawGUI()
    {
        var local = PlayerControl.LocalPlayer;
        if (!choosing) return;
        if (!IsAlive(local) || InMeeting)
        {
            choosing = false;
            return;
        }

        var options = PlayerControl.AllPlayerControls.ToArray().Where(p => IsAlive(p) && p != local).ToList();
        const int columns = 3;
        int rows = (options.Count + columns - 1) / columns;
        float w = columns * 230 + 60, h = 110 + rows * 52 + 70;
        var panel = new Rect((Ui.ScreenWidth - w) / 2f, (1080 - h) / 2f, w, h);
        Ui.Block(panel);
        GUI.Box(panel, new GUIContent(""), Ui.Panel);
        GUI.Label(new Rect(panel.x + 30, panel.y + 18, w - 120, 40), Lang.T("Wen soll die Vogelscheuche zeigen?", "Who should the scarecrow look like?"), Ui.Header);
        if (Ui.Button(new Rect(panel.xMax - 70, panel.y + 15, 50, 50), "X"))
        {
            choosing = false;
            return;
        }

        for (int i = 0; i < options.Count; i++)
        {
            var p = options[i];
            var rect = new Rect(panel.x + 30 + (i % columns) * 230, panel.y + 80 + (i / columns) * 52, 220, 46);
            if (!Ui.Button(rect, p.Data.PlayerName)) continue;

            choosing = false;
            if (local.inVent) return;
            SendToAll(ActionPlace, WorldMarker.Concat(p.PlayerId, WorldMarker.Encode(local.GetTruePosition())));
            button.Timer = cooldown.Value;
            return;
        }

        if (Ui.Button(new Rect(panel.x + 30, panel.yMax - 62, 220, 46), Lang.T("Abbrechen", "Cancel"))) choosing = false;
    }
}
