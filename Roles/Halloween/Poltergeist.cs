using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Halloween;

/// <summary>
/// Impostor (Halloween): kann fuer ein paar Sekunden durch Waende gehen.
/// Steht er am Ende nicht auf begehbarem Boden, wird er an die letzte sichere Stelle zurueckgesetzt.
///
/// "Begehbar" wird so geprueft: Die Waende im Spiel sind nur Linien. Eine Stelle gilt als sicher, wenn von ihr
/// aus eine freie Sichtlinie (ohne Wand dazwischen) zu einem bekannten begehbaren Punkt fuehrt - eigene Laufspuren,
/// andere lebende Spieler, Vents oder die Startpunkte der Map.
/// </summary>
public sealed class Poltergeist : CustomRole
{
    public override string Name => "Poltergeist";
    public override Color Color => new Color32(120, 200, 230, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Keine Wand hält dich auf", "No wall can stop you");
    public override string Description => Lang.T("Geh kurz durch Wände (F).", "Walk through walls briefly (F).");
    public override string LongDescription => Lang.T(
        "Halloween-Impostor. Mit Taste F kannst du für ein paar Sekunden durch Wände gehen - der Button zeigt, wie lange noch.\n\nSteckst du am Ende in einer Wand oder außerhalb der Map, kommst du aus dem nächsten Vent wieder heraus. Deinen normalen Kill-Button hast du weiterhin.",
        "Halloween impostor. Press F to walk through walls for a few seconds - the button shows how long is left.\n\nIf you end up inside a wall or outside the map, you come out of the nearest vent. You still have your normal kill button.");

    private const float TrailSpacing = 0.5f, AnchorRange = 9f;
    private const int MaxTrail = 3000;

    private RoleOption duration, cooldown;

    private bool phasing;
    private Vector2 lastSafe;                         // transform.position der letzten sicheren Stelle
    private readonly List<Vector2> trail = new();    // eigene Laufspur (Fuesse), nur auf begehbarem Boden aufgenommen

    protected override void Setup()
    {
        duration = AddOption("Duration", "Dauer|Duration", 5f, 2f, 10f, 1f, "s");
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 25f, 10f, 60f, 2.5f, "s");

        AddButton(new RoleButton
        {
            Label = "Durch Wände|Phase",
            Icon = "phase",
            Cooldown = () => cooldown.Value,
            Duration = () => duration.Value,
            Hotkey = KeyCode.F,
            NeedsTarget = false,
            OnClick = _ => StartPhase(),
            OnEffectEnd = () => EndPhase(true),
        });
    }

    public override void ResetState()
    {
        if (phasing) EndPhase(false);
        trail.Clear();
    }

    private void StartPhase()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !local.Collider) return;
        lastSafe = local.transform.position;
        phasing = true;
        local.Collider.enabled = false;
        Sfx.Play(Sfx.Vanish);
    }

    private void EndPhase(bool snapBack)
    {
        if (!phasing) return;
        phasing = false;
        var local = PlayerControl.LocalPlayer;
        if (!local) return;
        if (local.Collider) local.Collider.enabled = true;

        if (snapBack && IsAlive(local) && !InMeeting && !IsSafe(local.GetTruePosition(), local))
        {
            // Steckt in einer Wand / ausserhalb der Map: aus dem naechsten Vent wieder auftauchen
            var vent = ShipStatus.Instance ? ShipStatus.Instance.AllVents.ToArray()
                .Where(v => v).OrderBy(v => ((Vector2)v.transform.position - local.GetTruePosition()).sqrMagnitude).FirstOrDefault() : null;
            Vector2 target = vent ? (Vector2)(vent.transform.position + vent.Offset) : lastSafe;
            local.NetTransform.RpcSnapTo(target);
            // Wie beim normalen Aussteigen: Vent geht auf, man springt heraus (fuer alle sichtbar, mit Geraeusch)
            if (vent) local.MyPhysics.RpcExitVent(vent.Id);
            else Sfx.Play(Sfx.Splash);
            Notifier.Show($"<color={ColorHex}>{Lang.T("Du steckst fest - raus aus dem nächsten Vent!", "You got stuck - out through the nearest vent!")}</color>", 2.5f);
        }
        Sfx.Play(Sfx.Appear);
    }

    public override void Update()
    {
        if (!IsLocalRole) return;
        var local = PlayerControl.LocalPlayer;
        if (!IsAlive(local))
        {
            if (phasing) EndPhase(false);
            return;
        }

        Vector2 feet = local.GetTruePosition();
        if (!phasing)
        {
            // Normale Bewegung = garantiert begehbar -> Laufspur aufzeichnen
            if (trail.Count == 0 || Vector2.Distance(trail[trail.Count - 1], feet) >= TrailSpacing)
            {
                trail.Add(feet);
                if (trail.Count > MaxTrail) trail.RemoveAt(0);
            }
            return;
        }

        if (IsSafe(feet, local)) lastSafe = local.transform.position;
    }

    /// <summary>Freie Sichtlinie zu einem bekannten begehbaren Punkt in der Naehe?</summary>
    private bool IsSafe(Vector2 pos, PlayerControl self)
    {
        foreach (var anchor in Anchors(self).Where(a => (a - pos).sqrMagnitude <= AnchorRange * AnchorRange)
                                              .OrderBy(a => (a - pos).sqrMagnitude).Take(25))
        {
            if ((anchor - pos).sqrMagnitude < 0.0001f) return true;
            if (!Physics2D.Linecast(pos, anchor, Constants.ShipAndObjectsMask)) return true;
        }
        return false;
    }

    private IEnumerable<Vector2> Anchors(PlayerControl self)
    {
        foreach (var t in trail) yield return t;
        foreach (var p in PlayerControl.AllPlayerControls)
            if (p && p != self && IsAlive(p) && !p.inVent) yield return p.GetTruePosition();
        var ship = ShipStatus.Instance;
        if (!ship) yield break;
        foreach (var v in ship.AllVents)
            if (v) yield return v.transform.position;
        yield return ship.InitialSpawnCenter;
        yield return ship.MeetingSpawnCenter;
    }
}
