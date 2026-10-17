using System;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spieleabend.Core;

/// <summary>
/// Ein Faehigkeiten-Button unten rechts (Klon des Kill-Buttons) mit Abklingzeit, Taste und optionalem Ziel.
/// Wird in CustomRole.Setup per AddButton(new RoleButton { ... }) angelegt.
/// </summary>
public class RoleButton
{
    /// <summary>Text auf dem Button, als "Deutsch|English".</summary>
    public string Label = "";

    private string ShownLabel => Lang.Pick(Label);

    /// <summary>Optional: eigenes Symbol statt dem Messer des Kill-Buttons (Name aus Assets/Icons, z. B. "shield").</summary>
    public string Icon;

    /// <summary>Abklingzeit in Sekunden (meist eine RoleOption: () => meineOption.Value).</summary>
    public Func<float> Cooldown = () => 30f;

    /// <summary>Abklingzeit zum Rundenstart (wird auf Cooldown begrenzt).</summary>
    public float StartCooldown = 10f;

    public KeyCode Hotkey = KeyCode.None;

    /// <summary>true: Button ist nur aktiv, wenn ein Spieler in Reichweite ist (wie der Kill-Button).</summary>
    public bool NeedsTarget = true;

    /// <summary>Optional: welche Spieler als Ziel erlaubt sind.</summary>
    public Func<PlayerControl, bool> TargetFilter;

    /// <summary>Wird beim Klick aufgerufen. Ziel ist null, wenn NeedsTarget = false.</summary>
    public Action<PlayerControl> OnClick;

    public bool UsableWhenDead;

    /// <summary>Optional: Button nur anzeigen, wenn true (z. B. Arsonist "Anzuenden" erst wenn alle uebergossen).</summary>
    public Func<bool> Visible;

    /// <summary>Optional: Button nur benutzbar, wenn true (z. B. nur ein Schild gleichzeitig).</summary>
    public Func<bool> CanUse;

    /// <summary>
    /// Fuer Impostor-Faehigkeiten, die toeten (Biss, Fluch ...): teilt sich die Abklingzeit mit dem normalen Kill-Button.
    /// Nach der Faehigkeit ist der Kill gesperrt und nach einem Kill die Faehigkeit - kein Doppelkill.
    /// </summary>
    public bool SharesKillCooldown;

    /// <summary>
    /// Optional: Wirkungsdauer (z. B. Tarnung 10 s). Waehrenddessen zeigt der Button wie beim Shapeshifter,
    /// wie lange die Faehigkeit noch laeuft; die Abklingzeit startet erst danach.
    /// </summary>
    public Func<float> Duration;

    /// <summary>Optional: wie oft der Knopf noch benutzt werden kann (Zahl am Knopf, wie beim Engineer).</summary>
    public Func<int> UsesLeft;

    /// <summary>Optional: wird aufgerufen, wenn die Wirkungsdauer endet.</summary>
    public Action OnEffectEnd;

    private float effectUntil = -1f, effectLength;

    /// <summary>Laeuft die Faehigkeit gerade (Wirkungsdauer)?</summary>
    public bool EffectActive => effectUntil >= 0f;

    /// <summary>Wirkung vorzeitig beenden (z. B. Meeting).</summary>
    public void EndEffect()
    {
        if (!EffectActive) return;
        effectUntil = -1f;
        Timer = Cooldown();
        OnEffectEnd?.Invoke();
    }

    public float Timer { get; set; }
    public PlayerControl Target { get; private set; }
    public CustomRole Owner { get; internal set; }

    private KillButton ui;

    /// <summary>Der sichtbare Knopf (fuer die Anordnung in ButtonManager), sonst null.</summary>
    internal Transform ShownTransform => ui && ui.gameObject.activeSelf ? ui.transform : null;

    internal void ResetTimer(bool roundStart)
    {
        float max = Cooldown();
        Timer = roundStart ? Mathf.Min(StartCooldown, max) : max;
    }

    internal void Hide()
    {
        if (EffectActive && !CustomRole.IsAlive(PlayerControl.LocalPlayer)) EndEffect();
        SetTarget(null);
        if (ui) ui.gameObject.SetActive(false);
    }

