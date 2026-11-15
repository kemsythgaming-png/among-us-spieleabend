using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Vergibt beim Spielstart (nur Host, deterministisch an alle verteilt) optional zufällige Namen, Farben und
/// Dekorationen. Gilt nur für die laufende Runde; beim Verlassen der Lobby stellt Among Us Account-Werte wieder her.
/// </summary>
internal static class RandomLooks
{
    private struct Look { public string Name; public int Color; public string Hat, Skin, Visor, Pet; }
    private static readonly Dictionary<byte, Look> originals = new();

    /// <summary>Vor dem ersten Überschreiben das Original-Aussehen eines Spielers merken (einmal pro Runde).</summary>
    public static void Snapshot(PlayerControl pc)
    {
        if (!pc || pc.Data == null || originals.ContainsKey(pc.PlayerId)) return;
        var o = pc.Data.DefaultOutfit;
        originals[pc.PlayerId] = new Look
        {
            Name  = pc.Data.PlayerName,
            Color = o != null ? o.ColorId : 0,
            Hat   = o != null ? o.HatId   : "",
            Skin  = o != null ? o.SkinId  : "",
            Visor = o != null ? o.VisorId : "",
            Pet   = o != null ? o.PetId   : "",
        };
    }

    /// <summary>Echten (ursprünglichen) Namen eines Spielers, falls überschrieben – sonst null.</summary>
    public static string RealName(byte id) => originals.TryGetValue(id, out var o) ? o.Name : null;

    /// <summary>Alles wieder auf das gemerkte Original zurücksetzen (nach dem Spiel). Danach ist alles normal.</summary>
    public static void RestoreAll()
    {
        foreach (var kv in originals)
        {
            var pc = Find(kv.Key);
            if (!pc) continue;
            var o = kv.Value;
            try { pc.SetName(o.Name); } catch { }
            if (pc.Data != null) try { pc.Data.PlayerName = o.Name; } catch { }
            try { pc.SetColor(o.Color); } catch { }
            try { pc.SetHat(o.Hat ?? "", o.Color); } catch { }
            try { pc.SetSkin(o.Skin ?? "", o.Color); } catch { }
            try { pc.SetVisor(o.Visor ?? "", o.Color); } catch { }
            try { pc.SetPet(o.Pet ?? ""); } catch { }
        }
        originals.Clear();
    }

    public static void AssignAsHost()
    {
        if (!CustomRole.AmHost) return;
        try { RandomNames.AssignAsHost(); } catch (System.Exception e) { Plugin.Logger.LogWarning($"Random-Namen: {e.Message}"); }
        try { AssignColors(); } catch (System.Exception e) { Plugin.Logger.LogWarning($"Random-Farben: {e.Message}"); }
        try { AssignDecor(); } catch (System.Exception e) { Plugin.Logger.LogWarning($"Random-Deko: {e.Message}"); }
    }

    /// <summary>Freeplay-Test: Namen/Farben/Deko (je nach Option) lokal auf alle Spieler + Dummys anwenden, ohne RPC.</summary>
    public static void RunFreeplay()
    {
        var players = Players();
        if (players.Count == 0) return;
        try { RandomNames.AssignLocal(players); } catch (System.Exception e) { Plugin.Logger.LogWarning($"Random-Namen (FP): {e.Message}"); }
        try { var c = BuildColors(players); if (c.Count > 0) ApplyColors(c); } catch (System.Exception e) { Plugin.Logger.LogWarning($"Random-Farben (FP): {e.Message}"); }
        try { var d = BuildDecor(players); if (d.Count > 0) ApplyDecor(d); } catch (System.Exception e) { Plugin.Logger.LogWarning($"Random-Deko (FP): {e.Message}"); }
    }

    public static List<PlayerControl> Players() =>
        PlayerControl.AllPlayerControls.ToArray().Where(p => p && p.Data != null && !p.Data.Disconnected).ToList();

    /// <summary>Freeplay: nur Farben würfeln (unabhängig von der Option).</summary>
    public static void RunColorsFreeplay()
    {
        try { var c = BuildColors(Players(), true); if (c.Count > 0) ApplyColors(c); }
        catch (System.Exception e) { Plugin.Logger.LogWarning($"Random-Farben (FP): {e.Message}"); }
    }

    /// <summary>Freeplay: nur Dekoration würfeln (unabhängig von der Option).</summary>
    public static void RunDecorFreeplay()
    {
        try { var d = BuildDecor(Players(), true); if (d.Count > 0) ApplyDecor(d); }
        catch (System.Exception e) { Plugin.Logger.LogWarning($"Random-Deko (FP): {e.Message}"); }
    }

