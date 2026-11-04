# Testprotokoll – nur im echten Spiel (Online)

Diese Punkte lassen sich **nicht im Practise** prüfen – sie brauchen echte Mitspieler, mehrere Runden oder eine echte Sieg-/Kill-Situation.
Stand: lokal gebaut + installiert, **noch nicht veröffentlicht**. Legende: `[ ]` offen · `[x]` ok · `[!]` Fehler.

> **Hinweis fürs Testen:** Am besten eine kleine Testrunde mit 2–3 Leuten, Rollen gezielt auf 100 % stellen und die restlichen Slots leer lassen.

## Kill-/Vent-Knöpfe & Rollen-Grundlagen
- [ ] **7 + 17. Kein Kill-/Vent-Knopf bei Crew/Neutral** – wenn jemand in der **Vorrunde** Impostor war und diese Runde Crew/Neutral ist, darf **kein** Kill- oder Vent-Knopf mehr da sein.
- [ ] **12. Neutrale haben keine Aufgaben** – Jester, Arsonist, Executioner usw. starten die Runde **ohne Aufgabenliste** oben links.
- [ ] **6. „Rolle wird nicht angezeigt"** – prüfen, ob die Rolle oben links / im Intro korrekt steht *(du wolltest mir noch sagen, wo genau sie fehlte)*.

## Kill-abhängige Rollen
- [ ] **2. Bodyguard** – greift ein echter Impostor den Schützling an, sterben **Bodyguard + Angreifer**, der Schützling lebt.
- [ ] **3. CandyKid-Schild** – der Schild fängt **einen** echten Kill ab (danach verbraucht).
- [ ] **13. Schutz des ersten Todes** – wirkt **ab Runde 2** und nur bei aktivierter Option: der erste Tod der Vorrunde ist diese Runde kurz geschützt.
- [ ] **8. Paranoid (Modifier)** – mit neuer **Reichweiten-Einstellung**: hört er einen Tod nur, wenn er in Reichweite ist (0 = ganze Map).

## Rauswurf-/Sieg-Rollen
- [ ] **9. Waybler nicht auf Jester/Executioner** – diese beiden bekommen den Waybler-Modifier **nicht** (kein doppelter Rauswurf-Gewinn).
- [ ] **18. Nur Neutral + Impostor übrig** – sind alle Crew tot und es leben nur noch **Impostor + Neutrale**, **gewinnen die Impostor sofort** (kein ewiges 1-gegen-1). *(NEU gebaut)*
- [ ] **28. Executioner-Ziel** – das Ziel steht oben links bei der Rolle. Im echten Spiel prüfen, ob dort **ein Name** steht (nicht leer). *(Fallback NEU gebaut)*

## Vielfraß / Spinne
- [ ] **14. Vielfraß** – ein **echter** verschluckter Mitspieler steht still und läuft nach dem Freilassen **nicht mehr nur nach rechts**.
- [ ] **25. Vielfraß** – nacheinander **zwei echte** Spieler schlucken funktioniert.

## Voodoo
- [ ] **16. Voodoo – echte Steuerung** – bei echten Mitspielern **technisch nicht möglich** (nur Sicht + Kill). Bitte bestätigen, dass Sicht/Kill zumindest funktionieren.
- [ ] **19. Voodoo – Impostor sichtbar** – nach dem Besitzen: sieht man als Voodoo die Impostor markiert?
- [ ] **29. Voodoo-Cooldown** – nach der **2. Übernahme**: ist der Knopf wieder normal nutzbar (nicht dauerhaft gesperrt)?

## Joker / BOO
- [ ] **23. BOO-Kleckse auf Joker** – wenn der Joker die BOO-Fähigkeit auslöst, sehen **die anderen** die Kleckse auf dem Bildschirm (der BOO/Joker selbst nicht). *(Im Practise unsichtbar, weil keine Mitspieler da sind, die die Kleckse sehen.)*

## Spukgestalt
- [ ] **4. Spukgestalt durch Wand** – wenn als Geist/Spukgestalt: kommt man durch Wände, wo man nicht soll? Bitte Map + Stelle notieren – Collider-Thema für den nächsten Batch.
- [ ] **11. Jester-Vent in Electrical** – bitte Situation beschreiben (gar nicht rein? nicht weiter? welche Map?).

---

**Gefundene Fehler hier notieren:**
1.
2.
3.
