# Testliste – ALLE Rollen & Modifier (Gesamtübersicht)

Zweck: nichts vergessen. Pro Rolle steht kurz, **was** zu testen ist. Vieles geht **Freeplay (F3, mit Dummys)**; manches braucht ein **echtes Online-Spiel** (Meeting, Votes, Sabotage, mehrere Menschen) – das ist mit *(Online)* markiert.

Neue Rollen dieses Batches stehen ausführlich in `Testliste-NeuerBatch.md` und sind hier am Ende nur gelistet.

---

## Crew

- [ ] **Sheriff** – Schuss **Q**: Impostor stirbt / sonst er selbst. Optionen: Fehlschuss tötet Ziel, darf Neutrale.
- [ ] **Medic** – Schild **F** (grüne Blase); nächster Angriff scheitert; Medic tot → Schild weg.
- [ ] **Bodyguard** – Schutz **F** (blau, nur er); Schützling angegriffen → Bodyguard stirbt + nimmt Angreifer mit.
- [ ] **Mayor** – Stimme zählt mehrfach *(Online)*.
- [ ] **Seer** – Prüfen **F** → Gut/Böse am Namen (Neutrale = Böse).
- [ ] **Guter Guesser** – Rateknopf im Meeting; richtig → Ziel tot, falsch → Guesser tot *(Online)*.
- [ ] **Administrator** – Admin-Tisch zeigt echte Farben; **F** Admin-Ansicht von überall.
- [ ] **Kameramann** – **F** mobile Kamera setzen, **C** Überwachung von überall; Kamera blinkt beim Zuschauen.
- [ ] **Feuerwehr** (Firefighter) – löscht Arsonist-Feuer / brennende Spieler.
- [ ] **Teleporter** – **F** Karte öffnen, Ziel anklicken → teleport auf begehbaren Punkt.
- [ ] **Aufgabenprüfer** (TaskInspector) – sieht Aufgaben-Fortschritt o. ä. (Funktion prüfen).
- [ ] **Tracker** (Tracker2) – Pfeil/Spur zu einem Ziel.
- [ ] **Trankbrauer** (PotionBrewer) – braut/benutzt Tränke (alle Effekte durchgehen).
- [ ] **Vampirjäger** – Pflock **F**; gegen Vampir/Hexe/Zombie tödlich, sonst Pflock weg.
- [ ] **Süßigkeiten-Kind** – sieht Bonbons; genug → Zuckerrausch (schneller + 1 Schild).
- [ ] **Kürbiskopf** – **F** Kürbis auf: unverwundbar, Sicht nur durch Augenlöcher.
- [ ] **Survivor** (SecondLife) – getötet → Geist an Todesstelle, nach Meeting zurück, kann kein Meeting rufen; 2. Tod endgültig *(Online)*.
- [ ] **Spürnase** (Sleuth) – meldet Leiche → sieht Rolle des Toten.
- [ ] **Radar** – **F** Pfeil zum nächsten Spieler.
- [ ] **Paranoid** – Warnung, wenn ganz nah jemand getötet wird.

## Impostor

- [ ] **Camouflager** – **F** alle grau/namenlos für ein paar Sekunden.
- [ ] **Böser Guesser** – Kill + Raten im Meeting (nur Crew-Rollen/Neutrale) *(Online)*.
- [ ] **Joker** – nach jeder Nutzung andere Impostor-Fähigkeit; Name bleibt Joker; Kill bleibt.
- [ ] **Spinne** (Spider) – Netz/Falle legen; Opfer stirbt (Mechanik prüfen).
- [ ] **Vielfraß** (Glutton) – Fressen/Wachsen-Mechanik + evtl. Selbst-Kill.
- [ ] **Mumie** (Mummy) – Mechanik prüfen *(Online: Meeting?)*.
- [ ] **Vampir** – Biss **F**: Opfer stirbt verzögert; Meeting → sofort ohne Leiche; Schild/GA schützt.
- [ ] **Hexe** (Witch) – Fluch **F**; nicht rausgevotet → alle Verfluchten sterben; Totenkopf-Symbol *(Online)*.
- [ ] **Poltergeist** – **F** durch Wände; am Ende in Wand → aus nächstem Vent raus.
- [ ] **Schwarze Katze** – Meeting: Katzen-Symbol → eine Stimme zählt nicht *(Online)*.
- [ ] **Vogelscheuche** (Scarecrow) – **F** Kopie aufstellen; Nahe erstarren; verschwindet beim Meeting.
- [ ] **Laubstreuer** – Laubhaufen **F**; drin unsichtbar; Leichen im Laub nur drinnen sichtbar; Admin/Kamera-Lücke.
- [ ] **Voodoo-Puppe** (VoodooDoll) – Übernahme-Menü; Kill in der Nähe des Übernommenen.
- [ ] **Glücksspieler** (Gambler) – Fähigkeiten Feuerwehr/Teleport/Trank durchspielen; Button-NaN prüfen.
- [ ] **Kürbisbomber** (PumpkinBomber) – **F** Kürbis legen; Radius sichtbar; nach Zündzeit alle im Radius tot (auch Impostor/er).
- [ ] **Boo** – Mechanik prüfen (×4 war Thema).

