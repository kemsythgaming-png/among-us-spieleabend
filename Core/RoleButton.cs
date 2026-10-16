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
    private bool wasShown;

    /// <summary>Das Spiel ordnet die Buttons unten rechts in einem Raster an - nach jeder Sichtbarkeits-Aenderung neu einsortieren.</summary>
    private void Rearrange()
    {
        if (!ui || !ui.transform.parent) return;
        var grid = ui.transform.parent.GetComponent<GridArrange>();
        if (!grid) return;
        grid.GetChildsActive();
        grid.ArrangeChilds();
    }

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
        if (wasShown)
        {
            wasShown = false;
            Rearrange();
        }
    }

    internal void Update(HudManager hud, bool inMeeting)
    {
        var local = PlayerControl.LocalPlayer;
        bool alive = !local.Data.IsDead;
        bool show = (alive || UsableWhenDead) && ShipStatus.Instance && !inMeeting && hud.ReportButton.gameObject.activeSelf
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
        if (!wasShown)
        {
            wasShown = true;
            Rearrange();
        }
        if (ui.buttonLabelText && ui.buttonLabelText.text != ShownLabel) ui.OverrideText(ShownLabel); // falls das Spiel sie doch zuruecksetzt

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

    private void CreateUi(HudManager hud)
    {
        ui = Object.Instantiate(hud.KillButton, hud.KillButton.transform.parent);
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

        KeepGridTidy(hud);
    }

    private static int lastActiveButtons = -1;
    private static float nextOverlapCheck;

    /// <summary>
    /// Das Spiel blendet eigene Knoepfe (Haustier, Benutzen, Sabotage ...) spaeter ein oder aus und sortiert dann ohne
    /// unsere Knoepfe - die rutschen sonst darunter. Darum: bei jeder Aenderung und wenn zwei Knoepfe uebereinander liegen,
    /// das Raster neu anordnen. Gilt fuer alle Rollen-Knoepfe.
    /// </summary>
    private static void KeepGridTidy(HudManager hud)
    {
        var parent = hud.KillButton ? hud.KillButton.transform.parent : null;
        if (!parent) return;

        int active = 0;
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).gameObject.activeSelf) active++;

        bool rearrange = active != lastActiveButtons;
        lastActiveButtons = active;

        if (!rearrange && Time.time >= nextOverlapCheck)
        {
            nextOverlapCheck = Time.time + 0.5f;
            var positions = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < parent.childCount && !rearrange; i++)
            {
                var child = parent.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                foreach (var pos in positions)
                    if ((pos - child.localPosition).sqrMagnitude < 0.01f) rearrange = true;
                positions.Add(child.localPosition);
            }
        }

        if (!rearrange) return;
        var grid = parent.GetComponent<GridArrange>();
        if (!grid) return;
        grid.GetChildsActive();
        grid.ArrangeChilds();
    }
}