    internal void Update(HudManager hud, bool inMeeting)
    {
        var local = PlayerControl.LocalPlayer;
        bool alive = !local.Data.IsDead;
        // Lebende: HUD sichtbar = Melden-Knopf da. Geister haben keinen Melden-Knopf - dort zaehlt, ob das Knopf-Raster sichtbar ist.
        bool hudVisible = alive
            ? hud.ReportButton.gameObject.activeSelf
            : hud.KillButton && hud.KillButton.transform.parent && hud.KillButton.transform.parent.gameObject.activeInHierarchy;
        bool show = (alive || UsableWhenDead) && ShipStatus.Instance && !inMeeting && hudVisible
                    && (Visible == null || Visible());

        if (!show)
        {
            Hide();
            if (inMeeting)
            {
                EndEffect();
                ResetTimer(false); // nach jedem Meeting volle Abklingzeit
            }
            return;
        }

        if (!ui) CreateUi(hud);
        ui.gameObject.SetActive(true);
        if (ui.buttonLabelText && ui.buttonLabelText.text != ShownLabel) ui.OverrideText(ShownLabel); // falls das Spiel sie doch zuruecksetzt
        UpdateUses();

        // Wirkungsdauer laeuft: Fuellbalken wie beim Shapeshifter, danach startet die Abklingzeit
        if (EffectActive)
        {
            float left = effectUntil - Time.time;
            if (left > 0f)
            {
                ui.SetFillUp(left, effectLength);
                ui.SetDisabled();
                SetTarget(null);
                return;
            }
            EndEffect();
        }

        if (local.CanMove || local.inVent) Timer = Mathf.Max(0f, Timer - Time.deltaTime);
        if (SharesKillCooldown) Timer = Mathf.Max(Timer, local.killTimer); // nach einem normalen Kill auch hier warten
        ui.SetCoolDown(Timer, Mathf.Max(Cooldown(), Timer));

        SetTarget(NeedsTarget ? FindTarget(local) : null);
        bool ready = Timer <= 0f && (!NeedsTarget || Target) && (CanUse == null || CanUse());
        if (ready) ui.SetEnabled();
        else ui.SetDisabled();

        if (Hotkey != KeyCode.None && Input.GetKeyDown(Hotkey)) Click();
    }

    private int shownUses = int.MinValue;
    private GameObject usesBadge;
    private TMPro.TextMeshPro usesText;
    private static Sprite badgeSprite;

    /// <summary>
    /// Zaehler am Knopf (runde Plakette oben rechts, wie beim Engineer) - nur wenn UsesLeft gesetzt ist.
    /// Der kopierte Kill-Knopf hat keinen eigenen Zaehler, darum bauen wir ihn selbst.
    /// </summary>
    private void UpdateUses()
    {
        if (UsesLeft == null) return;
        int uses = Mathf.Max(0, UsesLeft());
        if (!usesBadge) CreateBadge();
        if (uses == shownUses || !usesText) return;
        shownUses = uses;
        usesText.text = uses.ToString();
    }

    private void CreateBadge()
    {
        try
        {
            usesBadge = new GameObject("Spieleabend_UsesBadge");
            usesBadge.layer = ui.gameObject.layer;
            usesBadge.transform.SetParent(ui.transform, false);
            var sr = usesBadge.AddComponent<SpriteRenderer>();
            sr.sprite = BadgeSprite();
            if (ui.graphic)
            {
                sr.sortingLayerID = ui.graphic.sortingLayerID;
                sr.sortingOrder = ui.graphic.sortingOrder + 2;
                // Oben rechts auf das Knopfbild setzen
                var b = ui.graphic.bounds;
                var corner = ui.transform.InverseTransformPoint(new Vector3(b.max.x - b.size.x * 0.12f, b.max.y - b.size.y * 0.12f, ui.graphic.transform.position.z));
                usesBadge.transform.localPosition = new Vector3(corner.x, corner.y, -0.1f);
                float size = b.size.y * 0.38f / Mathf.Max(0.001f, ui.transform.lossyScale.y);
                usesBadge.transform.localScale = new Vector3(size, size, 1f);
            }

            if (!ui.buttonLabelText) return;
            var textGo = Object.Instantiate(ui.buttonLabelText.gameObject, usesBadge.transform);
            textGo.name = "Spieleabend_UsesText";
            foreach (var translator in textGo.GetComponents<TextTranslatorTMP>()) Object.Destroy(translator);
            usesText = textGo.GetComponent<TMPro.TextMeshPro>();
            usesText.enableAutoSizing = false;
            usesText.alignment = TMPro.TextAlignmentOptions.Center;
            usesText.rectTransform.sizeDelta = new Vector2(2f, 1f);
            textGo.transform.localPosition = new Vector3(0f, 0.02f, -0.05f);
            var want = ui.buttonLabelText.transform.lossyScale * 1.3f;
            var parent = usesBadge.transform.lossyScale;
            textGo.transform.localScale = new Vector3(want.x / parent.x, want.y / parent.y, 1f);
            var textRenderer = textGo.GetComponent<Renderer>();
            if (textRenderer) textRenderer.sortingOrder = sr.sortingOrder + 1;
        }
        catch (Exception e) { Plugin.Logger.LogWarning($"Zaehler am Knopf: {e.Message}"); }
    }

