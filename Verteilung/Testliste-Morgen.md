# Testliste für morgen

Stand: lokal gebaut + installiert, Stand nach **4.9.8** (plus Admin-Fix + neue Rolle Orakel, noch unveröffentlicht).
Legende: `[ ]` offen · `[x]` ok · `[!]` Fehler. Panels im Spiel: **F1** Einstellungen · **F2** Rollen-Info · **F4** Update-Notes.

---

## ✅ Alleine im Freeplay testbar (F1 → Freeplay)

### Freeplay-Testwerkzeuge (NEU, Panel F3)
- [ ] **Sabotage starten (auch als Crew)**: unten im F3-Panel 4 Knöpfe – **Licht, Reaktor, O2, Komm.** – lösen die jeweilige Sabotage sofort aus.
- [ ] **Dummy-Angriff auf Knopf**: in der Dummy-Liste rechts je Dummy ein **„Angriff"**-Knopf → der Dummy übt seine Rolle an dir aus: Impostor-Rolle/Fake-Impostor = **Kill** (über den echten Kill-Pfad, also Schild/Bodyguard/Medic greifen), **BOO** = Kleckse.
- [ ] **Angriff bei Nähe**: Schalter „Impostor-Dummies greifen bei Nähe an" → kommst du einem Impostor-Dummy zu nah, killt er dich (alle ~3 s). Gut zum Testen von Schilden (Süßigkeitenkind usw.).

### Kameramann (Rolle setzen)
- [ ] **Kamera setzen (F)**: echtes Kamera-Symbol erscheint an deiner Stelle, für alle sichtbar; nur **1 pro Runde** (nach Meeting wieder).
- [ ] **Symbol blinkt nur bei Nutzung**: ruhig, solange niemand zuschaut; **rot blinkend**, sobald jemand die Überwachung offen hat.
- [ ] **Überwachung (C)**: öffnet von überall **ein großes Bild** + große Pfeile **‹ ›**; unten „Kamera 1–4" bzw. „📹 Mobile Kamera".
- [ ] **Mobiler Feed**: die eigene Kamera ist beim Durchschalten dabei und zeigt **Figuren** (nicht nur den Raum), scharf.
- [ ] **5 Nutzungen/Runde**: Zahl am Knopf, **kein Cooldown**, **kein Zeitlimit**; bei 0 ist der Knopf aus. Zahl setzt sich zum Rundenstart zurück.
- [ ] **Normale Konsole**: geh an die echte Security-Konsole → auch dort **1 Bild + Pfeile** (weil ein Kameramann dabei ist).

### Teleporter
- [ ] **Button-Icon**: zeigt jetzt das **Teleporter-Symbol** (Pfeil ins Portal), nicht mehr die Klopapier-Grafik.
- [ ] **Karte + Teleport (F)**: Klick auf einen Raum → Landung auf dem Vent (O2/Comms Mitte, Storage unten), nie in der Wand.

### Gambler  (neue Fähigkeiten)
- [ ] **Automat**: Symbole auf den Walzen sichtbar; 2 gleich = 1x, 3 gleich = 2x.
- [ ] **Prüfer & Enttarnen sind raus.** Neue Auswahl: Schild, Sheriff-Schuss, Radar, Tempo, **Feuerwehrmann**, **Teleport**, **Trank**.
- [ ] **Feuerwehrmann** (C): behebt die **aktive Sabotage** sofort; ohne laufende Sabotage „Keine Sabotage aktiv" (Fähigkeit bleibt).
- [ ] **Teleport** (C): öffnet die **Karte**, Klick → Teleport auf den Vent/Raum (wie beim Teleporter); erst nach dem Teleport verbraucht. Schließen ohne Klick → Fähigkeit bleibt.
- [ ] **Trank** (C, jemand in der Nähe): löst bei ihm einen **zufälligen Trankbrauer-Effekt** aus (Schild des Süßigkeitenkinds schluckt ihn).

### Süßigkeitenkind (Rolle setzen)
- [ ] **3 Bonbons mehr** auf der Map als früher.
- [ ] **Keine Sabotage-Bonbons** mehr (bei Sabotage springt nichts mehr heraus).
- [ ] **1 Bonbon pro Aufgabe**: nach jeder erledigten Aufgabe erscheint 1 neuer Bonbon – **an sicherer Stelle, nicht in der Wand** (auch bei Licht-Sabotage prüfen).
- [ ] **Zuckerrausch**: genug gesammelt → schneller + Schild.

### Administrator
- [ ] **Farben am Admin-Tisch** (mit Dummys in Räumen) und **Admin-Ansicht von überall (F)**, ~10 s / 40 s Cooldown.

### Allgemein
- [ ] **Tote ohne Rolle**: über einem toten „normalen" Spieler steht „Crewmate"/„Impostor", **nicht „STRMISS"** (nur wenn Tote Rollen sehen dürfen).
- [ ] **Update-Notes (F4)**: Kameramann steht jetzt oben in „Neue Rollen".

