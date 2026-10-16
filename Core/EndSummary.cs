using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Spielende-Uebersicht (wie in bekannten Rollenmods): links auf dem Ergebnis-Bildschirm steht fuer jeden Spieler,
/// welche Rolle(n) er hatte, wie viele Kills er gemacht hat und ob er ueberlebt hat.
/// </summary>
internal static class EndSummary
{
    private sealed class Entry
    {
        public string Name;
        public string Roles;
        public int Kills;
        public bool Alive;
        public bool Impostor;
    }

    private static readonly Dictionary<byte, int> kills = new();
    private static readonly List<Entry> entries = new();

    /// <summary>Waehrend der Runde (alle Clients): Kill mitzaehlen. Selbst-Kills (Fehlschuss, Biss) zaehlen nicht.</summary>
    internal static void OnDeath(PlayerControl victim, PlayerControl killer, DeathCause cause)
    {
        if (!killer || killer == victim || cause == DeathCause.Exile) return;
        kills.TryGetValue(killer.PlayerId, out int n);
        kills[killer.PlayerId] = n + 1;
    }

    internal static void ResetKills() => kills.Clear();

    /// <summary>Beim Spielende, solange Rollen und Spieler noch da sind.</summary>
    internal static void Capture()
    {
        entries.Clear();
        if (!GameData.Instance) return;
        foreach (var info in GameData.Instance.AllPlayers)
        {
            if (info == null || info.Disconnected && info.Object == null) continue;
            var p = info.Object;
            kills.TryGetValue(info.PlayerId, out int k);
            entries.Add(new Entry
            {
                Name = ColoredName(info),
                Roles = RolesOf(p, info),
                Kills = k,
                Alive = !info.IsDead && !info.Disconnected,
                Impostor = RoleWasImpostor(info),
            });
        }
        // Impostor zuerst, dann nach Kills
        entries.Sort((a, b) => a.Impostor != b.Impostor ? (a.Impostor ? -1 : 1) : b.Kills.CompareTo(a.Kills));
        kills.Clear();
    }

    private static string ColoredName(NetworkedPlayerInfo info)
    {
        var colors = Palette.PlayerColors;
        int id = info.DefaultOutfit != null ? info.DefaultOutfit.ColorId : 0;
        var c = id >= 0 && id < colors.Length ? (Color)colors[id] : Color.white;
        return $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{info.PlayerName}</color>";
    }

    private static bool RoleWasImpostor(NetworkedPlayerInfo info)
    {
        var type = info.RoleWhenAlive.HasValue ? info.RoleWhenAlive.Value : info.Role ? info.Role.Role : RoleTypes.Crewmate;
        var role = RoleManager.Instance ? RoleManager.Instance.GetRole(type) : null;
        return role ? role.IsImpostor : info.Role && info.Role.IsImpostor;
    }

    /// <summary>Alle Rollen ehrlich auflisten - auch geheime (Verraeter) und die Geisterrolle.</summary>
    private static string RolesOf(PlayerControl p, NetworkedPlayerInfo info)
    {
        var parts = new List<string>();
        var main = p ? RoleRegistry.GetRole(p) : null;
        if (main != null) parts.Add(main.ColoredName);
        else
        {
            var type = info.RoleWhenAlive.HasValue ? info.RoleWhenAlive.Value : info.Role ? info.Role.Role : RoleTypes.Crewmate;
            var vanilla = RoleManager.Instance ? RoleManager.Instance.GetRole(type) : null;
            bool imp = vanilla ? vanilla.IsImpostor : false;
            string name = vanilla ? vanilla.NiceName : type.ToString();
            parts.Add($"<color={(imp ? "#FF1919" : "#8CFFFF")}>{name}</color>");
        }
        if (p)
            foreach (var extra in RoleRegistry.GetModifiers(p))
                parts.Add(extra.IsGhostRole ? $"{extra.ColoredName} <size=80%>({Lang.T("Geist", "ghost")})</size>" : extra.ColoredName);
        return string.Join(" + ", parts);
    }

    /// <summary>Text links auf dem Ergebnis-Bildschirm.</summary>
    internal static void Show(EndGameManager manager)
    {
        if (entries.Count == 0 || !manager || !manager.WinText || !Camera.main) return;

        var go = Object.Instantiate(manager.WinText.gameObject, manager.transform);
        go.name = "Spieleabend_EndSummary";
        foreach (var c in go.GetComponents<MonoBehaviour>())
            if (!c.TryCast<TextMeshPro>()) Object.Destroy(c); // Animationen des Siegtexts entfernen
        var tmp = go.GetComponent<TextMeshPro>();

        var lines = new List<string> { $"<b>{Lang.T("Rollen dieser Runde", "Roles this game")}</b>" };
        foreach (var e in entries)
        {
            string status = e.Alive ? "" : " <color=#AAAAAA>†</color>";
            string killText = e.Kills > 0 ? $"  <color=#FF6666>{e.Kills} {(e.Kills == 1 ? "Kill" : "Kills")}</color>" : "";
            lines.Add($"{e.Name}{status}  -  {e.Roles}{killText}");
        }
        tmp.text = string.Join("\n", lines);
        tmp.color = Color.white;
        tmp.enableAutoSizing = false;
        tmp.enableWordWrapping = false;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.fontSize = 2.2f;

        // Oben links, etwas unter dem Rand
        var cam = Camera.main;
        Vector3 topLeft = cam.ViewportToWorldPoint(new Vector3(0.02f, 0.96f, 0f));
        tmp.rectTransform.pivot = new Vector2(0f, 1f);
        tmp.rectTransform.sizeDelta = new Vector2(30f, 20f);
        go.transform.localScale = Vector3.one;
        go.transform.rotation = Quaternion.identity;
        go.transform.position = new Vector3(topLeft.x, topLeft.y, manager.WinText.transform.position.z);
        go.SetActive(true);
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
internal static class EndSummaryCapturePatch
{
    public static void Prefix() => EndSummary.Capture();
}

[HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
internal static class EndSummaryShowPatch
{
    public static void Postfix(EndGameManager __instance)
    {
        try { EndSummary.Show(__instance); }
        catch (System.Exception e) { Plugin.Logger.LogWarning($"Spielende-Uebersicht: {e.Message}"); }
    }
}
