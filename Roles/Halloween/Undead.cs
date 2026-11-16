using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Spieleabend.Core;
using UnityEngine;

namespace Spieleabend.Roles.Halloween;

/// <summary>
/// Neutral: Wird er getoetet, steht er nach dem naechsten Meeting als Untoter wieder auf. Im Meeting gilt er als tot
/// (keine Stimme). Sammelt er Seelen (an fremden Leichen stehen), wird er wieder lebendig und spielt fuer die Crew.
/// Schafft er das nicht bis Spielende, verliert er.
/// </summary>
public sealed class Undead : CustomRole
{
    public override string ConfigSection => "Untoter";
    public override string Name => Lang.T("Untoter", "Undead");
    public override Color Color => new Color32(120, 170, 110, 255);
    public override RoleTeam Team => RoleTeam.Neutral;
    public override string IntroText => Lang.T("Der Tod ist erst der Anfang", "Death is only the beginning");
    public override string Description => Stage(PlayerControl.LocalPlayer) switch
    {
        UndeadStage.Risen => Lang.T($"Sauge Leichen aus - sie lösen sich auf.\n({SoulsOf(PlayerControl.LocalPlayer)}/{souls.Int})", $"Drain bodies - they dissolve.\n({SoulsOf(PlayerControl.LocalPlayer)}/{souls.Int})"),
        _ => Lang.T("Lass dich von einem Impostor töten -\nnach dem Meeting stehst du wieder auf.", "Get killed by an impostor -\nyou rise again after the meeting."),
    };
    public override string LongDescription => Lang.T(
        "Halloween-Neutral. Wirst du getötet, bist du bis zum Ende des nächsten Meetings ein Geist - aber ohne Rollen-Infos. Danach stehst du als Untoter wieder auf.\n\nAls Untoter läufst du sichtbar herum, giltst im Meeting aber als tot (keine Stimme, du schweigst). Steh an einer fremden Leiche, bis du ihre Seele aufgesaugt hast - danach löst sich die Leiche auf. Jede Leiche gibt genau 1 Seele, du musst also verschiedene Leichen finden. Hast du genug (Standard 2), gewinnst du allein und das Spiel endet. Sonst verlierst du.\n\nTöten können dich nur Impostor und Neutrale - Crew-Rollen wie der Sheriff nicht. Ein zweiter Tod ist endgültig.",
        "Halloween neutral. If you are killed, you are a ghost until the end of the next meeting - but without role info. Then you rise again as undead.\n\nAs undead you walk around visibly, but count as dead in meetings (no vote, you stay silent). Stand at another player's body until you drain its soul - then the body dissolves. Each body gives exactly 1 soul, so you must find different bodies. Once you have enough (default 2), you win alone and the game ends. Otherwise you lose.\n\nOnly impostors and neutrals can kill you - crew roles like the Sheriff can't. A second death is final.");

    private const byte ActionRise = 0, ActionSoul = 1, ActionRestore = 2;

    internal static Undead Instance { get; private set; }

    private RoleOption souls, soulTime;

    // bei allen Clients
    private readonly Dictionary<byte, UndeadStage> stages = new();
    private readonly Dictionary<byte, int> collected = new();

    // lokal beim Untoten: Leiche -> Zeit an ihr; schon ausgesaugte Leichen; Seelen-Animationen
    private readonly Dictionary<byte, float> standing = new();
    private readonly HashSet<byte> harvested = new(); // jede Leiche nur einmal (löst sich danach auf)
    private readonly Dictionary<byte, GameObject> soulOrbs = new();
    private static Sprite soulSprite;

    // auf allen Clients: Leichen, die sich gerade auflösen (aufsteigende Seelen-Kugel)
    private sealed class RiseOrb { public GameObject Go; public float Start; }
    private static readonly List<RiseOrb> rising = new();

    /// <summary>Namen der Untoten, die gerade NICHT gewinnen duerfen (fuer die Siegerliste am Spielende).</summary>
    internal static readonly HashSet<string> LosingNames = new();

    protected override void Setup()
    {
        Instance = this;
        souls = AddOption("SoulsV2", "Benötigte Leichen|Bodies needed", 2f, 1f, 10f, 1f);
        soulTime = AddOption("SoulTime", "Zeit je Leiche|Time per body", 12f, 1f, 30f, 1f, "s");
    }

    public override void ResetState()
    {
        stages.Clear();
        collected.Clear();
        standing.Clear();
        harvested.Clear();
        ClearOrbs();
        // LosingNames bleibt bis zum Ende-Bildschirm stehen (wird dort benutzt und geleert)
    }

    public override void OnMeetingStart() => ClearOrbs();

    private void ClearOrbs()
    {
        foreach (var o in soulOrbs.Values) if (o) Object.Destroy(o);
        soulOrbs.Clear();
    }

