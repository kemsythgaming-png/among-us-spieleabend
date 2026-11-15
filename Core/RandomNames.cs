using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Vergibt beim Spielstart zufällige Namen aus vorbelegten Pools (Filter in der Option "Zufällige Namen").
/// Nur der Host zieht die Namen und schickt sie an alle (deterministisch -> kein Desync). Die Namen gelten nur
/// für die laufende Runde; beim Verlassen der Lobby stellt Among Us den echten Account-Namen wieder her.
/// </summary>
internal static class RandomNames
{
    // Index passt zu GeneralOptions.RandomNames.ValueNames: 0 = Aus, 1..4 = Pool.
    private static readonly string[][] Pools =
    {
        null, // 0 = Aus
        // 1 = Vulgär/derb (Partyabend-Spaß, keine echten Beleidigungen gegen Personen)
        new[] { "Arschgeige","Dumpfbacke","Pissnelke","Hodensack","Vollpfosten","Schnarchnase","Backpfeife",
                "Klobürste","Furzknoten","Rotzlöffel","Saftsack","Hackfresse","Nervensäge","Dreckspatz",
                "Lappen","Pupsklaus","Knalltüte","Vollhorst","Trantüte","Flachpfeife" },
        // 2 = Pronomen (stiftet im Meeting herrliche Verwirrung)
        new[] { "Ich","Du","Er","Sie","Wir","Ihr","Es","Man","Jemand","Niemand","Keiner","Jeder","Wer?",
                "Der da","Die da","Das","Uns","Euch","Ihn","Mich" },
        // 3 = Tiere
        new[] { "Hai","Dackel","Waschbär","Faultier","Igel","Otter","Erdmännchen","Pinguin","Koala","Lama",
                "Wombat","Nashorn","Flamingo","Qualle","Dachs","Biber","Frettchen","Alpaka","Seekuh","Murmeltier" },
        // 4 = Essen/Snacks
        new[] { "Döner","Pommes","Keks","Toast","Bratwurst","Käsebrot","Gurke","Pudding","Nudel","Brezel",
                "Schnitzel","Kartoffel","Muffin","Waffel","Spaghetti","Senf","Wurst","Banane","Gummibär","Kloß" },
    };

    /// <summary>Namen für die aktuelle Filter-Option ziehen (leer, wenn Filter aus).</summary>
    private static List<(byte, string)> Build(List<PlayerControl> players)
    {
        int filter = GeneralOptions.RandomNames != null ? GeneralOptions.RandomNames.Int : 0;
        var pairs = new List<(byte, string)>();
        if (filter <= 0 || filter >= Pools.Length || Pools[filter] == null) return pairs;

        var pool = Pools[filter].OrderBy(_ => Random.value).ToList(); // Mischpool (möglichst eindeutig)
        int used = 0;
        foreach (var p in players)
        {
            string name = used < pool.Count ? pool[used] : pool[used % pool.Count] + " " + (used / pool.Count + 1);
            used++;
            pairs.Add((p.PlayerId, name));
        }
        return pairs;
    }

    /// <summary>Host: Namen ziehen und an alle verteilen. Wird im Intro aufgerufen (nicht im Freeplay).</summary>
    public static void AssignAsHost()
    {
        if (!CustomRole.AmHost) return;
        var players = PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p && p.Data != null && !p.Data.Disconnected).ToList();
        var pairs = Build(players);
        if (pairs.Count == 0) return;
        RoleRpc.SendNames(pairs); // an alle anderen
        Apply(pairs);             // beim Host selbst anwenden
        Plugin.Logger.LogInfo($"Zufällige Namen vergeben an {pairs.Count} Spieler.");
    }

    /// <summary>Freeplay: lokal auf die gegebenen Spieler anwenden (kein RPC).</summary>
    public static void AssignLocal(List<PlayerControl> players)
    {
        var pairs = Build(players);
        if (pairs.Count > 0) Apply(pairs);
    }

    /// <summary>Anzahl wählbarer Pools (ohne "Aus").</summary>
    public static int PoolCount => Pools.Length - 1;

    /// <summary>Kurzer Pool-Name für die Button-Beschriftung (filter 1..PoolCount).</summary>
    public static string PoolLabel(int filter) => filter switch
    {
        1 => Lang.T("Vulgär", "Vulgar"),
        2 => Lang.T("Pronomen", "Pronouns"),
        3 => Lang.T("Tiere", "Animals"),
        4 => Lang.T("Essen", "Food"),
        _ => Lang.T("Aus", "Off"),
    };

    /// <summary>Freeplay: einen bestimmten Pool (1..PoolCount) lokal anwenden, unabhängig von der Option.</summary>
    public static void ApplyPool(List<PlayerControl> players, int filter)
    {
        if (filter <= 0 || filter >= Pools.Length || Pools[filter] == null) return;
        var pool = Pools[filter].OrderBy(_ => Random.value).ToList();
        var pairs = new List<(byte, string)>();
        int used = 0;
        foreach (var p in players)
        {
            string name = used < pool.Count ? pool[used] : pool[used % pool.Count] + " " + (used / pool.Count + 1);
            used++;
            pairs.Add((p.PlayerId, name));
        }
        if (pairs.Count > 0) Apply(pairs);
    }

    /// <summary>Empfangen/lokal: die Namen setzen.</summary>
    public static void Apply(List<(byte Player, string Name)> pairs)
    {
        foreach (var (id, name) in pairs)
        {
            var pc = Find(id);
            if (!pc) continue;
            RandomLooks.Snapshot(pc); // Original-Namen merken (für Statistik + Reset nach dem Spiel)
            try { pc.SetName(name); } catch (System.Exception e) { Plugin.Logger.LogWarning($"Name setzen ({id}): {e.Message}"); }
            if (pc.Data != null) try { pc.Data.PlayerName = name; } catch { }
        }
    }

    private static PlayerControl Find(byte id)
    {
        foreach (var p in PlayerControl.AllPlayerControls)
            if (p && p.PlayerId == id) return p;
        return null;
    }
}
