using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Halloween;

/// <summary>Crew: stellt Vogelscheuchen auf, die nur sie sieht. Laeuft jemand vorbei, erfaehrt sie, wer es war.</summary>
public sealed class Scarecrow : CustomRole
{
    public override string ConfigSection => "Vogelscheuche";
    public override string Name => Lang.T("Vogelscheuche", "Scarecrow");
    public override Color Color => new Color32(210, 175, 90, 255);
    public override RoleTeam Team => RoleTeam.Crewmate;
    public override string IntroText => Lang.T("Deine Vogelscheuchen sehen alles", "Your scarecrows see everything");
    public override string Description => Lang.T("Stelle Vogelscheuchen auf (F).\nSie melden, wer vorbeiläuft.", "Place scarecrows (F).\nThey report who walks by.");
    public override string LongDescription => Lang.T(
        "Halloween-Crew. Mit Taste F stellst du dort, wo du stehst, eine Vogelscheuche auf. Nur du siehst sie.\n\nLäuft jemand an einer Vogelscheuche vorbei, bekommst du eine Nachricht mit seinem Namen - auch wenn du weit weg bist. Während einer Tarnung steht dort nur \"Jemand\".",
        "Halloween crew. Press F to place a scarecrow where you stand. Only you can see it.\n\nWhenever someone walks past a scarecrow, you get a message with their name - even when you are far away. During a camouflage it only says \"Someone\".");

    private const float RepeatDelay = 15f; // dieselbe Person an derselben Vogelscheuche erst wieder nach 15 s melden

    private RoleOption cooldown, maxCount, range;

    // nur lokal bei der Vogelscheuche
    private readonly List<(Vector2 Pos, GameObject Visual)> scarecrows = new();
    private readonly Dictionary<(int, byte), float> lastSeen = new();

    protected override void Setup()
    {
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 15f, 5f, 60f, 2.5f, "s");
        maxCount = AddOption("Max", "Vogelscheuchen|Scarecrows", 3f, 1f, 6f, 1f);
        range = AddOption("Range", "Reichweite|Range", 2.5f, 1f, 5f, 0.5f);

        AddButton(new RoleButton
        {
            Label = "Aufstellen|Place",
            Icon = "scarecrow",
            NeedsTarget = false,
            Cooldown = () => cooldown.Value,
            Hotkey = KeyCode.F,
            CanUse = () => scarecrows.Count < maxCount.Int,
            OnClick = _ =>
            {
                var pos = PlayerControl.LocalPlayer.GetTruePosition();
                scarecrows.Add((pos, WorldMarker.Spawn("scarecrow", pos + new Vector2(0f, 0.3f), 0.45f, "Scarecrow")));
                Sfx.Play(Sfx.TaskUpdate);
            },
        });
    }

    public override void ResetState()
    {
        foreach (var s in scarecrows)
            if (s.Visual) Object.Destroy(s.Visual);
        scarecrows.Clear();
        lastSeen.Clear();
    }

    public override void Update()
    {
        if (!IsLocalRole || scarecrows.Count == 0) return;
        var local = PlayerControl.LocalPlayer;
        bool active = IsAlive(local) && !InMeeting;
        foreach (var s in scarecrows)
            if (s.Visual) s.Visual.SetActive(active);
        if (!active) return;

        for (int i = 0; i < scarecrows.Count; i++)
        {
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (p == local || !IsAlive(p) || p.inVent) continue;
                if (Vector2.Distance(p.GetTruePosition(), scarecrows[i].Pos) > range.Value) continue;

                var key = (i, p.PlayerId);
                if (lastSeen.TryGetValue(key, out float t) && Time.time - t < RepeatDelay)
                {
                    lastSeen[key] = Time.time; // steht noch daneben - nicht erneut melden
                    continue;
                }
                lastSeen[key] = Time.time;

                string who = Camouflage.Active ? Lang.T("Jemand", "Someone") : p.Data.PlayerName;
                Sfx.Play(Sfx.TaskUpdate, 0.5f);
                Notifier.Show($"<color={ColorHex}>{Lang.T("Vogelscheuche", "Scarecrow")} {i + 1}: {who}</color>", 3f);
            }
        }
    }
}
