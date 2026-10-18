using System;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spieleabend.Core;

/// <summary>
/// Eigene Knoepfe oben rechts im Stil des Spiels (Kopien des Notizblock-Buttons "Match Info"):
/// Rollen-Info (immer) und Rollen-Einstellungen (nur Lobby/Practice), links neben dem Original.
/// </summary>
internal static class HudButtons
{
    private static PassiveButton infoButton, settingsButton;
    private static AspectPosition infoPos, settingsPos;

    /// <summary>true, wenn unsere Knoepfe oben rechts sichtbar sind (dann braucht es die Ersatz-Knoepfe unten links nicht).</summary>
    public static bool InfoVisible => infoButton && infoButton.gameObject.activeInHierarchy;
    public static bool SettingsVisible => settingsButton && settingsButton.gameObject.activeInHierarchy;

    public static void Update(HudManager hud)
    {
        try
        {
            var source = hud.MatchInfoButton;
            if (!source) return;
            var sourcePos = source.GetComponent<AspectPosition>();

            if (!infoButton) (infoButton, infoPos) = Create(source, "hud_roleinfo", "Spieleabend_RoleInfoButton", RoleGuide.Toggle);
            if (!settingsButton) (settingsButton, settingsPos) = Create(source, "hud_settings", "Spieleabend_SettingsButton", SettingsPanel.Toggle);

            // Oben rechts neben "Match Info Guide" - in der Lobby UND im Spiel. Aber NICHT waehrend einer Aufgabe (Minigame,
            // sonst liegt der Knopf ueber dem Aufgabenfenster), nicht im Meeting (dort gibt es den IMGUI-Ersatzknopf),
            // nicht in der Kleiderkammer und nicht im Intro.
            bool hudUp = (LobbyBehaviour.Instance || ShipStatus.Instance) && !IntroCutscene.Instance
                         && !PlayerCustomizationMenu.Instance && !Minigame.Instance;
            infoButton.gameObject.SetActive(hudUp && RoleRegistry.Roles.Count > 0);
            settingsButton.gameObject.SetActive(hudUp && SettingsWindow.Available);

            // Abstand wie zwischen den Original-Knoepfen (z. B. Chat -> Match Info Guide); der zweite Knopf steht daneben.
            float spacing = Spacing(hud, sourcePos, source);
            Place(infoPos, sourcePos, spacing);
            Place(settingsPos, sourcePos, spacing * 2f);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"HUD-Knoepfe: {e.Message}");
        }
    }

    private static (PassiveButton, AspectPosition) Create(PassiveButton source, string icon, string name, Action onClick)
    {
        var go = Object.Instantiate(source.gameObject, source.transform.parent);
        go.name = name;

        // Logik des Originals entfernen (es wuerde sonst den Match Info Guide oeffnen bzw. sich selbst verschieben)
        foreach (var c in go.GetComponents<MatchInfoHudButton>()) Object.Destroy(c);

        var button = go.GetComponent<PassiveButton>();
        button.OnClick = new Button.ButtonClickedEvent();
        button.OnClick.AddListener(onClick);

        // Unser komplettes Icon (Rahmen + Symbol) in den groessten Sprite setzen und alle anderen Sprites ausblenden,
        // damit nur EIN Rahmen zu sehen ist (kein doppelter aus Spiel-Rahmen + Symbol-Rahmen).
        var renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer main = null;
        float largest = -1f;
        foreach (var sr in renderers)
        {
            if (!sr || !sr.sprite) continue;
            float area = sr.sprite.bounds.size.x * sr.sprite.bounds.size.y;
            if (area > largest) { largest = area; main = sr; }
        }
        foreach (var sr in renderers)
        {
            if (!sr) continue;
            if (sr == main)
            {
                var sprite = ButtonIcons.Get(icon, sr.sprite);
                if (sprite) sr.sprite = sprite;
            }
            else sr.enabled = false;
        }

        // Controller-Hinweise des Originals ausblenden
        for (int i = 0; i < go.transform.childCount; i++)
        {
            var child = go.transform.GetChild(i);
            if (child.name.Contains("Glyph")) child.gameObject.SetActive(false);
        }

        return (button, go.GetComponent<AspectPosition>());
    }

    private static float Spacing(HudManager hud, AspectPosition sourcePos, PassiveButton source)
    {
        var settingsPos = hud.SettingsButton ? hud.SettingsButton.GetComponent<AspectPosition>() : null;
        if (sourcePos && settingsPos)
        {
            float d = Mathf.Abs(sourcePos.DistanceFromEdge.x - settingsPos.DistanceFromEdge.x);
            if (d > 0.3f) return d;
        }
        return ButtonWidth(source) + 0.12f;
    }

    /// <summary>Breite des Knopfs = groesstes Bild darin (kleine Controller-Symbole zaehlen nicht).</summary>
    private static float ButtonWidth(PassiveButton source)
    {
        float w = 0f;
        foreach (var sr in source.GetComponentsInChildren<SpriteRenderer>())
            if (sr && sr.sprite) w = Mathf.Max(w, sr.bounds.size.x);
        return w > 0.1f ? w : 0.75f;
    }

    private static void Place(AspectPosition target, AspectPosition source, float offset)
    {
        if (!target || !source) return;
        var d = source.DistanceFromEdge;
        target.Alignment = source.Alignment; // gleiche Ausrichtung wie "Match Info Guide" (oben rechts)
        target.DistanceFromEdge = new Vector3(d.x + offset, d.y, d.z);
        target.AdjustPosition();
    }
}
