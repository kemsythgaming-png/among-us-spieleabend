using System;
using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Crew (Kaminbauer): stellt einen tragbaren Kamin ab. Spieler in seiner Naehe sind gegen Verlangsamungs- und
/// Frost-Effekte immun. Der Kamin erlischt nach 10 Sekunden.
/// </summary>
public sealed class FireplaceBuilder : CustomRole
{
    public override string ConfigSection => "Kaminbauer";
    public override string Name => Lang.T("Kaminbauer", "Fireplace Builder");
    public override Color Color => new Color32(255, 140, 40, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Wärme für alle", "Warmth for everyone");
    public override string Description => Lang.T("Stell einen Kamin (F) - Nähe\nist frost-immun.", "Place a fireplace (F) - nearby\nis frost-immune.");
    public override string LongDescription => Lang.T(
        "Du bist Crew. Mit Taste F stellst du einen tragbaren Kamin ab. Jeder in seiner Nähe ist gegen Verlangsamungs- und Frost-Effekte immun. Der Kamin erlischt nach 10 Sekunden. Danach läuft eine Abklingzeit.",
        "You are crew. Press F to place a portable fireplace. Everyone near it is immune to slow and frost effects. The fireplace goes out after 10 seconds. Then a cooldown runs.");

    private const byte ActionPlace = 0;

    private RoleOption duration, cooldown, radius;
    private sealed class Fire { public Vector2 Pos; public float Until; public GameObject Obj; }
    private readonly List<Fire> fires = new();

    protected override void Setup()
    {
        duration = AddOption("Duration", "Brenndauer|Burn time", 10f, 5f, 30f, 1f, "s");
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 40f, 15f, 120f, 5f, "s");
        radius = AddOption("Radius", "Radius|Radius", 2.5f, 1f, 5f, 0.5f);

        AddButton(new RoleButton
        {
            Label = "Kamin aufstellen|Place fireplace",
            Icon = "ignite",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer),
            OnClick = _ =>
            {
                var local = PlayerControl.LocalPlayer;
                if (!local) return;
                var pos = local.GetTruePosition();
                var data = new byte[8];
                BitConverter.GetBytes(pos.x).CopyTo(data, 0);
                BitConverter.GetBytes(pos.y).CopyTo(data, 4);
                SendToAll(ActionPlace, data);
            },
        });
    }

    public override void ResetState() => Clear();
    public override void OnMeetingStart() => Clear();

    private void Clear()
    {
        foreach (var f in fires) if (f.Obj) Object.Destroy(f.Obj);
        fires.Clear();
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionPlace || data.Length < 8 || !RoleRegistry.Has(sender, this)) return;
        var pos = new Vector2(BitConverter.ToSingle(data, 0), BitConverter.ToSingle(data, 4));

        var obj = new GameObject("Spieleabend_Fireplace");
        if (ShipStatus.Instance) obj.transform.SetParent(ShipStatus.Instance.transform, true);
        obj.transform.position = new Vector3(pos.x, pos.y, pos.y / 1000f - 0.01f);
        obj.transform.localScale = Vector3.one * 0.6f;
        obj.AddComponent<SpriteRenderer>().sprite = ButtonIcons.Get("ignite", null);
        fires.Add(new Fire { Pos = pos, Until = Time.time + duration.Value, Obj = obj });
        if (sender == PlayerControl.LocalPlayer) Sfx.Play(Sfx.TaskComplete, 0.6f);
    }

    public override void Update()
    {
        float t = Time.time;
        for (int i = fires.Count - 1; i >= 0; i--)
        {
            var f = fires[i];
            if (t >= f.Until) { if (f.Obj) Object.Destroy(f.Obj); fires.RemoveAt(i); continue; }
            if (f.Obj) f.Obj.transform.localScale = Vector3.one * (0.55f + 0.08f * Mathf.Sin(t * 8f)); // Flackern
            // alle in der Naehe immun halten (jeder Client fuer alle, damit es ueberall gilt)
            foreach (var p in PlayerControl.AllPlayerControls)
                if (IsAlive(p) && Vector2.Distance(p.GetTruePosition(), f.Pos) <= radius.Value)
                    WinterImmunity.Grant(p.PlayerId, 1.5f);
        }
    }
}
