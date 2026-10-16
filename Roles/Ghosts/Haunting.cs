using System.Collections.Generic;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Ghosts;

/// <summary>
/// Neutral-Geist: ein toter Crewmate, den alle Lebenden halb durchsichtig sehen. Schafft er alle seine Aufgaben,
/// gewinnt er allein. Klickt ihn ein Lebender an, ist er erloest und kann nicht mehr gewinnen.
/// </summary>
public sealed class Haunting : CustomRole
{
    public override string ConfigSection => "Spukgestalt";
    public override string Name => Lang.T("Spukgestalt", "Haunting");
    public override Color Color => new Color32(200, 170, 255, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override bool IsGhostRole => true;
    public override bool CanHaunt(PlayerControl dead) => !IsImpostor(dead) && !IsNeutral(dead);
    public override string IntroText => Lang.T("Spuk dich zum Sieg", "Haunt your way to victory");
    public override string Description => Lang.T("Geist: Alle sehen dich halb. Schaff deine\nAufgaben, ohne angeklickt zu werden!", "Ghost: everyone half-sees you. Finish your\ntasks without being clicked!");
    public override string LongDescription => Lang.T(
        "Geisterrolle für tote Crewmates. Die Lebenden sehen dich als halb durchsichtigen Geist.\n\nSchaffst du alle deine Aufgaben, gewinnst du allein und das Spiel endet. Klickt dich aber ein Lebender an, bist du erlöst - dann bist du ein ganz normaler Geist und kannst nicht mehr gewinnen.",
        "Ghost role for dead crewmates. The living see you as a half-transparent ghost.\n\nFinish all your tasks and you win alone - the game ends. But if a living player clicks you, you are released - then you are a normal ghost and can't win anymore.");

    private const byte ActionRelease = 0;
    private const float ClickRadius = 0.7f, ClickReach = 3f;

    private readonly HashSet<byte> released = new();
    private readonly HashSet<byte> shownToLiving = new();

    protected override void Setup() { }

    public override void ResetState()
    {
        HideAll();
        released.Clear();
    }

    /// <summary>Spukt dieser Spieler gerade (Geist mit dieser Rolle, nicht erloest)?</summary>
    private bool Haunts(PlayerControl p) =>
        p && p.Data != null && p.Data.IsDead && !p.Data.Disconnected && RoleRegistry.Has(p, this) && !released.Contains(p.PlayerId);

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (action != ActionRelease || data.Length < 1 || !IsAlive(sender)) return;
        var ghost = FindPlayer(data[0]);
        if (!Haunts(ghost)) return;
        released.Add(ghost.PlayerId);
        Hide(ghost);

        var local = PlayerControl.LocalPlayer;
        if (local == sender || local == ghost) Sfx.Play(Sfx.Protect);
        if (local == sender)
            Notifier.Show($"<color={ColorHex}>{Lang.T("Du hast", "You released")} {ghost.Data.PlayerName}{Lang.T(" erlöst.", ".")}</color>", 3f);
        else if (local == ghost)
            Notifier.Show($"<color={ColorHex}>{Lang.T("Du wurdest erlöst - du kannst nicht mehr gewinnen.", "You were released - you can't win anymore.")}</color>", 5f);
    }

    public override void Update()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || local.Data == null) return;
        bool livingViewer = IsAlive(local) && !InMeeting;

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!p || p == local) continue;
            if (livingViewer && Haunts(p)) Show(p);
            else if (shownToLiving.Contains(p.PlayerId)) Hide(p);
        }

        // Lebende klicken die Spukgestalt an
        if (livingViewer && Input.GetMouseButtonDown(0) && !Ui.IsMouseBlocked() && Camera.main && !Minigame.Instance)
        {
            Vector2 click = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 me = local.GetTruePosition();
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (!Haunts(p)) continue;
                Vector2 pos = p.transform.position;
                if (Vector2.Distance(click, pos) > ClickRadius || Vector2.Distance(me, pos) > ClickReach) continue;
                SendToAll(ActionRelease, p.PlayerId);
                break;
            }
        }

        // Host: alle Aufgaben geschafft -> Sieg
        if (AmHost && !InMeeting)
            foreach (var p in PlayerControl.AllPlayerControls)
                if (Haunts(p) && AllTasksDone(p)) CustomWin.Trigger(this, new[] { p });
    }

    private static bool AllTasksDone(PlayerControl p)
    {
        var tasks = p.Data.Tasks;
        if (tasks == null || tasks.Count == 0) return false;
        foreach (var t in tasks)
            if (t == null || !t.Complete) return false;
        return true;
    }

    /// <summary>Fuer Lebende sichtbar machen: halb durchsichtig, auf der Spieler-Ebene (Geister-Ebene sieht man lebend nicht).</summary>
    private void Show(PlayerControl p)
    {
        shownToLiving.Add(p.PlayerId);
        if (!p.cosmetics) return;
        if (!p.Visible) p.Visible = true;
        SetLayer(p.cosmetics.gameObject, LayerMask.NameToLayer("Players"));
        p.cosmetics.SetPhantomRoleAlpha(0.45f);
    }

    private void Hide(PlayerControl p)
    {
        if (!shownToLiving.Remove(p.PlayerId) || !p || !p.cosmetics) return;
        p.cosmetics.SetPhantomRoleAlpha(1f);
        var local = PlayerControl.LocalPlayer;
        if (p.Data != null && p.Data.IsDead && IsAlive(local))
        {
            SetLayer(p.cosmetics.gameObject, LayerMask.NameToLayer("Ghost"));
            p.Visible = false;
        }
    }

    private void HideAll()
    {
        foreach (var id in new List<byte>(shownToLiving)) Hide(FindPlayer(id));
        shownToLiving.Clear();
    }

    private static void SetLayer(GameObject go, int layer)
    {
        if (layer < 0 || !go || go.layer == layer) return;
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    public override string GetNameMark(PlayerControl viewer, PlayerControl target) =>
        viewer == target && Haunts(target) ? $"<color={ColorHex}>[{Lang.T("Spuk", "Haunt")}]</color>" : null;
}