    /// <summary>Freeplay: Namen aus einem bestimmten Pool würfeln.</summary>
    public static void RunNamesFreeplay(int filter)
    {
        try { RandomNames.ApplyPool(Players(), filter); }
        catch (System.Exception e) { Plugin.Logger.LogWarning($"Random-Namen (FP): {e.Message}"); }
    }

    // ---------- Farben ----------
    private static List<(byte, byte)> BuildColors(List<PlayerControl> players, bool force = false)
    {
        var pairs = new List<(byte, byte)>();
        if (!force && (GeneralOptions.RandomColors == null || !GeneralOptions.RandomColors.Bool)) return pairs;
        int colorCount = Palette.PlayerColors != null ? Palette.PlayerColors.Length : 12;
        if (colorCount <= 0) return pairs;

        // Farben möglichst eindeutig: gemischte Reihenfolge, bei zu vielen Spielern wiederholen.
        var order = Enumerable.Range(0, colorCount).OrderBy(_ => Random.value).ToList();
        for (int i = 0; i < players.Count; i++)
            pairs.Add((players[i].PlayerId, (byte)order[i % order.Count]));
        return pairs;
    }

    private static void AssignColors()
    {
        var pairs = BuildColors(Players());
        if (pairs.Count == 0) return;
        RoleRpc.SendColors(pairs);
        ApplyColors(pairs);
        Plugin.Logger.LogInfo($"Zufällige Farben an {pairs.Count} Spieler vergeben.");
    }

    public static void ApplyColors(List<(byte Player, byte Color)> pairs)
    {
        foreach (var (id, color) in pairs)
        {
            var pc = Find(id);
            if (!pc) continue;
            Snapshot(pc);
            try { pc.SetColor(color); } catch (System.Exception e) { Plugin.Logger.LogWarning($"Farbe setzen ({id}): {e.Message}"); }
        }
    }

    // ---------- Dekoration ----------
    private static List<(byte, string, string, string, string)> BuildDecor(List<PlayerControl> players, bool force = false)
    {
        var pairs = new List<(byte, string, string, string, string)>();
        if (!force && (GeneralOptions.RandomDecor == null || !GeneralOptions.RandomDecor.Bool)) return pairs;
        var hm = HatManager.Instance;
        if (!hm) return pairs;

        var hats = hm.allHats;   int nHats   = hats   != null ? hats.Length   : 0;
        var skins = hm.allSkins; int nSkins  = skins  != null ? skins.Length  : 0;
        var visors = hm.allVisors; int nVisors = visors != null ? visors.Length : 0;
        var pets = hm.allPets;   int nPets   = pets   != null ? pets.Length   : 0;

        foreach (var p in players)
        {
            string hat   = nHats   > 0 ? hats[Random.Range(0, nHats)].ProductId   : "";
            string skin  = nSkins  > 0 ? skins[Random.Range(0, nSkins)].ProductId : "";
            string visor = nVisors > 0 ? visors[Random.Range(0, nVisors)].ProductId : "";
            string pet   = nPets   > 0 ? pets[Random.Range(0, nPets)].ProductId   : "";
            pairs.Add((p.PlayerId, hat, skin, visor, pet));
        }
        return pairs;
    }

    private static void AssignDecor()
    {
        var pairs = BuildDecor(Players());
        if (pairs.Count == 0) return;
        RoleRpc.SendDecor(pairs);
        ApplyDecor(pairs);
        Plugin.Logger.LogInfo($"Zufällige Dekoration an {pairs.Count} Spieler vergeben.");
    }

    public static void ApplyDecor(List<(byte Player, string Hat, string Skin, string Visor, string Pet)> pairs)
    {
        foreach (var (id, hat, skin, visor, pet) in pairs)
        {
            var pc = Find(id);
            if (!pc) continue;
            Snapshot(pc);
            int color = pc.Data != null && pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
            try { if (!string.IsNullOrEmpty(hat))   pc.SetHat(hat, color); }   catch { }
            try { if (!string.IsNullOrEmpty(skin))  pc.SetSkin(skin, color); } catch { }
            try { if (!string.IsNullOrEmpty(visor)) pc.SetVisor(visor, color); } catch { }
            try { if (!string.IsNullOrEmpty(pet))   pc.SetPet(pet); }          catch { }
        }
    }

    private static PlayerControl Find(byte id)
    {
        foreach (var p in PlayerControl.AllPlayerControls)
            if (p && p.PlayerId == id) return p;
        return null;
    }
}
