using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Rollenanzeige: Intro, Aufgabenliste, eigene Namensfarbe und eine kleine Info-Zeile ueber jedem Namen
/// (eigene Rolle, alle Rollen fuer Tote, Markierungen wie Lover-Herz). Auch im Meeting.
/// </summary>
internal static class RoleUI
{
    private static ImportantTextTask roleTask;
    private static string roleTaskText;

    private static readonly Dictionary<byte, TextMeshPro> worldLabels = new();
    private static readonly Dictionary<byte, TextMeshPro> meetingLabels = new();

    public static void Reset()
    {
        var local = PlayerControl.LocalPlayer;
        if (local && local.cosmetics) local.cosmetics.nameText.color = Color.white;
        RemoveRoleTask(local);
        foreach (var label in worldLabels.Values.Concat(meetingLabels.Values))
            if (label) Object.Destroy(label.gameObject);
        worldLabels.Clear();
        meetingLabels.Clear();
    }

    public static void Update()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || local.Data == null) return;

        if (FreeplayTest.IsFreeplay) FreeplayTest.ColorFakeImpostors();

        // Als Geist zaehlt nur noch die Geisterrolle (nicht "Sheriff + Waechtergeist")
        var all = RoleRegistry.GetDisplayed(local).ToList();
        var main = RoleRegistry.GetGhostRole(local) ?? RoleRegistry.LocalRole;

        UpdateRoleTask(local, all);
        if (main != null)
        {
            local.cosmetics.nameText.color = main.NameColor;
            UpdateIntro(main, all);
        }

        if (!ShipStatus.Instance) return;
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!p || !p.cosmetics || p.Data == null) continue;
            UpdateWorldLabel(local, p);
            UpdateWorldIcon(local, p);
        }
    }

    public static void UpdateMeeting(MeetingHud meeting)
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || local.Data == null) return;
        var main = RoleRegistry.LocalRole;

        foreach (var role in RoleRegistry.GetAll(local).ToList())
        {
            try { role.OnMeetingUpdate(meeting); }
            catch (System.Exception e) { Plugin.Logger.LogError($"{role.Name}.OnMeetingUpdate: {e.Message}"); }
        }

        foreach (var state in meeting.playerStates)
        {
            if (!state) continue;
            byte id = (byte)state.PlayerId;
            UpdateMeetingIcon(local, state);
            if (id == local.PlayerId && main != null) state.NameText.color = main.NameColor;

            var player = CustomRole.FindPlayer(id);
            string info = player ? InfoFor(local, player) : "";

            if (!meetingLabels.TryGetValue(id, out var label) || !label)
            {
                label = Object.Instantiate(state.NameText, state.NameText.transform.parent);
                label.name = "RoleInfo";
                label.transform.localPosition = state.NameText.transform.localPosition + new Vector3(0f, -0.17f, 0f);
                label.transform.localScale = state.NameText.transform.localScale * 0.7f;
                meetingLabels[id] = label;
            }
            label.text = info;
            label.gameObject.SetActive(!string.IsNullOrEmpty(info));
        }
    }

    /// <summary>Symbol hinter dem Namen auf der Meeting-Karte (z. B. Fluch der Hexe).</summary>
    private static void UpdateMeetingIcon(PlayerControl viewer, PlayerVoteArea state)
    {
        var target = CustomRole.FindPlayer((byte)state.PlayerId);
        string icon = null;
        if (target && RoleRegistry.HasAssignments)
            foreach (var role in RoleRegistry.Roles)
                if ((icon = role.GetMeetingIcon(viewer, target)) != null) break;
        SetNameIcon(state.NameText, icon);
    }

    /// <summary>Symbol hinter dem Namen eines Spielers auf der Map.</summary>
    private static void UpdateWorldIcon(PlayerControl viewer, PlayerControl target)
    {
        string icon = null;
        if (RoleRegistry.HasAssignments && !CustomRole.InMeeting)
            foreach (var role in RoleRegistry.Roles)
                if ((icon = role.GetNameIcon(viewer, target)) != null) break;
        SetNameIcon(target.cosmetics.nameText, icon);
    }

    private static readonly Dictionary<System.IntPtr, string> iconKeys = new();

    /// <summary>Kleines Symbol direkt hinter dem Namen (Hoehe wie die Schrift). icon = null blendet es aus.</summary>
    private static void SetNameIcon(TextMeshPro nameText, string icon)
    {
        if (!nameText) return;
        var existing = nameText.transform.Find("Spieleabend_NameIcon");
        bool show = icon != null && nameText.gameObject.activeInHierarchy && !string.IsNullOrEmpty(nameText.text);
        if (!show)
        {
            if (existing) existing.gameObject.SetActive(false);
            return;
        }

        SpriteRenderer sr;
        if (existing) sr = existing.GetComponent<SpriteRenderer>();
        else
        {
            var go = new GameObject("Spieleabend_NameIcon");
            go.layer = nameText.gameObject.layer;
            go.transform.SetParent(nameText.transform, false);
            sr = go.AddComponent<SpriteRenderer>();
            var textRenderer = nameText.GetComponent<Renderer>();
            if (textRenderer)
            {
                sr.sortingLayerID = textRenderer.sortingLayerID;
                sr.sortingOrder = textRenderer.sortingOrder + 1;
            }
        }
        sr.gameObject.SetActive(true);
        if (!iconKeys.TryGetValue(sr.gameObject.Pointer, out var key) || key != icon)
        {
            sr.sprite = ButtonIcons.Get(icon, null);
            iconKeys[sr.gameObject.Pointer] = icon;
        }
        if (!sr.sprite) return;

        // Hinter das Textende setzen, so hoch wie die Schrift
        var bounds = nameText.textBounds;
        float h = Mathf.Max(bounds.size.y, 0.1f);
        float scale = h * 1.15f / sr.sprite.bounds.size.y;
        sr.transform.localScale = new Vector3(scale, scale, 1f);
        sr.transform.localPosition = new Vector3(bounds.max.x + h * 0.7f, bounds.center.y, -0.01f);
    }

    /// <summary>Was der Betrachter ueber target sieht (Rolle + Markierungen), "" = nichts.</summary>
    private static string InfoFor(PlayerControl viewer, PlayerControl target)
    {
        var parts = new List<string>();

        bool deadView = !CustomRole.IsAlive(viewer) && GeneralOptions.DeadSeeRoles.Bool
                        && !(Roles.Halloween.Undead.Instance?.HidesRoles(viewer) ?? false); // toter Untoter steht wieder auf
        bool self = target == viewer && GeneralOptions.ShowOwnRole.Bool;
        if (deadView || self)
        {
            string label = RoleLabel(target);
            if (!string.IsNullOrEmpty(label)) parts.Add(label);
        }

        // Alle Rollen werden gefragt - jede Rolle entscheidet selbst, ob der Betrachter die Markierung sieht
        foreach (var role in RoleRegistry.Roles)
        {
            var mark = role.GetNameMark(viewer, target);
            if (!string.IsNullOrEmpty(mark)) parts.Add(mark);
        }

        return string.Join(" ", parts);
    }

    /// <summary>Rollenname (Mod-Rolle oder Spiel-Rolle) plus Anpassungen, eingefaerbt.</summary>
    public static string RoleLabel(PlayerControl player)
    {
        var ghost = RoleRegistry.GetGhostRole(player);
        if (ghost != null) return ghost.ColoredName; // Geist: nur noch die Geisterrolle

        var main = RoleRegistry.GetRole(player);
        string text;
        if (main != null) text = main.ColoredName;
        else if (FreeplayTest.IsFakeImpostor(player)) text = $"<color=#FF1919>Impostor ({Lang.T("Test", "test")})</color>";
        else if (player.Data.Role)
        {
            string color = player.Data.Role.IsImpostor ? "#FF1919" : "#8CFFFF";
            text = $"<color={color}>{player.Data.Role.NiceName}</color>";
        }
        else text = "";

        foreach (var mod in RoleRegistry.GetModifiers(player).Where(m => !m.IsSecret(player))) text += " " + mod.ColoredName;
        return text.Trim();
    }

    private static void UpdateWorldLabel(PlayerControl viewer, PlayerControl p)
    {
        var nameText = p.cosmetics.nameText;
        string info = Camouflage.Active && p != viewer && CustomRole.IsAlive(viewer) ? "" : InfoFor(viewer, p);

        if (!worldLabels.TryGetValue(p.PlayerId, out var label) || !label)
        {
            if (string.IsNullOrEmpty(info)) return;
            label = Object.Instantiate(nameText, nameText.transform.parent);
            label.name = "RoleInfo";
            label.transform.localPosition = nameText.transform.localPosition + new Vector3(0f, 0.24f, 0f);
            label.transform.localScale = nameText.transform.localScale * 0.65f;
            worldLabels[p.PlayerId] = label;
        }

        // Sichtbarkeit wie der Name (unsichtbare Phantoms, Spieler im Vent ...)
        bool visible = !string.IsNullOrEmpty(info) && nameText.gameObject.activeInHierarchy && nameText.enabled;
        label.gameObject.SetActive(visible);
        if (!visible) return;
        label.text = info;
        label.color = Color.white;
        label.alpha = nameText.alpha;
    }

    private static void UpdateRoleTask(PlayerControl local, List<CustomRole> roles)
    {
        if (roles.Count == 0)
        {
            RemoveRoleTask(local);
            return;
        }

        // Rollenname in eigener Zeile, damit der Text oben links ordentlich umbricht
        string text = string.Join("\n", roles.Select(r => $"<color={r.ColorHex}><b>{r.Name}:</b>\n{r.Description}</color>"));
        if (roleTask && roleTaskText == text && local.myTasks.Contains(roleTask)) return;
        RemoveRoleTask(local);

        roleTask = new GameObject("CustomRoleTask").AddComponent<ImportantTextTask>();
        roleTask.transform.SetParent(local.transform, false);
        roleTask.Text = text;
        roleTaskText = text;
        local.myTasks.Insert(0, roleTask);
    }

    private static void RemoveRoleTask(PlayerControl local)
    {
        if (!roleTask) return;
        if (local) local.myTasks.Remove(roleTask);
        Object.Destroy(roleTask.gameObject);
        roleTask = null;
        roleTaskText = null;
    }

    private static void UpdateIntro(CustomRole main, List<CustomRole> all)
    {
        var intro = IntroCutscene.Instance;
        if (!intro) return;

        intro.YouAreText.color = main.NameColor;
        intro.RoleText.text = main.Name;
        intro.RoleText.color = main.NameColor;

        string blurb = main.IntroText;
        foreach (var mod in all.Where(r => r.IsModifier)) blurb += $"\n<size=70%>+ {mod.ColoredName}</size>";
        intro.RoleBlurbText.text = blurb;
        intro.RoleBlurbText.color = main.NameColor;
    }
}
