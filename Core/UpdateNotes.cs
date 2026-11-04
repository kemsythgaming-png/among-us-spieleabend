using UnityEngine;

namespace Spieleabend.Core;

/// <summary>
/// Zeigt die Neuerungen im Spiel (Taste F4, öffnet sich einmal pro Sitzung in der Lobby von selbst).
/// Mit den Pfeilen ‹ › oben kann man zwischen den Versionen blättern (wie die Release-Liste auf GitHub).
/// Zwei Spalten (neue Rollen / Bugfixes), beide mit dem Mausrad scrollbar.
/// Bewusst nur mit GUI.Box + GUI.Label und gecachten Ui-Styles - keine im IL2CPP-Build wegoptimierten IMGUI-Methoden.
/// </summary>
internal static class UpdateNotes
{
    private const float W = 1040f, H = 820f;

    private sealed class Entry
    {
        public readonly string Ver;
        public readonly string[] Roles;
        public readonly string[] Fixes;
        public Entry(string ver, string[] roles, string[] fixes) { Ver = ver; Roles = roles; Fixes = fixes; }
    }

    // Neueste Version zuerst.
    private static readonly Entry[] History =
    {
        new Entry("4.9.17",
            new[]
            {
                Lang.T("<b>Keine neue Rolle</b>\nVorgefertigte Rollen-Sets zum schnellen Einstellen.",
                       "<b>No new role</b>\nBuilt-in role sets for quick setup."),
            },
            new[]
            {
                Lang.T("<b>Vorlagen-Sets:</b> im Reiter \"Spiel\" gibt es jetzt fertige Sets (Standard, Starke Crew, Halloween) – nur mit online erprobten Rollen. Ein Klick auf \"Aktivieren\" stellt die Rollen passend ein, der Rest geht aus. Halloween schaltet zusätzlich das Halloween-Design an.",
                       "<b>Template sets:</b> the \"Game\" tab now has ready-made sets (Standard, Strong Crew, Halloween) – only online-proven roles. One click on \"Activate\" sets the roles up and turns the rest off. Halloween also enables the Halloween theme."),
            }),

        new Entry("4.9.16",
            new[]
            {
                Lang.T("<b>Keine neue Rolle</b>\nUngetestete Rollen jetzt als einfacher An/Aus-Schalter.",
                       "<b>No new role</b>\nUntested roles are now a simple on/off switch."),
            },
            new[]
            {
                Lang.T("<b>Ungetestete Rollen: An/Aus.</b> Im Reiter \"Spiel\" (und \"Sets\") schaltest du ungetestete Rollen mit einem Knopf an oder aus. Ist der Schalter AUS, sind alle ungetesteten Rollen aus und lassen sich auch nicht einstellen (gesperrt, mit Schloss-Symbol). Standard: AUS.",
                       "<b>Untested roles: on/off.</b> In the \"Game\" (and \"Sets\") tab you switch untested roles on or off with one button. When OFF, all untested roles are off and cannot be configured (locked, with a lock icon). Default: OFF."),
            }),

        new Entry("4.9.15",
            new[]
            {
                Lang.T("<b>Keine neue Rolle</b>\nNetzwerk-Fix, bessere Einstellungs-Meldungen und ein neuer Standard: nur getestete Rollen an.",
                       "<b>No new role</b>\nNetwork fix, better settings messages and a new default: only tested roles on."),
            },
            new[]
            {
                Lang.T("<b>Verbindungsabbruch behoben:</b> die Einstellungs-Synchronisation wird in kleine Pakete aufgeteilt. Vorher konnte bei vielen aktiven Rollen ein zu großes Paket (>1500 Byte) vom Server verworfen werden - das führte zum Rauswurf (\"Reliable packet not ack'd\"). Alle Mitspieler brauchen dieses Update.",
                       "<b>Disconnect fixed:</b> the settings sync is split into small packets. Before, with many active roles a too-large packet (>1500 bytes) could be dropped by the server, causing a disconnect (\"Reliable packet not ack'd\"). All players need this update."),
                Lang.T("<b>Einstellungs-Meldungen:</b> Änderungen im Reiter \"Spiel\" (Kill-Abklingzeit, Tempo ...) werden jetzt wie Rollen-Änderungen unten links angezeigt. Die Rollen-Meldung zeigt nicht mehr \"STRMISS\".",
                       "<b>Settings messages:</b> changes in the \"Game\" tab (kill cooldown, speed ...) now show at the bottom left like role changes. The role message no longer shows \"STRMISS\"."),
                Lang.T("<b>Standard: nur getestete Rollen an.</b> Beim ersten Start sind nur geprüfte Rollen aktiv. Im Reiter \"Spiel\" schaltet der Test-Umschalter zwischen getesteten und ungetesteten Rollen um.",
                       "<b>Default: only tested roles on.</b> On first start only verified roles are active. In the \"Game\" tab the test toggle switches between tested and untested roles."),
            }),

        new Entry("4.9.14",
            new[]
            {
                Lang.T("<b>Orakel</b> (Neutral)\nRät im Meeting Rollen - wählt jetzt erst Crew/Impostor/Neutral, dann A-Z. Genug Treffer = Sieg.",
                       "<b>Oracle</b> (Neutral)\nGuesses roles in meetings - now pick Crew/Impostor/Neutral first, then A-Z. Enough hits = win."),
                Lang.T("<b>Mumie / Zombie / Spukgestalt</b>\nFeinschliff an Symbolen, Aufgaben und Sieg (Spukgestalt-Sieg jetzt auch im Freeplay sichtbar).",
                       "<b>Mummy / Zombie / Haunting</b>\nPolish on icons, tasks and winning (Haunting win now also shown in Freeplay)."),
            },
            new[]
            {
                Lang.T("<b>Rater-Fenster:</b> die Rollenliste bleibt immer im Bildschirm (Rand oben/unten) und ist bei vielen Rollen mit dem Mausrad scrollbar.",
                       "<b>Guesser window:</b> the role list always stays on screen (top/bottom margin) and scrolls with the mouse wheel when there are many roles."),
                Lang.T("<b>Mumie:</b> eigenes Symbol (Papierrolle) statt des Teleport-Pfeils.",
                       "<b>Mummy:</b> own icon (paper roll) instead of the teleport arrow."),
                Lang.T("<b>Viele Rollen-Feinschliffe</b> aus den Testrunden (Zauberer, Thor, Sniper, Werwolf, Falscher Weihnachtsmann, Vogelscheuche, Leichenschlepper u. v. m.).",
                       "<b>Many role tweaks</b> from the test rounds (Wizard, Thor, Sniper, Werewolf, False Santa, Scarecrow, Body Dragger and many more)."),
            }),

        new Entry("4.9.13",
            new[]
            {
                Lang.T("<b>Keine neue Rolle</b>\nMehr Komfort bei den Einstellungen und bei Geistern.",
                       "<b>No new role</b>\nMore convenience in the settings and for ghosts."),
            },
            new[]
            {
                Lang.T("<b>Rollen-Sets:</b> im neuen Reiter Sets kannst du benannte Profile speichern (z. B. Halloween) - Aktivieren schaltet nur diese Rollen an, alle anderen aus.",
                       "<b>Role sets:</b> in the new Sets tab you can save named profiles (e.g. Halloween) - Activate turns only those roles on and all others off."),
                Lang.T("<b>Team-weise setzen:</b> in jedem Rollen-Reiter (Crew, Impostor, Neutral, Anpassungen) kannst du oben Chance oder Anzahl nur für dieses Team auf einmal setzen - nicht mehr nur global.",
                       "<b>Set per team:</b> in each role tab (Crew, Impostor, Neutral, Modifiers) you can set chance or count for only that team at once at the top - no longer only globally."),
                Lang.T("<b>Geister:</b> ein rollenloser Geist, der ALLE seine Aufgaben erledigt hat, bekommt jetzt garantiert eine Geisterrolle.",
                       "<b>Ghosts:</b> a role-less ghost who has finished ALL their tasks now gets a ghost role guaranteed."),
            }),

        new Entry("4.9.12",
            new[]
            {
                Lang.T("<b>Keine neue Rolle</b>\nZwei neue, einzeln schaltbare Vents auf The Skeld.",
                       "<b>No new role</b>\nTwo new, individually toggleable vents on The Skeld."),
            },
            new[]
            {
                Lang.T("<b>Extra-Vents (Skeld):</b> im Reiter Spiel einzeln einschaltbar - Cafeteria (verbunden mit O2 und Upper Engine) und Communications (verbunden mit Shields und Admin).",
                       "<b>Extra vents (Skeld):</b> toggle each in the Game tab - Cafeteria (linked to O2 and Upper Engine) and Communications (linked to Shields and Admin)."),
            }),

        new Entry("4.9.11",
            new[]
            {
                Lang.T("<b>Keine neue Rolle</b>\nKleine Verbesserung an der Rollen-Info.",
                       "<b>No new role</b>\nSmall improvement to the role info."),
            },
            new[]
            {
                Lang.T("<b>Rollen-Info (F2):</b> im Reiter Aktuell aktiv kann man jetzt oben zwischen Rollen und Anpassungen umschalten - übersichtlicher, wenn viele Anpassungen aktiv sind.",
                       "<b>Role info (F2):</b> in the Active-now tab you can switch between roles and modifiers at the top - clearer when many modifiers are active."),
            }),

        new Entry("4.9.10",
            new[]
            {
                Lang.T("<b>Keine neue Rolle</b>\nDieses Update bringt vor allem Fehlerbehebungen und neue Freeplay-Testwerkzeuge.",
                       "<b>No new role</b>\nThis update is mostly bug fixes and new freeplay test tools."),
            },
            new[]
            {
                Lang.T("<b>Orakel/Guesser:</b> nach einem Kill im Meeting geht das Menü sofort weiter; unten stehen die möglichen Schüsse (beim Orakel plus Treffer x/3); die Rate-Liste zeigt nur aktive Rollen (ohne Anpassungen).",
                       "<b>Oracle/Guesser:</b> after a meeting kill the menu works right away; the available shots are shown at the bottom (Oracle also correct x/3); the guess list only shows active roles (no modifiers)."),
                Lang.T("<b>Böser Guesser:</b> kann im Freeplay die Fake-Impostor-Dummys anvisieren; Namen sind nur bei echten Mitimpostor rot.",
                       "<b>Evil Guesser:</b> can target the fake-impostor dummies in freeplay; names are only red for real fellow impostors."),
                Lang.T("<b>Vampir:</b> das Opfer stirbt an Ort und Stelle - niemand fliegt mehr zum Kill.",
                       "<b>Vampire:</b> the victim dies in place - nobody flies over for the kill anymore."),
                Lang.T("<b>Administrator:</b> während der Admin-Ansicht bleibst du stehen (keine Autobewegung).",
                       "<b>Administrator:</b> you stay put while the admin view is open (no auto-walking)."),
                Lang.T("<b>Hexe:</b> das Fluch-Symbol siehst du nur, wenn das Ziel wirklich in Sicht ist.",
                       "<b>Witch:</b> you only see the curse symbol when the target is actually in sight."),
                Lang.T("<b>BOO:</b> deutlich mehr Kleckse pro Druck.",
                       "<b>BOO:</b> many more blots per press."),
                Lang.T("<b>Freeplay:</b> Sabotage-Knöpfe (Licht/Reaktor/O2/Komm.), angreifende Dummys (Knopf + bei Nähe) und ein Knopf, der die Abklingzeiten zurücksetzt.",
                       "<b>Freeplay:</b> sabotage buttons (lights/reactor/O2/comms), attacking dummies (button + on proximity) and a button that resets cooldowns."),
            }),

        new Entry("4.9.9",
            new[]
            {
                Lang.T("<b>Orakel</b>  (Neutral)\nRät im Meeting Rollen. Rät er 3 Spieler richtig, gewinnt er allein (jeder richtig Geratene stirbt). 5 Versuche gesamt, max. 3 pro Meeting; ein falscher Tipp tötet ihn nicht - er stirbt erst, wenn 3 Treffer nicht mehr möglich sind.",
                       "<b>Oracle</b>  (Neutral)\nGuesses roles in meetings. Guess 3 players right to win alone (each correct guess dies). 5 attempts total, max 3 per meeting; a wrong guess doesn't kill him - he only dies once 3 hits are impossible."),
            },
            new[]
            {
                Lang.T("<b>Administrator:</b> der Knopf öffnet die Admin-Ansicht wieder (auch beim 1. Mal) und hat ein neues Monitor-Icon.",
                       "<b>Administrator:</b> the button opens the admin view again (even on first use) and has a new monitor icon."),
            }),

        new Entry("4.9.8",
            new[]
            {
                Lang.T("<b>Kameramann</b>  (Crew)\nSetzt (F) eine echte Kamera (deren Licht blinkt, wenn jemand zuschaut). Mit (C) öffnet er von überall die Überwachung - 1 großes Bild, mit Pfeilen durch alle Kameras (auch seine). Spielt er mit, zeigt auch die normale Konsole 1 Bild. 5x pro Runde.",
                       "<b>Cameraman</b>  (Crew)\nDrops (F) a real camera (its light blinks while someone is watching). Press (C) to open surveillance from anywhere - 1 big image, arrows through all cameras (incl. his). While he plays, the normal console also shows 1 image. 5x per round."),
                Lang.T("<b>Teleporter</b>  (Crew)\nÖffnet die Karte (F) und teleportiert sich per Klick an eine Stelle. Landet immer begehbar, nie in einer Wand.",
                       "<b>Teleporter</b>  (Crew)\nOpens the map (F) and teleports to a clicked spot. Always lands walkable, never in a wall."),
                Lang.T("<b>Gambler</b>  (Crew)\nSpielautomat: 3 Walzen. 2 gleiche = Fähigkeit 1x, 3 gleiche = 2x. Fähigkeiten: Schild, Sheriff-Schuss, Radar, Tempo, Feuerwehrmann, Teleport, Trank.",
                       "<b>Gambler</b>  (Crew)\nSlot machine: 3 reels. 2 matching = ability once, 3 matching = twice. Abilities: shield, sheriff shot, radar, speed, firefighter, teleport, potion."),
                Lang.T("<b>Hilfssheriff</b>  (Anpassung)\nErledigt ein Crewmate mit dieser Anpassung alle Aufgaben, wird er automatisch zum Sheriff.",
                       "<b>Deputy</b>  (Modifier)\nA crewmate with this modifier automatically becomes the Sheriff once all tasks are done."),
            },
            new[]
            {
                Lang.T("<b>Süßigkeiten-Kind:</b> der Schild fängt jetzt JEDE Interaktion ab (jeder Kill, auch Trankbrauer) und löst sich dann auf; lädt nach 50 s. Bonbons: 3 mehr auf der Map, nach jeder Aufgabe 1 neuer (keine Sabotage-Bonbons mehr).",
                       "<b>Candy Kid:</b> the shield now blocks ANY interaction (any kill, even the Potion Brewer) and then dissolves; recharges after 50 s. Candy: 3 more on the map, one per finished task (no more sabotage candy)."),
                Lang.T("<b>Executioner:</b> wird NICHT mehr zum Jester, wenn sein Ziel anders als per Rauswurf stirbt.",
                       "<b>Executioner:</b> no longer turns into a Jester when the target dies other than by being voted out."),
                Lang.T("<b>Impostor:</b> die Namen der Mitimpostor (auch Mod-Impostor/Verräter) sind wieder rot - auf der Map und im Meeting.",
                       "<b>Impostor:</b> teammates' names (incl. mod impostors/traitor) are red again - on the map and in meetings."),
                Lang.T("<b>Tote ohne Rolle:</b> es steht „Crewmate\"/„Impostor\" statt „STRMISS\".",
                       "<b>Dead without a role:</b> shows \"Crewmate\"/\"Impostor\" instead of \"STRMISS\"."),
                Lang.T("<b>Rollen-Info-Knopf:</b> kein doppeltes/überlappendes Icon mehr.",
                       "<b>Role info button:</b> no more doubled/overlapping icon."),
                Lang.T("<b>Bodyguard:</b> nur der Bodyguard stirbt, nicht der Angreifer. <b>Button Barry:</b> Knopf während Sabotage gesperrt.",
                       "<b>Bodyguard:</b> only the bodyguard dies, not the attacker. <b>Button Barry:</b> button locked during a sabotage."),
                Lang.T("<b>Allgemein:</b> Fähigkeits-Cooldowns laufen immer runter (auch in Aufgaben); nur Impostor + Neutrale übrig → Spiel endet erst, wenn eine Seite tot ist.",
                       "<b>General:</b> ability cooldowns always tick down (even during tasks); only impostors + neutrals left → the game ends once one side is dead."),
            }),
    };

