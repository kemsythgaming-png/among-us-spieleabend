using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spieleabend.Roles;

/// <summary>
/// Neutral: wickelt einen Spieler ein. Das Opfer stirbt (wird Geist), seine Leiche wird versteckt und an seiner
/// Stelle bleibt eine graue Camouflage-Figur stehen - so verschwindet das Opfer unauffällig.
/// </summary>
public sealed class Mummy : CustomRole
{
    public override string ConfigSection => "Mumie";
    public override string Name => Lang.T("Mumie", "Mummy");
    public override Color Color => new Color32(220, 210, 180, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override string IntroText => Lang.T("Wickel sie ein", "Wrap them up");
    public override string Description => Lang.T("Wickle einen Spieler ein (F):\nKill ohne Leiche.", "Wrap a player (F):\na kill without a body.");
    public override string LongDescription => Lang.T(
        "Neutral: Mit dem Knopf wickelst du einen Spieler in der Nähe ein. Er stirbt (wird Geist), seine Leiche wird versteckt und an seiner Stelle steht eine graue Camouflage-Figur. So fällt der Tod erst spät auf.",
        "Neutral: the button wraps a nearby player. They die (become a ghost), their body is hidden and a gray camouflage figure stands where they were. The death stays unnoticed for a while.");

    internal static Mummy Instance { get; private set; }

    private const byte ActionWrap = 0, ActionWrapped = 1;
    private RoleOption cooldown;
    private readonly HashSet<byte> wrappedDead = new(); // deren Leiche versteckt wird
    private readonly List<GameObject> decoys = new();

    protected override void Setup()
    {
        Instance = this;
        cooldown = AddOption("Cooldown", "Abklingzeit|Cooldown", 30f, 10f, 90f, 2.5f, "s");

        AddButton(new RoleButton
        {
            Label = "Einwickeln|Wrap",
            Icon = "tp",
            NeedsTarget = true,
            SharesKillCooldown = true,
            Cooldown = () => cooldown.Value,
            StartCooldown = 10f,
            Hotkey = KeyCode.F,
            OnClick = target => { if (target) SendToHost(ActionWrap, target.PlayerId); },
        });
    }

    public override void ResetState()
    {
        wrappedDead.Clear();
        ClearDecoys();
    }

    public override void OnMeetingStart()
    {
        wrappedDead.Clear();
        ClearDecoys(); // beim Meeting sind die Toten eh aufgedeckt
    }

    private void ClearDecoys()
    {
        foreach (var d in decoys) if (d) Object.Destroy(d);
        decoys.Clear();
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action == ActionWrap && AmHost && data.Length >= 1)
        {
            var target = FindPlayer(data[0]);
            if (IsAlive(target))
            {
                Vector2 pos = target.GetTruePosition();
                KillSystem.ForceKill(target, target);                            // zuverlässiger Kill (Selbst-Kill, kein Teleport)
                SendToAll(ActionWrapped, WorldMarker.Concat(data[0], WorldMarker.Encode(pos)));
            }
        }
        else if (action == ActionWrapped && data.Length >= 5)
        {
            wrappedDead.Add(data[0]);
            SpawnDecoy(WorldMarker.Decode(data, 1));
        }
    }

    public override void Update()
    {
        // Die Leichen der Eingewickelten verstecken (unsichtbar + nicht anklickbar)
        if (wrappedDead.Count == 0) return;
        foreach (var body in Object.FindObjectsOfType<DeadBody>())
        {
            if (!body || !wrappedDead.Contains(body.ParentId)) continue;
            foreach (var r in body.GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = false;
            foreach (var col in body.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;
        }
    }

    /// <summary>Graue Camouflage-Figur an der Stelle des Opfers.</summary>
    private void SpawnDecoy(Vector2 pos)
    {
        var src = PlayerControl.LocalPlayer ? PlayerControl.LocalPlayer.cosmetics?.currentBodySprite?.BodySprite : null;
        if (!src || !src.sprite) return;

        var go = new GameObject("Spieleabend_MummyDecoy");
        if (ShipStatus.Instance) go.transform.SetParent(ShipStatus.Instance.transform, true);
        go.transform.position = new Vector3(pos.x, pos.y, pos.y / 1000f);
        go.transform.localScale = src.transform.lossyScale; // echte Welt-Größe des Körpers
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = src.sprite;
        try { sr.color = Palette.PlayerColors[15]; } catch { sr.color = new Color(0.5f, 0.5f, 0.56f, 1f); } // echtes Camouflage-Grau
        decoys.Add(go);
    }
}
