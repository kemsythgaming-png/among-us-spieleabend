# Testliste – aktueller Build (unveröffentlicht)

Alles lokal gebaut + installiert, Version bleibt **4.9.2**, **noch nicht veröffentlicht**.
Legende: `[ ]` offen · `[x]` ok · `[!]` Fehler. Neu: **Update-Notes im Spiel mit Taste F4** (öffnen sich beim ersten Lobby-Besuch einer Sitzung auch von selbst).

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
