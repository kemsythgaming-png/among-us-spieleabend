using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Neutral: uebergiesst Spieler mit Benzin und zuendet sie an - alle Uebergossenen gehen in Flammen auf und sterben.
/// Sind danach alle anderen tot, gewinnt er allein. Zuendungen pro Spiel sind begrenzt.
/// </summary>
public sealed class Arsonist : CustomRole
{
    public override string Name =>
        Lang.T("Arsonist", "Arsonist");
    public override Color Color => new Color32(238, 112, 46, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override string IntroText =>
        Lang.T("Übergieß sie und zünde sie an", "Douse them and ignite");
    public override string Description =>
        Lang.T("Übergieße Spieler (F),\nzünde sie an (G).", "Douse players (F),\nignite them (G).");
    public override string LongDescription =>
        Lang.T("Du bist neutral. Mit Taste F übergießt du Spieler mit Benzin - sie merken nichts ([Benzin] am Namen, nur du siehst es).\n\nMit Taste G zündest du an: Alle Übergossenen gehen in Flammen auf und sterben. Sind danach alle anderen tot, gewinnst du allein. Du hast nur wenige Zündungen.",
               "You are neutral. Press F to douse players with fuel - they don't notice ([Doused] next to their name, only you see it).\n\nPress G to ignite: everyone doused bursts into flames and dies. If everyone else is dead afterwards, you win alone. You only have a few ignitions.");

    private const byte ActionDouse = 0, ActionIgnite = 1, ActionBurn = 2;
    private const float BurnTime = 1.6f;

    private RoleOption cooldown, ignitions;

    /// <summary>Arsonist -> uebergossene Spieler (bei allen Clients bekannt).</summary>
    private readonly Dictionary<byte, HashSet<byte>> doused = new();

    private int ignitionsLeft;                                                     // lokal beim Arsonist
    private readonly Dictionary<byte, int> ignitionsUsed = new();                   // beim Host
    private readonly List<(byte Arsonist, List<byte> Victims, float At)> burning = new(); // beim Host

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 15f, 5f, 60f, 2.5f, "s");
        ignitions = AddOption("Ignitions", "Zündungen pro Spiel|Ignitions per game", 1f, 1f, 3f, 1f);

        AddButton(new RoleButton
        {
            Label = "Übergießen|Douse",
            Icon = "douse",
            Cooldown = () => cooldown.Value,
            Hotkey = KeyCode.F,
            TargetFilter = p => !IsDoused(PlayerControl.LocalPlayer, p),
            OnClick = target => SendToAll(ActionDouse, target.PlayerId),
        });

        AddButton(new RoleButton
        {
            Label = "Anzünden|Ignite",
            Icon = "ignite",
            Cooldown = () => 1f,
            StartCooldown = 1f,
            Hotkey = KeyCode.G,
            NeedsTarget = false,
            // sichtbar, sobald mindestens ein lebender Spieler uebergossen ist
            Visible = () => ignitionsLeft > 0 && DousedAlive(PlayerControl.LocalPlayer).Any(),
            OnClick = _ =>
            {
                ignitionsLeft--;
                SendToHost(ActionIgnite);
            },
        });
    }

    public override void ResetState()
    {
        doused.Clear();
        ignitionsUsed.Clear();
        burning.Clear();
    }

    public override void OnAssigned(PlayerControl player)
    {
        if (player == PlayerControl.LocalPlayer) ignitionsLeft = ignitions.Int;
    }

    private bool IsDoused(PlayerControl arsonist, PlayerControl p) =>
        arsonist && doused.TryGetValue(arsonist.PlayerId, out var set) && set.Contains(p.PlayerId);

    private IEnumerable<PlayerControl> DousedAlive(PlayerControl arsonist) =>
        PlayerControl.AllPlayerControls.ToArray().Where(p => IsAlive(p) && p != arsonist && IsDoused(arsonist, p));

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        switch (action)
        {
            case ActionDouse when data.Length > 0 && RoleRegistry.Has(sender, this):
            {
                if (!doused.TryGetValue(sender.PlayerId, out var set)) doused[sender.PlayerId] = set = new HashSet<byte>();
                set.Add(data[0]);
                if (sender == PlayerControl.LocalPlayer) Sfx.Play(Sfx.Splash);
                break;
            }

            case ActionIgnite when AmHost && RoleRegistry.Has(sender, this) && IsAlive(sender) && !InMeeting:
            {
                ignitionsUsed.TryGetValue(sender.PlayerId, out int used);
                if (used >= ignitions.Int) return;
                var victims = DousedAlive(sender).Select(p => p.PlayerId).ToList();
                if (victims.Count == 0) return;
                ignitionsUsed[sender.PlayerId] = used + 1;

                // Alle sehen das Feuer; [0] = Arsonist, danach die Opfer
                SendToAll(ActionBurn, new[] { sender.PlayerId }.Concat(victims).ToArray());
                burning.Add((sender.PlayerId, victims, Time.time + BurnTime));
                break;
            }

            case ActionBurn when data.Length >= 2:
            {
                // Bei allen Clients: Flammen an den Opfern, danach ist das Benzin "verbraucht"
                Sfx.Play(Sfx.Alarm); // alle hoeren das Feuer
                foreach (var id in data.Skip(1))
                {
                    FireEffect.Play(FindPlayer(id), BurnTime + 0.4f);
                    if (doused.TryGetValue(data[0], out var set)) set.Remove(id);
                }
                break;
            }
        }
    }

    // Beim Host: nach der Feuer-Animation sterben die Opfer (auch sofort, falls ein Meeting beginnt)
    public override void Update()
    {
        if (!AmHost || burning.Count == 0) return;
        foreach (var burn in burning.Where(b => Time.time >= b.At).ToList()) Burn(burn);
    }

    public override void OnMeetingStart()
    {
        if (!AmHost) return;
        foreach (var burn in burning.ToList()) Burn(burn);
    }

    private void Burn((byte Arsonist, List<byte> Victims, float At) burn)
    {
        burning.Remove(burn);
        var arsonist = FindPlayer(burn.Arsonist);
        foreach (var id in burn.Victims)
        {
            var victim = FindPlayer(id);
            if (!IsAlive(victim)) continue;
            if (victim.protectedByGuardianId >= 0 || KillSystem.Intercept(arsonist, victim)) continue; // Schild/Schutz
            KillSystem.KillNoBody(victim, arsonist);
        }

        // Alle anderen tot? Dann gewinnt der Arsonist
        if (IsAlive(arsonist) && !PlayerControl.AllPlayerControls.ToArray().Any(p => IsAlive(p) && p != arsonist))
            CustomWin.Trigger(this, new[] { arsonist });
    }

    public override string GetNameMark(PlayerControl viewer, PlayerControl target) =>
        RoleRegistry.Has(viewer, this) && IsDoused(viewer, target) ? $"<color={ColorHex}>{Lang.T("[Benzin]", "[Doused]")}</color>" : null;
}