    internal UndeadStage Stage(PlayerControl p) =>
        p && stages.TryGetValue(p.PlayerId, out var s) ? s : UndeadStage.Alive;

    private int SoulsOf(PlayerControl p) => p && collected.TryGetValue(p.PlayerId, out var c) ? c : 0;

    /// <summary>Toter Untoter, der noch aufstehen wird (bekommt keine Geisterrolle).</summary>
    internal bool WillRise(PlayerControl p) => RoleRegistry.Has(p, this) && Stage(p) == UndeadStage.Dead;

    /// <summary>Untoter, der im Meeting als tot gilt.</summary>
    internal bool CountsAsDeadInMeeting(PlayerControl p) => RoleRegistry.Has(p, this) && Stage(p) == UndeadStage.Risen && IsAlive(p);

    /// <summary>Toter Untoter, der noch aufstehen wird - bekommt keine Rollen-Infos.</summary>
    internal bool HidesRoles(PlayerControl viewer) => RoleRegistry.Has(viewer, this) && Stage(viewer) is UndeadStage.Dead or UndeadStage.Risen;

    public override void OnPlayerDied(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        if (!RoleRegistry.Has(victim, this)) return;
        var stage = Stage(victim);
        // Erster Tod durch Kill (auch ohne Leiche) -> steht wieder auf; Rauswurf oder zweiter Tod -> endgueltig
        stages[victim.PlayerId] = stage == UndeadStage.Alive && cause != DeathCause.Exile ? UndeadStage.Dead : UndeadStage.Final;
        if (victim == PlayerControl.LocalPlayer && stages[victim.PlayerId] == UndeadStage.Dead)
            Notifier.Show($"<color={ColorHex}>{Lang.T("Du bist gestorben ... aber nicht für immer. Nach dem nächsten Meeting stehst du wieder auf.", "You died ... but not forever. You will rise again after the next meeting.")}</color>", 6f);
    }

    public override void OnMeetingEnd(PlayerControl exiled)
    {
        if (!AmHost) return;
        foreach (var pair in stages.Where(s => s.Value == UndeadStage.Dead).ToList())
        {
            var p = FindPlayer(pair.Key);
            if (!p || p.Data == null || p.Data.Disconnected || p == exiled) continue;
            SendToAll(ActionRise, pair.Key);
        }
    }

    public override void OnAction(PlayerControl sender, byte action, byte[] data)
    {
        if (data.Length < 1) return;
        var p = FindPlayer(data[0]);
        if (!p) return;

        switch (action)
        {
            case ActionRise:
                if (Stage(p) != UndeadStage.Dead) return;
                stages[p.PlayerId] = UndeadStage.Risen;
                p.Revive();
                if (AmHost) p.RpcSetRole(RoleTypes.Crewmate, true);
                ProtectionVisuals.ShowBurst(p, Color);
                if (p == PlayerControl.LocalPlayer)
                {
                    Sfx.Play(Sfx.Creepy);
                    Notifier.Show($"<color={ColorHex}>{Lang.T($"Du bist auferstanden! Sammle {souls.Int} Seelen an fremden Leichen.", $"You have risen! Collect {souls.Int} souls at other bodies.")}</color>", 6f);
                }
                break;

            case ActionSoul:
                if (Stage(p) != UndeadStage.Risen) return;
                collected[p.PlayerId] = SoulsOf(p) + 1;
                DissolveBody(data.Length > 1 ? data[1] : byte.MaxValue); // Leiche auf allen Clients auflösen
                if (p == PlayerControl.LocalPlayer && SoulsOf(p) < souls.Int)
                {
                    Sfx.Play(Sfx.TaskUpdate);
                    Notifier.Show($"<color={ColorHex}>{Lang.T("Seele", "Soul")} {SoulsOf(p)}/{souls.Int} - {Lang.T("finde die nächste Leiche", "find the next body")}</color>", 2.5f);
                }
                if (AmHost && SoulsOf(p) >= souls.Int) SendToAll(ActionRestore, p.PlayerId); // genug -> Sieg (host-autoritativ)
                break;

            case ActionRestore:
                // Genug Seelen: Der Untote gewinnt allein
                if (Stage(p) != UndeadStage.Risen) return;
                stages[p.PlayerId] = UndeadStage.Restored;
                ProtectionVisuals.ShowBurst(p, Color);
                if (p == PlayerControl.LocalPlayer) Sfx.Play(Sfx.TaskComplete);
                if (AmHost) CustomWin.Trigger(this, new[] { p });
                break;
        }
    }

