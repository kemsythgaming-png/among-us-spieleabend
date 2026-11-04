using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Neutral: steckt Spieler an. Ein angesteckter Spieler stirbt nach einer festen Zeit (Standard 2 Minuten). Die
/// Ansteckung springt in der Naehe auf weitere Spieler ueber. Reine Host-Logik, laeuft unsichtbar.
/// </summary>
public sealed class Syphilis : CustomRole
{
    public override string ConfigSection => "Ansteckung";
    public override string Name => Lang.T("Überträger", "Carrier");
    public override Color Color => new Color32(180, 100, 180, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override string IntroText => Lang.T("Teile großzügig", "Share generously");
    public override string Description => Lang.T("Stecke jemanden an (F).\nEr stirbt nach 2 Minuten.", "Infect someone (F).\nThey die after 2 minutes.");
    public override string LongDescription => Lang.T(
        "Du bist neutral. Mit Taste F steckst du den nächsten Spieler an. Ein Angesteckter stirbt nach 2 Minuten - und steckt in der Nähe weitere Spieler an, die dann ebenfalls ihren eigenen Countdown bekommen. Unsichtbar: niemand sieht die Ansteckung.",
        "You are neutral. Press F to infect the nearest player. An infected player dies after 2 minutes - and infects others nearby, who then get their own countdown. Invisible: nobody sees the infection.");

    private const byte ActionInfect = 0;

    private RoleOption cooldown, deathDelay, spreadRadius;
    private readonly Dictionary<byte, float> deathAt = new(); // Host: Spieler -> Todeszeit
    private float nextSpread;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 20f, 5f, 60f, 5f, "s");
        deathDelay = AddOption("Delay", "Zeit bis Tod|Time to death", 120f, 30f, 240f, 10f, "s");
        spreadRadius = AddOption("Spread", "Ansteckungs-Radius|Spread radius", 1.2f, 0f, 4f, 0.2f);

        AddButton(new RoleButton
        {
            Label = "Anstecken|Infect",
            Icon = "curse",
            NeedsTarget = true,
            TargetFilter = p => IsAlive(p),
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting,
            OnClick = target => { if (target) SendToHost(ActionInfect, target.PlayerId); },
        });
    }

    public override void ResetState() { deathAt.Clear(); nextSpread = 0f; }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionInfect || !AmHost || data.Length < 1 || !RoleRegistry.Has(sender, this)) return;
        Infect(data[0]);
    }

    private void Infect(byte id)
    {
        var p = FindPlayer(id);
        if (!IsAlive(p) || deathAt.ContainsKey(id)) return;
        deathAt[id] = Time.time + deathDelay.Value;
    }

    public override void Update()
    {
        if (!AmHost || deathAt.Count == 0) return;
        float now = Time.time;

        // Ausbreitung in der Naehe (nicht jeden Frame)
        if (spreadRadius.Value > 0f && now >= nextSpread)
        {
            nextSpread = now + 1f;
            foreach (var id in deathAt.Keys.ToList())
            {
                var carrier = FindPlayer(id);
                if (!IsAlive(carrier) || carrier.inVent) continue;
                Vector2 at = carrier.GetTruePosition();
                foreach (var p in PlayerControl.AllPlayerControls)
                    if (IsAlive(p) && !p.inVent && !deathAt.ContainsKey(p.PlayerId) && Vector2.Distance(at, p.GetTruePosition()) <= spreadRadius.Value)
                        deathAt[p.PlayerId] = now + deathDelay.Value;
            }
        }

        // Faellige Tode
        foreach (var pair in deathAt.Where(kv => now >= kv.Value).ToList())
        {
            deathAt.Remove(pair.Key);
            var victim = FindPlayer(pair.Key);
            if (IsAlive(victim)) KillSystem.KillNoBody(victim);
        }
    }
}
