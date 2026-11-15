using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor (Frostbeißer): öffnet die Karte, wählt einen Raum - alle Spieler in diesem Raum frieren 15-20 Sekunden
/// ein (können sich nicht bewegen + Eis über dem Bildschirm). Während die Karte offen ist, steht der Frostbeißer still.
/// Im Raum liegt solange ein Frost-Symbol auf dem Boden (für alle sichtbar). Nur der Frostbeißer und Immune bleiben frei.
/// </summary>
public sealed class Frostbiter : CustomRole
{
    public override string ConfigSection => "Frostbeisser";
    public override string Name => Lang.T("Frostbeißer", "Frostbiter");
    public override Color Color => new Color32(120, 210, 255, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Alles erstarrt", "Everything freezes");
    public override string Description => Lang.T("Karte öffnen, Raum wählen (F) -\nalle darin frieren ein.", "Open map, pick a room (F) -\neveryone inside freezes.");
    public override string LongDescription => Lang.T(
        "Impostor: Mit Taste F geht die Karte auf - du stehst still und klickst einen Raum an. Alle Spieler in diesem Raum können sich 15-20 Sekunden nicht bewegen und bekommen Eis über den Bildschirm; im Raum liegt solange ein Frost-Symbol auf dem Boden (für alle sichtbar). Nur du selbst und gerade Frost-Immune bleiben frei. Danach läuft eine Abklingzeit.",
        "Impostor: press F to open the map - you stand still and click a room. All players in that room can't move for 15-20 seconds and get ice over their screen; a frost mark sits on the room floor meanwhile (visible to all). Only you and anyone currently frost-immune stay free. Then a cooldown runs.");

    private const byte ActionFreeze = 0;

    private RoleOption cooldown, duration;
    private readonly MapPicker picker = new();
    private bool frozenMove; // Frostbeißer steht beim Zielen still
    private SystemTypes frozenRoom;
    private float frozenUntil;
    private byte casterId = 255;
    private GameObject iceOverlay;
    private GameObject floorFrost;
    private float floorUntil;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 35f, 15f, 90f, 5f, "s");
        duration = AddOption("Duration", "Frost-Dauer|Freeze time", 17f, 15f, 20f, 1f, "s");

