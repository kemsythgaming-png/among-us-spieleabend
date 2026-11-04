using System;
using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor (Clown): wirbelt fuer kurze Zeit Farben und Namen aller Spieler durcheinander - jeder sieht bei jedem
/// eine zufaellige Farbe und einen vertauschten Namen. Nach Ablauf (oder Meeting) ist alles wieder normal.
/// </summary>
public sealed class Clown : CustomRole
{
    public override string ConfigSection => "Clown";
    public override string Name => Lang.T("Clown", "Clown");
    public override Color Color => new Color32(240, 120, 200, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Reines Chaos", "Pure chaos");
    public override string Description => Lang.T("Vertausche Farben & Namen\naller (F).", "Swap everyone's colors\n& names (F).");
    public override string LongDescription => Lang.T(
        "Impostor: Mit Taste F vertauschst du für kurze Zeit die Identitäten aller Spieler - jeder sieht aus wie ein anderer echter Spieler (dessen Farbe UND Name zusammen). So wirkt es glaubhaft, aber jeder ist wer anderes. Nach Ablauf (oder einem Meeting) ist alles wieder normal. Deinen normalen Kill hast du zusätzlich.",
        "Impostor: press F to swap everyone's identities for a short time - each looks like another real player (their color AND name together). It looks believable, but everyone is someone else. After it ends (or a meeting) everything is normal again. You also keep your normal kill.");

    private const byte ActionClown = 0;

    private RoleOption cooldown, duration;
    private sealed class Orig { public NetworkedPlayerInfo.PlayerOutfit Outfit; public PlayerOutfitType Type; }
    private readonly Dictionary<byte, Orig> originals = new();
    private float until;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 40f, 15f, 120f, 5f, "s");
        duration = AddOption("Duration", "Dauer|Duration", 15f, 5f, 40f, 1f, "s");

        AddButton(new RoleButton
        {
            Label = "Chaos|Scramble",
            Icon = "camo",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && IsAlive(PlayerControl.LocalPlayer),
            OnClick = _ => SendToAll(ActionClown, (byte)UnityEngine.Random.Range(1, 255)),
        });
    }

    public override void ResetState() { RestoreAll(); until = 0f; }
    public override void OnMeetingStart() { RestoreAll(); until = 0f; }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionClown || data.Length < 1 || !RoleRegistry.Has(sender, this)) return;
        RestoreAll();
        int seed = data[0];
        var rng = new System.Random(seed);

        var players = PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p && p.Data != null && IsAlive(p)).OrderBy(p => p.PlayerId).ToList();
        int n = players.Count;
        if (n == 0) return;

        // Konsistente Vertauschung: jeder bekommt Farbe UND Namen eines ANDEREN Spielers (als Paar).
        // Versatz 1..n-1 garantiert, dass niemand sich selbst behält.
        int offset = n > 1 ? 1 + rng.Next(0, n - 1) : 0;

        for (int i = 0; i < n; i++)
        {
            var p = players[i];
            var src = players[(i + offset) % n];
            originals[p.PlayerId] = new Orig { Outfit = p.Data.DefaultOutfit, Type = p.CurrentOutfitType };
            var s = src.Data.DefaultOutfit;
            // Komplettes Aussehen eines anderen übernehmen (Farbe, Name UND Design: Hut/Skin/Visier/Pet)
            var outfit = new NetworkedPlayerInfo.PlayerOutfit
            {
                ColorId = s.ColorId, PlayerName = s.PlayerName,
                HatId = s.HatId, SkinId = s.SkinId, VisorId = s.VisorId,
                PetId = s.PetId, NamePlateId = s.NamePlateId,
            };
            p.RawSetOutfit(outfit, PlayerOutfitType.MushroomMixup);
        }
        until = Time.time + duration.Value;
    }

    public override void Update()
    {
        if (until > 0f && Time.time >= until) { RestoreAll(); until = 0f; }
    }

    private void RestoreAll()
    {
        foreach (var pair in originals)
        {
            var p = FindPlayer(pair.Key);
            if (p && p.Data != null) p.RawSetOutfit(pair.Value.Outfit ?? p.Data.DefaultOutfit, PlayerOutfitType.Default);
        }
        originals.Clear();
    }
}
