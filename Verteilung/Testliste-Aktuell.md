# Testliste – aktueller Build (unveröffentlicht)

Alles lokal gebaut + installiert, Version bleibt **4.9.2**, **noch nicht veröffentlicht**.
Legende: `[ ]` offen · `[x]` ok · `[!]` Fehler. Neu: **Update-Notes im Spiel mit Taste F4** (öffnen sich beim ersten Lobby-Besuch einer Sitzung auch von selbst).

---

## 🩹 Fixes & Änderungen (NEU – zuletzt geändert)

- [ ] **Untoter:** als Auferstandener (untot) kann er **kein Meeting mehr** einberufen (kein Notfallknopf, keine Leiche melden) – gilt ja als tot.
- [ ] **Untoter – Seelen sammeln:** über der Leiche schwebt eine **animierte Seelen-Kugel** (pulsiert, wächst mit dem Fortschritt). **Jede Leiche gibt genau 1 Seele und löst sich danach auf** (aufsteigende Seele) – man braucht also **2 verschiedene Leichen** zum Gewinnen. Standard „Benötigte Leichen" = 2 (einstellbar).
- [ ] **Schneeballkönig:** komplett **entfernt**.
- [ ] **Mayor:** Stimmen-Anzeige oben links geht nach einem **Skip** jetzt hoch (auch im Freeplay).
- [ ] **Schneesturm-Bändiger:** im Freeplay per Dummy auslösbar (beim Dummy „Fähigkeit" → Sturm an seiner Position, du bekommst den Schnee).
- [ ] **Freeplay – Dummys folgen deinem Vote:** Dummys stimmen jetzt zuverlässig genauso ab wie du (Stimme wird notfalls direkt gesetzt).
- [x] **Astronaut** im Freeplay getestet – ok.
- [x] **Aufgabenprüfer** im Freeplay getestet – ok.

---

## ❄️ Survivor-Tarnung & Schneesturm (zuvor)

- [ ] **Survivor:** sieht in der Wartephase jetzt **durchgehend alles getarnt** (grau, ohne Namen) – vorher fielen vorbeilaufende Spieler nach kurzer Zeit wieder in Normalansicht (Tarnung wird jetzt jeden Frame neu aufgetragen).
- [ ] **Schneesturm-Bändiger:** **mehr Schnee** (dichteres Gestöber + häufiger große Flocken).
- [ ] **Schneesturm-Bändiger:** **bei sich selbst kein Schnee** und **keine Sichtminderung** – er sieht normal, nur alle anderen im Sturm.
- [x] **Tank** im Freeplay getestet – ok.

---

## 🎲 Zufalls-Optionen beim Start (zuvor)

Alle drei stehen im Reiter **Spiel**, werden vom **Host** vergeben und gelten **nur für die Runde** (Account-Name/-Aussehen kommt beim Verlassen der Lobby zurück). Nicht im Freeplay.

- [ ] **Zufällige Namen** (Dropdown): **Aus / Vulgär / Pronomen / Tiere / Essen**. Jeder kriegt beim Start einen zufälligen Namen aus dem gewählten Pool (möglichst eindeutig; bei zu vielen Spielern Zahl dahinter).
  - [ ] Vulgär: derbe Spaß-Namen
  - [ ] Pronomen (Ich/Du/Er …): Meeting-Verwirrung
  - [ ] Tiere / Essen
- [ ] **Zufällige Farben** (AN/AUS): jeder bekommt eine zufällige Spielerfarbe (möglichst verschieden).
- [ ] **Zufällige Dekoration** (AN/AUS): zufälliger Hut/Skin/Visier/Pet pro Spieler.
- [ ] Alle sehen bei den anderen **dasselbe** (Host verteilt deterministisch, kein Desync).
- Kombinierbar (z. B. Pronomen-Namen + Zufallsfarben + Zufallsdeko gleichzeitig).
- [ ] **Freeplay (F3):** eigene Reihe **„Zufalls-Looks testen"** mit 4 Buttons – wirkt sofort auf dich + Dummys, **unabhängig vom F1-Fenster**:
  - [ ] **Namen** – schaltet pro Klick durch die Pools (Vulgär → Pronomen → Tiere → Essen), Button zeigt den nächsten Pool.
  - [ ] **Farben** – würfelt allen eine Farbe.
  - [ ] **Deko** – würfelt Hut/Skin/Visier/Pet.
  - [ ] **Alles** – Namen + Farben + Deko auf einmal.
- [ ] **Intro-Trigger:** „Rollen-Intro abspielen" (F3) wendet die im **F1-Fenster** eingestellten Looks an (wie online beim Spielstart).
- [ ] **F3 rechte Seite ist jetzt scrollbar** – alle Steuer-Knöpfe sind erreichbar (vorher unten abgeschnitten).
- [ ] **Nach dem Spiel wieder normal:** Namen/Farben/Deko werden beim Spielende auf das Original zurückgesetzt.
- [ ] **Statistik-Screen:** zeigt die **echten Namen** (nicht die Zufallsnamen) und bei jedem die **Kill-Anzahl**.

---

## 🧪 Map-Erweiterung v3 (zuvor)

- [ ] **Position:** Raum sitzt jetzt **bündig an der Cafeteria** (an der echten aufgetrennten Türkante verankert, schwebt nicht mehr).
- [ ] **Durchgang:** man läuft sauber rein/raus und **verschwindet nicht mehr hinter der Wand** (Naht hinter dem Spieler).
- [ ] **Fenster weg:** die gemalte Cafeteria-Wand/Fensterscheibe im Durchgang wird jetzt **ausgeblendet** (vorher stand die Scheibe wieder im Gang).
- [ ] **Echte Aufgabe im Raum:** „Align Engine Output"-Konsole funktioniert dort (wer die Aufgabe hat, kann sie im neuen Raum erledigen).
- Cross-Map-Deko (Polus-Teleskop) geht nicht – nur Skeld-Objekte sind auf Skeld geladen.
- → Screenshot schicken: sitzt der Raum richtig + ist die Konsole nutzbar?

---

## 🧪 Map-Erweiterung v2 (zuvor)

**Erweiterter Raum (The Skeld)** komplett neu gebaut – sauberer:
- Eigener **Panel-Boden** (Kacheln) + heller **Rahmen** + **Sternen-Fenster** oben.
- **Breite 4er-Öffnung** direkt an der Cafeteria statt engem Gang → leicht begehbar.
- Echter **Vent** (mit nächstem Cafeteria-Vent verbunden), Deko-Props.
- Einschalten: **Spiel → „Map-Erweiterung" AN**, Map **The Skeld**.
- [ ] Sieht ordentlich aus (Boden/Rahmen/Fenster, keine dunklen Lücken/überlappende Wände).
- [ ] Man läuft sauber rein/raus (keine unsichtbare Wand im Durchgang, man bleibt nicht stecken).
- [ ] Spieler verschwindet NICHT im Durchgang (Naht-Abdeckung ok).
- [ ] Vent funktioniert (Impostor rein/raus).
- → Bitte Screenshot, dann justiere ich Größe/Türbreite/Position.

---

## 🧪 Rollen-Feinschliff 14 (zuvor)

**Getestet:** Deaktivator, Frostbeißer, Wettkönig, Chamäleon, Geheimagent.
**Voodoo-Dauer:** fest **10 s**. **Schneesturm:** noch **mehr Flocken**. **Aufgabenprüfer:** Symbol wieder da.
**Mayor:** aktuelle **Stimmenzahl steht oben links** im Aufgabenbereich.
**Tank:** Info erscheint erst **im Voting-Fenster** (nicht während Intro/Report-Animation); Beschreibung neu/klarer.
**Survivor:** sieht bis zur Wiederbelebung **alle grau + ohne Namen/Rollen** (auch Dummys); nach dem Aufstehen wieder normal.
**Astronaut:** Asteroiden-Aufgabe bleibt vorher aus der Liste und kommt erst nach allen anderen (unverändert, bitte gegentesten).
**Freeplay-Draft:** im Freeplay-Panel (F3) neuer Knopf **„Draft starten"** – du wählst selbst, die Dummys bekommen nach Ablauf zufällig eine ihrer 3 Rollen. (Manuelles Draften je Dummy ginge nur mit mehr Aufwand – sag Bescheid.)

---

## 🧪 Rollen-Feinschliff 13 (zuvor)

**Entfernt:** Thermospezialist, Kaminbauer, Vampirjäger.
**Astronaut:** Asteroiden-Aufgabe wird vorher **aus der Liste entfernt** und kommt erst, wenn **alle anderen** Tasks fertig sind.
**Wettkönig – Doppelkill:** zählt nur bei **genau 2** Toten bis zum Meeting (bei 3 nicht). (FirstTasks/Rausgevotet lassen sich im Freeplay schwer testen – Dummys stimmen/erledigen nicht wie Spieler; Treffer-Zähler links beachten.)
**Survivor:** sieht bis zur Wiederbelebung **keine Namen** (unsichtbar) + alles grau (keine Farben).
**Tank:** Meeting-Info jetzt **mittig** auf dem Bildschirm in **Crew-Blau**.
**Mayor:** **keine Maximal-Stimmen** mehr (sammelt unbegrenzt).
**Aufgabenprüfer:** **kein Cooldown** mehr; genau **1× bis zum nächsten Meeting** (kein Ansammeln). Erklärung angepasst.
**Bodyguard:** Schild **bricht nicht** mehr – blockt **alles**, was mit dem Schützling interagiert (Kills, Guesser, Spinnenbiss, Trankbrauer …), blitzt **jedes Mal** blau; hält **bis zum nächsten Meeting**, danach neuen Schützling wählbar.

---

## 🧪 Rollen-Feinschliff 12 (zuvor)

**Dummy-Spawner entfernt.**
**Frostbeißer**: friert jetzt **alle im Raum ein – auch Dummys** (Freeplay testbar).
**Deaktivator**: wählt man denselben Raum (nicht erlaubt), gibt es die **Abklingzeit zurück** → sofort einen anderen Raum wählbar.
**Schneesturm-Bändiger**: **mehr Flocken**, jede fällt **unterschiedlich** (Tempo/Pendeln variiert, auch beim Nachrücken); ab und zu bleibt eine **große Flocke** hängen und **löst sich langsam auf**.
**Geheimagent**: wird im **Impostor-Intro** als Teammitglied gelistet (3 Impostor; im Freeplay 2, wenn ein Dummy die Rolle hat und du das Intro per Knopf startest).
**Voodoo** (zur Info): letzte Runde zurückgesetzt – voller Durchblick + Körper bleibt stehen, Sichtradius ~Raum+Gänge (4.5); diese Runde **nicht** angefasst.

---

## 🧪 Rollen-Feinschliff 11 (zuvor)

**Entfernt:** Störsender, Lawinenleger, Seelensammler (raus aus dem Spiel).
**Frostbeißer (umgebaut):** F → **Karte** öffnen (dabei **stehen bleiben**), **Raum anklicken** → **alle Spieler im Raum** frieren **15-20 s** ein (bewegungslos + Eis-Overlay); im Raum liegt solange ein **Frost-Symbol auf dem Boden** (für alle sichtbar). Frostbeißer + Immune bleiben frei.
**NEU: Geheimagent (Crew, ungetestet):** ist ein normaler Crewmate mit Aufgaben, wird den **Impostoren aber als 3. Impostor (roter Name)** angezeigt. Er selbst sieht **nicht**, wer die echten Impostor sind → keiner weiß, wer der echte Partner ist. Gewinnt mit der Crew.

---

## 🧪 Rollen-Feinschliff 10 (jetzt mit installiert)

**Mörser & Virus** stehen jetzt auf **getestet**.
**Astronaut** (umbenannt von Kometenschnüffler): die **Asteroiden-Aufgabe** kommt erst, wenn **alle anderen** Aufgaben erledigt sind (dann 200 schießen).
**VoodooPuppe**: Sichtradius bei Übernahme jetzt **~Raum + Gänge** (Standard 4.5) – fokussiert auf den Raum, Gänge sichtbar.
**Deaktivator**: beim Öffnen der Karte muss man jetzt **stehen bleiben** (kein Weiterlaufen); man kann **nicht zweimal denselben Raum** sperren (muss wechseln).
**Dummy-Spawner**: Figur wieder **zuverlässig sichtbar** (einfache Körperform in korrekter Spielerfarbe), **dreht sich** in Laufrichtung. (Komplettes Kostüm-Kopieren war unsichtbar/fehleranfällig und ist bewusst raus.)

> Wettkönig: bei gezielten Wetten (z. B. „wer ruft das nächste Meeting") nach der Art-Wahl **noch einen Spieler im zweiten Menü anklicken** – erst dann ist die Wette gesetzt. Sag, falls eine gesetzte Wette nicht abgerechnet wird.

---

## 🧪 Rollen-Feinschliff 9 (zuvor)

**Wettkönig**: Das **Dauer-„Ausgewählt"-Geräusch** beim Überfahren der Spieler im Auswahl-Menü ist weg – solange das Menü offen ist, werden neue Sounds unterdrückt (laufende Musik bleibt).

---

## 🧪 Rollen-Feinschliff 8 (zuvor)

**VoodooPuppe**: Sichtradius bei Übernahme **halbiert** (Standard jetzt 2 statt 4).
**Deaktivator**: klickt man einen Raum an, werden jetzt **alle Crew-Aufgaben-Konsolen des Raums rot + gesperrt** (auch Cafeteria); **Sabotage/Licht** ausgenommen (reparierbar), **Vents** sind kein Console und werden nie rot/gesperrt.
**Dummy-Spawner**: **Farbe korrekt** (richtige Color-API), Deko **zappelt nicht mehr** (Animationen angehalten), Figur **dreht sich in Laufrichtung um** (Spiegeln).

---

## 🧪 Rollen-Feinschliff 7 (zuvor)

**Mörser**: beim Öffnen der Karte wird die **Bewegung sofort gestoppt** (man läuft nicht mehr mit dem letzten Input weiter).
**VoodooPuppe**: Wand-Sicht-Änderung **rückgängig** – wieder voller Durchblick + Körper bleibt stehen (man sieht also über Wände). Option „eigenen Körper töten" bleibt.
**Deaktivator**: sperrt/färbt jetzt **nur echte Crew-Aufgaben-Konsolen** (Sabotage-Typen wie **Licht/Reaktor/O2/Komm.** ausgeschlossen) → **Lights bleibt reparierbar**, Vents/Lights **nicht rot**, für alle Impostor nutzbar.
**Virus**: Cooldown jetzt wirklich **100 s** (neuer Options-Schlüssel, alter zu niedriger Wert wird verworfen; bis 120 einstellbar).

---

## 🧪 Rollen-Feinschliff 6 (zuvor)

**Spinne** steht jetzt auf **getestet**.
**Dummy-Spawner**: **Farbe** jetzt korrekt (Material-Farbe nach Klonen neu gesetzt), **Spielergröße** (echte lossyScale), prallt jetzt auch an **Tischen/ShortObjects** ab (läuft nicht mehr durch Café-Tische).
**Mörser**: farbige Explosion jetzt nur **minimal größer als der Kill-Kreis** (≈ Radius).
**VoodooPuppe**: neue Optionen – **„Wände blocken die Sicht"** (an = normale Sicht, Körper reist mit, keine Durchsicht durch Wände) und **„Kann den eigenen Körper töten"** (an). Standard: beides an.
**Deaktivator**: **Lights/Sabotage weiterhin reparierbar**, Vents/alles für Impostor nutzbar – es werden wirklich nur echte Aufgaben-Konsolen gesperrt/rot.
**Virus**: Abklingzeit Standard **100 s** (bis 120 einstellbar).

---

## 🧪 Wettkönig-Ausbau (zuvor)

Der Wett-Knopf zeigt jetzt **2 zufällige Wetten zur Auswahl** (mittiges Menü). Eine klicken:
- Braucht sie einen Spieler → Spieler-Menü; sonst wird sie **sofort** gesetzt.
- **Erneuter Knopfdruck (alle 15 s)** → wieder 2 neue Wetten, die **alte zählt nicht mehr**.

Wett-Arten (triff die Vorhersage bis zum nächsten Meeting = Punkt):
- [ ] **Wer stirbt als Nächstes?** (Spieler)
- [ ] **Wer wird rausgevotet?** (Spieler)
- [ ] **Wer findet die nächste Leiche?** (Spieler, = wer meldet)
- [ ] **Wer ruft das nächste Meeting?** (Spieler, Report oder Notfall)
- [ ] **Wer ist zuerst mit allen Aufgaben fertig?** (Spieler)
- [ ] **Doppelkill** – mind. 2 Tote bis zum Meeting (kein Spieler)
- [ ] **Skip/Gleichstand** – nächstes Meeting wirft niemanden raus (kein Spieler)
- [ ] Titel über dem Menü zeigt die gewählte Wett-Art; Bestätigung unten links.

---

## 🧪 Rollen-Feinschliff 5 (zuvor)

**Bodyguard**: schützt wieder – fängt **EINEN** Angriff auf den Schützling ab (Schützling überlebt), blitzt blau, danach verbraucht.
**Mörser**: Abschuss (Hochfliegen) ist jetzt **unsichtbar**; am Ziel **taucht** die Granate von oben auf und **explodiert groß & hell** (immer sichtbar, kein Fog mehr auf der Explosion). Radius-Standard 2.0.
**Deaktivator**: gesperrte Aufgaben-Konsolen werden jetzt **rot abgedunkelt** (sichtbar, dass sie aus sind). Lights/Sabotage/Vents weiterhin frei.
**VoodooPuppe**: wieder **voller Durchblick + eigener Körper bleibt stehen**, aber nun auf einen **Sichtradius** ums Ziel begrenzt (einstellbar, Standard 4). Kein Kreiseln mehr.
**Dummy-Spawner**: Figur ist jetzt eine **echte Cosmetics-Kopie** (volles Design/Klamotten + Name) in **Spielergröße** mit **echter Lauf-Animation** (RunAnim). Hinter Wänden unsichtbar, nah immer sichtbar.
**Freeplay-Testdummys**: Angreifer-Dummys töten den nächsten – auch andere Dummys (Angriffsknopf / auto beim Laufen).

---

## 🧪 Rollen-Feinschliff 4 (zuvor)

**Getestet (Freeplay) → jetzt „getestet"**: Loner, Ausbilder, Spurensucher.

**Tank**: Meeting-Info erscheint jetzt **unten links** (die oberen Banner waren abgeschaltet – daher fehlte sie). Nur du, nur wenn du lebst und ein Angriff abgewehrt wurde.
**Chamäleon**: während unsichtbar ist der **Knopf/Timer weg**; **Haustier** verschwindet jetzt mit.
**Dummy-Spawner**: Figur jetzt **spielergroß** (auf Höhe normiert), mit **echter Spielerfarbe/Design** (PlayerMaterial), **Lauf-/Watschel-Animation**, Name drüber.
**Mörser**: **viel langsamer**; Granate steigt an **deiner** Stelle hoch und **taucht am Ziel von oben** wieder auf (fliegt nicht rüber); du **musst stehen bleiben** ab Karten-Öffnung bis zum Einschlag.
**Spinne**: Leiche löst sich wieder **wie bei der Viper** auf, jetzt in **Spielerfarbe**; Leichen-Netz ist **voll zufällig groß** (kein Wand-Clamp mehr).
**VoodooPuppe**: beim Steuern hast du jetzt **normale Impostor-Sicht am Ziel** (Körper reist mit) – nicht mehr dunkel.
**Freeplay-Testdummys**: ein Angreifer-Dummy (Dummy mit Impostor-Rolle) killt jetzt den **nächsten in der Nähe – auch andere Dummys**, nicht nur dich. Per Dummy-**Angriffsknopf** oder automatisch mit „Dummys greifen an" + „Dummys laufen" (so töten sie sich beim Umherlaufen). Tote Dummys mit „Dummys wiederbeleben" zurückholen.

---

## 🧪 Rollen-Feinschliff 3 (zuvor)

**Rollen-Farbe oben links**: Impostor-Rollen jetzt immer **rot**, Crew-Rollen immer **türkis**, Neutral behält die eigene Farbe.
**Deaktivator**: komplett neu – blockiert nur noch die **Aufgaben** im angeklickten Raum (über Console.CanUse). **Sabotage, Lights/Komm. und Vents bleiben** jetzt wirklich nutzbar, es wird nichts mehr entfernt.
**Dummy-Spawner**: spawnt wieder (Body-Sprite statt Klon). Figur hat jetzt **Namen** drüber, **Spielerfarbe/-design**, **Spielergröße** (kleiner) und bleibt hinter Wänden unsichtbar; Auswahl per **Menü**.
**Mörser**: andere sehen den Mörser **nur mit Sichtlinie** (im eigenen Raum). Der Mörser **kann sich jetzt selbst töten** (im Umkreis).
**Spinne**: gebissene Leiche löst sich jetzt in **Spielerfarbe** auf; Netz danach **zufällig groß (1.0–3.1)**; aus einem **Flur** siehst du jetzt auch die Netze **in angrenzenden Räumen** (nur echte Wände blocken).
**VoodooPuppe**: Dauer **10 s**; beim Steuern **normale Impostor-Sicht** (nicht mehr ganze Map); im übernommenen Körper **kann man wieder töten**.
**Chamäleon**: siehe Feinschliff 2 (3 s Fade, bleibt unsichtbar beim Stehen).
**Virus**: als **Geist** nicht mehr anstecken.
**Ausbilder**: neues Symbol (Doktorhut); ein **Dummy** kann die Aufgabe auch **an dich** geben (Freeplay).
**Tank**: **kein Ton, keine Stack-Anzeige**; Info nur im **Meeting** (nur wenn du lebst und ein Angriff abgewehrt wurde – Meeting nötig).

---

## 🧪 Rollen-Feinschliff 2 (zuvor)

**Kometenschnüffler**: nach allen Aufgaben nur **eine** Asteroiden-Aufgabe in der Liste (nicht mehr 10 Stück); zählt weiter bis **200**.
**Mörser**: Geräusch **nur wenn wirklich jemand stirbt**; Granate fliegt **hoch und wieder runter**; Einschlag mit **mehr Licht/Explosion** (Feuerball + weißer Kern).
**Deaktivator**: blockiert jetzt **nur normale Aufgaben-Konsolen** – **Lights, Sabotage und Vents bleiben nutzbar**.
**Dummy-Spawner**: **Menü** zum Spieler-Auswählen; Figur hat **Name + komplettes Design** des Spielers, ist **spielergroß** und **hinter Wänden unsichtbar** (Fog).
**Wettkönig**: Menü-**Geräusch weg**; oben steht **„Als Nächstes stirbt:"** bzw. **„Als Nächstes rausgevotet:"**; pro Knopfdruck zufällig **eine der 2 Wetten** (stirbt / rausgevotet), die **zuletzt geklickte zählt**.
**Spinne**: Spinnennetze sind **hinter Wänden nicht mehr sichtbar**; Netz-Größen weiter zufällig.
**Chamäleon**: wird über **3 s unsichtbar**; **bleibt unsichtbar beim Stillstehen** (kein Zeitlimit); **Cooldown läuft erst, wenn du wieder sichtbar** bist (nach Bewegung).
**VoodooPuppe**: im übernommenen Körper kann man **wieder töten** (Knopf hing an der eigenen Reichweite – behoben).
**Ausbilder**: neues Symbol (Klemmbrett +).
**Virus**: Knopf + Namens-Symbol jetzt **grüner Erreger** (Infekt-Look).

> Wett-Ideen für später: „nobody stirbt diese Runde" (Überleben), „wer findet die nächste Leiche", „wer ruft das nächste Meeting", „wer ist zuerst mit allen Aufgaben fertig".

---

## 🧪 Rollen-Feinschliff (NEU in diesem Build)

**Spinne**
- [ ] Netz aus der Leiche weiter zufällig groß; **Auflösungszeit** jetzt Standard **10 s**.
- [ ] Gesetztes Netz hält jetzt Standard **30 s**, **25 %** Chance, jemanden zu töten.

**Mörser** (Impostor)
- [ ] Eigenes Symbol (Explosions-Stern). F → **Karte geht auf, du stehst still**, Stelle anklicken.
- [ ] Granate fliegt jetzt **von dir aus im Bogen** zum Ziel (nicht mehr sofort am Ziel), Einschlag tötet im Umkreis. Rechtsklick bricht ab.

**Dummy-Spawner** (Impostor)
- [ ] Eigenes Symbol (Figur). Läufer ist jetzt eine **echte Among-Us-Figur** (Körper+Farbe).
- [ ] Stehst du **bei einem Spieler**, sieht der Läufer wie DIESER aus und startet an seiner Stelle; sonst wie **du**. Läuft, prallt ab, tötet Nicht-Impostor bei Kontakt.

**Deaktivator** (Impostor)
- [ ] Eigenes Symbol (An/Aus). F → **Karte**, Raum anklicken → dessen **Aufgaben**-Konsolen 60 s aus.
- [ ] **Sabotagen** werden NICHT geblockt; **Vents** bleiben nutzbar.

**Chamäleon** (Crew)
- [ ] Eigenes Symbol (durchgestrichenes Auge). F → jetzt **wirklich unsichtbar** für dich UND die anderen (Phantom-Alpha). Bewegung macht wieder sichtbar; Leiche sichtbar.

**Tank** (Crew)
- [ ] Info „Angriff abgewehrt" kommt jetzt **nur im Meeting** und **nur für ihn** (mit Anzahl), nicht mehr live.

**Spurensucher** (Crew)
- [ ] Eigenes Symbol (Fußspuren). Spuren liegen jetzt **direkt unter der Figur** (mittig).
- [ ] Du siehst jetzt **auch deine eigenen** Spuren. Bei **Tarnung (Camouflager)** sind die Spuren **grau**. Von Rollen **gespawnte Figuren** (z. B. Dummy-Spawner) hinterlassen ebenfalls Spuren.

**Kometenschnüffler** (Neutral)
- [ ] Nach allen Aufgaben kommt in **Weapons die echte Asteroiden-Aufgabe**; insgesamt **200** statt 20 (Aufgabe kommt mehrfach). Gibt es die Aufgabe auf der Map nicht → Knopf-Fallback.

---

## 🆕 Draft-Modus (NEU in diesem Build) — nur ECHTES Spiel, mehrere Spieler

Einstellen im F1-Fenster → Reiter **Spiel**:
- **Draft-Modus (Rolle selbst wählen)** auf **An**.
- **Draft: Impostor-Anzahl** — muss zur **AU-Impostor-Einstellung** in der Lobby passen (z. B. beides 2).
- **Draft: Neutrale-Anzahl** — so viele Crewmates werden zu Neutralen; **der Rest wird Crew** (es gibt keine extra Crew-Zahl, Crew = alle Übrigen).
- **Draft: Bedenkzeit** — Countdown (Standard 20 s).
- Es werden **nur getestete Rollen** vorgeschlagen.

Zu testen:
- [ ] **Ablauf**: Runde starten → das normale Intro läuft (zeigt noch Crewmate/Impostor) → danach legt sich das **Auswahl-Fenster** über den Bildschirm, **Bewegung ist gesperrt**.
- [ ] **3 Karten**: jeder sieht **3 zufällige Rollen seines Teams** (Name + Kurztext + Beschreibung), klickt **Wählen** → Karte wird grün, „Gewählt – warte auf die anderen".
- [ ] **Linke Liste zeigt nur Team**: bei den anderen Spielern sieht man **nur** Crew/Impostor/Neutral (Punkt + Teamfarbe) und einen **Haken**, wenn sie gewählt haben — **nicht** die konkrete Rolle.
- [ ] **Countdown / Auto-Wahl**: wählt jemand nicht rechtzeitig, bekommt er nach Ablauf **automatisch eine seiner 3** Rollen; haben alle gewählt, geht es sofort weiter.
- [ ] **Verteilung stimmt**: am Ende hat jeder die **selbst gewählte** Rolle; Anzahl Impostor/Neutral/Crew wie eingestellt; Neutrale haben **keine Aufgaben**.
- [ ] **Draft aus = alles normal**: mit Draft-Modus **Aus** läuft die Runde wie bisher (Zufallsverteilung).

---

## ✅ Im Freeplay (Übungsmodus) testbar

Setze die Testrolle über F1 → Freeplay, dann:

### Heute geändert: Orakel-Kategorie-Menü + Rater-Liste scrollbar + Mumie-Symbol
- [ ] **Orakel – Kategorie-Menü**: im Meeting einen Spieler zum Raten wählen → zuerst kommt ein kleines Fenster **Crew / Impostor / Neutral** (nur vorhandene Kategorien). Nach der Wahl werden **nur** die Rollen dieser Kategorie gezeigt, **von A–Z sortiert**. Unten gibt es „◀ Kategorie" zum **Zurück/Wechseln** des Menüs. „Abbrechen" und „X" schließen ganz.
- [ ] **Orakel – Liste mit Rand + Scrollbar**: die Rollenliste ist nie höher als der Bildschirm (Rand oben/unten). Zu viele Rollen → mit dem **Mausrad** scrollen, oben/unten steht „▲ mehr" / „▼ mehr".
- [ ] **Guter & Böser Rater – Scrollen gleich**: dieselbe Rand-/Scroll-Korrektur (gemeinsame Basis). Das **Kategorie-Menü gibt es nur beim Orakel** – Guter/Böser Rater zeigen wie bisher die flache Liste.
- [ ] **Mumie – neues Symbol**: der Einwickeln-Knopf zeigt jetzt eine **Papierrolle** (Klopapier mit heraushängendem Blatt) statt des Teleport-Pfeils.

### Großer Rollen-Batch (NEU in diesem Build)
- [x] **Paranoid**: jetzt mit Knopf (Ausrufezeichen-Icon, F) - einschalten, ~10-15 s aktiv, 45 s Cooldown. Solange aktiv und IRGENDWO auf der Map jemand getötet wird, blitzt dein Bildschirm kurz hellblau. *(Tötung online/mit Dummy.)*
- [x] **Vertauscher**: Swap-Symbol (zwei Pfeile) auf den Karten, Klick-Ton. Zwei Karten wählen → Tausch (Karten gleiten animiert + Stimmen) passiert erst, wenn die **Abstimmung vorbei** ist.
- [x] **Seer**: nach dem Prüfen wird der Name grün (gut) / rot (böse) - nur für dich, auch im Meeting. Nur 1× pro Runde; nach jedem Meeting wieder 1 Versuch und alte Färbung weg.
- [ ] **Aufgabenprüfer**: ToDo-Brett-Icon, nur 1× pro Runde. Bei Impostor zählt jetzt **Türen schließen** mit (nicht mehr dauerhaft 0). *(Impostor-Teil online.)*
- [x] **Kürbiskopf**: andere sehen dich NICHT mehr als schwarze Silhouette - du siehst normal aus. Unverwundbarkeit + Kürbis-Sicht bleiben.
- [x] **Detektor** (war Frostdetektor): Scanner-Icon. Setz mit F einen Bereich (Ring nur für dich). Wird dort eine Fähigkeit benutzt, bekommst du **im nächsten Meeting eine Chat-Info**, welche Fähigkeit(en) - nicht wer. *(Zone online mit anderen testbar.)*
- [ ] **Zauberer**: neues Zauberstab-Icon. F drücken → Ziel-Linie → irgendwo hinklicken → Strahl fliegt in die Klickrichtung. Rechtsklick bricht ab.
- [ ] **Thor**: Hammer-Icon. F → Ziel-Linie → klicken → Hammer fliegt in die Klickrichtung. Zweiter Druck ruft zurück.
- [ ] **Leichenschlepper**: neues Schlepp-Icon. Aufgenommene Leiche zieht jetzt **hinter dir her** (nachlaufend), nicht mehr auf dir.
- [x] **Heiler (Dummy-Rolle)**: einem Dummy eine Rolle geben → sie steht jetzt im Freeplay über dem Dummy (auch tot), damit man sie prüfen kann.
- [ ] **Freeplay-Meeting**: läuft eine Sabotage, kommt jetzt der Hinweis „erst Sabotage beheben" statt dass nichts passiert.

### Rollen-Batch 7 (NEU) – die 6 offenen Umbauten
- [ ] **Clown**: vertauscht jetzt **Farbe + Name zusammen** - jeder sieht aus wie ein anderer echter Spieler (konsistent statt zufällig).
- [ ] **Falscher Weihnachtsmann**: die **Auswahl des 2. Spielers** geht jetzt (Menü bleibt offen, befüllt neu).
- [ ] **Störsender**: sperrt während der Störung **Karte (Tab), Admin UND Kameras**. Im Freeplay wirkt es auch auf dich selbst (zum Testen).
- [ ] **Sniper**: F → **15 s rauszoomen, stillstehen**; mit der **Maus auf einen Spieler klicken** = Kill (durch Wände). Danach Cooldown.
- [ ] **Wettkönig**: Wett-Fenster jetzt im **Mod-/Among-Us-Stil** (Panel + Knöpfe).
- [ ] **Schneeballkönig**: wirft jetzt wie der Zauberer - **F, dann Richtung klicken**, fliegende Kugel tötet den ersten Getroffenen.

### Rollen-Batch 6 (NEU)
- [ ] **BOO**: startet mit 0 Stacks, lädt alle 30 s einen auf (bis 3). Radial zeigt die Aufladung, Zahl die Stacks.
- [ ] **Kopfgeldjäger**: Ziel steht jetzt im **Aufgaben-Text oben links** (nicht mehr als eigenes GUI).
- [ ] **Vogelscheuche**: nur noch **1 Kopie** möglich.
- [ ] **Leichenschlepper**: Leiche noch näher, direkt am Fuß.
- [ ] **Betrunken**: zusätzlich ein **Dreh-Modus** (rechts = oben) in der Rotation.
- [ ] **Thor**: Wurf-/Vorbeiflug-Zischen, **Kill-Sound** für den Werfer, Hammer tötet jetzt **auch Impostor** (nur nicht den Werfer).
- [ ] **Zauberer**: **kein Ziel-Pfeil** mehr (einfach klicken), Strahl in **zufälligen Farben**. Andere sehen den Strahl (networked).
- [ ] **Wintergeist**: nur **1 Markierung** gleichzeitig, Cooldown 30 s.
- [ ] **Kettentauscher**: neues **Ketten-Symbol**, klarerer Aufgaben-Text.
- [ ] **Werwolf**: Rausch **5 s**, tötet **alles** in der Nähe; roter Rausch-Ring + Countdown, Heul-Sound, normaler Kill wird beim Rausch mitgesperrt.
- [ ] **Techniker/Überwachung**: Abklingzeit startet jetzt **nach dem Schließen**; Restzeit der Überwachung wird oben angezeigt.

### Rollen-Batch 5 (NEU)
- [ ] **Thor**: Wurf → Hammer fliegt quer über die Map und schwebt am Ende. Zweiter Klick ruft zurück; auf dem **Rückweg tötet er auch**. Während er unterwegs ist, ist der Knopf gesperrt UND der normale Kill (gemeinsamer Cooldown). Ist er wieder da: **30 s** Abklingzeit.
- [ ] **Vogelscheuche**: zweiter Knopf **Platztausch** (Taste G, 35 s) - tauscht deinen Platz mit deiner nächsten Kopie (Kopie kommt an deine alte Stelle).
- [ ] **Leichenschlepper**: Leiche jetzt **direkt am Fuß** (auch um Ecken), nicht mehr nachlaufend. Nach dem **Ablegen 20 s** Abklingzeit.
- [ ] **Kopfgeldjäger**: dein Ziel steht jetzt **oben links** am Bildschirm (nur du), auch im **Freeplay** bekommst du ein Ziel.
- [ ] **Assassine**: „Punkt setzen" hat jetzt **45 s** Cooldown.
- [ ] **Tatortreiniger**: Cooldown **30 s**, teilt sich die Abklingzeit mit dem Kill-Knopf (Kill + Wegputzen).
- [ ] **Sprinter**: Knopf ist jetzt ein **An/Aus-Schalter** (kein Zeitlimit).

### Rollen-Batch 4 (NEU)
- [ ] **Zauberer**: schießt jetzt **durch Wände**; Strahl reicht quer über die Map (Reichweite Standard 40). Erster Spieler auf der Linie stirbt - egal ob Wand dazwischen.
- [ ] **Tatortreiniger**: neues Symbol (Grabschaufel) statt Feuerlöscher.

### Rollen-Batch 3 (NEU)
- [ ] **BOO**: sammelt jetzt bis zu **3 Stacks** an (Zahl am Knopf), lädt alle ~15 s einen nach. Kaum Sperre → schnelles Doppel-/Dreifach-Klicken möglich.
- [ ] **Sprinter**: neues Symbol (laufender Fuß mit Tempo-Strichen) statt Blitz.
- [ ] **Thor**: der Hammer **dreht sich** jetzt im Flug. Die Ziel-Anzeige ist ein **Pfeil zum Mauszeiger** statt eines Balkens (gilt auch für den Zauberer).

### Rollen-Batch 2 (NEU)
- [ ] **Bodyguard** (umgebaut): Schützling 1× pro Runde wählen, opfert sich NICHT mehr. Greift/zielt jemand (Crew/Impostor/Neutral) auf den Schützling - auch ohne Kill, z. B. **Trankbrauer** - blitzt dein Bild blau.
- [ ] **Techniker/Überwachung**: Symbol jetzt wie beim Kameramann (Konsole). Cooldown 45 s, Überwachung schließt nach 15 s automatisch (einstellbar).
- [ ] **Betrunken**: jetzt links/rechts UND oben/unten verdreht; wechselt alle 13-18 s zwischen verdreht und normal, mal nur eine Achse. **Bierdalf** macht ebenfalls beide Achsen verdreht.
- [ ] **Köder**: funktioniert jetzt im Freeplay (Dummy-Killer meldet die Leiche). Köder-Rolle setzen, von einem Dummy töten lassen → Dummy meldet automatisch.
- [ ] **Assassine**: „Punkt setzen" hat ein neues Fähnchen-Symbol und keine Start-Abklingzeit mehr. *(Falls du vorher 40 s gesehen hast: der Punkt-setzen-Cooldown ist im Code nur ~1 s - sag mir, welcher Knopf 40 s zeigte.)*

### Nachbesserungen (NEU)
- [ ] **Mayor**: startet mit 1 Stimme. Wählt er in einem Meeting NICHT (Skip/gar nicht), hat er im nächsten eine Stimme mehr (3. Meeting = 3). Wählt er dann einen Spieler, zählt sie mehrfach und fällt danach auf 1 zurück. Obergrenze = Option „Max-Stimmen" (Standard 5). *(Mehrere Meetings - online/mit Dummys testen.)*
- [ ] **Zauberer**: nach dem Treffer wirst du NICHT mehr zum Opfer teleportiert; Strahl macht jetzt ein „Zoom"-Zischen. Schilde fangen den Treffer weiter ab.
- [ ] **Leichenschlepper**: Leiche zieht jetzt ruhig hinterher (kein Rumspringen beim Rechts-/Runterlaufen); beim Ziehen bist du 0,2 langsamer.
- [ ] **Freeplay „Fähigkeit"-Knopf**: pro Dummy neuer Knopf „Fähigkeit" (neben Angriff/Töten). Bisher verdrahtet: **Zauberer** und **Thor** (Dummy feuert auf dich). Weitere Rollen kommen nach und nach - bei nicht verdrahteten Rollen kommt ein Hinweis.

### Heiler + Freeplay-Testknopf (NEU in diesem Build)
- [x] **Knopf „Aufgaben erledigen"**: im Freeplay-Panel (rechte Spalte, direkt unter „Rollen leeren") → ein Klick schließt **sofort alle eigenen Aufgaben** ab (Sabotagen zählen nicht). Chat meldet „alle Aufgaben erledigt".
- [x] **Heiler „Opfern" nach allen Aufgaben**: Testrolle „Heiler" setzen → der Opfern-Knopf ist **grau**, solange noch Aufgaben offen sind. „Aufgaben erledigen" drücken → an einer Dummy-Leiche stehen → Opfern (F) geht jetzt, Heiler stirbt, der Tote steht nach ein paar Sekunden wieder auf (ohne Spukgestalt-Rolle).

### Gambler  (Rolle „Gambler", im Practise sofort 15 Coins)
- [ ] **Symbole sichtbar**: beim Drehen zeigen die 3 Walzen jetzt die **Icons** der Fähigkeiten (waren vorher leer – Style-Hintergrund statt `GUI.DrawTexture`).
- [ ] **Automat-Fenster + Animation**: F drücken → mittig erscheint ein Spielautomat, 3 Walzen blättern durch die Symbole und halten nacheinander an.
- [ ] **Kosten + Cooldown**: ein Dreh kostet **3 Coins** und hat danach eine **Abklingzeit** (Standard 20 s, einstellbar).
- [ ] **Treffer-Logik**: unten steht das Ergebnis –
  - **2 gleiche Symbole** → Fähigkeit **1x** (Zahl „1" am Nutzen-Knopf),
  - **3 gleiche Symbole** → **2x** (grün „(2x)", Zahl „2" am Knopf),
  - **alles verschieden** → „Pech – nichts".
- [ ] **Nutzen-Knopf**: zeigt das **Bild** der gewonnenen Fähigkeit (nicht nur Text), Taste C nutzt sie; bei 2x bleibt sie für einen 2. Einsatz.

### Spukgestalt  (Rolle „Spukgestalt", dann im Laptop sterben)
- [ ] **Keine Wände mehr durchlaufen**: überall gegen Wände laufen – man muss **überall** hängen bleiben wie ein Crewmate. *(Layer „Players" + Collider im Physik-Schritt.)*
- [ ] **Spawn auf zufälligem Vent**: beim Sterben **und nach jedem Meeting** erscheint sie auf einem zufälligen Vent – nie in einer Wand.
- [ ] **Name versteckt**: ein lebender Mitspieler sieht nur die schwache Figur, **keinen Namen** darüber (nur online testbar). Im Meeting ist der Name normal da.
- [ ] **In Meetings als tot**: zählt als Geist (kein Abstimmen/Gewählt-werden) – gewollt.
- [ ] **Aufgaben + Sieg**: beim Werden neue Aufgaben; sind alle erledigt, gewinnt sie allein.
- [ ] **NEU – Freeplay-Sieg-Popup**: Spukgestalt setzen → im Laptop sterben → „Aufgaben erledigen" drücken → es erscheint jetzt ein **großes Sieg-Popup in der Bildmitte** („Spukgestalt gewinnt!"), plus Abschluss-Sound. (Vorher kam nichts, weil die oberen Hinweis-Banner abgeschaltet sind.) Im Freeplay läuft das Spiel danach weiter.

### Türgeist  (Rolle „Türgeist", als Geist an einer Tür)
- [ ] **Nur die eine Tür**: direkt in einer Tür stehen, F → **nur diese** Tür geht zu, nicht mehr alle Türen des Raums.
- [ ] **Bleibt zu**: die geschlossene Tür bleibt zu und **flackert nicht** mehr (kein „geht auf und sofort wieder zu").
- [ ] **Wieder aufmachen geht**: an derselben Tür nochmal F → sie geht **auf und bleibt offen** (kein erneutes Zuschnappen, auch nicht „2× nach dem Timer").
- [ ] **Meeting**: zugehaltene Türen gehen zum Meeting wieder auf.

### Jester  (Rolle „Jester")
- [ ] **Alle Vents nutzbar**: an **jedem** Vent Taste V – auch Ecken-Vents (Electrical auf der Skeld).

### Arsonist  (Rolle „Arsonist")
- [ ] **Benzin-Sound**: F (Übergießen) → weiches Zischen, **kein** Vent-Geräusch.

### Voodoo-Puppe  (Rolle „Voodoo-Puppe")
- [ ] **Cooldown läuft durch**: Dummy übernehmen → der Übernehmen-Knopf bleibt sichtbar und zählt **durchgehend** von 35 s runter (auch während der Übernahme), danach wieder nutzbar. Nicht mehr eingefroren.
- [ ] **NEU – du bleibst da**: Während der Übernahme bleibt **dein eigener Körper sichtbar an seinem Platz stehen** (nicht mehr unsichtbar/weg). Nur die **Kamera** schwenkt auf das Ziel – du siehst durch seine Augen und steuerst es (Dummy/Host zuverlässig), es kann nur zusehen.
- [ ] **NEU – du bist angreifbar**: Weil dein Körper stehen bleibt, kann dich in dieser Zeit jemand an deinem Standort **töten/melden**. Dein Name bleibt normal weiß, die anderen Namen sind crew-blau.
- [ ] **Kamera zurück**: Nach Ablauf schwenkt die Kamera sauber auf deinen Körper zurück.

### Spinne  (Rolle „Spinne")
- [ ] **Biss tötet nach 8 s**: Spinnenbiss auf ein Opfer → es wird über **8 Sekunden** immer langsamer bis zum Stillstand, **dann stirbt es** (vorher 30 s). Spinnen selbst sind immun.
- [ ] **Leiche löst sich auf → Netz**: Die vergiftete Leiche **verblasst/löst sich auf** (wie bei der Viper); an der Stelle bleibt danach ein **Spinnennetz** liegen (verlangsamt, 10 % tödlich).
- [ ] **Auch bei der Viper**: Tötet eine **Viper** jemanden (Leiche löst sich auf), bleibt an der Stelle ebenfalls ein Spinnennetz zurück.
- [ ] **Netz unverändert**: Netz-Knopf legt weiter ein zufällig großes Netz (3 s langsam, 10 % tödlich, 15 s).

### Hilfssheriff  (NEU, Anpassung)
- [ ] **Sheriff nach allen Aufgaben**: einem Crewmate die Anpassung „Hilfssheriff" geben (Einstellungen) → erledigt er alle Aufgaben, wird er **garantiert** zum Sheriff (Kill-Button). *(Echtes Spiel – im Freeplay schwer zu testen.)*

### Kameramann  (NEU, Rolle „Kameramann")
- [ ] **Kamera setzen (F)**: an deiner Stelle erscheint ein **echtes Kamera-Symbol** (für alle sichtbar), **eine pro Runde** – nach einem Meeting wieder setzbar, die alte ist dann weg.
- [ ] **Blinkt nur bei Nutzung**: das Symbol blinkt **nur rot, wenn gerade jemand die Überwachung offen hat** (echte Konsole networked, plus der Kameramann selbst) – sonst ruhig.
- [ ] **Auch in der NORMALEN Konsole**: sobald ein **Kameramann mitspielt**, zeigt die echte Security-Konsole für **jeden** nur **1 großes Bild mit Pfeilen** (statt 4er-Raster) – schon mit den 4 Standard-Kameras, auch bevor eine mobile gesetzt ist. Ist eine mobile Kamera gesetzt, ist sie als weiterer Feed dabei.
- [ ] **Überwachung (C)**: der Kameramann öffnet die Überwachung **von überall**; ein großes Bild, mit den großen **Pfeilen ‹ ›** durch alle Kameras blättern. Unten steht „Kamera 1–4" bzw. „📹 Mobile Kamera".
- [ ] **Mobiler Feed mit Spielern**: die Umschaltung erreicht auch die mobile Kamera – ihr Bild wird vom **echten Kamera-Prefab** des Spiels gerendert, daher sind **Figuren sichtbar** (nicht nur der Raum) und schärfer (512er). Startet direkt auf der mobilen Kamera.
- [ ] **5 Nutzungen pro Runde, kein Cooldown**: am Knopf steht die Zahl der **verbleibenden Nutzungen** (Standard 5 pro ganzer Runde, einstellbar). **Keine Abklingzeit** und **kein Zeitlimit** – jede Überwachung darf so lange laufen, wie man will. Ist die Zahl 0, ist der Knopf aus. Beim Zuschauen stehst du still.

### Bugfix-Batch (NEU, bitte prüfen)
- [ ] **Teleporter-Icon**: der Teleportieren-Knopf zeigt jetzt ein echtes Teleporter-Symbol (Pfeil ins Portal) statt der Klopapier-Grafik.
- [ ] **Gambler „Enttarnen"**: zeigt beim nächsten Spieler nur noch **gut** (grün) oder **böse** (rot) – nicht mehr die genaue Rolle.
- [ ] **Süßigkeitenkind-Bonbons**: 3 Bonbons mehr auf der Map; **keine Sabotage-Bonbons** mehr; nach **jeder erledigten Aufgabe erscheint 1 neuer Bonbon** (an sicherer Stelle, nicht in der Wand). *(Teils online, Aufgaben nur im echten Spiel.)*
- [ ] **Tote ohne Rolle**: über einem toten Spieler ohne Sonderrolle steht jetzt „Crewmate"/„Impostor" statt „STRMISS". *(Nur sichtbar, wenn Tote Rollen sehen.)*
- [ ] *(Online)* **Impostor-Partner rot**: als Impostor siehst du die Namen deiner Mitimpostor (auch Mod-Impostor/Verräter) wieder **rot** – auf der Map und im Meeting.
- [ ] *(Online)* **Vampir-Kill**: stirbt dein Biss-Opfer, kommt **nicht mehr** die „selbst getötet"-Animation; außerdem fängt ein **Süßigkeitenkind-Schild** auch den verzögerten Biss ab.
- [ ] *(Info)* **Executioner → Jester**: dass der Executioner zum Jester wird, wenn sein Ziel *anders als per Rauswurf* stirbt, ist die Option „Wird Jester, wenn das Ziel anders stirbt" (standardmäßig an). Sag Bescheid, wenn du sie aus willst.

### Administrator  (NEU, Rolle „Administrator")
- [ ] **Farben am Admin-Tisch**: an den Admin-Tisch gehen (Skeld: Cafeteria) → die Punkte zeigen die **echten Spielerfarben** (mit Dummys in Räumen gut sichtbar), nicht nur die Anzahl.
- [ ] **Admin-Ansicht von überall (F)**: Knopf drücken → die Admin-Ansicht öffnet sich auch fern vom Tisch, ~10 s lang (einstellbar), dann 40 s Cooldown. Auch hier farbige Punkte.

### Teleporter  (NEU, Rolle „Teleporter")
- [ ] **Echte Karte öffnen + teleportieren**: Taste **F** öffnet die echte Among-Us-Karte. Auf eine Stelle klicken → du teleportierst sofort dorthin (landet dort, wo du klickst).
- [ ] **Landet auf dem Vent**: Klick auf einen Raum → Landung auf dessen **Vent** (nie in einer Wand). Vent-lose Räume: O2/Communications = Mitte, Storage = unten. Klick außerhalb → nächster Raum.
- [ ] **Cooldown**: nach dem Teleport 45 s Abklingzeit (einstellbar).

### Neu gefixt (dieser Build – installiert beim nächsten Spielstart)
- [ ] **Cooldowns laufen immer runter**: Ability starten → in eine Aufgabe gehen → Cooldown läuft weiter (gilt für alle Rollen).
- [ ] **Vielfraß-Geist**: Gefangenen verschlucken → er stirbt (Meeting/Report) → sein Geist ist frei, steckt nicht mehr im Bauch.
- [ ] **Button Barry bei Sabotage**: Sabotage auslösen → Notfall-Knopf von Button Barry ist gesperrt; nach Behebung wieder nutzbar.
- [ ] **Patch-Notes (F4)**: zwei Spalten – links „Neue Rollen", rechts „Bugfixes".

### Überall
- [ ] **Update-Notes**: Taste **F4** öffnet/schließt die Neuerungen (jetzt zweispaltig). In der Lobby erscheinen sie einmal pro Sitzung automatisch.

---

## 🔎 NICHT im Freeplay testbar (nur echtes Spiel, mehrere Spieler)

### Neu in diesem Build
- [ ] **Bodyguard**: greift ein Impostor den Schützling an → **nur der Bodyguard stirbt**, Schützling lebt, Angreifer lebt. *(Im Practise fehlt der echte Angreifer.)*
- [ ] **Süßigkeiten-Kind-Schild**: nach dem Zuckerrausch fängt der Schild **jede** Interaktion **lautlos** ab – Kill, Mod-Kill **und auch eine Banane** (das Kind rutscht nicht aus). Es merkt nur „etwas abgefangen", nicht ob es ein Kill war. Danach weg, kommt **nach 50 s** wieder. *(Banane im Freeplay mit 2. Dummy testbar; die lautlose Kill-Blockade nur online.)*
- [ ] **Schutz des ersten Todes**: geschützter Spieler stirbt nicht – **ohne Rückzuck-/Schutz-Animation**, der Kill passiert einfach nicht. (Ab Runde 2, Option an.)
- [ ] **BOO-Kleckse**: beim Drücken erscheinen bei allen anderen dunkle Kleckse am Bildschirm. *(Im Practise allein nicht sichtbar – keine Mitspieler.)*
- [ ] **Endphase Neutral vs. Impostor**: sind nur noch Impostor + Neutrale übrig, endet das Spiel **erst, wenn eine Seite tot ist** – kein sofortiger Impostor-Sieg. (Neutrale mit eigener Regel, z. B. Jester per Rauswurf, gewinnen weiter über ihre Regel.)
- [ ] **Spukgestalt-Aufgaben**: die Spukgestalt bekommt beim Tod neue Aufgaben – **auch im Freeplay** (zum Prüfen).

### Weiterhin offen aus der letzten Liste
- [ ] **7 + 17** Crew/Neutral ohne Kill-/Vent-Knopf aus der Vorrunde.
- [ ] **8** Paranoid-Reichweite.
- [ ] **9** Waybler nicht auf Jester/Executioner.
- [ ] **12** Neutrale ohne Aufgaben.
- [ ] **14 / 25** Vielfraß: Laufen nach Freilassen, 2 nacheinander schlucken.
- [ ] **16 / 19** Voodoo: Sicht + Kill, Impostor sichtbar.
- [ ] **23** BOO-Kleckse auch wenn ein **Joker** sie auslöst.
- [ ] **28** Executioner: Ziel-Name steht oben links.
- [ ] **29** Voodoo-Cooldown **gefixt**: der Übernehmen-Knopf bleibt während der Übernahme sichtbar (ausgegraut) und sein Cooldown läuft jetzt **durchgehend ab Aktivierung** (vorher fror er während der Übernahme ein → wirkte zu lang/inkonsistent). Im Practise und online prüfen, dass der Cooldown gleichmäßig von 35 s runterzählt. *(Echtes Steuern echter Mitspieler bleibt netcode-bedingt nur bei Dummys zuverlässig – Cooldown-Fix gilt aber für beide.)*

---

**Gefundene Fehler hier notieren:**
1.
2.
3.