    private static bool open;
    private static bool autoShown;
    private static int shown; // 0 = neueste
    private static GUIStyle bodyStyle, headStyle;
    private static float scrollLeft, scrollRight;

    public static void Toggle()
    {
        open = !open;
        if (open) { SettingsPanel.Close(); shown = 0; scrollLeft = scrollRight = 0f; }
    }

    public static void Close() => open = false;

    /// <summary>Einmal pro Sitzung in der Lobby von selbst zeigen, damit die Neuerungen auffallen.</summary>
    public static void MaybeAutoShow()
    {
        if (autoShown || !LobbyBehaviour.Instance || !PlayerControl.LocalPlayer) return;
        autoShown = true;
        open = true;
        shown = 0;
    }

    internal static void Draw()
    {
        MaybeAutoShow();
        if (!open) return;

        bodyStyle ??= new GUIStyle(Ui.Text) { wordWrap = true, fontSize = 22, richText = true };
        headStyle ??= new GUIStyle(Ui.Title) { fontSize = 26, alignment = TextAnchor.MiddleCenter };

        var win = new Rect((Ui.ScreenWidth - W) / 2f, (1080 - H) / 2f, W, H);
        GUI.Box(win, GUIContent.none, Ui.Panel);
        Ui.Block(win);

        // Titel mit Blättern zwischen den Versionen.
        float cx = win.x + W / 2f;
        var titleStyle = new GUIStyle(Ui.Title) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(cx - 220f, win.y + 16f, 440f, 46f),
            Lang.T($"Neu in Version {History[shown].Ver}", $"New in version {History[shown].Ver}"), titleStyle);

