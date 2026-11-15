using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using BepInEx.Configuration;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Solo-Test im Freeplay (Uebungsmodus): Du bekommst die eingestellte Testrolle,
/// und die ersten Dummies gelten als Impostor (Name rot), damit man Treffer testen kann.
/// Die Laptop-Funktion "Alle wiederbeleben" funktioniert weiterhin.
/// </summary>
internal static class FreeplayTest
{
    private static ConfigEntry<string> testRole;
    private static ConfigEntry<string> testModifier;
    private static ConfigEntry<string> dummyRole;
    private static ConfigEntry<int> dummyImpostors;
    private static ConfigEntry<bool> dummiesFollowVote;
    private static ConfigEntry<bool> dummiesAttack;
    private static ConfigEntry<bool> buttonsWhenDead;
    private static ConfigEntry<bool> dummiesWalk;

    // Laufende Dummys: Richtung + nächster Richtungswechsel + letzte Position (Wand-Erkennung).
    private static readonly Dictionary<byte, Vector2> walkDir = new();
    private static readonly Dictionary<byte, float> walkTurnAt = new();
    private static readonly Dictionary<byte, Vector2> walkLast = new();

    /// <summary>Angriffs-Abklingzeit je Dummy (bei Nähe-Angriff).</summary>
    private static readonly Dictionary<byte, float> nextAttack = new();

    private static ShipStatus appliedFor;
    private static readonly HashSet<byte> fakeImpostors = new();

    /// <summary>Dummy-PlayerId -> zugewiesene Testrolle.</summary>
    private static readonly Dictionary<byte, byte> dummyRoles = new();

    public static bool IsFreeplay => AmongUsClient.Instance && AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay;

    public static string TestRoleName => testRole.Value;

    public static string TestRoleDisplayName =>
        RoleRegistry.Roles.FirstOrDefault(r => r.ConfigSection == testRole.Value || r.Name == testRole.Value)?.Name ?? testRole.Value;
    public static int DummyImpostors => dummyImpostors.Value;

    /// <summary>Zusätzliche Anpassung/Geisterrolle, die du neben deiner Hauptrolle bekommst (leer = keine).</summary>
    public static string TestModifierName => testModifier != null ? testModifier.Value : "";
    public static string TestModifierDisplay =>
        RoleRegistry.Roles.FirstOrDefault(r => r.ConfigSection == TestModifierName || r.Name == TestModifierName)?.Name ?? TestModifierName;

    public static string DummyRoleName => dummyRole.Value;
    public static string DummyRoleDisplay =>
        RoleRegistry.Roles.FirstOrDefault(r => r.ConfigSection == dummyRole.Value || r.Name == dummyRole.Value)?.Name ?? dummyRole.Value;
    public static bool DummiesFollowVote => dummiesFollowVote.Value;
    public static void ToggleDummiesFollowVote() => dummiesFollowVote.Value = !dummiesFollowVote.Value;
    public static bool DummiesAttack => dummiesAttack.Value;
    public static void ToggleDummiesAttack() => dummiesAttack.Value = !dummiesAttack.Value;
    /// <summary>Freeplay: Rollen-Knöpfe auch benutzen, wenn man tot ist (zum Testen).</summary>
    public static bool ButtonsWhenDead => IsFreeplay && buttonsWhenDead != null && buttonsWhenDead.Value;
    public static void ToggleButtonsWhenDead() { if (buttonsWhenDead != null) buttonsWhenDead.Value = !buttonsWhenDead.Value; }
    /// <summary>Freeplay: Dummys laufen umher (zum Testen von Verlangsamungen o. ä.).</summary>
    public static bool DummiesWalk => IsFreeplay && dummiesWalk != null && dummiesWalk.Value;
    public static void ToggleDummiesWalk()
    {
        if (dummiesWalk == null) return;
        dummiesWalk.Value = !dummiesWalk.Value;
        if (!dummiesWalk.Value) // beim Ausschalten sofort anhalten
            foreach (var d in PlayerControl.AllPlayerControls)
                if (d && d.isDummy && d.MyPhysics && d.MyPhysics.body) d.MyPhysics.body.velocity = Vector2.zero;
    }

