using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Halloween;

/// <summary>
/// Crew: stellt kurz eine Kuerbislaterne auf. In ihrem Schein sieht jeder normal (auch bei Nebel und Licht-Sabotage)
/// und Impostor koennen dort niemanden toeten.
/// </summary>
public sealed class LanternBearer : CustomRole
{
    public override string ConfigSection => "Laternentraeger";
    public override string Name => Lang.T("Laternenträger", "Lantern Bearer");
    public override Color Color => new Color32(255, 200, 80, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Bring Licht ins Dunkel", "Bring light into the dark");
    public override string Description => Lang.T("Stelle eine Laterne auf (F):\nLicht und Schutz für alle darin.", "Place a lantern (F):\nlight and safety for everyone in it.");
    public override string LongDescription => Lang.T(
        "Halloween-Crew. Mit Taste F stellst du für ein paar Sekunden eine leuchtende Kürbislaterne auf. Alle sehen sie.\n\nIn ihrem Schein sieht jeder normal weit - auch bei Nebel oder Licht-Sabotage. Und solange sie brennt, können Impostor dort niemanden töten.",
        "Halloween crew. Press F to place a glowing pumpkin lantern for a few seconds. Everyone can see it.\n\nIn its light everyone sees normally - even in fog or during the lights sabotage. And while it burns, impostors can't kill anyone in it.");

    private const byte ActionPlace = 0, ActionBlocked = 1;

    internal static LanternBearer Instance { get; private set; }

    private RoleOption cooldown, duration, radius, protects;

    private sealed class Lantern
    {
        public Vector2 Pos;
        public float Until;
        public GameObject Visual;
    }

    // bei allen Clients
    private readonly List<Lantern> lanterns = new();

    protected override void Setup()
    {
        Instance = this;
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 30f, 10f, 90f, 2.5f, "s");
        duration = AddOption("Duration", "Brenndauer|Burn time", 12f, 3f, 30f, 1f, "s");
        radius = AddOption("Radius", "Lichtschein|Light radius", 2.5f, 1f, 5f, 0.5f);
        protects = AddToggle("Protects", "Kein Töten im Licht|No killing in the light", true);

        AddButton(new RoleButton
        {
            Label = "Laterne|Lantern",
            Icon = "lantern",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            Duration = () => duration.Value,
            Hotkey = KeyCode.F,
            CanUse = () => !PlayerControl.LocalPlayer.inVent,
            OnClick = _ => SendToAll(ActionPlace, WorldMarker.Encode(PlayerControl.LocalPlayer.GetTruePosition())),
        });
    }

    public override void ResetState() => ClearLanterns();

    public override void OnMeetingStart() => ClearLanterns();

    private void ClearLanterns()
    {
        foreach (var l in lanterns)
            if (l.Visual) Object.Destroy(l.Visual);
        lanterns.Clear();
    }

    /// <summary>Steht pos gerade im Schein einer Laterne?</summary>
    internal bool InLight(Vector2 pos) => lanterns.Any(l => l.Until > Time.time && Vector2.Distance(pos, l.Pos) <= radius.Value);

    public override void Update()
    {
        if (lanterns.Count == 0) return;
        float t = Time.time;
        for (int i = lanterns.Count - 1; i >= 0; i--)
        {
            var l = lanterns[i];
            if (l.Until <= t)
            {
                if (l.Visual) Object.Destroy(l.Visual);
                lanterns.RemoveAt(i);
                continue;
            }
            if (l.Visual)
            {
                float s = 0.4f + Mathf.Sin(t * 3f + i) * 0.02f; // leichtes Flackern
                l.Visual.transform.localScale = new Vector3(s, s, 1f);
            }
        }
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionPlace && data.Length >= 4 && RoleRegistry.Has(sender, this))
        {
            var pos = WorldMarker.Decode(data, 0);
            lanterns.Add(new Lantern { Pos = pos, Until = Time.time + duration.Value, Visual = WorldMarker.Spawn("lantern", pos + new Vector2(0f, 0.2f), 0.4f, "Lantern") });
            if (sender == PlayerControl.LocalPlayer) Sfx.Play(Sfx.Appear);
        }
        else if (action == ActionBlocked && data.Length >= 1)
        {
            var target = FindPlayer(data[0]);
            if (target) ProtectionVisuals.ShowBurst(target, Color);
            if (target == PlayerControl.LocalPlayer) Sfx.Play(Sfx.Protect);
        }
    }

    public override KillIntercept InterceptKill(PlayerControl killer, PlayerControl target)
    {
        if (!protects.Bool || !IsImpostor(killer) || killer == target || !InLight(target.GetTruePosition())) return KillIntercept.None;
        killer.RpcMurderPlayer(target, false); // Kill scheitert sichtbar, Abklingzeit des Killers startet neu
        SendToAll(ActionBlocked, target.PlayerId);
        return KillIntercept.Handled;
    }
}

/// <summary>Im Laternenschein volle Sicht - laeuft als letztes, damit es Nebel und Licht-Sabotage aufhebt.</summary>
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CalculateLightRadius))]
internal static class LanternVisionPatch
{
    [HarmonyPriority(Priority.Last)]
    public static void Postfix(ShipStatus __instance, NetworkedPlayerInfo player, ref float __result)
    {
        var lb = LanternBearer.Instance;
        if (lb == null || player == null || player.IsDead || !player.Object) return;
        if (!lb.InLight(player.Object.GetTruePosition())) return;
        float crewLight = GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.CrewLightMod);
        __result = Mathf.Max(__result, __instance.MaxLightRadius * crewLight);
    }
}