        if (shown < History.Length - 1 && Ui.Button(new Rect(cx - 280f, win.y + 15f, 50f, 48f), "‹")) { shown++; scrollLeft = scrollRight = 0f; } // älter
        if (shown > 0 && Ui.Button(new Rect(cx + 230f, win.y + 15f, 50f, 48f), "›")) { shown--; scrollLeft = scrollRight = 0f; }                 // neuer

        if (GUI.Button(new Rect(win.xMax - 74f, win.y + 15f, 50f, 50f), new GUIContent("X"), Ui.CloseButton)) { open = false; return; }

        float top = win.y + 80f, colH = H - 160f, gap = 20f;
        float colW = (W - 48f - gap) / 2f;
        var left = new Rect(win.x + 24f, top, colW, colH);
        var right = new Rect(left.xMax + gap, top, colW, colH);

        DrawColumn(left, $"<color=#5ACDB4>{Lang.T("Neue Rollen", "New roles")}</color>", History[shown].Roles, ref scrollLeft);
        DrawColumn(right, $"<color=#FFD24D>{Lang.T("Bugfixes", "Bugfixes")}</color>", History[shown].Fixes, ref scrollRight);

        if (Ui.Button(new Rect(win.x + (W - 240f) / 2f, win.yMax - 64f, 240f, 48f), Lang.T("Schließen (F4)", "Close (F4)"))) open = false;
    }

    /// <summary>Eine Spalte mit scrollbarer Liste (Mausrad). Zeigt nur ganz sichtbare Einträge - keine abgeschnittenen Zeilen.</summary>
    private static void DrawColumn(Rect box, string header, string[] entries, ref float scroll)
    {
        GUI.Box(box, GUIContent.none, Ui.Inner);
        GUI.Label(new Rect(box.x, box.y + 10f, box.width, 34f), header, headStyle);

        var view = new Rect(box.x + 20f, box.y + 54f, box.width - 40f, box.height - 66f);

        var heights = new float[entries.Length];
        float total = 0f;
        for (int i = 0; i < entries.Length; i++)
        {
            heights[i] = EntryHeight(entries[i], view.width) + 16f;
            total += heights[i];
        }

        scroll = Ui.Scroll(view, total, scroll);

        float y = view.y - scroll;
        for (int i = 0; i < entries.Length; i++)
        {
            if (y >= view.y - 0.5f && y + heights[i] <= view.yMax + 0.5f)
                GUI.Label(new Rect(view.x, y, view.width, heights[i] - 16f), entries[i], bodyStyle);
            y += heights[i];
        }
    }

    private static float EntryHeight(string text, float width)
    {
        try { return bodyStyle.CalcHeight(new GUIContent(text), width); }
        catch
        {
            int breaks = 1;
            foreach (var ch in text) if (ch == '\n') breaks++;
            breaks += text.Length / 46;
            return breaks * (bodyStyle.fontSize + 6f);
        }
    }
}
