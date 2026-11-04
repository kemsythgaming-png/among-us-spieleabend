using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor (Leichenschlepper): kann eine Leiche aufnehmen und hinter sich herziehen, dann woanders wieder ablegen.
/// Jeder Client laesst die Leiche dem Traeger folgen; beim Ablegen bleibt sie liegen. Normaler Kill bleibt.
/// </summary>
public sealed class BodyDragger : CustomRole
{
    public override string ConfigSection => "Leichenschlepper";
    public override string Name => Lang.T("Leichenschlepper", "Body Dragger");
    public override Color Color => new Color32(110, 70, 90, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Räum die Spuren weg", "Move the evidence");
    public override string Description => Lang.T("Leiche aufnehmen/ablegen (F).", "Pick up / drop a body (F).");
    public override string LongDescription => Lang.T(
        "Impostor: Stehst du an einer Leiche, nimmst du sie mit Taste F auf und ziehst sie hinter dir her. Ein zweiter Druck legt sie an deiner Stelle wieder ab. So schleppst du eine Leiche weg von deinem Tatort. Deinen normalen Kill hast du zusätzlich.",
        "Impostor: standing at a body, press F to pick it up and drag it behind you. A second press drops it at your spot. So you move a body away from your kill site. You also keep your normal kill.");

    private const byte ActionPick = 0, ActionDrop = 1;
    private const float Range = 1.2f;

    // alle Clients: Leiche (ParentId) -> Traeger (PlayerId)
    private readonly Dictionary<byte, byte> dragging = new();
    private RoleButton button;
    private RoleOption dropCooldown;

    protected override void Setup()
    {
        dropCooldown = AddOption("DropCooldown", "Abklingzeit nach Ablegen|Cooldown after drop", 20f, 0f, 60f, 5f, "s");

        button = AddButton(new RoleButton
        {
            Label = "Aufnehmen/Ablegen|Pick up/Drop",
            GameIcon = _ => DragSprite(),
            Icon = "ghost",
            NeedsTarget = false,
            Cooldown = () => 0.3f, // Aufnehmen/Ablegen fast sofort; die 20 s kommen erst nach dem Ablegen
            StartCooldown = 3f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && (MyBody() != byte.MaxValue || NearbyBody() != null),
            OnClick = _ =>
            {
                var local = PlayerControl.LocalPlayer;
                if (!local) return;
                byte mine = MyBody();
                if (mine != byte.MaxValue)
                {
                    SendToAll(ActionDrop, mine);
                    if (button != null) button.Timer = dropCooldown.Value; // nach dem Ablegen 20 s Abklingzeit
                    return;
                }
                var body = NearbyBody();
                if (body) SendToAll(ActionPick, body.ParentId, local.PlayerId);
            },
        });
    }

    public override void ResetState() => StopAll();
    public override void OnMeetingStart() => StopAll();

    private void StopAll()
    {
        foreach (var kv in dragging) SetFigureShadow(FindPlayer(kv.Value), true); // Figur-Schatten zurück
        foreach (var body in Object.FindObjectsOfType<DeadBody>()) SetBodyShadow(body, true);
        dragging.Clear();
    }

    /// <summary>Wer gerade eine Leiche zieht, läuft etwas langsamer (0,2 weniger).</summary>
    public override float SpeedMultiplier(PlayerControl local) =>
        IsLocalRole && IsAlive(local) && MyBody() != byte.MaxValue && !InMeeting ? 0.8f : 1f;

    private byte MyBody()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local) return byte.MaxValue;
        foreach (var kv in dragging) if (kv.Value == local.PlayerId) return kv.Key;
        return byte.MaxValue;
    }

    /// <summary>Den Schatten unter der Leiche an/aus (Renderer, dessen Objektname „shadow" enthält).</summary>
    private static void SetBodyShadow(DeadBody body, bool on)
    {
        foreach (var r in body.GetComponentsInChildren<SpriteRenderer>(true))
            if (r && r.name.ToLower().Contains("shadow")) r.enabled = on;
    }

    /// <summary>Den Schatten unter der Spielfigur an/aus.</summary>
    private static void SetFigureShadow(PlayerControl p, bool on)
    {
        if (!p) return;
        foreach (var r in p.GetComponentsInChildren<SpriteRenderer>(true))
            if (r && r.name.ToLower().Contains("shadow")) r.enabled = on;
    }

    private static DeadBody NearbyBody()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !IsAlive(local) || local.inVent) return null;
        Vector2 me = local.GetTruePosition();
        return Object.FindObjectsOfType<DeadBody>().Where(b => b && Vector2.Distance(me, b.TruePosition) <= Range)
            .OrderBy(b => Vector2.Distance(me, b.TruePosition)).FirstOrDefault();
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (!RoleRegistry.Has(sender, this)) return;
        if (action == ActionPick && data.Length >= 2) dragging[data[0]] = data[1];
        else if (action == ActionDrop && data.Length >= 1)
        {
            if (dragging.TryGetValue(data[0], out var ownerId)) SetFigureShadow(FindPlayer(ownerId), true); // Figur-Schatten wieder an
            dragging.Remove(data[0]);
            foreach (var body in Object.FindObjectsOfType<DeadBody>())
                if (body && body.ParentId == data[0]) SetBodyShadow(body, true); // Schatten wieder an
        }
    }

    public override void Update()
    {
        if (dragging.Count == 0) return;
        foreach (var kv in dragging.ToList())
        {
            var owner = FindPlayer(kv.Value);
            if (!IsAlive(owner)) { dragging.Remove(kv.Key); continue; }
            Vector2 pos = owner.GetTruePosition(); // direkt am Träger (kein Nachlauf)
            float behindZ = owner.transform.position.z + 0.5f; // deutlich größeres z -> klar hinter dem Körper

            SetFigureShadow(owner, false); // eigenen Figur-Schatten ausblenden beim Tragen

            foreach (var body in Object.FindObjectsOfType<DeadBody>())
                if (body && body.ParentId == kv.Key)
                {
                    // höheres y = hinter dem Spieler; etwas nach rechts; größeres z schiebt nach hinten
                    body.transform.position = new Vector3(pos.x + 0.18f, pos.y + 0.35f, behindZ);
                    SetBodyShadow(body, false); // Schatten der Leiche aus beim Tragen
                }
        }
    }

    // ---------- Eigenes Schlepp-Symbol (Leiche mit Zug-Linien) ----------
    private static Sprite dragSprite;
    private static Sprite DragSprite()
    {
        if (dragSprite) return dragSprite;
        const int S = 96;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) tex.SetPixel(x, y, Color.clear);
        var body = new Color(0.85f, 0.85f, 0.9f, 1f);
        var line = new Color(0.85f, 0.85f, 0.9f, 0.8f);
        // „Leiche" (liegende Kapsel) rechts
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            var c = new Vector2((x - 62f) / 26f, (y - 40f) / 15f);
            if (c.sqrMagnitude < 1f) tex.SetPixel(x, y, body);
        }
        // drei Zug-Striche links (Schlepp-Spur)
        void Dash(int x0, int x1, int yy) { for (int x = x0; x <= x1; x++) for (int dy = -2; dy <= 2; dy++) { int yv = yy + dy; if (x >= 0 && x < S && yv >= 0 && yv < S) tex.SetPixel(x, yv, line); } }
        Dash(6, 24, 40); Dash(6, 20, 54); Dash(6, 20, 26);
        tex.Apply();
        dragSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        dragSprite.hideFlags = HideFlags.HideAndDontSave;
        return dragSprite;
    }
}
