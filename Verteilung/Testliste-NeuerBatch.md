# Testliste – Neuer Batch (Herbst 2025)

So testen: Im Spiel **F1** öffnen → Reiter **Sets** → oben der **Test-Umschalter**.
Ein Klick = alle **ungetesteten** Rollen an (Anzahl 1, Chance 100 %), der Rest aus. Nochmal klicken = alle **getesteten** an.
Also: Umschalter auf „ungetestete“ stellen, dann mit genug Dummys im Freeplay (F3) eine nach der anderen durchgehen und hier abhaken.

**Als bereits getestet gelten** alle Rollen, die es vor diesem Batch schon gab (Sheriff, Medic, Jester, Vampir, Hexe … und alle Geister/Anpassungen). Falls davon doch etwas unklar ist, sag Bescheid – dann nehme ich es wieder in die „ungetestet“-Liste.

Technisch im Code hinterlegt ist die Liste in `Core/TestStatus.cs` (dort nach dem Testen Namen rausnehmen).

---

## Crew

- [ ] **Heiler** – an eine Leiche stellen, **F**: Heiler stirbt sofort (ohne Leiche), nach 5 s steht der Tote an der Leichenstelle wieder auf. Meeting dazwischen bricht ab. Auch Impostor kann zurückkommen.
- [ ] **Tauscher** – **F** öffnet Menü, zwei Ziele wählen (einmal einen Spieler, einmal eine Leiche testen) → Positionen getauscht. Keine Geister im Menü.
- [ ] **Schildtechniker** – **F** legt sichtbare Zone; in der Zone 10 s kein Kill möglich (Dummy-Impostor testen). Cooldown 50 s.
- [ ] **Thermospezialist** – **F** auf Spieler in Reichweite → 5 s frost-immun; mit Frostbeißer gegentesten. Nur 2×/Runde (Zahl am Knopf).
- [ ] **Kaminbauer** – **F** stellt Kamin; wer in der Nähe steht ist frost-immun; Kamin erlischt nach 10 s. Mit Frostbeißer gegentesten.
- [ ] **Frostdetektor** – jemanden in der Nähe eine Fähigkeit nutzen lassen, dann **F**: Meldung „Fähigkeit benutzt“. Außer Reichweite/zu alt: „Nichts entdeckt“. Zeigt nicht Wer/Was.
- [ ] **Vertauscher** – **F** zwei Spieler markieren, dann Meeting: Stimmen für A zählen für B und umgekehrt. Danach Markierung weg.

## Anpassungen

- [ ] **Märtyrer** – als Erster sterben (durch Kill) → sofort Sieg. Vorher stirbt jemand anderes → kein Sieg. Rauswurf zählt nicht.
- [ ] **Leuchtkäfer** – alle 20 s leuchtet er 3 s; andere in der Nähe sehen das Leuchten (auch durch dünne Wand). Takt synchron bei allen.
- [ ] **Techniker** – **C** öffnet die Kameras von überall; auf Karte ohne Kameras Hinweis.

## Impostor

- [ ] **Sprinter** – **F** macht kurz schneller; danach Cooldown.
- [ ] **Werwolf** – **F** Rausch: schneller + tötet automatisch Nicht-Impostor in Reichweite in kurzen Abständen.
- [ ] **Selbstmordbomber** – **F**: alle im Radius sterben, er selbst auch (auch andere Impostor).
- [ ] **Assassine** – **F** Punkt setzen, **G** zurückspringen (SnapTo). Normaler Kill bleibt.
- [ ] **Tatortreiniger** – neben Leiche **F** → Leiche weg, nicht mehr meldbar.
- [ ] **Störsender** – **F**: Nicht-Impostor können Karte/Admin nicht öffnen; offene Karte schließt sich; Impostor sehen weiter alles.
- [ ] **Frostbeißer** – **F**: andere im Umkreis 8 s bewegungslos + Eis-Overlay; er selbst und Immune frei. CD 35.
- [ ] **Schneesturm-Bändiger** – **F**: 7 s weniger Sicht für alle im Bereich. CD 40.
- [ ] **Falscher Weihnachtsmann** – **F** Menü: erst Ziel, dann Vorbild → Ziel sieht 10 s aus wie Vorbild (inkl. Name), danach normal. Meeting bricht ab.
- [ ] **Lawinenleger** – **F** markiert Bereich, blinkt, nach 5 s Lawine → wer drin bleibt, stirbt.
- [ ] **Grinch** – **F** markiert Spieler mit Rolle; nach dem nächsten Meeting ist dessen Rolle weg (Knöpfe verschwinden).
- [ ] **Zauberer** – **F** Strahl in Blickrichtung (links/rechts je nach Figur); erster Spieler im Band stirbt; Wand stoppt.
- [ ] **Thor** – **F** wirft Hammer Richtung Maus; fliegt durch Wände über die Map; tötet Nicht-Impostor unterwegs; **F** erneut = zurückrufen; Meeting setzt zurück.
- [ ] **Sniper** – **F** zoomt raus; **G** erschießt nächsten Spieler auf Distanz (auch durch Wände); danach Zoom aus + Cooldown.
- [ ] **Leichenschlepper** – an Leiche **F** aufnehmen (folgt mit), woanders **F** ablegen. Andere sehen die Leiche mitwandern.
- [ ] **Clown** – **F**: Farben + Namen aller kurz vertauscht, danach wieder normal (auch nach Meeting).

## Neutral

- [ ] **Schneeballkönig** – **F** tötet nächsten Spieler in (größerer) Reichweite.
- [ ] **Wintergeist** – **F** markiert heimlich nächsten Spieler; stirbt ein Markierter → Schneeflocke; bei 3 Sieg.
- [ ] **Überträger** – **F** steckt nächsten an; nach 2 min tot; steckt in der Nähe weitere an (eigener Countdown).
- [ ] **Wettkönig** – kleines Fenster links: Wette wählen; am Rundenende Abrechnung; 2× richtig = Sieg.
- [ ] **Kettentauscher** – neben jemanden stellen, **F**; beim Meeting tauschen die Rollen; der neue Kettentauscher bekommt eine Meldung.
- [ ] **Seelensammler** – kann nicht killen; **F** sammelt Leiche (verschwindet); neben einem Impostor **G** füttern (zählt nur beim Impostor); genug Fütterungen = Sieg; kurze Info bei jedem Tod.

---

## Noch nicht gebaut (Idee steht, Technik offen)

Diese fünf fehlen noch – sie brauchen tiefere Eingriffe (Aufgaben-System, echte Dummys, natürliches Spielende). Sag, welche du als nächstes willst:

- [ ] **Tierfreund** (Crew) – Haustier bleibt stehen, wenn jemand stirbt; 1×/Runde; gibt ein Haustier, falls keins da.
- [ ] **Task-Geber** (Impostor) – gibt einem Crewmate per Knopf eine neue Aufgabe.
- [ ] **Dummy-Macher** (Crew) – stellt Dummys auf; Impostor müssen sie auch töten; solange sie leben, kann Impostor nicht gewinnen.
- [ ] **Überlebender** (Neutral) – gewinnt, wenn er am Rundenende noch lebt (mit Impostor am Ende: beide).
- [ ] **Schutzfee** (Neutral) – beschützt einen Spieler (5×), sieht dessen Rolle, gewinnt, wenn der Spieler gewinnt.
