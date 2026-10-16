# Among Us Spieleabend

> This mod is not affiliated with Among Us or Innersloth LLC, and the content contained therein is not endorsed or otherwise sponsored by Innersloth LLC. Portions of the materials contained herein are property of Innersloth LLC. © Innersloth LLC.

Unser eigener Among-Us-Rollenmod, ein BepInEx-6-Plugin (IL2CPP). Er enthält:

- ein **Rollen-Framework**: Für eine neue Rolle legst du eine Datei in `Roles/` an. Verteilung, Netzwerk, Buttons, Anzeige und Einstellungen erledigt der Mod.
- ein **Einstellungsfenster (F1)** wie die Rollen-Einstellungen im echten Spiel: Anzahl, Wahrscheinlichkeit und die eigenen Optionen jeder Rolle.
- einen **Solo-Testmodus** im Freeplay.

## Rollen

Mod-Rollen bekommen nur Spieler, denen das Spiel einen **normalen** Crewmate oder Impostor gegeben hat. Die Rollen aus dem Spiel (Engineer, Scientist, Guardian Angel, Shapeshifter, Phantom, Noisemaker, Tracker, Detective, Viper, Judge, Spirit Guide) bleiben unverändert und funktionieren weiter. Der Guardian-Angel-Schutz gilt auch gegen Mod-Kills.

Rollen, die es im Spiel schon gibt, sind bewusst **nicht** dabei: Mechaniker (≈ Engineer), Morphling (≈ Shapeshifter), Janitor (≈ Viper), Tracker.