## Neutral

- [ ] **Jester** – rausgevotet → Sieg allein; kann venten *(Online)*.
- [ ] **Executioner** – Ziel rausgevotet → Sieg; Ziel stirbt anders → wird Jester (einstellbar) *(Online)*.
- [ ] **Arsonist** – übergießen **F**, anzünden **G** → alle Übergossenen tot; danach letzter übrig → Sieg.
- [ ] **Zombie** – frisst Leichen (**F**); genug gefressen → Sieg; kann nicht killen.
- [ ] **Untoter** (Undead) – getötet → Geist/zurück; an 2 fremden Leichen je 5 s → Sieg *(Online)*.
- [ ] **Orakel** (Oracle) – Rate-Mechanik + Anzeige (x/3) *(Online)*.

## Anpassungen (Modifier)

- [ ] **Lover** – 2 Verliebte ♥; einer tot → anderer tot; unter letzten 3 → gemeinsam Sieg *(Online)*.
- [ ] **Flash** – schneller.
- [ ] **Giant** – größer + langsamer.
- [ ] **Mini** – kleiner + schneller.
- [ ] **Betrunken** (Drunk) – Steuerung vertauscht.
- [ ] **Kurzsichtig** / **Adlerauge** – Sicht kleiner / größer.
- [ ] **Fackel** (Torch) – Licht-Sabotage stört nicht *(Online)*.
- [ ] **Köder** (Bait) – Mörder meldet Leiche automatisch.
- [ ] **Tiebreaker** – Gleichstand → seine Stimme entscheidet *(Online)*.
- [ ] **Knopf-Barry** (ButtonBarry) – Notfallknopf-Mechanik prüfen *(Online)*.
- [ ] **Bierdalf** – Effekt prüfen.
- [ ] **Bananenschale** (BananaPeel) – Ausrutschen prüfen.
- [ ] **Waybler** / **Hamster** – Effekt prüfen.
- [ ] **Deputy** – Hilfssheriff-Mechanik prüfen.
- [ ] **Verräter** (Traitor) – alle Impostor tot → wird (Chance) Impostor; geheim bis dahin *(Online)*.

## Geisterrollen (beim Tod)

- [ ] **Rachegeist** (VengefulSpirit) – **F** Lebende im Raum erstarren.
- [ ] **Wächtergeist** (GuardianSpirit) – **F** Vent sperren.
- [ ] **Spukgestalt** (Haunting) – fast unsichtbar; Aufgaben schaffen → Sieg; angeklickt → erlöst.
- [ ] **Türgeist** (DoorSpirit) – Türen-Mechanik prüfen.
- [ ] **Klagegeist** (WailingSpirit) – Effekt prüfen.

## Neuer Batch (Details in `Testliste-NeuerBatch.md`)

Heiler, Tauscher, Schildtechniker, Thermospezialist, Kaminbauer, Frostdetektor, Vertauscher, Märtyrer, Leuchtkäfer, Techniker, Sprinter, Werwolf, Selbstmordbomber, Assassine, Tatortreiniger, Störsender, Frostbeißer, Schneesturm-Bändiger, Falscher Weihnachtsmann, Lawinenleger, Grinch, Zauberer, Thor, Sniper, Leichenschlepper, Clown, Schneeballkönig, Wintergeist, Überträger, Wettkönig, Kettentauscher, Seelensammler.
Noch offen: Tierfreund, Task-Geber, Dummy-Macher, Überlebender, Schutzfee.

## Allgemeine Dinge (nicht vergessen)

- [ ] **Test-Umschalter** (Sets): schaltet ungetestete/getestete sauber um.
- [ ] **Sets** speichern/aktivieren/umbenennen/löschen.
- [ ] **Team-Sammel-Setzen** (Chance/Anzahl je Team).
- [ ] **F1-Einstellungen**: Text wird nicht abgeschnitten (offener Punkt #3).
- [ ] **Extra-Vents (Skeld)** an/aus.
- [ ] **Rollen-Info (F2)**, **Spielende-Übersicht**, **Fehlerprotokoll**.
- [ ] Mod-Rollen nur an normale Crew/Impostor; Steam-Original unberührt.
