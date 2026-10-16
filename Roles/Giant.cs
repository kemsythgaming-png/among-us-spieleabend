using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>Anpassung: groesser (fuer alle sichtbar) und etwas langsamer.</summary>
public sealed class Giant : CustomRole
{
    public override string Name =>
        Lang.T("Giant", "Giant");
    public override Color Color => new Color32(255, 179, 102, 255);
    public override RoleTeam Team => RoleTeam.Modifier;
    public override string IntroText =>
        Lang.T("Du bist riesig", "You are huge");
    public override string Description =>
        Lang.T("Du bist größer, aber langsamer.", "You are bigger, but slower.");
    public override string LongDescription =>
        Lang.T("Anpassung: Du bist für alle sichtbar größer und etwas langsamer. Deine Rolle bleibt.", "Modifier: you are visibly bigger and a bit slower. Your role stays.");

    private RoleOption size, speed;

    private readonly Dictionary<byte, Vector3> baseScales = new();

    protected override void Setup()
    {
        size = AddOption("Size", "Größe|Size", 1.4f, 1.1f, 1.6f, 0.05f, "x");
        speed = AddOption("Speed", "Tempo|Speed", 0.85f, 0.6f, 1f, 0.05f, "x");
    }

    public override void Update()
    {
        foreach (var p in Players)
        {
            if (!p) continue;
            if (!baseScales.ContainsKey(p.PlayerId)) baseScales[p.PlayerId] = p.transform.localScale;
            var b = baseScales[p.PlayerId];
            p.transform.localScale = new Vector3(b.x * size.Value, b.y * size.Value, b.z);
        }

    }

    public override float SpeedMultiplier(PlayerControl local) => IsLocalRole && IsAlive(local) ? speed.Value : 1f;

    public override void ResetState()
    {
        foreach (var pair in baseScales)
        {
            var p = FindPlayer(pair.Key);
            if (p) p.transform.localScale = pair.Value;
        }
        baseScales.Clear();

    }
}