    public override void Update()
    {
        var local = PlayerControl.LocalPlayer;
        if (!IsLocalRole || Stage(local) != UndeadStage.Risen || !IsAlive(local) || InMeeting)
        {
            if (soulOrbs.Count > 0) ClearOrbs();
            return;
        }

        Vector2 me = local.GetTruePosition();
        var near = new Dictionary<byte, Vector2>();
        foreach (var body in Object.FindObjectsOfType<DeadBody>())
        {
            if (!body || body.ParentId == local.PlayerId) continue;
            if (Vector2.Distance(me, body.TruePosition) > 0.7f) continue; // näher an der Leiche stehen
            near[body.ParentId] = body.TruePosition;
        }

        foreach (var id in standing.Keys.ToList())
            if (!near.ContainsKey(id)) standing.Remove(id); // weggegangen -> von vorn
        foreach (var id in soulOrbs.Keys.ToList())
            if (!near.ContainsKey(id)) { if (soulOrbs[id]) Object.Destroy(soulOrbs[id]); soulOrbs.Remove(id); }

        foreach (var kv in near)
        {
            byte id = kv.Key;
            if (harvested.Contains(id)) continue; // schon ausgesaugt (Leiche verschwindet gleich)
            standing.TryGetValue(id, out float t);
            t += Time.deltaTime;

            AnimateOrb(id, kv.Value, Mathf.Clamp01(t / soulTime.Value), false);

            if (t < soulTime.Value) { standing[id] = t; continue; }

            // Leiche ausgesaugt: 1 Seele, Leiche löst sich auf (auf allen Clients). Du brauchst also verschiedene Leichen.
            harvested.Add(id);
            standing.Remove(id);
            if (soulOrbs.TryGetValue(id, out var orb)) { if (orb) Object.Destroy(orb); soulOrbs.Remove(id); }
            SendToAll(ActionSoul, local.PlayerId, id);
        }
    }

    /// <summary>Löst die Leiche mit dieser ParentId auf (aufsteigende, verblassende Seele) - auf jedem Client.</summary>
    private void DissolveBody(byte bodyId)
    {
        if (bodyId == byte.MaxValue) return;
        foreach (var body in Object.FindObjectsOfType<DeadBody>())
        {
            if (!body || body.ParentId != bodyId) continue;
            SpawnRiseOrb(body.TruePosition);
            Object.Destroy(body.gameObject);
            break;
        }
    }

    private void SpawnRiseOrb(Vector2 pos)
    {
        var go = new GameObject("Spieleabend_SoulRise");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SoulSprite();
        sr.sortingOrder = 61;
        var c = Color; c.a = 0.9f; sr.color = c;
        go.transform.position = new Vector3(pos.x, pos.y + 0.4f, -3f);
        go.transform.localScale = Vector3.one * 0.55f;
        rising.Add(new RiseOrb { Go = go, Start = Time.time });
    }

    /// <summary>Jeden Frame (alle Clients): aufsteigende Seelen-Kugeln animieren. Von Patches aufgerufen.</summary>
    internal static void TickEffects()
    {
        const float life = 0.9f;
        for (int i = rising.Count - 1; i >= 0; i--)
        {
            var r = rising[i];
            float age = Time.time - r.Start;
            if (!r.Go || age >= life) { if (r.Go) Object.Destroy(r.Go); rising.RemoveAt(i); continue; }
            float k = age / life;
            r.Go.transform.position += new Vector3(0f, Time.deltaTime * 1.3f, 0f);
            float s = 0.55f * (1f + k * 0.6f);
            r.Go.transform.localScale = new Vector3(s, s, 1f);
            var sr = r.Go.GetComponent<SpriteRenderer>();
            if (sr) { var c = sr.color; c.a = 0.9f * (1f - k); sr.color = c; }
        }
    }

    /// <summary>Animierte Seelen-Kugel über der Leiche: schwebt auf und ab, pulsiert und wächst mit dem Fortschritt.</summary>
    private void AnimateOrb(byte bodyId, Vector2 bodyPos, float progress, bool blocked)
    {
        if (!soulOrbs.TryGetValue(bodyId, out var go) || !go)
        {
            go = new GameObject("Spieleabend_SoulOrb");
            var sr0 = go.AddComponent<SpriteRenderer>();
            sr0.sprite = SoulSprite();
            sr0.sortingOrder = 60;
            soulOrbs[bodyId] = go;
        }
        float tt = Time.time;
        float bob = Mathf.Sin(tt * 3.2f) * 0.14f;
        float pulse = 0.85f + Mathf.Sin(tt * 6f) * 0.15f;
        float scale = (0.28f + 0.5f * progress) * pulse;
        go.transform.position = new Vector3(bodyPos.x, bodyPos.y + 0.75f + bob, bodyPos.y / 1000f - 3f);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        var sr = go.GetComponent<SpriteRenderer>();
        // blockiert (gleiche Leiche) -> rötlich blinken, sonst grün der Rolle - deutlich heller.
        var baseCol = blocked ? new Color(1f, 0.55f, 0.55f) : Color.Lerp(Color, Color.white, 0.45f);
        baseCol.a = 0.85f + 0.15f * Mathf.Abs(Mathf.Sin(tt * 4f)); // heller, immer gut sichtbar
        sr.color = baseCol;
    }

