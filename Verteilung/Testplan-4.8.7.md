# Testplan – Among Us Spieleabend 4.8.7

> Vorher: alle einmal über den **Launcher** neu starten, damit 4.8.7 geladen ist
> (unten links im Rollen-Info/Settings steht die Version bzw. im BepInEx-Log „… 4.8.7 geladen“).

## 🆕 Neue Rollen

### 🍌 Bananenschale (Modifier – Crew, Impostor oder Neutral)
- [ ] Knopf **„Bananenschale“** unten rechts ist da, Cooldown **25 s**.
- [ ] Drücken legt an deiner Stelle eine **gelbe Schale** ab, die **alle** sehen.
- [ ] Ein Mitspieler läuft drüber → er **rutscht aus** (ca. 2 s langsam), über seinem Kopf blitzt **„BANANA!“**.
- [ ] Man sieht **nicht**, wer die Schale gelegt hat.
- [ ] Legst du eine **3.** Schale, verschwindet die **älteste** (max 2 gleichzeitig).
- [ ] Unberührte Schalen verschwinden nach **15 s** von selbst.
- [ ] Funktioniert auch, wenn ein **Impostor** oder **Neutraler** die Bananenschale hat.

### 🟢 Waybler (Modifier)
- [ ] Wird **genau der Waybler** im **ersten** Rauswurf rausgevotet → er **gewinnt allein**, Spiel endet.
- [ ] Wird **jemand anderes zuerst** rausgevotet → der Waybler hat **keine Wirkung** mehr (spielt normal weiter).

### 🐹 Hamster (Modifier)
- [ ] Bei **aktiver Sabotage** (Licht, Reaktor, O2, Komm) wird der Hamster **deutlich schneller**.
- [ ] Nach **Behebung** der Sabotage wieder **normales Tempo**.

### 👻 BOO (Impostor)
- [ ] Knopf **„BOO!“** ist da, Cooldown 30 s.
- [ ] Drücken → bei **allen anderen** (nicht bei BOO selbst) erscheinen **3–6 dunkle Kleckse** auf dem Bildschirm.
- [ ] Die Kleckse **verblassen nach und nach** und sind im Meeting weg.
- [ ] Ein **zweiter BOO** sieht die Kleckse ebenfalls **nicht**.

## 🔧 Wichtige Fixes zum Gegenchecken

- [ ] **Haunt-Knopf**: normaler Geist hat ihn weiterhin (auch Guardian Angel / Spirit Guide).
- [ ] **Spukgestalt / Wächtergeist / Survivor / Untoter** haben **keinen** Haunt-Knopf.
- [ ] **Survivor** bekommt beim (ersten) Tod **keine** „Spirit Guide“-Fähigkeit; nach dem Wiederbeleben normaler Crewmate.
- [ ] **Verräter + Survivor**: im Meeting, in dem der Survivor stirbt, wird **nicht** „Verräter“ angezeigt.
- [ ] **Spukgestalt**: wird sie erlöst (angeklickt), verliert sie ihre Aufgaben; Crew-Aufgabenbalken bleibt sauber.
- [ ] **Spukgestalt** läuft **nicht** durch Wände; erscheint nach dem Meeting an **ihrer alten Stelle**.
- [ ] **Paranoid** bekommt bei **jedem** Kill eine Warnung (egal wie weit weg).
- [ ] **Tarnung (Camouflager)**: während alle grau sind, sieht man **keine** Schutz-Blase.
- [ ] **Evil Guesser**: Ratеliste zeigt nur **aktive** Rollen (Anzahl *und* Chance > 0).
- [ ] **Neuer Lobby-Beitritt**: sieht nach ein paar Sekunden die bereits **aktiven Rollen**.
- [ ] **Einstellung ändern** (z. B. Vampir-Chance): erscheint bei **allen** im **Chat** („Vampir: 1x, 70 %“).
- [ ] **Zombie** frisst eine Leiche → das **Pet** der Leiche verschwindet mit.
- [ ] **Mod-Knöpfe** (Rollen-Info / Einstellungen) sitzen **oben rechts**; während einer **Aufgabe** sind sie weg.
- [ ] **Kamera-Wackeln** ist in neuer Lobby standardmäßig **aus**.

---
*Kurz notieren, was nicht passt – dann fixe ich es gezielt.*