---

## 🔎 Nur online testbar (mehrere Spieler)

### Orakel  (NEU, Neutral)
- [ ] **Raten im Meeting**: wie der Guesser erscheint im Meeting der „Raten"-Knopf; Rolle eines Spielers tippen.
- [ ] **Richtig = Ziel stirbt**, zählt als Treffer. **Falsch tötet dich NICHT** (nur Fehlschuss-Sound).
- [ ] **Sieg bei 3 Richtigen**: hat das Orakel 3 Spieler richtig geraten, gewinnt es allein.
- [ ] **5 Versuche gesamt, max. 3 pro Meeting**: so kann es in einem Meeting gewinnen oder braucht ein 2. Meeting.
- [ ] **Tod nur wenn uneinholbar**: sind so viele Versuche weg, dass 3 Treffer nicht mehr möglich sind (z. B. 3 Fehlschüsse), stirbt das Orakel.
- [ ] **Admin-Knopf** (Administrator): neues Monitor-Icon; Admin-Ansicht öffnet jetzt auch beim 1. Mal (ohne vorher die Karte zu öffnen).

### Bugfixes (NEU, bitte prüfen)
- [ ] **Guesser & Orakel – 2. Schuss**: nach einem Treffer/Kill kannst du im selben Meeting (bei „Mehrere Schüsse") bzw. weiter noch einmal schießen – das Meeting endet nicht mehr sofort, nur weil der Getötete noch nicht gewählt hatte.
- [ ] **Guesser & Orakel – Anzeige**: im Meeting steht oben mittig, wie viele **Schüsse/Versuche** du noch hast (Orakel zusätzlich „X Richtige zum Sieg").
- [ ] **Freeplay-Rollenwechsel**: beim Umschalten der Testrolle bleiben keine alten Buttons mehr hängen.
- [ ] **Vampir-Kill**: stirbt das Biss-Opfer, **fliegt niemand** mehr zum Kill – die Leiche bleibt beim Opfer (Schild fängt den Biss weiter ab).
- [ ] **Admin-Ansicht**: während die Admin-Übersicht (Knopf) offen ist, **läufst du nicht mehr automatisch weiter** (Bewegung gesperrt). *(Auch im Freeplay prüfbar.)*
- [ ] **Hexen-Symbol**: die Hexe sieht den Fluch auf der Map nur noch, wenn das Ziel **wirklich in Sicht** ist (nicht mehr durch Wände/ohne Vision).
- [ ] **Guesser killt Vote-Leader**: killt der (böse) Guesser im Meeting den, der schon alle Stimmen hat, werden **alle Stimmen zurückgesetzt und die Wahlzeit neu gegeben** → man kann sauber neu wählen.

### Bestehend online

### Impostor
- [ ] **Partner-Namen rot**: als Impostor siehst du die Namen deiner Mitimpostor (auch Mod-Impostor wie Vampir/Hexe und **Verräter**) wieder **rot** – auf der Map **und** im Meeting.

### Vampir
- [ ] **Kein Selbstmord-Effekt**: stirbt dein Biss-Opfer, kommt **nicht** die „du hast dich selbst getötet"-Animation; die Leiche liegt dort, wo das Opfer stand.
- [ ] **Schild fängt Biss ab**: beißt du ein **Süßigkeitenkind mit Schild**, stirbt es nicht – der Schild schluckt auch den verzögerten Biss.

### Süßigkeitenkind-Schild (jetzt universal)
- [ ] **Blockt JEDE Interaktion** und löst sich danach auf – nicht nur normale Kills:
  - [ ] jede **Impostor-Rolle** (Vampir-Biss, Hexe, Untoter, normaler Kill …),
  - [ ] **Sheriff-Schuss**, **Arsonist-Feuer**, **Vielfraß**, **Gambler**, **Spinne**, **Voodoo** usw.,
  - [ ] **auch harmlose** Interaktionen: **Trankbrauer** (der Trank-Effekt wird geschluckt, nichts passiert).
- [ ] Lautlos (das Kind merkt nur „etwas abgefangen"), lädt nach ~50 s wieder. 2. Interaktion vor Nachladen greift (gewollt).

### Kameramann (zu zweit/mehr)
- [ ] **Blinken sehen alle**: nutzt jemand die Überwachung, blinkt die Kamera für **alle** rot (networked).
- [ ] **Mobile Kamera für alle**: auch Mitspieler sehen an der normalen Konsole die mobile Kamera per Pfeil.

### Executioner
- [ ] Ziel **rausgevotet** → Executioner **gewinnt**.
- [ ] Ziel **anders gestorben** → bleibt **Executioner** (wird **nicht** mehr zum Jester).

---

**Gefundene Fehler hier notieren:**
1.
2.
3.
