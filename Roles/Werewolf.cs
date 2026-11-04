using System.Linq;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Impostor: aktiviert einen Rausch (Rampage). Waehrend des Rausches ist er schneller und toetet automatisch jeden
/// Nicht-Impostor in Reichweite in kurzen Abstaenden - so kann er in wenigen Sekunden mehrere Spieler erwischen.
/// </summary>
public sealed class Werewolf : CustomRole
{
    public override string ConfigSection => "Werwolf";
    public override string Name => Lang.T("Werwolf", "Werewolf");
    public override Color Color => new Color32(120, 90, 60, 255);
    public override RoleTeam Team => RoleTeam.Impostor;
    public override string IntroText => Lang.T("Lass das Tier raus", "Let the beast out");
    public override string Description => Lang.T("Rausch (F, 3 s): tötet\nalles in der Nähe.", "Rampage (F, 3 s): kills\neverything nearby.");
    public override string LongDescription => Lang.T(
        "Impostor: Mit Taste F startest du für 3 Sekunden einen Rausch - in dieser Zeit stirbt jeder Nicht-Impostor, der dir nahekommt, sofort. Danach läuft eine lange Abklingzeit, und dein normaler Kill ist beim Rausch ebenfalls gesperrt.",
        "Impostor: press F to start a 3-second rampage - during it every non-impostor who comes close dies instantly. Then a long cooldown runs, and your normal kill is also locked by the rampage.");

    private const byte ActionKill = 0;

    private const float RampageTime = 3f; // fest 3 Sekunden (nicht über die Config änderbar)
    private RoleOption cooldown, range;
    private float rampageUntil;
    private GameObject aura;

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 40f, 15f, 120f, 5f, "s");
        range = AddOption("Range", "Reichweite|Range", 1.4f, 0.8f, 3f, 0.2f);

        AddButton(new RoleButton
        {
            Label = "Rausch|Rampage",
            Icon = "bite",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && rampageUntil <= Time.time,
            OnClick = _ =>
            {
                rampageUntil = Time.time + RampageTime;
                Sfx.Play(Sfx.Creepy, 0.8f); // Wolfs-Heulen (nächster Spiel-Sound)
                // Normaler Kill ist beim Rausch ebenfalls gesperrt (gemeinsamer Cooldown)
                var l = PlayerControl.LocalPlayer;
                if (l) try { l.SetKillTimer(cooldown.Value); } catch { }
            },
        });
    }

    public override void ResetState() { rampageUntil = 0f; if (aura) Object.Destroy(aura); aura = null; }
    public override void OnMeetingStart() { rampageUntil = 0f; if (aura) aura.SetActive(false); }

    private bool Rampaging => rampageUntil > Time.time && !InMeeting;
    // kein Tempo-Boost mehr

    public override void Update()
    {
        if (!IsLocalRole) return;
        var local = PlayerControl.LocalPlayer;
        bool on = IsAlive(local) && Rampaging;

        // Rausch-Anzeige: pulsierender roter Ring um den Werwolf
        if (on)
        {
            if (!aura) aura = CreateAura(local);
            if (aura)
            {
                aura.SetActive(true);
                float s = 1.6f + 0.15f * Mathf.Sin(Time.time * 10f);
                aura.transform.localScale = new Vector3(s, s, 1f);
            }
        }
        else if (aura) aura.SetActive(false);

        if (!on) return;

        // Alles in Reichweite töten (nicht nur einen)
        Vector2 me = local.GetTruePosition();
        foreach (var p in PlayerControl.AllPlayerControls.ToArray())
            if (p && p != local && IsAlive(p) && !IsImpostor(p) && !p.inVent
                && Vector2.Distance(me, p.GetTruePosition()) <= range.Value)
                SendToHost(ActionKill, p.PlayerId);
    }

    /// <summary>Großer roter „RAUSCH"-Hinweis mit Restzeit.</summary>
    public override void DrawGUI()
    {
        if (!IsLocalRole || !Rampaging) return;
        float left = rampageUntil - Time.time;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = new Color(1f, 0.3f, 0.2f, 0.9f);
        GUI.Label(new Rect(Ui.ScreenWidth / 2f - 300f, 120f, 600f, 60f), $"{Lang.T("RAUSCH", "RAMPAGE")}  {left:0.0}s", style);
    }

    private GameObject CreateAura(PlayerControl local)
    {
        var go = new GameObject("Spieleabend_WolfAura");
        go.transform.SetParent(local.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, 0.1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Shapes.Ring();
        sr.color = new Color(1f, 0.25f, 0.2f, 0.75f);
        return go;
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionKill || data.Length < 1 || !AmHost || !RoleRegistry.Has(sender, this) || !IsAlive(sender)) return;
        var target = FindPlayer(data[0]);
        if (IsAlive(target)) KillSystem.TryKill(sender, target);
    }
}
