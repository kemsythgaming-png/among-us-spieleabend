using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Gemeinsames Tempo-System: Alle Rollen/Anpassungen liefern einen Faktor (CustomRole.SpeedMultiplier),
/// die Faktoren werden multipliziert (z. B. Flash 1,25 x Zuckerrausch 1,3) und einmal aufs Grundtempo angewendet.
/// So ueberschreiben sich Effekte nicht gegenseitig.
/// </summary>
internal static class SpeedControl
{
    private static float baseSpeed = -1f;

    internal static void Update()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || !local.MyPhysics || !ShipStatus.Instance) return;

        float factor = 1f;
        foreach (var role in RoleRegistry.Roles)
        {
            try { factor *= role.SpeedMultiplier(local); }
            catch { }
        }

        if (Mathf.Approximately(factor, 1f))
        {
            if (baseSpeed > 0f) Reset();
            return;
        }

        if (baseSpeed < 0f) baseSpeed = local.MyPhysics.Speed;
        local.MyPhysics.Speed = baseSpeed * factor;
    }

    internal static void Reset()
    {
        var local = PlayerControl.LocalPlayer;
        if (baseSpeed > 0f && local && local.MyPhysics) local.MyPhysics.Speed = baseSpeed;
        baseSpeed = -1f;
    }
}
