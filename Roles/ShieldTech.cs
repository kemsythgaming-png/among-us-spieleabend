using System;
using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Crew (Schildtechniker): erzeugt an seiner Stelle einen sichtbaren Schutzbereich. Fuer 10 Sekunden kann darin
/// niemand getoetet werden. Fuer alle sichtbar, lange Abklingzeit.
/// </summary>
public sealed class ShieldTech : CustomRole
{
    public override string ConfigSection => "Schildtechniker";
    public override string Name => Lang.T("Schildtechniker", "Shield Tech");
    public override Color Color => new Color32(80, 180, 255, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Hier wird nicht gestorben", "No dying here");
    public override string Description => Lang.T("Erzeuge eine kill-freie Zone (F).", "Create a no-kill zone (F).");
    public override string LongDescription => Lang.T(
        "Du bist Crew. Mit Taste F erzeugst du an deiner Stelle einen Schutzbereich - 7 bis 11 Sekunden lang kann darin niemand getötet werden. Du selbst siehst ihn sofort, alle anderen erst nach 3-5 Sekunden. Je mehr Aufgaben du erledigt hast, desto größer wird der Kreis (0,1 Radius pro Aufgabe). Danach läuft eine lange Abklingzeit.",
        "You are crew. Press F to create a protection zone at your spot - for 7 to 11 seconds nobody can be killed inside it. You see it instantly, everyone else only after 3-5 seconds. The more tasks you have done, the larger the circle grows (0.1 radius per task). Then a long cooldown runs.");

    private const byte ActionPlace = 0;

    private RoleOption cooldown, radius, growth;
    private sealed class Zone { public Vector2 Pos; public float Until; public float Radius; public float VisibleAt; public GameObject Ring; }
    private readonly List<Zone> zones = new();

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 50f, 15f, 120f, 5f, "s");
        radius = AddOption("Radius", "Grund-Radius|Base radius", 1.2f, 0.5f, 5f, 0.1f);
        growth = AddOption("Growth", "Radius je Aufgabe|Radius per task", 0.1f, 0f, 1f, 0.05f);

        AddButton(new RoleButton
        {
            Label = "Schutzbereich|Shield zone",
            Icon = "shield",
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
                float r = radius.Value + growth.Value * CompletedTasks(local); // wächst pro erledigter Aufgabe
                float showAfter = UnityEngine.Random.Range(3f, 5f);            // andere sehen es erst nach 3-5 s
                float dur = UnityEngine.Random.Range(7f, 11f);                 // Dauer zufällig 7-11 s
                var data = new byte[20];
                BitConverter.GetBytes(pos.x).CopyTo(data, 0);
                BitConverter.GetBytes(pos.y).CopyTo(data, 4);
                BitConverter.GetBytes(r).CopyTo(data, 8);
                BitConverter.GetBytes(showAfter).CopyTo(data, 12);
                BitConverter.GetBytes(dur).CopyTo(data, 16);
                SendToAll(ActionPlace, data);
            },
        });
    }

    public override void ResetState() => Clear();
    public override void OnMeetingStart() => Clear();

    private void Clear()
    {
        foreach (var z in zones) if (z.Ring) Object.Destroy(z.Ring);
        zones.Clear();
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionPlace || data.Length < 20 || !RoleRegistry.Has(sender, this)) return;
        var pos = new Vector2(BitConverter.ToSingle(data, 0), BitConverter.ToSingle(data, 4));
        float r = BitConverter.ToSingle(data, 8);
        float showAfter = BitConverter.ToSingle(data, 12);
        float dur = BitConverter.ToSingle(data, 16);

        // Der Schildtechniker selbst sieht sein Schild sofort; alle anderen erst nach 3-5 s.
        bool owner = sender == PlayerControl.LocalPlayer;

        var ring = new GameObject("Spieleabend_ShieldZone");
        if (ShipStatus.Instance) ring.transform.SetParent(ShipStatus.Instance.transform, true);
        ring.transform.position = new Vector3(pos.x, pos.y, pos.y / 1000f + 0.02f);
        ring.transform.localScale = Vector3.one * (r * 2f);
        var rs = ring.AddComponent<SpriteRenderer>();
        rs.sprite = Shapes.Ring();
        rs.color = new Color(Color.r, Color.g, Color.b, 0.5f);
        rs.enabled = owner;
        zones.Add(new Zone { Pos = pos, Until = Time.time + dur, Radius = r, VisibleAt = owner ? Time.time : Time.time + showAfter, Ring = ring });
        if (sender == PlayerControl.LocalPlayer) Sfx.Play(Sfx.TaskComplete, 0.6f);
    }

    public override void Update()
    {
        float t = Time.time;
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            var z = zones[i];
            if (t >= z.Until) { if (z.Ring) Object.Destroy(z.Ring); zones.RemoveAt(i); continue; }
            if (z.Ring)
            {
                var rr = z.Ring.GetComponent<SpriteRenderer>();
                if (rr)
                {
                    if (t < z.VisibleAt) { rr.enabled = false; }       // noch unsichtbar (3-5 s)
                    else { rr.enabled = true; rr.color = new Color(Color.r, Color.g, Color.b, 0.35f + 0.2f * Mathf.Sin(t * 3f)); }
                }
            }
        }
    }

    /// <summary>Host: Kill blocken, wenn das Ziel in einem aktiven Schutzbereich steht.</summary>
    public override KillIntercept InterceptKill(PlayerControl killer, PlayerControl target)
    {
        if (!target) return KillIntercept.None;
        Vector2 p = target.GetTruePosition();
        float t = Time.time;
        foreach (var z in zones)
            if (t < z.Until && Vector2.Distance(p, z.Pos) <= z.Radius) return KillIntercept.Handled;
        return KillIntercept.None;
    }

    /// <summary>Anzahl erledigter (echter) Aufgaben des Spielers.</summary>
    private static int CompletedTasks(PlayerControl p)
    {
        int n = 0;
        if (p && p.myTasks != null)
            foreach (var t in p.myTasks)
                if (t && t.TryCast<SabotageTask>() == null && t.IsComplete) n++;
        return n;
    }
}