| Rolle | Team | Fähigkeit (Taste) |
|---|---|---|
| Sheriff | Crew | Schießt (**Q**). Trifft er einen Impostor, stirbt der Impostor, sonst er selbst. Optionen: Abklingzeit, „Fehlschuss tötet auch Ziel“, „Darf Neutrale erschießen“. |
| Medic | Crew | Gibt einem Spieler einen Schild (**F**), sichtbar als grüne Schutzblase (nur für Medic und, einstellbar, den Geschützten). Der nächste Angriff scheitert, die Blase platzt mit einem Ring-Effekt. Optionen: Schild bricht, Geschützter sieht Schild, Medic wird benachrichtigt, Schild endet mit Tod des Medics. |
| Bodyguard | Crew | Bewacht einen Spieler (**F**), sichtbar als blaue Schutzblase, die **nur der Bodyguard** sieht. Bei einem Angriff sterben Bodyguard und Angreifer statt ihm. Option: Schutz endet beim Meeting. |
| Mayor | Crew | Seine Stimme zählt 2–5-fach (einstellbar). |
| Seer | Crew | Prüft Spieler (**F**) und sieht danach „Gut/Böse/Neutral“ an deren Namen, optional die genaue Rolle. |
| Guter Guesser | Crew | Im Meeting eine Spielerkarte antippen: neben Häkchen und X fährt ein **?-Knopf** aus (nach dem Abstimmen sitzt er direkt auf der Karte). Anklicken, Rolle wählen. Richtig: Der Spieler stirbt sofort ohne Leiche, ohne Meldung (nur Kill-Sound). Falsch: Der Guesser stirbt. Die Rolle muss genau stimmen: „Impostor“ ist nur richtig bei einem Impostor ohne Rolle. Ratbar: Impostor-Rollen, Neutrale, optional Crew-Rollen. Optionen: Schüsse pro Spiel, mehrere pro Meeting. |
| Camouflager | Impostor | Tarnt alle Spieler für ein paar Sekunden (**F**): grau, ohne Namen und Kosmetik. |
| Böser Guesser | Impostor | Hat seinen normalen Kill und rät zusätzlich im Meeting wie der Gute Guesser, aber nur Crew-Rollen und Neutrale - und (einstellbar) „Crewmate“, richtig nur bei einem Crewmate ohne Rolle. Andere Impostor kann er nicht anvisieren. |
| Jester | Neutral | Gewinnt **allein**, wenn er rausgevotet wird. |
| Executioner | Neutral | Hat ein Ziel ([Ziel] am Namen). Wird das Ziel rausgevotet, gewinnt er allein. Stirbt das Ziel anders, wird er zum Jester (einstellbar). |
| Arsonist | Neutral | Übergießt Spieler (**F**). Anzünden (**G**) geht, sobald jemand übergossen ist: Alle Übergossenen gehen für alle sichtbar in Flammen auf und sterben. Sind danach alle anderen tot, gewinnt er allein. Zündungen pro Spiel einstellbar (Standard 1). |
| **Vampir** 🎃 | Impostor | Beißt (**F**): Das Opfer stirbt erst nach ein paar Sekunden, wo es gerade steht. Kommt vorher ein Meeting, stirbt es sofort ohne Leiche. Schild und Guardian Angel schützen. |
| **Hexe** 🎃 | Impostor | Verflucht Spieler (**F**). Wird sie im nächsten Meeting nicht rausgevotet, sterben danach alle Verfluchten. Verfluchte haben einen violetten Totenkopf hinter dem Namen (Hexe immer, im Meeting einstellbar für alle). |
| **Zombie** 🎃 | Neutral | Infiziert andere (**F**). Sie werden selbst Zombies, auch Impostor (einstellbar; ein infizierter Impostor verliert seine Impostor-Rolle). Die Zombies gewinnen, wenn alle Lebenden Zombies sind. Solange noch Zombies leben, gewinnt die Crew nicht schon dadurch, dass keine Impostor mehr da sind. |
| **Vampirjäger** 🎃 | Crew | Hat eine feste Anzahl Pflöcke (**F**, Zahl am Knopf), keine Abklingzeit. Gegen Vampir, Hexe oder Zombie stirbt das Monster, sonst ist nur der Pflock weg. |
| **Süßigkeiten-Kind** 🎃 | Crew | Nur es sieht Bonbons (Gummi-Crewmates), am Rand von Räumen fern von Aufgaben (garantiert innerhalb der Map) (Anzahl einstellbar, Standard 8). Hat es alle gesammelt: Zuckerrausch - schneller und ein Schild gegen einen Angriff. |
| **Poltergeist** 🎃 | Impostor | Geht für ein paar Sekunden durch Wände (**F**). Steckt er am Ende in einer Wand oder außerhalb der Map, kommt er aus dem nächsten Vent wieder heraus. |
| **Schwarze Katze** 🎃 | Impostor | Normaler Kill. Im Meeting einmal das Katzen-Symbol an einer Spielerkarte antippen (auch der eigenen): Eine Stimme gegen diesen Spieler zählt nicht. In der Auswertung fehlt dafür der Kopf dieses Wählers - wer aufpasst, merkt es. |
| **Vogelscheuche** 🎃 | Impostor | Normaler Kill. Stellt eine täuschend echte Kopie eines gewählten Spielers (mit Name) auf (**F**). Wer ihr nahe kommt, erstarrt kurz vor Angst (Impostor nicht). Verschwindet beim nächsten Meeting. |
| **Laubstreuer** 🎃 | Impostor | Normaler Kill. Legt Laubhaufen aus (**F**, max. 3, der älteste verschwindet). Wer im Laub steht, ist unsichtbar - jeder, auch die Crew. Nach 5 s raschelt es (hören alle in der Nähe, Laub wackelt). Wer im Laub war, fehlt 10 s lang auf Admin-Karte und Kameras. |
| **Kürbiskopf** 🎃 | Crew | Setzt kurz einen Kürbis auf (**F**): unverwundbar, aber Kürbis-Sicht (zwei Augenlöcher, kleine Sicht). Die anderen sehen ihn so lange als schwarze Silhouette ohne Namen. |
| **Untoter** 🎃 | Neutral | Wird er getötet, ist er bis nach dem nächsten Meeting Geist (ohne Rollen-Infos) und steht dann wieder auf. Als Untoter läuft er sichtbar herum, gilt im Meeting aber als tot (keine Stimme). Steht er je 5 s an 2 fremden Leichen, gewinnt er allein (Spielende). Sonst verliert er. Töten können ihn nur Impostor und Neutrale, keine Crew-Rollen. Zweiter Tod = endgültig. |
| Lover | Anpassung | Zwei Verliebte (♥). Stirbt einer, stirbt der andere mit. Sind beide unter den letzten 3, gewinnen sie zusammen. Option: Impostor kann Lover sein. |
| Flash | Anpassung | Läuft schneller (Tempo einstellbar). |
| Giant | Anpassung | Ist für alle sichtbar größer und etwas langsamer. |
| Betrunken | Anpassung | Links und rechts vertauscht (optional auch oben/unten). |
| Mini | Anpassung | Kleiner und etwas schneller (Gegenstück zum Giant). |
| Kurzsichtig | Anpassung | Sieht weniger weit. |
| Adlerauge | Anpassung | Sieht weiter. |
| Fackel | Anpassung (Crew) | Licht-Sabotage stört nicht. |
| Köder | Anpassung (Crew) | Wer ihn tötet, meldet die Leiche automatisch sofort selbst. |
| Zweites Leben | Crew | Überlebt den ersten Angriff einmal. |
| Tiebreaker | Anpassung | Bei Stimmengleichstand entscheidet seine Stimme. |
| Spürnase | Anpassung | Meldet er eine Leiche, sieht er die Rolle des Toten. |
| Radar | Crew | Ein Pfeil zeigt immer zum nächsten Spieler. |
| Paranoid | Crew | Warnung, wenn ganz in der Nähe jemand getötet wird. |
| Verräter | Anpassung (Crew) | Sterben alle Impostor, wird er (Chance einstellbar) zum neuen Impostor mit Kill-Button und das Spiel geht weiter. Eine Crew-Rolle (Sheriff, Medic ...) verliert er dabei. Neutrale bekommen ihn nicht. Geheim: Nicht einmal er selbst sieht die Anpassung, bis sie greift oder er stirbt. |

