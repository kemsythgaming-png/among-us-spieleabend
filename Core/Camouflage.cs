using System.Collections.Generic;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Tarnung (Camouflager): Alle Spieler sehen fuer kurze Zeit gleich aus - grau, ohne Hut, Skin, Haustier und Namen.
/// Rein optisch und lokal auf jedem Client; der Camouflager startet sie per Nachricht an alle.
/// </summary>
public static class Camouflage
{
    private const int GrayColorId = 15;

    private static float until;
    private static readonly Dictionary<byte, PlayerOutfitType> camouflaged = new();

    public static bool Active => until > Time.time && !CustomRole.InMeeting;

    public static void Start(float duration) => until = Time.time + duration;

    public static void Reset()
    {
        until = 0f;
        RestoreAll();
    }

    internal static void Update()
    {
        if (Active)
        {
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (!p || p.Data == null || p.Data.IsDead || camouflaged.ContainsKey(p.PlayerId)) continue;
                camouflaged[p.PlayerId] = p.CurrentOutfitType;
                p.RawSetOutfit(GrayOutfit(), PlayerOutfitType.MushroomMixup);
            }
        }
        else if (camouflaged.Count > 0)
        {
            until = 0f; // Meeting beendet die Tarnung
            RestoreAll();
        }
    }

    private static void RestoreAll()
    {
        foreach (var pair in camouflaged)
        {
            var p = CustomRole.FindPlayer(pair.Key);
            if (!p || p.Data == null) continue;

            var type = pair.Value;
            NetworkedPlayerInfo.PlayerOutfit outfit = null;
            if (p.Data.Outfits.ContainsKey(type)) outfit = p.Data.Outfits[type];
            if (outfit == null)
            {
                outfit = p.Data.DefaultOutfit;
                type = PlayerOutfitType.Default;
            }
            p.RawSetOutfit(outfit, type);
        }
        camouflaged.Clear();
    }

    private static NetworkedPlayerInfo.PlayerOutfit GrayOutfit()
    {
        var outfit = new NetworkedPlayerInfo.PlayerOutfit
        {
            ColorId = GrayColorId,
            HatId = "hat_NoHat",
            SkinId = "skin_None",
            VisorId = "visor_EmptyVisor",
            PetId = "pet_EmptyPet",
            NamePlateId = "nameplate_NoPlate",
            PlayerName = "",
        };
        return outfit;
    }
}