    private static Sprite SoulSprite()
    {
        if (soulSprite) return soulSprite;
        const int S = 48;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(23.5f, 23.5f)) / 23.5f;
            float a = d >= 1f ? 0f : Mathf.SmoothStep(1f, 0f, d); // weiche Kugel
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        soulSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        soulSprite.hideFlags = HideFlags.HideAndDontSave;
        return soulSprite;
    }

    /// <summary>
    /// Beim Spielende (bevor die Rollen geloescht werden): Der Untote gewinnt nur allein mit seinen Seelen -
    /// bei jedem anderen Spielende verliert er (auch wenn die Crew gewinnt).
    /// </summary>
    internal void CaptureLosers()
    {
        LosingNames.Clear();
        if (CustomWin.WinningRole == this) return;
        foreach (var p in Players)
            if (p && p.Data != null) LosingNames.Add(p.Data.PlayerName);
    }

    /// <summary>Nur Impostor und Neutrale koennen den Untoten toeten - Crew-Rollen (Sheriff ...) nicht.</summary>
    public override KillIntercept InterceptKill(PlayerControl killer, PlayerControl target)
    {
        if (!RoleRegistry.Has(target, this) || killer == target || IsImpostor(killer) || IsNeutral(killer)) return KillIntercept.None;
        killer.RpcMurderPlayer(target, false); // scheitert sichtbar
        return KillIntercept.Handled;
    }

    public override string GetNameMark(PlayerControl viewer, PlayerControl target)
    {
        if (!RoleRegistry.Has(target, this) || Stage(target) != UndeadStage.Risen || !IsAlive(target)) return null;
        // Alle sehen, dass er untot ist; er selbst sieht seinen Fortschritt
        if (viewer == target) return $"<color={ColorHex}>[{Lang.T("Untot", "Undead")} {SoulsOf(target)}/{souls.Int}]</color>";
        return $"<color={ColorHex}>[{Lang.T("Untot", "Undead")}]</color>";
    }
}

internal enum UndeadStage
{
    Alive,
    Dead,
    Risen,
    Restored,
    Final,
}

/// <summary>Im Meeting: Der Untote gilt als tot - Karte durchgestrichen, keine Stimme.</summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
internal static class UndeadMeetingStartPatch
{
    public static void Postfix(MeetingHud __instance)
    {
        var undead = Undead.Instance;
        if (undead == null) return;
        foreach (var state in __instance.playerStates)
        {
            if (!state) continue;
            var p = CustomRole.FindPlayer((byte)state.PlayerId);
            if (undead.CountsAsDeadInMeeting(p)) state.SetDead(true);
        }
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Select))]
internal static class UndeadSelectPatch
{
    public static bool Prefix(ref bool __result)
    {
        var undead = Undead.Instance;
        if (undead == null || !undead.CountsAsDeadInMeeting(PlayerControl.LocalPlayer)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CmdCastVote))]
internal static class UndeadCastVotePatch
{
    public static bool Prefix(byte playerId)
    {
        var undead = Undead.Instance;
        return undead == null || !undead.CountsAsDeadInMeeting(CustomRole.FindPlayer(playerId));
    }
}

/// <summary>Auferstandener Untoter: kein Notfallknopf, keine Leiche melden (er gilt als tot).</summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdReportDeadBody))]
internal static class UndeadNoMeetingPatch
{
    public static bool Prefix(PlayerControl __instance)
    {
        var undead = Undead.Instance;
        if (undead == null || !undead.CountsAsDeadInMeeting(__instance)) return true;
        if (__instance == PlayerControl.LocalPlayer)
            Notifier.Show($"<color={undead.ColorHex}>{Lang.T("Als Untoter kannst du kein Meeting einberufen.", "As undead you can't call a meeting.")}</color>", 2.5f);
        return false;
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
internal static class UndeadGameEndPatch
{
    public static void Prefix() => Undead.Instance?.CaptureLosers();
}

/// <summary>Spielende: Untote, die ihre Seelen nicht geschafft haben, stehen nicht bei den Gewinnern.</summary>
[HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
[HarmonyPriority(Priority.Low)]
internal static class UndeadWinnerPatch
{
    public static void Prefix()
    {
        if (Undead.LosingNames.Count == 0 || EndGameResult.CachedWinners == null) return;
        var keep = new Il2CppSystem.Collections.Generic.List<CachedPlayerData>();
        foreach (var w in EndGameResult.CachedWinners)
            if (w != null && !Undead.LosingNames.Contains(w.PlayerName)) keep.Add(w);
        EndGameResult.CachedWinners = keep;
    }

    public static void Postfix() => Undead.LosingNames.Clear();
}