    public static void BindConfig(ConfigFile config)
    {
        testRole = config.Bind("Freeplay-Test", "Rolle", "Sheriff", "Rolle, die du im Freeplay bekommst (Name der Rolle, leer = keine).");
        testModifier = config.Bind("Freeplay-Test", "Anpassung", "", "Zusätzliche Anpassung/Geisterrolle neben deiner Hauptrolle (leer = keine).");
        dummyRole = config.Bind("Freeplay-Test", "DummyRolle", "Jester", "Rolle, die ein Dummy per Knopf bekommt.");
        dummyImpostors = config.Bind("Freeplay-Test", "DummyImpostors", 0, "So viele Dummies gelten im Freeplay als Impostor (rot). Standard 0 = normale Namen.");
        dummiesFollowVote = config.Bind("Freeplay-Test", "DummiesFollowVote", true, "Dummies stimmen im Meeting so ab wie du (zum Testen von Rausvote-Rollen).");
        dummiesAttack = config.Bind("Freeplay-Test", "DummiesAttack", false, "Impostor-Dummies greifen dich an, wenn du ihnen zu nahe kommst (zum Testen von Schilden).");
        buttonsWhenDead = config.Bind("Freeplay-Test", "ButtonsWhenDead", false, "Rollen-Knöpfe auch als Toter benutzbar (nur Freeplay, zum Testen).");
        dummiesWalk = config.Bind("Freeplay-Test", "DummiesWalk", false, "Dummys laufen umher (zum Testen von Verlangsamungen).");
    }

    // ---------- Sabotage + Dummy-Angriff (Freeplay-Testhilfen) ----------

