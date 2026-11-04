using System.Collections.Generic;
using System.Linq;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles;

/// <summary>
/// Neutral (Rollentausch-Kette): markiert einen Spieler in der Naehe. Beim naechsten Meeting tauschen die beiden ihre
/// Hauptrolle - der Markierte wird also der neue Kettentauscher. Wer am Ende der Runde der Kettentauscher ist, verliert.
/// </summary>
public sealed class ChainShifter : CustomRole
{
    public override string ConfigSection => "Kettentauscher";
    public override string Name => Lang.T("Kettentauscher", "Chain Shifter");
    public override Color Color => new Color32(170, 90, 160, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override string IntroText => Lang.T("Gib ihn weiter!", "Pass it on!");
    public override string Description => Lang.T("Du bist der Kettentauscher - gib ihn weiter!\nBerühr einen Nachbarn (F): beim Meeting tauscht ihr die Rolle. Wer am Rundenende Kettentauscher ist, verliert.", "You are the chain shifter - pass it on!\nTouch a neighbor (F): at the meeting you swap roles. Whoever is chain shifter at round end loses.");
    public override string LongDescription => Lang.T(
        "Du bist neutral. Mit Taste F markierst du einen Spieler direkt neben dir. Beim nächsten Meeting tauscht ihr eure Hauptrolle - der Markierte wird der neue Kettentauscher und du bekommst dessen alte Rolle. Wer am Ende der Runde der Kettentauscher ist, verliert - gib ihn also weiter!",
        "You are neutral. Press F to mark a player right next to you. At the next meeting you swap your main role - the marked player becomes the new chain shifter and you get their old role. Whoever is the chain shifter at the end of the round loses - so pass it on!");

    private const byte ActionMark = 0;
    private RoleOption range;
    private readonly Dictionary<byte, byte> marks = new(); // Host: Tauscher -> markierter Nachbar

    protected override void Setup()
    {
        range = AddOption("Range", "Reichweite|Range", 1.5f, 0.8f, 4f, 0.2f);

        AddButton(new RoleButton
        {
            Label = "Markieren|Mark",
            GameIcon = _ => ChainSprite(),
            Icon = "tp",
            NeedsTarget = false,
            Cooldown = () => 5f,
            StartCooldown = 5f,
            Hotkey = KeyCode.F,
            CanUse = () => !InMeeting && Target() != null,
            OnClick = _ => { var t = Target(); if (t) SendToHost(ActionMark, t.PlayerId); },
        });
    }

    // ---------- Eigenes Symbol: zwei ineinander hängende Kettenglieder ----------
    private static Sprite chainSprite;
    private static Sprite ChainSprite()
    {
        if (chainSprite) return chainSprite;
        const int S = 96;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) tex.SetPixel(x, y, Color.clear);
        var metal = new Color(0.85f, 0.8f, 0.55f, 1f);

        void Ring(float cx, float cy, float rad, float th)
        {
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Abs(new Vector2(x - cx, y - cy).magnitude - rad);
                if (d <= th) tex.SetPixel(x, y, metal);
            }
        }
        Ring(36f, 56f, 18f, 4.5f); // oberes Glied
        Ring(60f, 40f, 18f, 4.5f); // unteres Glied (überlappt)
        tex.Apply();
        chainSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        chainSprite.hideFlags = HideFlags.HideAndDontSave;
        return chainSprite;
    }

    public override void ResetState() => marks.Clear();

    public override void OnAssigned(PlayerControl player)
    {
        if (RoleRegistry.IsRoleChange && player == PlayerControl.LocalPlayer)
            Notifier.Show($"<color={ColorHex}>{Lang.T("Du bist jetzt der Kettentauscher! Gib ihn weiter.", "You are now the chain shifter! Pass it on.")}</color>", 8f);
    }

    private PlayerControl Target()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !IsAlive(local) || local.inVent) return null;
        Vector2 me = local.GetTruePosition();
        return PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p && p != local && IsAlive(p) && !p.inVent && Vector2.Distance(me, p.GetTruePosition()) <= range.Value)
            .OrderBy(p => Vector2.Distance(me, p.GetTruePosition())).FirstOrDefault();
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionMark || !AmHost || data.Length < 1 || !RoleRegistry.Has(sender, this)) return;
        marks[sender.PlayerId] = data[0];
        if (sender == PlayerControl.LocalPlayer) Notifier.Show($"<color={ColorHex}>{Lang.T("Markiert - Tausch beim Meeting.", "Marked - swap at the meeting.")}</color>", 3f);
    }

    public override void OnMeetingEnd(PlayerControl exiled)
    {
        if (!AmHost) return;
        foreach (var pair in marks.ToList())
        {
            var shifter = FindPlayer(pair.Key);
            var neighbor = FindPlayer(pair.Value);
            if (!shifter || !neighbor || RoleRegistry.GetRole(shifter) != this) continue;
            var oldNeighborRole = RoleRegistry.GetRole(neighbor); // kann null sein
            RoleRegistry.ChangeRole(shifter, oldNeighborRole);
            RoleRegistry.ChangeRole(neighbor, this);
        }
        marks.Clear();
    }
}
