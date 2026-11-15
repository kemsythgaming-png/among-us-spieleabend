using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Crew (Tank): hält mehrere Angriffe aus. Jeder Kill-Versuch (Impostor) und jeder Guesser-Schuss verbraucht nur eine
/// Panzerung und setzt den Angreifer auf Abklingzeit - erst wenn die letzte Panzerung weg ist, stirbt der Tank.
/// Unten an den Knöpfen sieht er seine verbleibende Panzerung; nach jedem Meeting bekommt er eine Info.
/// </summary>
public sealed class Tank : CustomRole
{
    public override string ConfigSection => "Tank";
    public override string Name => Lang.T("Tank", "Tank");
    public override Color Color => new Color32(120, 130, 140, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Halt was aus", "Take the hits");
    public override string Description => Lang.T("Du überlebst mehrere Angriffe.\nErst der letzte tötet dich.", "You survive several attacks.\nOnly the last one kills you.");
    public override string LongDescription => Lang.T(
        "Du bist Crew und hältst mehrere Angriffe aus (Standard: 2 Panzerungen). Jeder Kill-Versuch (Impostor, Guesser usw.) kostet dich NUR EINE Panzerung - du stirbst nicht und der Angreifer kommt auf Abklingzeit. Erst der Angriff, bei dem keine Panzerung mehr übrig ist, tötet dich. Im nächsten Meeting erscheint mittig in Crew-Blau, wie oft ein Angriff abgewehrt wurde - das sieht nur du.",
        "You are crew and survive several attacks (default: 2 armor). Every kill attempt (impostor, Guesser, etc.) costs you ONLY ONE armor - you don't die and the attacker goes on cooldown. Only the attack with no armor left kills you. In the next meeting a message appears centered in crew blue showing how many attacks were absorbed - only you see it.");

    private const byte ActionArmor = 0;

    private RoleOption startStacks;
    private static readonly Dictionary<byte, int> armor = new(); // Spieler-Id -> verbleibende Panzerung
    private int absorbedSinceMeeting; // nur beim Tank: abgewehrte Angriffe seit dem letzten Meeting
    private string centerMsg;
    private float centerUntil;
    private static GUIStyle centerStyle;

    private int Max => startStacks != null ? Mathf.RoundToInt(startStacks.Value) : 2;
    private int ArmorOf(PlayerControl p) => p && armor.TryGetValue(p.PlayerId, out var a) ? a : Max;

    protected override void Setup()
    {
        startStacks = AddOption("Stacks", "Panzerung|Armor", 2f, 1f, 5f, 1f);
        // Kein Knopf/keine Stack-Anzeige - die Panzerung wirkt passiv; Info kommt nur im Meeting.
    }

    public override void ResetState() { armor.Clear(); absorbedSinceMeeting = 0; }

    public override KillIntercept InterceptKill(PlayerControl killer, PlayerControl target)
    {
        if (!AmHost || !RoleRegistry.Has(target, this) || !IsAlive(target) || killer == target) return KillIntercept.None;

        int cur = ArmorOf(target);
        if (cur > 1)
        {
            cur--;
            armor[target.PlayerId] = cur;
            SendToAll(ActionArmor, target.PlayerId, (byte)cur);
            // Angreifer auf Abklingzeit (kein Teleport) - der Angriff "verpufft".
            if (IsImpostor(killer) && GameManager.Instance)
                try { killer.SetKillTimer(GameManager.Instance.LogicOptions.GetKillCooldown()); } catch { }
            return KillIntercept.Handled; // Angriff abgefangen, Tank lebt weiter
        }

        // letzte Panzerung weg -> der Angriff tötet den Tank normal
        armor[target.PlayerId] = 0;
        SendToAll(ActionArmor, target.PlayerId, 0);
        return KillIntercept.None;
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionArmor || data.Length < 2) return;
        bool absorbed = data[1] >= 1; // >=1 = abgefangen (Panzerung übrig), 0 = tödlicher Treffer
        armor[data[0]] = data[1];
        var local = PlayerControl.LocalPlayer;
        if (absorbed && local && local.PlayerId == data[0] && RoleRegistry.Has(local, this))
            absorbedSinceMeeting++; // kein Ton, kein Live-Hinweis - Info kommt erst im nächsten Meeting
    }

    // Nur der Tank selbst erfährt im Meeting, dass (und wie oft) ein Angriff auf ihn abgewehrt wurde.
    public override void OnMeetingStart()
    {
        var local = PlayerControl.LocalPlayer;
        if (!RoleRegistry.Has(local, this) || !IsAlive(local)) return;
        if (absorbedSinceMeeting > 0)
        {
            string msg = absorbedSinceMeeting == 1
                ? Lang.T("Ein Angriff auf dich wurde abgewehrt!", "An attack on you was absorbed!")
                : Lang.T($"{absorbedSinceMeeting} Angriffe auf dich wurden abgewehrt!", $"{absorbedSinceMeeting} attacks on you were absorbed!");
            centerMsg = $"{msg}\n{Lang.T($"Panzerung: {ArmorOf(local)}", $"Armor: {ArmorOf(local)}")}";
            centerUntil = Time.time + 12f; // reicht bis ins Voting-Fenster; angezeigt wird nur solange MeetingHud offen (DrawGUI)
            absorbedSinceMeeting = 0;
        }
    }

    public override void DrawGUI()
    {
        if (!IsLocalRole || string.IsNullOrEmpty(centerMsg) || Time.time >= centerUntil) return;
        if (!MeetingHud.Instance) return; // erst wenn das Voting-Fenster offen ist (nach Intro/Report-Animation)
        centerStyle ??= new GUIStyle(Ui.Text) { fontSize = 40, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
        float sw = Ui.ScreenWidth;
        GUI.Label(new Rect(0, 360, sw, 160), $"<color=#{ColorUtility.ToHtmlStringRGB(CrewBlue)}>{centerMsg}</color>", centerStyle);
    }
}