### Geisterrollen

Geisterrollen werden nicht zu Spielbeginn verteilt, sondern **beim Tod**: Anzahl und Wahrscheinlichkeit stellt der Host wie bei jeder Rolle im F1-Fenster ein (Standard: aus). Jeder bekommt höchstens eine; die bisherige Rolle bleibt.

| Geist | Für | Fähigkeit (Taste) |
|---|---|---|
| Rachegeist | tote Impostor | **F**: Alle lebenden Crewmates und Neutralen im selben Raum erstarren kurz vor Kälte (im Gang: alle in der Nähe). |
| Wächtergeist | tote Crewmates | **F** an einem Vent: Es ist eine Weile unsichtbar zu - niemand kommt hinein, heraus oder durch die Gänge dorthin. |
| Spukgestalt (Neutral) | tote Crewmates | Läuft wie eine normale Figur, ist für Lebende aber fast unsichtbar (12 %, einstellbar). Bekommt neue Aufgaben (zählen nicht für die Crew). Schafft sie alle, gewinnt sie allein. Klickt ein Lebender sie an, ist sie erlöst. |

**Anpassungen** (Lover, Flash, Giant) kommen zusätzlich zu jeder Rolle dazu, auch zu Spiel-Rollen wie Engineer oder zum normalen Impostor.

Nur Sheriff und Jester sind standardmäßig an (Anzahl 1). Alle anderen stellt der Host im F1-Fenster ein.

### Allgemeine Regeln (Reiter „Allgemein“)

- **Sprache (nur für dich):** Deutsch (Standard), English oder Automatisch (= Sprache des Spiels). Gilt für alle Mod-Texte: Rollen, Erklärungen, Buttons, Einstellungen und Einblendungen. Jeder Spieler stellt das für sich ein.

