# Testliste – Batch vom 07.11. (lokal gebaut + installiert, **nicht veröffentlicht**)

Legende: `[ ]` offen · `[x]` ok · `[!]` Fehler. Spiel nach dem Build **neu starten**, damit die neue DLL lädt.

## 🕷️ Spinne
- [ ] **Biss macht NICHT mehr langsam** – der Gebissene läuft normal weiter.
- [ ] **Tod nach 8 s** mit **Kill-Geräusch** (bei allen hörbar).
- [ ] **Leiche löst sich auf** (wie Viper) und hinterlässt danach ein **Netz**.
- [ ] **Netz bleibt in Wänden** – es ragt nicht mehr durch Wände (Radius wird an Wänden gekappt).
- [ ] **Leichen-Netze bleiben liegen** bis zum nächsten **Meeting / bis eine Leiche gemeldet** wird (laufen nicht ab). Normale gelegte Netze verschwinden weiter nach der Zeit.
- [ ] **Netz-Kill-Geräusch** – tötet ein Netz jemanden, kommt ein Kill-Sound.

## ⛓️ Kettentauscher
- [ ] **Cooldown 40 s** nach dem Markieren.
- [ ] *(Online/Dummy)* **Impostor→Neutral beim Tausch**: tauschst du mit jemandem, der Impostor war, wird sein Name **nicht mehr rot** und er verliert den Kill (Vanilla-Basis wird mitgetauscht). *(Nur im echten Rollentausch beim Meeting prüfbar.)*

## ❄️ Wintergeist
- [ ] **Taskmenü**: bessere Erklärung + Anzeige **„Schneeflocken: x/3"**.
- [ ] **Cooldown 35 s**, immer nur **1 Markierung**.
- [ ] **Schneeflocken-Symbol** über dem aktuell markierten Ziel (nur du siehst es).
- [ ] **Nach Meeting**: Markierung weg, Cooldown läuft wieder (Punkte bleiben).

## 🌨️ Schneesturm-Bändiger
- [ ] **Schnee über den ganzen Bildschirm** aller Spieler (wie BOO-Overlay), solange der Sturm läuft.
- [ ] *(Freeplay)* Löst ein **Dummy** die Fähigkeit aus, passiert **nichts**.

## ♥ Lover
- [ ] 2 Dummys zu Lovern machen, einen töten → beim Partner liegt jetzt **eine Leiche** (vorher verschwand er spurlos).

## 🃏 Joker
- [ ] Bekommt nur **eingeschaltete UND getestete** Impostor-Rollen.
- [ ] Rollen mit **2 Nutzungen** (Falscher Weihnachtsmann, Tatortreiniger) funktionieren jetzt **beide Male**, bevor neu gewürfelt wird (Cooldown greift erst nach der letzten Nutzung).
- [ ] **Extra-Knopf „Neue Fähigkeit" (🎰, Standard 60 s)** – **gebaut**: jederzeit per Knopf eine neue zufällige Fähigkeit würfeln. Bleibt **dauerhaft sichtbar**, auch während du eine geliehene Rolle trägst (neben deren Knopf). Die geliehene Rolle behält ihren eigenen Cooldown; der „Neue Fähigkeit"-Knopf hat seine eigene 60-s-Abklingzeit (einstellbar im Impostor-Reiter).

## ⛄ Schneeballkönig
- [ ] Ball **prallt nicht mehr ab** – er fliegt gerade (auch durch Gänge) und bleibt an der Wand liegen.
- [ ] Ball ist **halb so groß**.
- [ ] **Wurf-Geräusch** passt besser (leiser Schwung statt Platsch).

## 🚪 Türgeist (neu gebaut)
- [ ] **Nur die eine Tür**, in der du stehst, geht zu – nie alle (lag an doppelten Tür-Ids; jetzt per Index).
- [ ] **Kein Flackern mehr**: eine zugehaltene Tür bleibt zu, sie springt nicht nach ihrem Timer kurz auf und wieder zu (Öffnen durch das Spiel wird per Patch blockiert).
- [ ] **Wieder aufmachen** mit F an derselben Tür klappt sauber.
- [ ] **Meeting**: alle gehen wieder auf.
  *(Getestet auf The Skeld/Polus – PlainDoor. Mira/Fungle-Türen anders, dort bitte kurz gegenchecken.)*

## 🪆 Voodoo-Puppe
- [ ] **Kill-Cooldown bei neuer Übernahme = 0** (sofort killbar).
- [ ] **Übernahme-Cooldown startet erst**, wenn du wieder im eigenen Körper bist.
- [ ] **Vision beim Steuern**: Dunkelheit ist während der Übernahme aus (du siehst die Umgebung).
- [ ] **Keine Wände mehr**: das gesteuerte Ziel läuft über die Physik, nicht mehr durch Wände. *(Zuverlässig nur bei Dummys/als Host.)*

