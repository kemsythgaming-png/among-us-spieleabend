# Testprotokoll – großer Bugfix-Batch (29 Punkte)

Stand: lokal gebaut + installiert, **noch nicht veröffentlicht**. Legende: `[ ]` offen · `[x]` ok · `[!]` Fehler.
✅ = diese Runde gefixt · 🔎 = nur im **echten Spiel** testbar · ⚠️ = technische Grenze · 📋 = nächster Batch / Info nötig

## ✅ Gefixt – bitte testen
- [ ] **1. CandyKid** – Sabotage-/versteckte Bonbons mit mehr Wand-Abstand (kein Bonbon mehr in der Wand).
- [ ] **5. Banane** – Cooldown läuft jetzt **auch während einer Aufgabe** runter.
- [ ] **7 + 17. Kein Kill/Vent-Knopf** bei Crew-/Neutral-Rollen (Aufgabenprüfer & Impostor-Rest aus der Vorrunde).
- [ ] **8. Paranoid** – neue **Reichweiten-Einstellung** (0 = ganze Map, sonst nur Kills in der Nähe).
- [ ] **9. Waybler** sitzt nicht mehr auf **Jester/Executioner** (kein Doppel-Rauswurf-Gewinn).
- [ ] **10. Feuerwehrmann** – **3 einstellbare Ladungen**, von **überall**, **Cooldown 50 s** (Zahl am Knopf).
- [ ] **12. Neutrale Rollen** haben **keine Aufgaben** mehr.
- [ ] **14. Vielfraß** – Freigelassene laufen nicht mehr „nur nach rechts" (Rest-Schwung wird genullt).
- [ ] **15. Spinne** – neue 2. Fähigkeit **Spinnenbiss**: verlangsamt bis zu 30 s bis zum Stillstand, dann Tod.
- [ ] **25. Vielfraß** – 2. Dummy schlucken geht (schon Verschluckte sind nicht mehr anvisierbar).
- [ ] **26. Arsonist** – **kein `[Benzin]`-Hinweis** mehr; du musst dir selbst merken, wen du hattest.
- [ ] **28. Executioner** – Ziel steht oben links **auch im Practise** (an Dummys).
- [ ] **21. Gambler Slot-Animation** – scrollende Walzen (war schon gebaut; kommt sichtbar mit dem nächsten Release).

## ✅ War schon vorhanden
- [ ] **24. Verräter** – ob jemand Verräter wird, regelst du über **Anzahl/Wahrscheinlichkeit** des Verräters in den Einstellungen (0 = keiner).
- [ ] **27. Arsonist-Sound** – Übergießen spielt bereits einen Wasser-/Splash-Ton (nur du hörst ihn).

## 🔎 Nur im echten Spiel testbar
- [ ] **2. Bodyguard** – greift in echten Spielen (Bodyguard **und** Angreifer sterben, Schützling lebt). Im Practise gibt's keinen echten Angreifer.
- [ ] **3. CandyKid-Schild** – der Schild fängt den Kill ab (`InterceptKill`). Bitte echt gegen einen Impostor testen.
- [ ] **13. Schutz des ersten Todes** – wirkt **nur im echten Spiel, ab Runde 2** und wenn die Option **an** ist (im Practise bewusst aus).

## ⚠️ Technische Grenze
- **16. Voodoo echte Steuerung** eines Mitspielers – über das Netzwerk **nicht möglich**; zuverlässig nur bei Dummys. Bei echten Spielern: Sicht + Kill.
- **19. Voodoo – Impostor sichtbar nach Steuerung** – hängt an genau dieser co-location/Sicht; ohne echte Steuerung schwer sauber zu trennen.

## 📋 Nächster Batch / ich brauche Infos
- **4. Spukgestalt durch Wand** – Collider-Thema, nehme ich mir gezielt vor.
- **6. „Rolle wird nicht angezeigt"** – bitte sag genau **wo**: Task-Liste oben links? Über dem Kopf? Im Intro?
- **11. Jester-Vent in Electrical** – bitte beschreib die Situation (gar nicht reinkommen? nicht weiter? welche Map?).
- **18. Nur Neutral + Impostor übrig → 1vs1** – braucht Eingriff in die Sieg-Bedingung (Neutrale nicht als Crew zählen). Nächster Batch.
- **20. Aufgabenprüfer zeigt nach Meeting 0** – untersuche ich (der Zähler sollte durchlaufen).
- **22. Gambler – freigeschalteter Button ohne Animation** – Detail, mache ich mit.
- **23. BOO-Kleckse auf Joker** – untersuche ich.
- **29. Voodoo-Cooldown nach 2. Übernahme** – bitte: ist der Knopf **sofort** wieder nutzbar?

**Gefundene Fehler hier notieren:**
1.
2.
3.
