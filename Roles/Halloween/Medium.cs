using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Halloween;

/// <summary>Crew: befragt Leichen und erfaehrt, wie lange der Tod her ist und welche Rolle der Moerder hat.</summary>
public sealed class Medium : CustomRole
{
    public override string Name => Lang.T("Medium", "Medium");
    public override Color Color => new Color32(120, 160, 230, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Die Toten sprechen zu dir", "The dead speak to you");
    public override string Description => Lang.T("Befrage Leichen (F): Wann starben sie,\nwer war der Mörder?", "Question bodies (F): when did they die,\nwho was the killer?");
    public override string LongDescription => Lang.T(
        "Halloween-Crew. Stehst du an einer Leiche, kannst du sie mit Taste F befragen, statt sie gleich zu melden.\n\nDu erfährst, vor wie vielen Sekunden der Spieler starb und welche Rolle sein Mörder hat (je nach Einstellung nur das Team). Wen der Mörder getötet hat, sagt der Geist nicht - nur was er ist.",
        "Halloween crew. When you stand at a body, press F to question it instead of reporting it right away.\n\nYou learn how many seconds ago the player died and which role their killer has (or only the team, depending on settings). The ghost doesn't tell you who the killer is - only what they are.");

    private RoleOption cooldown, exactRole;

    // bei allen Clients: Opfer -> (Moerder oder 255, Todeszeit)
    private readonly Dictionary<byte, (byte Killer, float Time)> deaths = new();
    private readonly HashSet<byte> questioned = new();

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 20f, 5f, 60f, 2.5f, "s");
        exactRole = AddToggle("ExactRole", "Zeigt die genaue Rolle des Mörders|Shows the killer's exact role", true);

        AddButton(new RoleButton
        {
            Label = "Befragen|Question",
            Icon = "seance",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            Hotkey = KeyCode.F,
            CanUse = () => NearbyBody() != null,
            OnClick = _ => Question(NearbyBody()),
        });
    }

    public override void ResetState()
    {
        deaths.Clear();
        questioned.Clear();
    }

    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        if (cause == DeathCause.Kill) deaths[victim.PlayerId] = (killer ? killer.PlayerId : byte.MaxValue, Time.time);
    }

    private DeadBody NearbyBody()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || local.inVent) return null;
        Vector2 me = local.GetTruePosition();
        DeadBody best = null;
        float bestDist = local.MaxReportDistance;
        foreach (var body in Object.FindObjectsOfType<DeadBody>())
        {
            if (!body || body.Reported || questioned.Contains(body.ParentId)) continue;
            float dist = Vector2.Distance(me, body.TruePosition);
            if (dist > bestDist) continue;
            best = body;
            bestDist = dist;
        }
        return best;
    }

    private void Question(DeadBody body)
    {
        if (!body) return;
        questioned.Add(body.ParentId);
        var victim = FindPlayer(body.ParentId);
        string victimName = victim ? victim.Data.PlayerName : "?";

        string text;
        if (!deaths.TryGetValue(body.ParentId, out var death))
        {
            text = Lang.T("Der Geist schweigt ...", "The ghost stays silent ...");
        }
        else
        {
            int seconds = Mathf.RoundToInt(Time.time - death.Time);
            var killer = FindPlayer(death.Killer);
            string killerInfo;
            if (!killer || killer.PlayerId == body.ParentId)
                killerInfo = Lang.T("Der Mörder blieb im Schatten.", "The killer stayed in the shadows.");
            else if (exactRole.Bool)
                killerInfo = $"{Lang.T("Mörder", "Killer")}: {RoleUI.RoleLabel(killer)}";
            else if (IsImpostor(killer))
                killerInfo = $"{Lang.T("Mörder", "Killer")}: <color=#FF1919>Impostor</color>";
            else if (IsNeutral(killer))
                killerInfo = $"{Lang.T("Mörder", "Killer")}: <color=#AAAAAA>Neutral</color>";
            else
                killerInfo = $"{Lang.T("Mörder", "Killer")}: <color=#8CFFFF>Crew</color>";

            text = $"{victimName} {Lang.T("starb vor", "died")} {seconds} s{Lang.T("", " ago")}.\n{killerInfo}";
        }

        Sfx.Play(Sfx.Investigate);
        Notifier.Show($"<color={ColorHex}>{text}</color>", 7f);
    }
}
