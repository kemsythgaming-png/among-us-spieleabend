using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Object = UnityEngine.Object;

namespace Spieleabend.Core;

/// <summary>
/// Zeigt im "Match Info Guide" des Spiels (Reiter "Roles") zusaetzlich alle eingeschalteten Mod-Rollen an -
/// nur welche aktiv sind (Anzahl + Chance), nicht wer sie hat.
/// Eintraege werden ueber die Spiel-Funktion CreateRoleEntry angelegt, damit Layout und Scrollen stimmen,
/// und danach mit unseren Texten ueberschrieben.
/// </summary>
internal static class MatchInfoRoles
{
    private const string Tag = "SpieleabendRole_";

    private static IntPtr builtFor;
    private static string builtKey;

    public static void Ensure(MatchInfoGuide guide)
    {
        try
        {
            var enabled = RoleRegistry.Roles.Where(r => r.Count.Int > 0 && r.Chance.Int > 0).ToList();
            string key = string.Join(",", enabled.Select(r => r.Id));
            var ours = OurPanels(guide);

            if (builtFor == guide.Pointer && builtKey == key && ours.Count == enabled.Count)
            {
                for (int i = 0; i < ours.Count; i++) Fill(ours[i], enabled[i]); // Werte koennen sich in der Lobby aendern
            }
            else
            {
                foreach (var panel in ours) Object.DestroyImmediate(panel.gameObject);
                foreach (var role in enabled) Create(guide, role);
                builtFor = guide.Pointer;
                builtKey = key;
                Plugin.Logger.LogInfo($"Match Info Guide: {enabled.Count} Mod-Rolle(n) eingetragen.");
            }

            if (enabled.Count > 0 && guide.rolesEnabledMessage) guide.rolesEnabledMessage.SetActive(false);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"Match Info Guide konnte nicht ergaenzt werden: {e.Message}");
        }
    }

    private static List<MatchInfoRolePanel> OurPanels(MatchInfoGuide guide) =>
        guide.GetComponentsInChildren<MatchInfoRolePanel>(true).Where(p => p.name.StartsWith(Tag)).ToList();

    private static void Create(MatchInfoGuide guide, CustomRole role)
    {
        var baseType = role.Team == RoleTeam.Impostor ? RoleTypes.Impostor : RoleTypes.Crewmate;
        var baseRole = RoleManager.Instance.AllRoles.ToArray().FirstOrDefault(r => r && r.Role == baseType);
        if (!baseRole) return;

        var before = new HashSet<IntPtr>(guide.GetComponentsInChildren<MatchInfoRolePanel>(true).Select(p => p.Pointer));
        guide.CreateRoleEntry(baseRole);

        var panel = guide.GetComponentsInChildren<MatchInfoRolePanel>(true).FirstOrDefault(p => !before.Contains(p.Pointer));
        if (!panel) return;
        panel.name = Tag + role.Name;
        Fill(panel, role);
    }

    private static void Fill(MatchInfoRolePanel panel, CustomRole role)
    {
        string team = role.Team switch
        {
            RoleTeam.Crewmate => "Crew",
            RoleTeam.Impostor => "Impostor",
            RoleTeam.Neutral => "Neutral",
            _ => Lang.T("Anpassung", "Modifier"),
        };

        if (panel.roleName)
        {
            panel.roleName.text = $"{role.Name} <size=60%>({team})</size>";
            panel.roleName.color = role.NameColor;
        }
        if (panel.roleDescription) panel.roleDescription.text = role.Description;
        if (panel.roleCount) panel.roleCount.text = $"{role.Count.Int}x  {role.Chance.Int}%";
        if (panel.roleIcon) panel.roleIcon.color = role.Color;
    }
}

[HarmonyPatch(typeof(MatchInfoGuide), nameof(MatchInfoGuide.Open))]
internal static class MatchInfoOpenPatch
{
    public static void Postfix(MatchInfoGuide __instance) => MatchInfoRoles.Ensure(__instance);
}

[HarmonyPatch(typeof(MatchInfoGuide), nameof(MatchInfoGuide.SetActiveTab))]
internal static class MatchInfoTabPatch
{
    public static void Postfix(MatchInfoGuide __instance) => MatchInfoRoles.Ensure(__instance);
}