    /// <summary>Runde Plakette: dunkler Kreis mit heller Kante (1 Einheit gross).</summary>
    private static Sprite BadgeSprite()
    {
        if (badgeSprite) return badgeSprite;
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S / 2f, S / 2f));
            float a = Mathf.Clamp01(S / 2f - d);
            var c = d > S / 2f - 6f ? new Color(0.95f, 0.95f, 0.95f, a) : new Color(0.16f, 0.16f, 0.2f, a);
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        badgeSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        badgeSprite.hideFlags = HideFlags.HideAndDontSave;
        return badgeSprite;
    }

    private void CreateUi(HudManager hud)
    {
        // Nicht ins Raster des Spiels, sondern in unsere eigene Spalte daneben (siehe ButtonManager.Layout)
        ui = Object.Instantiate(hud.KillButton, ButtonManager.Container(hud));
        ui.name = $"{Owner?.Name}_{Label}_Button";

        // Der Kill-Button uebersetzt seine Beschriftung selbst immer wieder zu "Kill" - den Uebersetzer entfernen
        if (ui.buttonLabelText)
            foreach (var translator in ui.buttonLabelText.GetComponents<TextTranslatorTMP>())
                Object.Destroy(translator);
        ui.OverrideText(ShownLabel);

        if (!string.IsNullOrEmpty(Icon) && ui.graphic)
        {
            var sprite = ButtonIcons.Get(Icon, ui.graphic.sprite);
            if (sprite) ui.graphic.sprite = sprite;
        }

        // Vanilla-Klick-Logik (Kill-Anfrage) komplett ersetzen
        var passive = ui.GetComponent<PassiveButton>();
        passive.OnClick = new Button.ButtonClickedEvent();
        passive.OnClick.AddListener((Action)Click);
    }

    public void Click()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || local.Data == null || (local.Data.IsDead && !UsableWhenDead) || !local.CanMove) return;
        if (Owner == null || !Owner.IsLocalRole || Timer > 0f) return;
        if (NeedsTarget && !CustomRole.IsAlive(Target)) return;
        if (Visible != null && !Visible()) return;
        if (CanUse != null && !CanUse()) return;

        var target = Target;
        Timer = Cooldown();
        SetTarget(null);

        if (Duration != null)
        {
            effectLength = Mathf.Max(0.1f, Duration());
            effectUntil = Time.time + effectLength;
        }

        // Normalen Kill ebenfalls sperren (volle Kill-Abklingzeit)
        if (SharesKillCooldown && GameManager.Instance)
            local.SetKillTimer(Mathf.Max(local.killTimer, GameManager.Instance.LogicOptions.GetKillCooldown()));
        OnClick?.Invoke(target);
    }

    private PlayerControl FindTarget(PlayerControl local)
    {
        if (!local.CanMove || local.inVent) return null;

        float range = GameManager.Instance.LogicOptions.GetKillDistance();
        Vector2 myPos = local.GetTruePosition();
        PlayerControl best = null;

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p == local || !CustomRole.IsAlive(p) || p.inVent) continue;
            if (TargetFilter != null && !TargetFilter(p)) continue;

            Vector2 theirPos = p.GetTruePosition();
            float dist = Vector2.Distance(myPos, theirPos);
            if (dist > range) continue;
            if (PhysicsHelpers.AnythingBetween(myPos, theirPos, Constants.ShipAndObjectsMask, false)) continue;

            best = p;
            range = dist;
        }
        return best;
    }

    private void SetTarget(PlayerControl target)
    {
        if (Target == target) return;

        if (Target && Target.cosmetics)
            Target.cosmetics.SetOutline(false, new Il2CppSystem.Nullable<Color>());

        Target = target;

        if (Target && Target.cosmetics && Owner != null)
            Target.cosmetics.SetOutline(true, new Il2CppSystem.Nullable<Color>(Owner.Color));
    }
}