    /// <summary>Eine Sabotage starten (auch als Crew), z. B. Licht (Electrical), Reaktor, Sauerstoff (LifeSupp), Kommunikation.</summary>
    public static void StartSabotage(SystemTypes system)
    {
        if (!IsFreeplay || !ShipStatus.Instance) return;
        try
        {
            // Nach einer behobenen Sabotage läuft eine Abklingzeit, die ein erneutes Auslösen blockiert -> zurücksetzen.
            var sys = ShipStatus.Instance.Systems != null && ShipStatus.Instance.Systems.ContainsKey(SystemTypes.Sabotage)
                ? ShipStatus.Instance.Systems[SystemTypes.Sabotage] : null;
            var sab = sys?.TryCast<SabotageSystemType>();
            if (sab != null)
            {
                if (sab.AnyActive) { Notifier.Show(Lang.T("Erst die laufende Sabotage beheben.", "Fix the running sabotage first."), 2.5f); return; }
                sab.Timer = 0f; // Abklingzeit nach dem letzten Fix zurücksetzen
            }

            ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Sabotage, (byte)system);
        }
        catch (System.Exception e) { Plugin.Logger.LogWarning($"Sabotage starten: {e.Message}"); }
    }

    /// <summary>Gilt der Dummy als Angreifer? (Fake-Impostor oder Impostor-Rolle)</summary>
    private static bool IsAttacker(PlayerControl d)
    {
        // Nur Dummys mit zugewiesener Impostor-Rolle greifen an (keine Fake-Impostor mehr).
        var r = DummyRole(d.PlayerId);
        return r != null && r.Team == RoleTeam.Impostor;
    }

    private static bool InKillRange(PlayerControl a, PlayerControl b)
    {
        float range = 1f;
        try { range = GameManager.Instance.LogicOptions.GetKillDistance(); } catch { }
        return Vector2.Distance(a.GetTruePosition(), b.GetTruePosition()) <= range;
    }

    /// <summary>
    /// Dummy übt seine Fähigkeit/seinen Kill auf den lokalen Spieler aus. Fähigkeiten (BOO, Vampir-Biss ...) immer;
    /// ein reiner Kill nur, wenn der Spieler in Reichweite steht - sonst würde der Dummy zum Kill zu dir teleportieren.
    /// </summary>
    private static void DummyAttack(PlayerControl dummy, PlayerControl victim, bool requireRange)
    {
        if (!dummy || !victim || !CustomRole.IsAlive(dummy) || !CustomRole.IsAlive(victim)) return;
        var role = DummyRole(dummy.PlayerId);
        if (role != null && role.FreeplayAbility(dummy, victim)) return; // Fähigkeit selbst erledigt (z. B. BOO, Vampir-Biss)

        if (requireRange && !InKillRange(dummy, victim))
        {
            Notifier.Show(Lang.T("Opfer ist zu weit weg - stell dich/den Dummy näher dran.", "Victim is too far - get closer."), 2f);
            return;
        }
        KillSystem.TryKill(dummy, victim); // beachtet Medic/Bodyguard/Süßigkeitenkind/Erster-Tod ...
    }

    /// <summary>Nächstes tötbares Opfer eines Angreifer-Dummys in Kill-Reichweite (anderer Dummy ODER der lokale Spieler, keine Impostor).</summary>
    private static PlayerControl NearestVictim(PlayerControl attacker)
    {
        float range = 1f;
        try { range = GameManager.Instance.LogicOptions.GetKillDistance(); } catch { }
        PlayerControl best = null; float bestD = float.MaxValue;
        Vector2 at = attacker.GetTruePosition();
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!p || p == attacker || !CustomRole.IsAlive(p) || p.inVent || CustomRole.IsImpostor(p)) continue;
            float d = Vector2.Distance(at, p.GetTruePosition());
            if (d <= range && d < bestD) { bestD = d; best = p; }
        }
        return best;
    }

    /// <summary>Knopf: dieser Dummy greift den nächsten in der Nähe an (anderer Dummy oder du) - Fähigkeit immer, reiner Kill nur in Nähe.</summary>
    public static void DummyAttackNow(byte dummyId)
    {
        var d = CustomRole.FindPlayer(dummyId);
        if (!d) return;
        var victim = NearestVictim(d) ?? PlayerControl.LocalPlayer; // niemand in der Nähe -> auf dich (mit Reichweiten-Hinweis)
        if (victim) DummyAttack(d, victim, true);
    }

    /// <summary>Knopf: dieser Dummy benutzt seine Rollen-Fähigkeit (auf dich bzw. auf sich/den Boden), ohne Kill-Fallback.</summary>
    public static void DummyUseAbility(byte dummyId)
    {
        var d = CustomRole.FindPlayer(dummyId);
        var local = PlayerControl.LocalPlayer;
        if (!d || !local || !CustomRole.IsAlive(d)) return;
        var role = DummyRole(d.PlayerId);
        if (role == null) { ChatNote.Message(Lang.T("Dieser Dummy hat keine Rolle.", "This dummy has no role.")); return; }
        if (!role.FreeplayAbility(d, local))
            ChatNote.Message(Lang.T($"Fähigkeit von {role.Name} ist im Test noch nicht nachgebaut.", $"{role.Name}'s ability isn't wired for test yet."));
    }

    /// <summary>Jedes Frame: Angreifer-Dummies killen den nächsten in Reichweite - den lokalen Spieler ODER andere Dummys (so können Dummys sich beim Umherlaufen gegenseitig töten).</summary>
    private static void DoAttacks()
    {
        if (CustomRole.InMeeting) return;

        foreach (var d in PlayerControl.AllPlayerControls)
        {
            if (!d || !d.isDummy || !CustomRole.IsAlive(d) || !IsAttacker(d)) continue;
            var victim = NearestVictim(d);
            if (!victim) continue;
            nextAttack.TryGetValue(d.PlayerId, out float t);
            if (Time.time < t) continue;
            nextAttack[d.PlayerId] = Time.time + 3f;
            DummyAttack(d, victim, false); // Reichweite schon geprüft (NearestVictim)
        }
    }

    /// <summary>Andere Dummy-Testrolle waehlen (zum Durchblaettern).</summary>
    public static void CycleDummyRole(int direction)
    {
        var names = new List<string> { "" };
        names.AddRange(RoleRegistry.Roles.Where(r => !r.IsGhostRole).Select(r => r.ConfigSection));
        int index = Mathf.Max(0, names.IndexOf(dummyRole.Value));
        dummyRole.Value = names[(index + direction + names.Count) % names.Count];
    }

    /// <summary>
    /// Deine Testrolle direkt setzen (aus der Liste gewaehlt). Ist die gewaehlte Rolle eine Anpassung ODER eine
    /// Geisterrolle, wird sie als ZUSATZ (neben der Hauptrolle) gesetzt - so kannst du Rolle + Anpassung zugleich testen.
    /// </summary>
    public static void SetTestRole(string section)
    {
        var role = RoleRegistry.Roles.FirstOrDefault(r => r.ConfigSection == section || r.Name == section);
        if (role != null && role.IsModifier)
        {
            // Anpassung = Zusatz NEBEN der Hauptrolle (stapelt)
            testModifier.Value = role.ConfigSection;
        }
        else if (role != null && role.IsGhostRole)
        {
            // Geisterrolle ERSETZT die Hauptrolle (zum Testen stirbt man) - keine alten Fähigkeiten mehr
            testModifier.Value = role.ConfigSection;
            testRole.Value = "";
        }
        else
        {
            // normale Hauptrolle (leer = keine); eine evtl. gesetzte GEISTER-Zusatzrolle dabei entfernen,
            // damit man nicht dauerhaft als Geist (z. B. Türgeist) angezeigt bleibt. Anpassungen bleiben.
            testRole.Value = section ?? "";
            var cur = RoleRegistry.Roles.FirstOrDefault(r => r.ConfigSection == TestModifierName || r.Name == TestModifierName);
            if (cur != null && cur.IsGhostRole) testModifier.Value = "";
        }
        if (IsFreeplay && ShipStatus.Instance) Apply();
    }

    /// <summary>Die zusätzliche Anpassung/Geisterrolle wieder entfernen.</summary>
    public static void ClearTestModifier()
    {
        if (testModifier != null) testModifier.Value = "";
        if (IsFreeplay && ShipStatus.Instance) Apply();
    }

    /// <summary>Einem freien Dummy die gewaehlte Rolle geben.</summary>
    public static void GiveDummyRole(string section)
    {
        if (!IsFreeplay || !ShipStatus.Instance) return;
        var role = RoleRegistry.Roles.FirstOrDefault(r => r.ConfigSection == section || r.Name == section);
        if (role == null) return;
        var dummy = PlayerControl.AllPlayerControls.ToArray()
            .FirstOrDefault(p => p && p.isDummy && p != PlayerControl.LocalPlayer && !dummyRoles.ContainsKey(p.PlayerId));
        if (!dummy) return;
        dummyRoles[dummy.PlayerId] = role.Id;
        Apply();
    }

    /// <summary>Genau diesem Dummy die gewaehlte Rolle geben (leer = Rolle entfernen).</summary>
    public static void SetDummyRole(byte dummyId, string section)
    {
        if (!IsFreeplay || !ShipStatus.Instance) return;
        CustomRole assigned = null;
        if (string.IsNullOrEmpty(section)) dummyRoles.Remove(dummyId);
        else
        {
            var role = RoleRegistry.Roles.FirstOrDefault(r => r.ConfigSection == section || r.Name == section);
            if (role == null) return;
            dummyRoles[dummyId] = role.Id;
            assigned = role;
        }
        Apply();

        // Geisterrolle an einem Dummy = der Dummy soll ein Geist sein -> ihn töten (Selbst-Kill, kein Teleport).
        if (assigned != null && assigned.IsGhostRole)
        {
            var d = CustomRole.FindPlayer(dummyId);
            if (d && d.isDummy && CustomRole.IsAlive(d)) KillSystem.ForceKill(d, d);
        }
    }

    /// <summary>Zugewiesene Rolle eines Dummys (oder null).</summary>
    public static CustomRole DummyRole(byte dummyId) =>
        dummyRoles.TryGetValue(dummyId, out var id) ? RoleRegistry.Get(id) : null;

    /// <summary>Alle Dummy-Rollen wieder entfernen.</summary>
    public static void ClearDummyRoles()
    {
        dummyRoles.Clear();
        if (IsFreeplay && ShipStatus.Instance) Apply();
    }

    public static int DummyRoleCount => dummyRoles.Count;

    /// <summary>
    /// Freeplay: Rolle frisch zuweisen (setzt Nutzungen, Gewinne, „einmal pro Runde"-Flags usw. zurück) und die
    /// Abklingzeiten der eigenen Knöpfe auf 0 - so steht der Knopf sofort wieder voll einsatzbereit da.
    /// </summary>
    public static void ResetButtonCooldowns()
    {
        if (!IsFreeplay || !ShipStatus.Instance) return;
        Apply();
        var local = PlayerControl.LocalPlayer;
        if (!local) return;
        foreach (var role in RoleRegistry.GetAll(local))
            foreach (var b in role.Buttons)
                b.Timer = 0f;
    }

    /// <summary>Sich selbst umbringen (zum Testen, auch als Impostor).</summary>
    public static void SelfKill()
    {
        var local = PlayerControl.LocalPlayer;
        if (IsFreeplay && local && CustomRole.IsAlive(local)) local.RpcMurderPlayer(local, true);
    }

    /// <summary>Sich selbst wiederbeleben (Freeplay) und die Testrolle frisch setzen.</summary>
    public static void Revive()
    {
        var local = PlayerControl.LocalPlayer;
        if (!IsFreeplay || !local || !local.Data || !local.Data.IsDead) return;
        local.Revive();
        // Nach Revive bleibt die Vanilla-Rolle sonst "Geist" (CrewmateGhost) -> Knöpfe weg. Lebende Grundrolle erzwingen.
        var role = RoleRegistry.Roles.FirstOrDefault(r => r.ConfigSection == testRole.Value || r.Name == testRole.Value);
        bool imp = role != null && role.Team == RoleTeam.Impostor;
        try { RoleManager.Instance.SetRole(local, imp ? RoleTypes.Impostor : RoleTypes.Crewmate); } catch { }
        Apply();
    }

    /// <summary>Alle eigenen Aufgaben sofort abschließen (Testknopf) - z. B. um task-gebundene Fähigkeiten zu testen.</summary>
    public static void CompleteAllTasks()
    {
        var local = PlayerControl.LocalPlayer;
        if (!IsFreeplay || !local || local.myTasks == null) return;
        // Snapshot, da CompleteTask die Liste verändern kann.
        foreach (var t in local.myTasks.ToArray())
        {
            // Nur echte Crew-Aufgaben (keine Sabotage, keine Rollen-Textzeile).
            var npt = t == null ? null : t.TryCast<NormalPlayerTask>();
            if (npt == null || npt.IsComplete) continue;
            // Mehrstufige Aufgaben (z. B. Fix Wiring 0/3) Schritt für Schritt vorrücken.
            int guard = 0;
            while (!npt.IsComplete && guard++ < 30)
                try { npt.NextStep(); } catch { break; }
            // Zählung/Netz-Buchführung abschließen (aktualisiert die "Total Tasks"-Leiste).
            try { local.CompleteTask(npt.Id); } catch { }
        }
        ChatNote.Message(Lang.T("Freeplay: alle Aufgaben erledigt", "Freeplay: all tasks completed"));
    }

    /// <summary>Alle toten Dummys wiederbeleben.</summary>
    public static void ReviveDummies()
    {
        if (!IsFreeplay) return;
        foreach (var d in PlayerControl.AllPlayerControls)
            if (d && d.isDummy && d.Data != null && d.Data.IsDead) d.Revive();
    }

    /// <summary>Einen Dummy töten (Testknopf) - stirbt an Ort und Stelle, ohne dass man hinteleportiert wird.</summary>
    public static void KillDummy(byte dummyId)
    {
        var d = CustomRole.FindPlayer(dummyId);
        if (IsFreeplay && d && d.isDummy && CustomRole.IsAlive(d)) KillSystem.ForceKill(d, d); // Selbst-Kill: kein Teleport
    }

    /// <summary>Ein Notfall-Meeting starten (zum Testen) - geht auch, wenn man selbst tot ist (dann ruft ein lebender Spieler/Dummy es).</summary>
    public static void StartMeeting()
    {
        var local = PlayerControl.LocalPlayer;
        if (!IsFreeplay || !local || CustomRole.InMeeting) return;

        // Eine laufende Sabotage verhindert JEDES Meeting (auch für Lebende) -> erst beheben.
        try
        {
            var sys = ShipStatus.Instance != null && ShipStatus.Instance.Systems != null && ShipStatus.Instance.Systems.ContainsKey(SystemTypes.Sabotage)
                ? ShipStatus.Instance.Systems[SystemTypes.Sabotage] : null;
            var sab = sys?.TryCast<SabotageSystemType>();
            if (sab != null && sab.AnyActive)
            {
                Notifier.Show(Lang.T("Erst die laufende Sabotage beheben, dann geht das Meeting.", "Fix the running sabotage first, then the meeting works."), 3f);
                return;
            }
        }
        catch { }

        // Ein Toter kann kein Meeting auslösen -> einen lebenden Spieler (notfalls Dummy) als Melder nehmen.
        PlayerControl caller = CustomRole.IsAlive(local) ? local : null;
        if (!caller)
            foreach (var p in PlayerControl.AllPlayerControls)
                if (CustomRole.IsAlive(p)) { caller = p; break; }
        if (!caller)
        {
            Notifier.Show(Lang.T("Kein lebender Spieler da, der ein Meeting rufen kann.", "No living player to call a meeting."), 3f);
            return;
        }
        // CmdReportDeadBody(null) = Notfall-Meeting auf dem richtigen Vanilla-Pfad (richtet den Meeting-Tisch korrekt ein;
        // RpcStartMeeting ließ die Spielerzahl auf negative Werte laufen).
        try { caller.CmdReportDeadBody(null); }
        catch (System.Exception e)
        {
            Plugin.Logger.LogWarning($"Meeting starten: {e.Message}");
            try { caller.RpcStartMeeting(null); } catch { }
        }
    }

    /// <summary>Den „Schutz für die ersten Toten" an sich selbst zum Testen geben.</summary>
    public static void GiveSelfFirstDeathShield()
    {
        if (IsFreeplay) FirstDeathShield.GiveLocalForTest();
    }

    /// <summary>Freeplay: die Rollen-Enthüllungs-Animation abspielen (wie beim Online-Spielstart). Der Rollentext wird von RoleUI überschrieben.</summary>
    public static void PlayIntro()
    {
        if (!IsFreeplay || !HudManager.InstanceExists || !ShipStatus.Instance) return;
        var hud = HudManager.Instance;
        var local = PlayerControl.LocalPlayer;
        if (!local || local.Data == null) return;
        if (IntroCutscene.Instance) return; // läuft schon
        try
        {
            var prefab = hud.IntroPrefab;
            if (!prefab) { Plugin.Logger.LogWarning("Intro: kein IntroPrefab"); return; }
            // Prefab direkt instanziieren (setzt IntroCutscene.Instance -> unser Rollen-/Text-Override greift).
            var intro = Object.Instantiate(prefab, hud.transform);
            intro.gameObject.SetActive(true);
            hud.StartCoroutine(intro.CoBegin());
            Plugin.Logger.LogInfo("Intro (Prefab) instanziiert");
        }
        catch (System.Exception e) { Plugin.Logger.LogWarning($"Freeplay-Intro: {e.Message}"); }
    }

    /// <summary>Jedes Frame im Meeting: Hast du schon abgestimmt, stimmen alle Dummies sofort genauso ab (und das Meeting endet).</summary>
    public static void MeetingVoteFollow(MeetingHud meeting)
    {
        if (!IsFreeplay || !dummiesFollowVote.Value || !meeting) return;
        var local = PlayerControl.LocalPlayer;
        if (!local) return;

        PlayerVoteArea localState = null;
        foreach (var s in meeting.playerStates)
            if (s && (byte)s.PlayerId == local.PlayerId) { localState = s; break; }
        if (localState == null || !localState.DidVote) return;

        byte target = (byte)localState.VotedForId;
        bool any = false;
        foreach (var s in meeting.playerStates)
        {
            if (!s || s.DidVote) continue;
            var p = CustomRole.FindPlayer((byte)s.PlayerId);
            if (!p || !p.isDummy || !CustomRole.IsAlive(p)) continue;

            try { meeting.CastVote((byte)s.PlayerId, target); } catch { }
            // Fallback (Freeplay ohne echten Host): Stimme direkt in die Abstimmungsfläche schreiben, damit sie zählt.
            if (!s.DidVote)
            {
                try { s.VotedForId = target; s.DidVote = true; } catch { }
            }
            any = true;
        }
        if (any) { try { meeting.CheckForEndVoting(); } catch { } }
    }

    public static bool IsFakeImpostor(PlayerControl player) => IsFreeplay && player && fakeImpostors.Contains(player.PlayerId);

    /// <summary>Test-Impostor-Dummy wird zum normalen Dummy (z. B. vom Zombie infiziert).</summary>
    public static void RemoveFakeImpostor(PlayerControl player)
    {
        if (player) fakeImpostors.Remove(player.PlayerId);
    }

    public static void Reset()
    {
        appliedFor = null;
        fakeImpostors.Clear();
        dummyRoles.Clear();
    }

    /// <summary>Dummys laufen umher; dabei greifen alle positionsabhängigen Tempo-Faktoren (z. B. Frost-Zonen).</summary>
    private static void WalkDummies()
    {
        // Während einer Voodoo-Übernahme laufen die Dummys NICHT (sonst stört das Umherlaufen die Übernahme/den Kill).
        if (Spieleabend.Roles.VoodooDoll.LocalPossessing) return;
        float now = Time.time;
        foreach (var d in PlayerControl.AllPlayerControls)
        {
            if (!d || !d.isDummy || !CustomRole.IsAlive(d) || d.inVent || !d.MyPhysics || !d.MyPhysics.body) continue;

            if (!walkDir.TryGetValue(d.PlayerId, out var dir) || now >= walkTurnAt.GetValueOrDefault(d.PlayerId))
            {
                dir = RandomDir();
                walkDir[d.PlayerId] = dir;
                walkTurnAt[d.PlayerId] = now + UnityEngine.Random.Range(2.5f, 5f);
                walkLast[d.PlayerId] = d.GetTruePosition();
            }

            // Alle positionsabhängigen Tempo-Faktoren der Rollen auf den Dummy anwenden (z. B. Frost-Zone verlangsamt)
            float factor = 1f;
            foreach (var role in RoleRegistry.Roles)
            {
                try { factor *= role.SpeedMultiplier(d); } catch { }
            }
            factor = Mathf.Clamp(factor, 0f, 2f);

            // Wand-Erkennung NUR bei normalem Tempo: ein verlangsamter Dummy (z. B. in einer Frost-Zone) bewegt sich
            // ohnehin kaum und würde sonst jeden Frame die Richtung wechseln -> Links/Rechts-Gezappel.
            var pos = d.GetTruePosition();
            if (factor > 0.6f && walkLast.TryGetValue(d.PlayerId, out var last) && (pos - last).sqrMagnitude < 0.00025f)
            {
                dir = RandomDir();
                walkDir[d.PlayerId] = dir;
            }
            walkLast[d.PlayerId] = pos;

            d.MyPhysics.body.velocity = dir * (2.5f * factor);
        }
    }

    private static Vector2 RandomDir()
    {
        float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }

    public static void Update()
    {
        if (DummiesWalk && ShipStatus.Instance && !CustomRole.InMeeting) WalkDummies();

        if (IsFreeplay && ShipStatus.Instance && dummiesAttack.Value) DoAttacks();

        // (Kein Auto-Intro mehr - die Rollen-Enthüllung kommt nur noch über den Knopf "Rollen-Intro abspielen".)

        if (!IsFreeplay || !ShipStatus.Instance || appliedFor == ShipStatus.Instance) return;
        var local = PlayerControl.LocalPlayer;
        if (!local || local.Data == null || !local.Data.Role) return;
        Apply();
    }

    /// <summary>Aus dem Einstellungsfenster: andere Testrolle waehlen.</summary>
    public static void CycleRole(int direction)
    {
        var names = new List<string> { "" };
        names.AddRange(RoleRegistry.Roles.Select(r => r.ConfigSection));
        int index = Mathf.Max(0, names.IndexOf(testRole.Value));
        testRole.Value = names[(index + direction + names.Count) % names.Count];
        if (IsFreeplay && ShipStatus.Instance) Apply();
    }

    public static void ChangeDummyImpostors(int direction)
    {
        dummyImpostors.Value = Mathf.Clamp(dummyImpostors.Value + direction, 0, 14);
        if (IsFreeplay && ShipStatus.Instance) Apply();
    }

    private static void Apply()
    {
        appliedFor = ShipStatus.Instance;
        var local = PlayerControl.LocalPlayer;
        // Hauptrolle (keine Anpassung/Geist) + optionale Zusatz-Anpassung/Geisterrolle
        var role = RoleRegistry.Roles.FirstOrDefault(r => (r.ConfigSection == testRole.Value || r.Name == testRole.Value) && !r.IsExtra);
        var extra = testModifier != null
            ? RoleRegistry.Roles.FirstOrDefault(r => (r.ConfigSection == testModifier.Value || r.Name == testModifier.Value) && r.IsExtra)
            : null;

        // Vanilla-Grundrolle passend zum Team der Testrolle setzen
        if (role == null)
        {
            // Keine eigene Testrolle -> der lokale Spieler darf NICHT im Impostor-Team hängen (sonst sieht man alle Impostor-Dummys rot "als hätte jeder eine Impostor-Rolle").
            if (local.Data.Role && local.Data.Role.IsImpostor) try { RoleManager.Instance.SetRole(local, RoleTypes.Crewmate); } catch { }
        }
        else
        {
            bool wantImpostor = role.Team == RoleTeam.Impostor;
            if (local.Data.Role.IsImpostor != wantImpostor)
            {
                RoleManager.Instance.SetRole(local, wantImpostor ? RoleTypes.Impostor : RoleTypes.Crewmate);
                // Impostor haben keine echten Aufgaben - die alten Crew-Aufgaben aus dem Practice entfernen
                if (wantImpostor) local.ClearTasks();
            }
            // Neutrale Rollen haben keine Aufgaben (außer sie behalten sie ausdrücklich)
            if (role.Team == RoleTeam.Neutral && !role.KeepsTasks) local.ClearTasks();
        }

        fakeImpostors.Clear(); // keine Fake-Impostor mehr - Dummys töten nur mit zugewiesener Impostor-Rolle

        RoleUI.Reset();
        var pairs = new List<(byte, byte)>();
        if (role != null)
        {
            pairs.Add((local.PlayerId, role.Id));

            // Rollen fuer zwei Spieler (Lover): ein normaler Dummy wird Partner
            for (int i = 1; i < role.PlayersPerCount; i++)
            {
                var partner = PlayerControl.AllPlayerControls.ToArray()
                    .FirstOrDefault(p => p && p.isDummy && !fakeImpostors.Contains(p.PlayerId) && pairs.All(x => x.Item1 != p.PlayerId));
                if (partner) pairs.Add((partner.PlayerId, role.Id));
            }
        }

        // Zusätzliche Anpassung/Geisterrolle für dich (neben der Hauptrolle)
        if (extra != null && pairs.All(x => x.Item1 != local.PlayerId || x.Item2 != extra.Id))
            pairs.Add((local.PlayerId, extra.Id));

        // Dummy-Rollen mit aufnehmen und die passende Vanilla-Grundrolle setzen
        foreach (var kv in dummyRoles.ToArray())
        {
            var d = CustomRole.FindPlayer(kv.Key);
            var dRole = RoleRegistry.Get(kv.Value);
            if (!d || !d.isDummy || dRole == null) { dummyRoles.Remove(kv.Key); continue; }
            bool wantImp = dRole.Team == RoleTeam.Impostor;
            if (d.Data != null && d.Data.Role && d.Data.Role.IsImpostor != wantImp)
            {
                RoleManager.Instance.SetRole(d, wantImp ? RoleTypes.Impostor : RoleTypes.Crewmate);
                if (wantImp) d.ClearTasks();
            }
            if (pairs.All(x => x.Item1 != d.PlayerId)) pairs.Add((d.PlayerId, dRole.Id));
        }
        RoleRegistry.SetAssignments(pairs, resetRoles: false); // abgelegte Dinge (z. B. Spinnennetze) beim Wechsel behalten
        ButtonManager.OnRolesAssigned(); // Knopf-Timer neu (im Freeplay 3s)

        Plugin.Logger.LogInfo($"Freeplay-Test: Rolle '{testRole.Value}', {fakeImpostors.Count} Dummy-Impostor(s)");
    }

    public static void ColorFakeImpostors()
    {
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p && p.cosmetics && fakeImpostors.Contains(p.PlayerId))
                p.cosmetics.nameText.color = Palette.ImpostorRed;
        }
    }
}

