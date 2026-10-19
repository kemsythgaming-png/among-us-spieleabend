# Testprotokoll – Sammelbau (neue Rollen, Tränke, Feuerwehrmann, Bugfixes)

Stand: lokal gebaut + installiert, **nicht veröffentlicht**. Version noch 4.8.11.
Am schnellsten im **Freeplay/Practice** testen (Rolle an dich geben, Dummys nutzen). Haken: [ ] offen · [x] ok · [!] Fehler (Notiz dahinter).

## Vorbereitung
- [ ] Among Us (Testkopie) gestartet, Freeplay auf **The Skeld** betreten.
- [ ] Mod-Fenster **F1** geht auf; Reiter **Allgemein** und **Spiel** sichtbar.
- [ ] Rolle per Practise-Knopf an dich geben, Dummy-Rolle per „an Dummy".

---

## Neue Rollen

### 🕷️ Spinne (Impostor)
- [ ] Knopf „Spinnennetz" legt ein Netz; Netz verschwindet nach ~15 s.
- [ ] Wer durchläuft, wird ~3 s langsam; Spinnen selbst nicht.
- [ ] Gelegentlicher Kill beim Durchlaufen (seltener Zufall).

### 🎃 Kürbisbomber (Impostor)
- [ ] Kürbis platzieren; Radius-Ring sichtbar; Zünder ~12 s, blinkt am Ende.
- [ ] Explosion killt im Radius; andere Bomber bleiben verschont.

### 🍺 Bierdalf (Anpassung)
- [ ] Knopf „Prost!" macht Ziel ~8 s betrunken (Steuerung verdreht).

### 🔴 Button Barry (Anpassung)
- [ ] Knopf „Notfall" löst von überall ein Meeting aus; Abklingzeit danach.

### 🌙 Heulgeist (Crew-Geist)
- [ ] Als toter Crewmate: einmaliges „Heulen" → Geräusch + Glühen für alle.

### 🔍 Spurensucher (Crew)
- [ ] Knopf aktiviert ~30 s Fußspuren; Spuren in Spielerfarbe, verblassen.

### 📋 Aufgabenprüfer (Crew)
- [ ] Knopf zeigt pro Spieler die Anzahl erledigter Aufgaben.

### 🧻 Mumie (Impostor)
- [ ] Ziel „einwickeln" → Ziel steht fest und kann nicht melden.

### 🚪 Türgeist (Crew-Geist)
- [ ] Als Geist: Knopf öffnet die nächste Tür.

### 🚒 Feuerwehrmann (Crew)
- [ ] Bei aktiver Sabotage Knopf „Löschen" behebt: Reaktor, O2, Komm **und Licht**.
- [ ] Licht: nach dem Knopf stehen die Schalter richtig / Sicht wieder normal.

### 🃏 Joker (Impostor)
- [ ] Zu Beginn und nach jedem Meeting wird er zu einer zufälligen Impostor-Rolle (Anzeige wechselt).

### 🍖 Vielfraß (Neutral)
- [ ] „Verschlucken" → Opfer unsichtbar/gefangen, folgt dem Bauch.
- [ ] Bei Meeting/Report sterben die Verschluckten.
- [ ] Stirbt der Vielfraß vorher, werden sie freigelassen.
- [ ] Als Letzter übrig → Vielfraß gewinnt.

### 🪆 Voodoo-Puppe (Impostor)
- [ ] „Besitzen" friert dich und das Ziel ~15 s ein; Ziel bekommt Hinweis.
- [ ] Zweiter Knopf killt einen Spieler nahe dem Ziel – Kill dem Ziel zugeschrieben.

---

## 🧪 Trankbrauer – 14 Tränke (Crew)
Mit „Zufallstrank" (F) auf einen Dummy; mehrfach auslösen, bis du alle gesehen hast. Jeder Effekt kurz.
- [ ] **Schnecke** – Ziel langsam
- [ ] **Zeitschleim** – Ziel eingefroren
- [ ] **Hexenbesen** – Ziel schnell
- [ ] **Kopfüber** – Steuerung gespiegelt
- [ ] **Kicher** – schräge Geräusche
- [ ] **Schrumpf** – Ziel kleiner
- [ ] **Riese** – Ziel größer
- [ ] **Nebel** – Ziel sieht wenig
- [ ] **Adlerauge** – Ziel sieht weiter
- [ ] **Geisteressenz** – Ziel durchsichtig
- [ ] **Camo** – Ziel grau/namenlos
- [ ] **Fledermaus** – Ziel fast blind + Echo
- [ ] **Zappeltrank** – Steuerung um 90° gedreht
- [ ] **Kompass-Chaos** – falscher Pfeil beim Ziel
- [ ] Nach einem Meeting sind alle Trank-Effekte weg.

---

## Bugfixes
- [ ] **Bananenschale** lädt genau an/hinter den Füßen (nicht in der Wand).
- [ ] **Mehrere Sieger** gleichzeitig (z. B. Jester + Executioner): Spiel endet **einmal**, alle Gewinner dabei.
- [ ] **Spukgestalt**: eigene Aufgaben zählen unabhängig von der Crew.
- [ ] **CandyKid-Schild** fängt den nächsten Kill ab.
- [ ] **Practise-Dummys** voten direkt dasselbe, sobald du abstimmst.

---

## 🔭 Map-Erweiterung (Test, optional)
F1 → **Spiel** → „Map-Erweiterung (Skeld, Test)" an (Test-Raum **aus** lassen).
- [ ] Nördlich der Cafeteria ist ein Raum; Tür begehbar und optisch offen.
- [ ] Vent im Raum funktioniert (mit Cafeteria-Vent verbunden).
- [ ] (Optik „Among Us" ist noch Baustelle – nur grob prüfen.)

---

## Allgemein
- [ ] Rollen-Infos erscheinen im Meeting oben; keine Buttons in der Taskliste.
- [ ] Einstellungsänderungen erscheinen unten links wie im Original.
- [ ] Keine Fehler-Spam im BepInEx-Log (`BepInEx/LogOutput.log`).

**Gefundene Fehler hier notieren:**
1.
2.
3.