- **Chance auf Mod-Rollen pro Runde:** Mit dieser Wahrscheinlichkeit werden überhaupt Mod-Rollen verteilt, sonst ist es eine normale Runde.
- **Tote sehen alle Rollen:** Tote sehen über jedem Namen die Rolle, auch die Rollen aus dem Spiel und Anpassungen.
- **Eigene Rolle über dem Namen.**
- **Max. Crew-/Impostor-/Neutrale/Anpassungen** pro Runde (0 = keine Grenze).
- **Sheriff-Chance nach allen Aufgaben** (Standard: 0 % = aus): Erledigt ein normaler Crewmate ohne Mod-Rolle alle Aufgaben, wird er mit dieser Chance zum Sheriff.
- **Schutz für die ersten Toten** (Standard: aus): Wer in einer Runde als Erstes getötet wurde (mehrere, wenn sie innerhalb einer Sekunde sterben), bekommt in der nächsten Runde einen goldenen Schild, den alle sehen. Bis zum ersten Meeting scheitert jeder Angriff auf ihn. Rausgevotet werden zählt nicht.
- **Halloween-Design** (Standard: aus, im F1-Fenster unter „Allgemein“ einschaltbar): düster-lila Farbstimmung, dunkler Weltraum, flackernde Kürbisse neben den Vents (in der Lobby an den Spawnpunkten) und schwebende Geister an Aufgaben. Funktioniert auf jeder Map. Der Host schaltet es für alle ein oder aus, auch live in der Lobby. Alle Grafiken werden per Code erzeugt, es gibt keine Bilddateien (`Core/HalloweenTheme.cs`).

Zusätzlich hat jede Rolle ihre eigene **Anzahl** und **Wahrscheinlichkeit**, jeder Platz wird einzeln ausgewürfelt.

## Starten

- **Mit Mod:** Verknüpfung `D:\Programmierung\Among Us\Among Us Spieleabend.lnk`. Sie startet die Kopie `steamapps\common\Among Us Spieleabend`, eine Kopie der aktuellen Steam-Version (64-Bit) mit BepInEx 6 be.788. Steam muss laufen.
- **Ohne Mod:** ganz normal über Steam. Der Steam-Ordner wird nicht verändert.

## An Freunde weitergeben (Launcher)