        AddButton(new RoleButton
        {
            Label = "Einfrieren|Freeze",
            Icon = "frost",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer) && !picker.Active,
            OnClick = _ => { if (picker.Open()) Sfx.Play(Sfx.Investigate, 0.5f); },
        });
    }

    public override void ResetState() { picker.Cancel(); frozenUntil = 0f; RemoveOverlay(); RemoveFloor(); Unfreeze(); }
    public override void OnMeetingStart() { picker.Cancel(); frozenUntil = 0f; RemoveOverlay(); RemoveFloor(); Unfreeze(); }

    private void Unfreeze() { if (frozenMove && PlayerControl.LocalPlayer) PlayerControl.LocalPlayer.moveable = true; frozenMove = false; }

    // Jeder Spieler (auch Dummys) im eingefrorenen Raum kann sich nicht bewegen - außer der Caster und Immune.
    private bool IsFrozen(PlayerControl p)
    {
        if (Time.time >= frozenUntil || InMeeting || !IsAlive(p) || p.PlayerId == casterId || WinterImmunity.Has(p.PlayerId)) return false;
        var ro = RoomObj(frozenRoom);
        return ro && ro.roomArea && ro.roomArea.OverlapPoint(p.GetTruePosition());
    }

    public override float SpeedMultiplier(PlayerControl local) => IsFrozen(local) ? 0f : 1f;

    private static PlainShipRoom RoomObj(SystemTypes r)
    {
        var ship = ShipStatus.Instance;
        if (ship && ship.AllRooms != null)
            foreach (var x in ship.AllRooms) if (x && x.RoomId == r) return x;
        return null;
    }

    private static SystemTypes RoomAt(Vector2 p)
    {
        var ship = ShipStatus.Instance;
        if (ship && ship.AllRooms != null)
            foreach (var r in ship.AllRooms)
                if (r && r.roomArea && r.roomArea.OverlapPoint(p)) return r.RoomId;
        PlainShipRoom best = null; float bd = float.MaxValue;
        if (ship && ship.AllRooms != null)
            foreach (var r in ship.AllRooms)
            {
                if (!r || !r.roomArea) continue;
                float d = ((Vector2)r.roomArea.bounds.center - p).sqrMagnitude;
                if (d < bd) { bd = d; best = r; }
            }
        return best ? best.RoomId : SystemTypes.Hallway;
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionFreeze || data.Length < 1 || !RoleRegistry.Has(sender, this)) return;
        var room = (SystemTypes)data[0];
        var ro = RoomObj(room);
        if (ro == null || !ro.roomArea) return;

        // Raum einfrieren (gilt für alle Spieler + Dummys darin, über SpeedMultiplier).
        frozenRoom = room;
        casterId = sender ? sender.PlayerId : (byte)255;
        frozenUntil = Time.time + duration.Value;

        // Frost-Symbol auf den Boden des Raums (bei allen sichtbar).
        ShowFloor(ro.roomArea.bounds.center, Mathf.Max(ro.roomArea.bounds.size.x, ro.roomArea.bounds.size.y));
        Sfx.Play(Sfx.TaskUpdate, 0.7f);
    }

    public override void Update()
    {
        // Beim Zielen (Karte offen) stehen bleiben, nicht mit dem letzten Input weiterlaufen.
        var lp = PlayerControl.LocalPlayer;
        if (picker.Active)
        {
            if (lp) { lp.moveable = false; if (lp.MyPhysics && lp.MyPhysics.body) lp.MyPhysics.body.velocity = Vector2.zero; }
            frozenMove = true;
        }
        else if (frozenMove) Unfreeze();

        if (picker.Active && picker.Poll(out var world))
        {
            var room = RoomAt(world);
            if (room != SystemTypes.Hallway) SendToAll(ActionFreeze, (byte)room);
        }

        // Eis-Overlay zeigen, solange der lokale Spieler eingefroren ist.
        bool localFrozen = lp && IsFrozen(lp);
        if (localFrozen && !iceOverlay) ShowOverlay();
        if ((!localFrozen || !Camera.main) && iceOverlay) RemoveOverlay();
        if (floorFrost && Time.time >= floorUntil) RemoveFloor();
    }

    private void ShowFloor(Vector2 center, float size)
    {
        RemoveFloor();
        floorFrost = WorldMarker.Spawn("frost", center, Mathf.Clamp(size * 0.9f, 1.5f, 6f), "FrostFloor");
        var sr = floorFrost ? floorFrost.GetComponent<SpriteRenderer>() : null;
        if (sr) sr.color = new Color(0.7f, 0.9f, 1f, 0.6f);
        floorUntil = Time.time + duration.Value;
    }

    private void RemoveFloor() { if (floorFrost) Object.Destroy(floorFrost); floorFrost = null; }

    private void ShowOverlay()
    {
        RemoveOverlay();
        var hud = HudManager.Instance;
        var cam = Camera.main;
        if (!cam) return;
        iceOverlay = new GameObject("Spieleabend_Ice");
        iceOverlay.transform.SetParent(cam.transform, false);
        var sr = iceOverlay.AddComponent<SpriteRenderer>();
        sr.sprite = Shapes.Square();
        sr.color = new Color(0.7f, 0.9f, 1f, 0.5f);
        if (hud && hud.ShadowQuad) { sr.sortingLayerID = hud.ShadowQuad.sortingLayerID; sr.sortingOrder = hud.ShadowQuad.sortingOrder + 1; }
        float h = cam.orthographicSize * 2f * 1.1f, w = h * cam.aspect;
        var size = sr.sprite.bounds.size;
        float z = cam.transform.InverseTransformPoint(hud && hud.ShadowQuad ? hud.ShadowQuad.transform.position : cam.transform.position + cam.transform.forward).z - 0.05f;
        iceOverlay.transform.localPosition = new Vector3(0f, 0f, Mathf.Max(0.2f, z));
        iceOverlay.transform.localScale = new Vector3(w / size.x, h / size.y, 1f);
    }

    private void RemoveOverlay() { if (iceOverlay) Object.Destroy(iceOverlay); iceOverlay = null; }
}