/// <summary>Zeigt die Buttons der lokalen Rolle an und versteckt alle anderen.</summary>
internal static class ButtonManager
{
    public static void Reset()
    {
        foreach (var role in RoleRegistry.Roles)
            foreach (var button in role.Buttons)
                button.Hide();
    }

    public static void OnRolesAssigned()
    {
        foreach (var role in RoleRegistry.Roles)
            foreach (var button in role.Buttons)
                button.ResetTimer(true);
    }

    public static void Update(HudManager hud)
    {
        var local = PlayerControl.LocalPlayer;
        bool hasData = local && local.Data != null;
        bool inMeeting = MeetingHud.Instance || ExileController.Instance;

        foreach (var role in RoleRegistry.Roles)
        {
            bool mine = hasData && role.IsLocalRole; // Hauptrolle oder Anpassung
            foreach (var button in role.Buttons)
            {
                if (mine) button.Update(hud, inMeeting);
                else button.Hide();
            }
        }

        Layout(hud);
    }

    private static Transform container;

    /// <summary>
    /// Eigener Behaelter fuer alle Rollen-Knoepfe, neben dem Knopf-Raster des Spiels. Das Spiel blendet seine Knoepfe
    /// (Benutzen/Haustier, Melden, Sabotage ...) staendig um und sortiert dabei neu - unsere Knoepfe sind deshalb nicht
    /// mehr in seinem Raster, sondern stehen in einer eigenen Spalte links daneben und koennen nie darunter rutschen.
    /// </summary>
    internal static Transform Container(HudManager hud)
    {
        if (container) return container;
        var grid = hud.KillButton.transform.parent;
        var go = new GameObject("Spieleabend_RoleButtons");
        go.layer = grid.gameObject.layer;
        go.transform.SetParent(grid.parent, false);
        container = go.transform;
        return container;
    }

    private const int Rows = 2;

    /// <summary>Jedes Frame: Behaelter deckungsgleich zum Raster halten und unsere Knoepfe links neben die Spiel-Knoepfe stellen.</summary>
    private static void Layout(HudManager hud)
    {
        if (!container || !hud.KillButton) return;
        var grid = hud.KillButton.transform.parent;
        container.localPosition = grid.localPosition;
        container.localRotation = grid.localRotation;
        container.localScale = grid.localScale;

        var arrange = grid.GetComponent<GridArrange>();
        var cell = arrange ? new Vector2(Mathf.Abs(arrange.CellSize.x), Mathf.Abs(arrange.CellSize.y)) : new Vector2(1.1f, 1.1f);
        if (cell.x < 0.1f) cell.x = 1.1f;
        if (cell.y < 0.1f) cell.y = 1.1f;

        // Linker und unterer Rand der Spiel-Knoepfe
        float minX = float.MaxValue, minY = float.MaxValue;
        for (int i = 0; i < grid.childCount; i++)
        {
            var child = grid.GetChild(i);
            if (!child.gameObject.activeSelf) continue;
            minX = Mathf.Min(minX, child.localPosition.x);
            minY = Mathf.Min(minY, child.localPosition.y);
        }
        if (minX == float.MaxValue) return;

        int k = 0;
        foreach (var role in RoleRegistry.Roles)
            foreach (var button in role.Buttons)
            {
                var t = button.ShownTransform;
                if (!t) continue;
                t.localPosition = new Vector3(minX - cell.x * (1 + k / Rows), minY + cell.y * (k % Rows), t.localPosition.z);
                k++;
            }
    }
}
