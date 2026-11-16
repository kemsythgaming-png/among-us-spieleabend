# Testliste – Bugfixes dieser Sitzung

Stand: 2026-11-16 · Diese Fixes sind gebaut, aber **noch nicht eingespielt** (erst wenn das Spiel zu ist).

---

## 1. Meeting – Rolleninfo unter dem Namen
- [ ] **Online:** Meeting öffnen, eigene Karte ansehen → Rolle/Anpassung/Zähler sitzen **bündig unter dem Namen**, nicht mehr nach links über den Avatar verschoben.

## 2. Tauscher (Swapper)
- [ ] **Online:** Zwei lebende Spieler tauschen → beide stehen am Platz des anderen und **bleiben dort**, auch wenn sie sich danach bewegen (springen nicht mehr an den Start zurück).
- [ ] Tausch mit einer **Leiche** funktioniert weiterhin.

## 3. Orakel (Oracle)
- [ ] **Jede Person nur 1× ratbar:** Auf einen Spieler raten → danach ist derselbe Spieler **kein Ziel mehr** (Rate-Button weg); ein 2. Versuch auf dieselbe Person wird abgelehnt.
- [ ] **Spiel läuft weiter:** Orakel voted/killt den letzten Impostor raus → Spiel **endet NICHT** sofort, solange das Orakel seine nötigen Treffer noch schaffen kann.
- [ ] Sobald das Orakel nicht mehr gewinnen kann (tot / zu wenige Ziele/Versuche) → normales Spielende greift wieder.

## 4. Thor
- [ ] **Kein Sofort-Wurf:** Zu Rundenbeginn ist der Hammer-Knopf **auf Cooldown** (ca. 30 s), nicht direkt verfügbar.
- [ ] **1 Kill hin + 1 Kill zurück:** Hammer werfen → tötet **max. 1** auf dem Hinflug; nach dem Rückruf tötet er **1 weiteren** auf dem Rückweg (nicht mehr alle auf der Flugbahn).

## 5. Gambler
- [ ] **Schild:** Schild gewinnen + mit **C** aktivieren → ein **normaler Impostor-Kill prallt ab** (Schild bricht sichtbar, Gambler überlebt).
- [ ] Nach dem Abprall ist der Schild verbraucht (ein 2. Kill trifft normal).

## 6. Chamäleon
- [ ] **Schild + unsichtbar:** Chamäleon hat eine Schutzblase und tarnt sich (F) → die **Schutzblase verschwindet mit** der Tarnung (verrät den unsichtbaren Spieler nicht mehr).
- [ ] Beim Wiedersichtbarwerden ist die Blase wieder da.

---

### Bereits in v4.9.19 veröffentlicht (nur gegenprüfen, falls Zeit)
- [ ] Untoter: kein Meeting-Button nach Wiederbelebung; Seelen-Sammeln animiert; 2 verschiedene Leichen nötig
- [ ] Mayor: Stimmenanzeige geht nach Skip im Freeplay hoch
- [ ] Dummys folgen dem eigenen Vote (Freeplay)
- [ ] Schneeballkönig entfernt
