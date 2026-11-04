using System.Collections.Generic;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Gemeinsamer Schutz gegen Winter-Effekte (Einfrieren/Verlangsamung). Rollen wie Thermospezialist oder Kaminbauer
/// gewaehren ihn; Frostbeisser/Schneesturm fragen ihn ab. Zeitbasiert, auf allen Clients gepflegt.
/// </summary>
internal static class WinterImmunity
{
    private static readonly Dictionary<byte, float> until = new();

    public static void Grant(byte id, float seconds)
    {
        float t = Time.time + seconds;
        if (!until.TryGetValue(id, out var e) || e < t) until[id] = t;
    }

    public static bool Has(byte id) => until.TryGetValue(id, out var t) && Time.time < t;

    public static void Reset() => until.Clear();
}