## 🏁 Wettkönig (neu)
- [ ] Wett-Fenster zeigt **alle Spieler + „Niemand stirbt"**; du wählst einen.
- [ ] Stirbt der gewählte Spieler bis zum Meeting (bzw. niemand bei „Niemand stirbt") → **+1 Punkt**.
- [ ] **Keine Rauswurf-Wetten** mehr, kein Kill-Knopf. Genug Treffer = Sieg.

## 🦠 Virus (neue Mechanik)
- [ ] **F** steckt den nächsten Spieler **sofort** an.
- [ ] **Nähe-Ansteckung**: wer ~20 s (einstellbar) im Radius eines Angesteckten **oder des Virus** bleibt, wird selbst angesteckt.
- [ ] **Symbol am Namen** bei Angesteckten – **nur der Angesteckte selbst und der Virus** sehen es (Map-Icon + Meeting-Totenkopf).
- [ ] **Nach jedem Meeting** verlieren alle Angesteckten **ihre Fähigkeit** (alle Rollen-Knöpfe weg; angesteckte Impostor können auch nicht mehr killen).
- [ ] **Sieg**: sobald **kein anderer lebender Spieler** mehr eine Fähigkeit hat, gewinnt der Virus.
- [ ] Oben links Zähler **„Angesteckt: x"**.
  *(Kein Todes-Timer mehr – die alte „stirbt nach 2 Min"-Mechanik ist raus.)*

## 🧪 Freeplay
- [ ] **Meeting-Knopf repariert**: startet jetzt ein echtes Notfall-Meeting (über den Vanilla-Report-Pfad); die Spielerzahl geht nicht mehr auf -2. Als Toter ruft ein lebender Dummy das Meeting → so lässt sich **Untoter** testen.
- [ ] **Rolle + Anpassung gleichzeitig (für dich)**: Wähle eine normale Rolle → „Für dich". Wähle dann im Reiter **Anpass.** (oder **Geister**) eine Anpassung/Geisterrolle → „Für dich" – sie kommt jetzt **zusätzlich** dazu (vorher wurde die Hauptrolle ersetzt). Oben steht „Deine Rolle: X + Y". Mit **„Anpassung aus"** wieder entfernen.
- [ ] **Geisterrolle an dir testen (z. B. Rachegeist)**: Rachegeist über den **Geister**-Reiter als Zusatz „Für dich" geben → **Sterben** → **„Knöpfe auch als Toter benutzbar"** an → Geisterfähigkeit testen.
- [ ] **Geist-Dummy (Rachegeist)**: Im Freeplay-Fenster rechts einem **Dummy** über den **Geister**-Reiter „Rachegeist" geben → der Dummy wird automatisch zum **Geist** (stirbt). Dann **„Fähigkeit"** klicken → an der Position des Dummys erscheint die **Frost-Zone**; stell dich rein → du wirst extrem langsam. *(Andere Geisterrollen am Dummy sind noch nicht verdrahtet – sag Bescheid, welche du brauchst.)*
- [ ] **Dummys laufen umher** (neuer Schalter unten im Freeplay-Fenster): mit **An** laufen alle lebenden Dummys in Richtungen herum und prallen an Wänden ab. Dabei wirken **positionsabhängige Verlangsamungen** auf sie – z. B. laufen Dummys **sichtbar langsamer**, wenn sie durch eine Rachegeist-**Frost-Zone** gehen (gut zum Testen von Slows). Mit **Aus** halten sie sofort an.

## 📡 Störsender (eigene Fake-Störung – neu gebaut)
- [ ] **Blockiert NICHT die Impostor-Sabotage**: während der Störung kann der Impostor ganz normal Licht/Reaktor/O₂ sabotieren und killen (es wird nicht mehr die echte Komm-Sabotage benutzt).
- [ ] **2. Druck macht nichts mehr unklar**: Knopf ist gesperrt, solange die Störung läuft (erst nach Behebung + Cooldown wieder).
- [ ] **Nicht-Impostor** haben während der Störung: **keine Karte** (Map/Admin geht sofort wieder zu), **keine Kameras** (Security-Konsole schließt), **keine Aufgabenliste**.
- [ ] **Impostor** behält Karte/Admin/Kameras (damit er sabotieren kann).
- [ ] **Pfeil + Banner** für alle: oben „⚠ Störsender aktiv — <Raum>" und ein roter Richtungspfeil zur Ziel-Konsole.
- [ ] **Reparatur**: geht **irgendjemand** (~1,2 s) zur markierten Konsole, ist die Störung behoben (Welt-Symbol pulsiert dort). Die „Aufgabe" zählt für nichts.
- [ ] **Meeting** beendet die Störung sofort.
  *(Hinweis: Ziel ist eine zufällige echte Konsole der Map. Raumname kommt aus der Spielübersetzung – fehlt sie, steht nur „Störsender aktiv".)*

---
**Noch offen / Fragen** (siehe Chat):
- **Weitere Geist-Dummys**: Rachegeist am Dummy ist jetzt verdrahtet (siehe oben). Für die anderen Geisterrollen (Guardian/Wehklagender …) bei Bedarf sagen, welche du am Dummy brauchst.
- *(Hinweis: Die laufenden Dummys nutzen weiterhin die normale Spielfigur ohne eigene Beinanimation – sie bewegen sich aber jetzt echt über die Physik, inkl. Slow-Effekten.)*
- **Fähigkeit nach Tod/Rollenwechsel behalten**: Beim Rollenwechsel/Beleben wird im Freeplay neu zugewiesen; als Toter greift der Schalter „Knöpfe auch als Toter benutzbar". Falls dir irgendwo trotzdem eine Fähigkeit verloren geht, sag mir **welche Rolle + wann** genau.