Freunde laden `Among.Us.Spieleabend.exe` von der [Release-Seite](https://github.com/kemsythgaming-png/among-us-spieleabend/releases/latest) (oder aus dem Discord) und starten sie. Der Launcher:

1. findet Among Us in Steam und kopiert es nach `steamapps\common\Among Us Spieleabend` (nach Steam-Updates nur die geänderten Dateien),
2. spielt BepInEx und den Mod ein,
3. holt die neueste Mod-Version vom neuesten GitHub-Release,
4. legt eine Desktop-Verknüpfung an, startet Steam falls nötig und dann das Spiel.

Die EXE enthält **keine Spieldateien**, das verbietet die Among Us Mod Policy. Drin sind nur BepInEx und der Mod.

### Ein Update veröffentlichen

1. Version in `AmongUsSpieleabend.csproj` und `Plugin.cs` erhöhen (z. B. 4.5.1).
2. Paket bauen:
   ```
   powershell -ExecutionPolicy Bypass -File Verteilung\Paket-bauen.ps1
   ```
3. Release anlegen:
   ```
   gh release create v4.5.1 dist/AmongUsSpieleabend.dll "dist/Among Us Spieleabend.exe" --title "Among Us Spieleabend 4.5.1" --notes "Was neu ist"
   ```

Beim nächsten Start holt sich jeder Launcher die neue `AmongUsSpieleabend.dll` automatisch. Alle Spieler brauchen dieselbe Mod-Version, also vor dem Spieleabend einmal über die Verknüpfung starten.

Nur wenn sich BepInEx oder der Launcher selbst ändert, braucht jeder die neue EXE einmal neu.

Ohne Launcher geht es weiterhin mit `dist\AmongUsSpieleabend-<Version>.zip` und `Installieren.bat` (Anleitung in `Verteilung/LIESMICH.txt`).

## Server

Beim Start trägt der Mod die Modded-Server (**Modded EU**, NA, Asia) automatisch in die Serverliste ein (`Core/ServerSetup.cs`). Beim allerersten Start ist **Modded EU** gleich ausgewählt. Danach bleibt, was man selbst wählt (Weltkugel-Symbol unten rechts im Online-Menü).

Mit dem Mod immer auf **Modded EU** spielen. Die offiziellen Server haben einen Anti-Cheat, der die Mod-Nachrichten und Sheriff-Kills für Cheaten halten könnte. Alle in der Lobby müssen denselben Server gewählt haben.

## Knöpfe oben rechts

Neben dem Notizblock des Spiels sitzen zwei Knöpfe im selben Stil: **Rollen-Info** (immer, auch F2) und **Rollen-Einstellungen** (nur Lobby/Practice, auch F1). Die Knöpfe unten links erscheinen nur noch als Ersatz, falls das einmal nicht klappt. Außerdem stehen die eingeschalteten Mod-Rollen im „Roles“-Reiter des Match Info Guide (Notizblock).

## Tempo

Alle Tempo-Effekte (Flash, Giant, Mini, Zuckerrausch) werden multipliziert und überschreiben sich nicht gegenseitig.

## Dauer-Anzeige

Fähigkeiten mit Wirkungsdauer (Poltergeist, Camouflager, Vampir-Biss) zeigen im Button wie beim Shapeshifter, wie lange sie noch laufen. Die Abklingzeit startet danach.

## Geräusche

Alle Fähigkeiten haben Original-Sounds des Spiels: Schild/Bewachen (Schutz-Sound), Seer/Spürnase (Verhör), Camouflager (Verwandeln, für alle), Vampir (leiser Biss; das Opfer hört beim Tod den Kill-Sound), Hexe (Verschwinden), Zombie (unheimlicher Ton), Vampirjäger-Fehlschuss, Arsonist (Plätschern beim Übergießen, Alarm für alle beim Anzünden), Guesser (Kill-Sound für alle), Poltergeist (Verschwinden/Erscheinen), Bonbons (Aufgaben-Sounds), Paranoid (Warnton). Wer ohne Leiche stirbt, hört ebenfalls den Kill-Sound.

## Rollen-Info (F2)

Der Button **„Rollen-Info (F2)“** erklärt jede Rolle: im Meeting oben links, in der Lobby neben „Rollen-Einstellungen“, während der Runde unten links.

- **Links** stehen alle Rollen nach Team sortiert. Deine eigene ist mit „(du)“ markiert und beim Öffnen gleich ausgewählt. Ausgeschaltete Rollen sind grau.
- **Rechts** steht eine ausführliche Erklärung, dazu Fähigkeiten mit Tasten, die aktuellen Einstellungen des Hosts und ob die Rolle eingeschaltet ist.
- Das Fenster verrät nicht, wer welche Rolle hat.

Klicks auf unsere Fenster (Rollen-Info, Einstellungen, Guesser) gehen nicht mehr zu den Spiel-Buttons darunter durch, z. B. zu den Spielerkarten im Meeting.

## Einstellungen (F1)

Das Fenster gibt es **nur in der Lobby und im Freeplay (Practice)**: **F1** oder der Button unten links. Im Hauptmenü und während einer laufenden Runde ist es nicht verfügbar.

- Oben sind Reiter: **Allgemein** (Mod-Regeln), **Spiel** (die normalen Spiel-Einstellungen: Impostor-Anzahl, Kill-Abklingzeit und -Reichweite, Tempo, Sicht, Meetings, Abstimmzeit, Aufgaben ...), **Spiel-Rollen** (Engineer, Scientist, Noisemaker, Tracker, Detective, Judge, Guardian Angel, Spirit Guide, Shapeshifter, Phantom, Viper mit Anzahl, Chance und ihren Einstellungen), dann unsere Rollen: Crew, Impostor, Neutral, Anpassungen.
- Die Reiter Spiel und Spiel-Rollen ändern direkt die Original-Einstellungen; das Spiel schickt sie selbst an alle. Links stehen die Rollen, rechts Anzahl, Wahrscheinlichkeit und die Optionen der gewählten Rolle.
- Nur der **Host** kann ändern. Die anderen sehen live die Werte des Hosts.
- Gespeichert wird beim Host in `BepInEx\config\de.spieleabend.amongus.cfg`.
- „Anzahl 0“ schaltet eine Rolle ab.

## Solo testen (Freeplay)

1. Mod starten, dann im Hauptmenü den Übungsmodus (Freeplay) wählen und eine Karte nehmen.
2. Du bist automatisch **Sheriff**. Die ersten 2 Dummies gelten als **Impostor** (roter Name).
3. Schieß auf einen roten Dummy: Er stirbt. Schieß auf einen weißen: Du stirbst.
4. Am Laptop „alle wiederbeleben“ und weitertesten.
5. Mit F1 kannst du die Testrolle und die Zahl der Dummy-Impostor ändern.

Mit F1 → Allgemein → Freeplay-Test lässt sich jede Rolle als Testrolle einstellen, auch Anpassungen. Beim Lover wird ein Dummy dein Partner. Siege zeigt der Freeplay nur als Einblendung an.

Jester im Freeplay: Testrolle per F1 auf „Jester“ stellen, ein Meeting einberufen und dich selbst rausvoten. Statt das Spiel zu beenden, zeigt der Freeplay nur „Jester gewinnt!“ an. Ob die Dummies im Meeting so abstimmen, dass du wirklich rausfliegst, hängt vom Spiel ab. Der echte Test geht nur mit Mitspielern.

## Eine neue Rolle bauen

1. `Roles/_Vorlage.cs.txt` kopieren, z. B. als `Roles/Medic.cs`.
2. Name, Farbe, Team (`Crewmate`/`Impostor`/`Neutral`), Intro- und Aufgabentext anpassen.
3. In `Setup()`:
   - `AddOption("Key", "Anzeigename", standard, min, max, schritt, "s")` für Zahlen,
   - `AddToggle("Key", "Anzeigename", true/false)` für Schalter,
   - `AddButton(new RoleButton { Label, Cooldown, Hotkey, NeedsTarget, OnClick })` für eine Fähigkeit.
4. In `OnAction()` die Logik: Mit `SendToHost(...)` geschickte Aktionen kommen nur beim Host an. Dort wird entschieden, z. B. `sender.RpcMurderPlayer(target, true)`.
5. `dotnet build -c Release`. Die DLL wird automatisch in die Mod-Kopie kopiert, und die Rolle erscheint im F1-Fenster.

Nützliche Helfer in `CustomRole`: `FindPlayer(id)`, `IsAlive(p)`, `IsImpostor(p)` (beachtet Freeplay-Dummy-Impostor), `RoleRegistry.GetRole(p)`, `RoleRegistry.PlayersWith(role)`.

Für neutrale Rollen und eigene Siege:
- `OnExiled(player)` wird aufgerufen, wenn ein Spieler mit der Rolle rausgevotet wurde.
- `GetExileText(player)` ersetzt den Text in der Rauswurf-Animation.
- `CustomWin.Trigger(this, gewinner)` beendet das Spiel als Sieg der Rolle; nur der Host ruft das auf.
- `TasksCount` legt fest, ob die Aufgaben der Rolle für den Crew-Sieg zählen. Bei Neutralen ist das standardmäßig aus.

Siehe `Roles/Jester.cs`.

Weitere Bausteine:
- **Kills immer über `KillSystem`:** `TryKill(killer, ziel)` beachtet Guardian Angel, Medic und Bodyguard. `ForceKill` tötet ohne Schutz, `KillNoBody` tötet ohne Leiche und geht auch im Meeting. Alles nur beim Host.
- **Kills abfangen:** `InterceptKill(killer, ziel)` → `KillIntercept.Handled` (siehe Medic, Bodyguard). Gilt auch für Kills normaler Impostor.
- **Ereignisse:** `OnPlayerDied(opfer, killer, ursache)`, `OnAnyExiled(spieler)`, `OnMeetingStart()`, `Update()` (jedes Frame), `ResetState()`.
- **Markierungen am Namen:** `GetNameMark(betrachter, ziel)`, z. B. „[Ziel]“ oder „♥“.
- **Eigene Fenster:** `DrawGUI()`, z. B. das Rate-Fenster des Guessers im Meeting. Hilfen dafür bietet die Klasse `Ui`; mit `Ui.Block(rect)` gehen Klicks nicht durch.
- **Erklärung:** `LongDescription` erscheint in der Rollen-Info (F2).
- **Rollenwechsel:** `RoleRegistry.ChangeRole(spieler, neueRolle)`, nur beim Host (Executioner → Jester).
- **Anpassungen:** `Team => RoleTeam.Modifier`, optional `PlayersPerCount` (Lover: 2).
- **Hinweis einblenden:** `Notifier.Show("Text")`.
- **Button-Symbol:** `Icon = "shield"` (Bilder in `Assets/Icons`, neu zeichnen mit `python Assets/make_icons.py`).
- **Buttons:** zusätzlich `Visible` und `CanUse`, z. B. damit nur ein Schild gleichzeitig aktiv ist.

## Projektaufbau

```
Plugin.cs              Einstieg
Core/CustomRole.cs     Basisklasse für Rollen
Core/RoleOption.cs     Einstellbare Optionen
Core/RoleRegistry.cs   Rollen finden + verteilen (Host)
Core/RoleRpc.cs        Netzwerk (RPC 210-215)
Core/GeneralOptions.cs Allgemeine Regeln (Tote sehen Rollen, Chancen, Maxima)
Core/KillSystem.cs     Kills, Schutz-Regeln, Todes-Ereignisse
Core/Camouflage.cs     Tarnung (Camouflager)
Core/Notifier.cs       Einblendungen
Core/RoleGuide.cs      Rollen-Info-Fenster (F2)
Core/ClickBlockPatches.cs Klicks unter unseren Fenstern sperren
Core/CustomWin.cs      Eigene Siege, Rauswurf-Hooks, Aufgabenzählung
Core/RoleButton.cs     Fähigkeiten-Buttons
Core/RoleUI.cs         Intro, Namensfarbe, Aufgabenliste
Core/SettingsWindow.cs F1-Fenster
Core/FreeplayTest.cs   Solo-Test
Core/DataIsolation.cs  Eigener Datenordner (schützt das Steam-Spiel)
Core/ServerSetup.cs    Modded-Server in die Serverliste eintragen
Core/Patches.cs        Harmony-Hooks
Roles/                 Die Rollen
Verteilung/            Installer + Paket-Skript für Freunde
```

## Schutz für das normale Steam-Spiel

- Der Mod liegt nur in der Kopie `Among Us Spieleabend`. Ein Build ins Steam-Spiel bricht mit Fehler ab.
- Die Mod-Kopie speichert in `AppData\LocalLow\Innersloth\Among Us Spieleabend`, nicht im gemeinsamen Ordner `...\Among Us`.
- Nur Unitys Protokoll `Player.log` landet weiter im Standardordner.
- Die Kopien „Among Us EHR“ und „Among Us Town of Hosts“ haben diesen Schutz **nicht**.

## Bauen

```
dotnet build -c Release
```

Die DLL wird automatisch nach `Among Us Spieleabend\BepInEx\plugins` kopiert. Läuft das Spiel gerade, ist die DLL gesperrt; dann mit `-p:SkipInstall=true` nur bauen.

Die Referenz-DLLs (BepInEx und `interop`) kommen aus derselben Kopie. Den Ordner `interop` erzeugt BepInEx beim ersten Spielstart neu, auch nach jedem Among-Us-Update. Danach neu bauen.

Die alte 32-Bit-Kopie (v17.3s) liegt als Backup unter `Among Us Spieleabend (alt v17.3s)`.
