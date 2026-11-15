# Testliste – Fixes-Runde 07.11. (b)  (lokal gebaut + installiert, **nicht veröffentlicht**)

Spiel **neu starten**, damit die neue DLL lädt. `[ ]` offen · `[x]` ok · `[!]` Fehler.

## ✅ Als „getestet" markiert
- [ ] **Kettentauscher** und **Lover** stehen jetzt auf *getestet* (erscheinen bei „nur Getestet", dürfen auch bei ungetestet-AUS rein).

## 🕷️ Spinne – Leiche löst sich wie Viper auf
- [ ] Nach dem Biss-Tod löst sich die Leiche jetzt mit der **echten Viper-Animation** auf (ich spawne die originale `ViperDeadBody` und entferne die normale Leiche). Danach erscheint wie bisher das **Leichen-Netz**.
- [ ] **Falls** die echte Animation auf einer Map mal nicht greift, blendet die Leiche ersatzweise sanft aus (Fallback) – bitte melden, falls du den Fallback siehst.

## ❄️ Wintergeist – Schneeflocke neben den Namen
- [ ] Das Schneeflocken-Symbol ist **nicht mehr riesig über dem Spieler**, sondern steht als kleines **❄ direkt neben dem Namen** des markierten Ziels (nur der Wintergeist sieht es) – wie bei anderen Rollen.
- [ ] Nach dem Einlösen/Meeting verschwindet die Markierung.

## ⛄ Schneeballkönig
- [ ] **Kein weißer Fleck** mehr, wo der Ball landet/runterfällt.
- [ ] **In Gängen werfbar**: Der Ball startet jetzt etwas vor dir und prallt nicht mehr sofort „an der eigenen Wand" ab.
- [ ] **Cooldown 40 s**.
- [ ] **Wurf ist langsamer** (Geschwindigkeit 11 → 7).

## 🚪 Türgeist – automatisch wieder auf
- [ ] Eine von dir zugehaltene Tür geht nach **7–10 Sekunden von selbst wieder auf** (ruhig, kein Flackern). Vorher per F erneut anklicken öffnet sie sofort. Meeting öffnet alle.

## 🧪 Freeplay – Rollenwechsel/Türgeist-Fehler
- [ ] **Wechsel zu einer normalen Rolle** ersetzt sauber – **keine alten Fähigkeiten** mehr übrig.
- [ ] **Geisterrolle (z. B. Türgeist) „Für dich"** ersetzt jetzt die Hauptrolle (du hast dann **nur** die Geisterrolle, keine alten Knöpfe). Wählst du danach wieder eine **normale Rolle**, verschwindet die Geister-Zusatzrolle automatisch → du bleibst **nicht mehr dauerhaft „Türgeist"**.
- [ ] Mit **„Anpassung aus"** lässt sich eine Zusatz-/Geisterrolle jederzeit entfernen.
- [ ] **Anpassungen** (Lover usw.) stapeln weiterhin absichtlich neben der Hauptrolle.

## 🏁 Wettkönig – Menü per Knopf
- [ ] Neuer **Wett-Knopf** (🎯) mit **15 s Cooldown**: öffnet das Wett-Menü.
- [ ] Im Menü siehst du **deine aktuelle Wette** (oben „aktuell: …" und der Eintrag ist mit ● markiert).
- [ ] Auswahl eines Spielers / „Niemand stirbt" setzt die Wette und **schließt** das Menü; **X** schließt ohne Änderung.
- [ ] Alle **15 s** kannst du den Knopf erneut drücken und die Wette **ändern**.
- [ ] Oben links steht deine aktuelle Wette + Trefferstand als Dauer-Hinweis.

## 🦠 Virus – Fähigkeiten zurück, wenn der Virus weg ist
- [ ] Wird der **Virus rausgevotet oder stirbt**, bekommen **alle Angesteckten ihre Fähigkeit zurück** – angesteckte **Impostor haben ihren Kill-Knopf wieder**.

## 🎬 Freeplay – Rollen-Intro
- [ ] Beim **Betreten eines Freeplay-Spiels** spielt jetzt **automatisch die Rollen-Enthüllung** ab (wie beim Online-Start), mit deiner Mod-Rolle.
- [ ] Neuer Knopf **„Rollen-Intro abspielen"** im Freeplay-Fenster, um die Animation jederzeit erneut zu sehen.
