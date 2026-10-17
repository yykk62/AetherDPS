# AetherDPS – Gestaltung (ab 0.9)

Eigene Richtung für den Neuaufbau der Desktop-App. Entstanden aus den Wünschen des Nutzers und dem Stand 0.8.2, **bevor** andere Meter angesehen wurden. Was danach dazukam, steht unten unter „Nach dem Abgleich“.

## Ausgangsfrage

Was wäre für einen deutschen AION-2-Spieler mitten im Spiel spürbar besser als heute?

0.8.2 behandelt das Hauptfenster wie ein Programm, in dem man sitzt: neun Reiter in vier Gruppen, jede Seite eine Kartenwand. So benutzt das niemand. Man schaut **kurz** hin, zwischen zwei Bossen, in der Stadt, auf dem zweiten Bildschirm. Das Hauptfenster muss deshalb sofort sagen, was gerade wichtig ist, statt erst gesucht zu werden.

## Haltung: drei Arten hinzusehen

| Wann | Wie lange | Was die App dann ist |
|---|---|---|
| Im Kampf | ein Blick, unter einer Sekunde | **Kampfanzeige** (Overlay): still, lesbar über dem Spiel, nichts zum Bedienen |
| Nach dem Kampf, zwischen den Bossen | ein paar Sekunden | **Jetzt**: Was ist passiert, war ich besser als sonst? |
| In der Stadt, vor dem nächsten Dungeon | eine Minute | **Jetzt** und **Welt**: Was steht an, wer ist in der Gruppe? |
| Einrichten | selten | Einstellungen als Seitenblatt, nicht als Ziel in der Navigation |

Daraus folgt: **Das Hauptfenster richtet sich nach der Lage, nicht nach Funktionen.** Die Startseite heißt „Jetzt“ und zeigt im Kampf den Kampf, danach das Ergebnis, sonst das, was als Nächstes kommt.

## Aufbau

Vier Orte statt neun Reiter, oben als Leiste. Die Seitenleiste entfällt, das Fenster gewinnt Breite für Daten.

| Ort | Inhalt (aus 0.8.2) |
|---|---|
| **Jetzt** | lagenabhängig: laufender Kampf, Ergebnis des letzten Laufs mit Messlatte, Als Nächstes (Termine, Checkliste, Feldbosse), Hinweise |
| **Kämpfe** | Läufe, Kampfanalyse je Spieler, Vergleich, Teilen (Bild, Text, Discord) |
| **Welt** | Timer und Feldbosse, Checkliste, Pets mit Karte und Radar |
| **Daevas** | eigener Charakter, aktuelle Gruppe, Suche nach Name und Server (Profile von NCSOFT) |

- **Verbindung** ist kein Ort, sondern ein Zustand: Statusfeld rechts oben („Verbunden“, „AION 2 läuft“). Ein Klick klappt die Messwerte und „Verbindung prüfen“ auf.
- **Einstellungen** öffnen als Blatt von rechts über der aktuellen Seite, damit man sieht, worauf eine Änderung wirkt.
- **Meter starten/stoppen** ist ein einziger Schalter fest rechts in der Leiste, der seinen Zustand zeigt („Meter starten“ bzw. „● Zeichnet auf | Stoppen“), mit Tastenkürzel, von jedem Ort aus erreichbar.
- **Zeiten sind eindeutig:** Dauern mit Einheit („18:18 Min.“), Uhrzeiten mit „Uhr“, Bossbeginn relativ zum Lauf („+06:54“). Zahlen in Spalten mit fester Nachkommastelle (`Format.CompactFixed`).
- Hinweise (Update, Nachricht, Zustimmung) sind eine Zeile unter der Leiste, nicht Karten zwischen den Daten.

## Gestaltung: „Instrument“

Das Bild dahinter ist ein Messinstrument eines Daeva: Gravur in dunklem Stein, Metall nur dort, wo es etwas bedeutet.

- **Rahmen statt Kartenwand.** Der Fensterhintergrund ist ein mittleres Schiefer-Violett. Tabellen liegen direkt darauf, getrennt durch feine Linien. Eingelassen (dunkle Oberkante, helle Unterkante, kein Schlagschatten) ist nur das Instrument der Seite, z. B. die Messlatte auf „Jetzt“. Eine Mulde je Ort, nicht mehr.
- **Gold heißt „du“.** Deine Zeile, deine Messnadel, deine Rekorde. Sonst nirgends, auch nicht auf Knöpfen. So findet man sich in jeder Tabelle in unter einer Sekunde. Die Hauptaktion ist hell (Pergament), ruhige Aktionen liegen erhaben auf dem Rahmen.
- **Äther-Violett heißt „Phase“.** Stagger, Brechen, Fokus der Tastatur. Es ist die Farbe des Scheins hinter den Icons.
- **Klassenfarben** bleiben für Spieler, aber zurückhaltend: als schmale Spur unter dem Namen, nicht als vollflächiger Balken.
- **Messlatte** ist das eine auffällige Element: eine gravierte Skala mit deinen früheren Läufen als Striche (der bisher beste heller), dem Schnitt als beschriftetes Dreieck und dem aktuellen Lauf als goldene, beschriftete Nadel. Rechts ist immer besser, die Enden sagen es in Worten („weniger … mehr“, „langsamer … schneller“). Links daneben steht groß die Abweichung zum Schnitt. Sie beantwortet die Frage, die man nach einem Boss wirklich hat: Besser als sonst? Und sie ist der Anfang des Langzeitverlaufs.
- **Urteil zuerst.** Nach einem Lauf steht oben ein Satz statt einer Kennzahl („Neue Bestzeit, 8 % mehr DPS als sonst“), darunter Zeitpunkt, Bosse und Platz in der Gruppe.
- **Icon-Set „Aether“** aus 0.8.0 bleibt unverändert. Die Farben sind auf die Goldglyphen mit violettem Schein abgestimmt.

### Schrift

**Bahnschrift**, die DIN-Schrift von Windows. DIN ist die deutsche Normschrift (Straßenschilder, Technik): passt zu „deutsch zuerst“ und zu einem Messgerät, trägt lange deutsche Komposita in schmalen Breiten und hat ruhige, gleich breite Ziffern. Sie ist auf jedem Windows ab 10 vorhanden, es muss nichts mitgeliefert werden. Für Japanisch, Koreanisch und Chinesisch springt die Systemschrift ein.

| Rolle | Größe | Schnitt |
|---|---|---|
| Anzeige (Urteil, Ortstitel) | 34 | SemiBold |
| Titel | 21 | SemiBold |
| Lead | 16 | Regular |
| Text | 14 | Regular |
| Klein | 12 | Regular |

Keine Großbuchstaben-Etiketten, keine Mittelpunkt-Ketten für Metadaten. Zahlen stehen rechtsbündig in eigenen Spalten.

### Tokens

Drei Stufen in `AetherDPS.App/Tokens.axaml`. Views verwenden **nur** die semantischen Namen (`{DynamicResource …}`), nie Hex-Werte.

**Grundpalette**

| Name | Wert | |
|---|---|---|
| `Stone950` | `#100E17` | tiefste Mulde |
| `Stone900` | `#17141F` | Mulde |
| `Stone800` | `#29243A` | Rahmen (Fenster) |
| `Stone700` | `#363049` | erhabene Bedienelemente, Spur |
| `Stone600` | `#4A4262` | Linien auf dem Rahmen |
| `Stone500` | `#5E5677` | |
| `Stone400` | `#9A92B3` | gedämpfter Text, Striche |
| `Parchment50` | `#FBF6EC` | Hauptaktion (Hover) |
| `Parchment100` | `#F1EADB` | Text, Hauptaktion |
| `Parchment300` | `#C9C0AE` | Nebentext |
| `Gold300` | `#F5CF86` | |
| `Gold400` | `#EDBB5F` | „du“, Rekord, Messnadel |
| `Gold700` | `#7A5A22` | Gold gedämpft |
| `Gold900` | `#2E2416` | Fläche deiner Zeile |
| `Aether400` | `#A898FF` | Phase, Fokus |
| `Life400` | `#6FD08C` | Leben, verbunden |
| `Danger400` | `#F2735F` | Tod, Fehler, Löschen |
| `Ember400` | `#F09A4E` | Warnung |

**Semantisch** (Auswahl): `Surface.Frame`, `Surface.Bar`, `Surface.WellEngraved`, `Surface.WellDeep`, `Surface.Raised`, `Surface.Self`, `Line.Frame`, `Line.Well`, `Line.Tick`, `Line.Bevel`, `Text.Primary`, `Text.Secondary`, `Text.Muted`, `Text.OnAction`, `Action.Primary`, `Surface.Placing`, `Accent.Self`, `Accent.Phase`, `Signal.Good`, `Signal.Bad`, `Signal.Warn`, `Focus.Ring`, `Font.Ui`, `Font.Display`.

Bausteine in `AetherDPS.App/Instrument.axaml` (gelten nur unter der Klasse `ins`, damit alte Fenster bis zu ihrem Umbau unverändert bleiben): `well`, `rule`, `framerule`, `place`, `cta`, `quiet`, `icon`, `keycap`, `selftag`, `trace` sowie die Textrollen `display`, `title`, `section`, `lead`, `small`, `muted`, `secondary`, `self`, `num`, `reading`.

Hinweis: Avalonia rendert `inset`-BoxShadows in `RenderTargetBitmap` mit Skalierung falsch (Inhalt doppelt skaliert). Die Gravur ist deshalb ein Verlauf plus Linie.

**Abstände** 4er-Raster: 4, 8, 12, 16, 24, 32, 48. **Radien** nach Rang: Mulde 10, Bedienelement 6, Marke 3. **Bewegung** nur als Antwort auf eine Handlung: 120 ms (Zustand), 200 ms (Aufklappen), keine Einblendungen beim Laden.

## Bedienung

- **Ein Ort, eine Hauptaktion.** Gold markiert sie. Alles andere ist ruhig.
- **Rückgängig statt Nachfrage.** Löschen eines Laufs passiert sofort, eine Zeile unten bietet „Rückgängig“ für ein paar Sekunden.
- **Jeder Zustand hat ein Bild:** leer (mit dem nächsten Schritt, z. B. „Starte einen Dungeon, das Ergebnis erscheint hier“), lädt, Fehler mit Ausweg, Erfolg.
- **Tastatur:** STRG+1 … 4 für die Orte, STRG+, für die Einstellungen, sichtbarer Fokusrahmen in Äther-Violett.
- **Klickflächen** mindestens 32 px hoch, im Overlay sind die Bedienelemente nur bei Maus sichtbar.

## Kampfanzeige (Overlay)

- Gleiche Tokens, aber höherer Kontrast und eine Mulde über die ganze Fläche (das Spiel ist der Rahmen).
- Oben das Ziel: Name, Lebensband mit eingravierten Zehntelstrichen, Stagger als violette Füllung im selben Band statt einer zweiten Leiste.
- Spielerzeilen: Name, Zahl, darunter die schmale Klassenspur. Deine Zeile mit Goldrand.
- Ruhe: ohne Kampf schrumpft die Anzeige auf eine Zeile, kein Flackern bei wechselnden Zielen.

## Sprache

Du-Form, kurze Verben, Satzanfang groß, keine Füllwörter. Eine Aktion heißt überall gleich („Meter starten“ → „Meter läuft“). Fehler sagen, was passiert ist und was hilft, ohne Entschuldigung.

## Stand

Umgesetzt: Tokens und Bausteine, Hauptfenster `ShellWindow` mit allen vier Orten (Jetzt, Kämpfe, Welt, Daevas), Verbindung und Einstellungen als Blätter, Hinweise als Zeilen, die Kampfanzeige (Overlay) und alle Nebenfenster auf den Tokens. Jede Runde von einem unabhängigen Kritik-Agenten geprüft, der nur Bilder und Zweck kennt.

Platzierungsmodus (`Views/OverlayPlacement.cs`, Einrasten in `Engine/OverlaySnap.cs`): STRG+P (belegbar), der Griff am Meter und am Radar, das Tray-Menü oder „Jetzt platzieren“ in den Einstellungen zeigen Kampfanzeige, Timer-Pills, Pet-Radar und Stagger-Warnung mit Beispieldaten in echter Größe. Jedes Overlay trägt einen Rahmen in Äther-Violett (`Surface.Placing`) mit seinem Namen, oben mittig steht eine Zeile mit Esc, Enter und „Fertig“. Ziehen rastet am Bildschirmrand und an Nachbarn ein (bündig mit 6 px Abstand oder Kante an Kante), Festlegen speichert alle Stellen. Esc und Enter laufen über `RegisterHotKey`, solange der Modus läuft, denn kein Overlay nimmt den Fokus (`WS_EX_NOACTIVATE`). Das Schloss je Fenster bleibt nur als Klick-durch. „Overlays nach Lage“ ist eine Einstellung (Standard aus).

Offen: Ruhezonen für Minimap und Skillleiste im Platzierungsmodus, Variante mit hohem Kontrast, README-Bilder neu erzeugen.

## Nach dem Abgleich

Nach der eigenen Richtung gegen andere AION-2-Werkzeuge geprüft, nur auf blinde Flecken und Fehler, die wir nicht wiederholen wollen.

- **Gold auf dunklem Navy ist der Standard der Gaming-Dashboards.** Darum bleibt unser Rahmen ein violetter Schiefer und Gold ist ausschließlich „du“ und die Hauptaktion, keine allgemeine Akzentfarbe. Jede Runde wird darauf geprüft.
- **Ehrlich über Datenqualität.** Geschätzte oder unvollständige Werte bekommen eine eigene Darstellung: „~“ vor der Zahl, gestrichelte statt durchgezogene Spur, Erfassungsquote sichtbar, wenn sie unter 100 % liegt (Token `Text.Estimate`, Strichmuster `Line.Estimate`).
- **Nie nur über Farbe.** Deine Zeile hat zusätzlich zum Gold ein Zeichen, Zustände (lebt, vermutlich, geschätzt) haben Form und Wort, nicht nur Farbe. Später eine Variante mit hohem Kontrast über dieselben Tokens und eine Schriftgröße unabhängig von der Fenstergröße.
- **Eine Schicht für alle Overlays.** Kampfanzeige, Timer, Radar und Stagger-Warnung teilen einen Platzierungsmodus statt eines Schlosses je Fenster: Ein Kürzel zeigt alle Overlays mit Beispieldaten in echter Größe, mit Einrasten und Ruhezonen für Minimap und Skillleiste; Esc legt sie fest. Overlays nehmen nie den Fokus.
- **Overlays nach Lage.** Dieselbe Idee wie „Jetzt“: in der Stadt Timer, im Dungeon die Kampfanzeige, im Bosskampf zusätzlich Stagger. Wer will, schaltet einzelne Overlays fest.
- **Wenige Einstellungen.** Funktionsfülle mit Hunderten Schaltern und vielen Kürzeln ist ein bekannter Weg in die Unübersichtlichkeit. Neue Funktionen bringen sinnvolle Vorgaben statt neuer Schalter.
- **Bestzeit je Boss** gehört zur Messlatte: dieselbe Skala für Zeit, mit dem Abstand zur eigenen Bestzeit.
- **Hinweise ohne Ton**: ein kurzes Aufleuchten im Overlay statt Signalton, Ton bleibt eine bewusste Einstellung.
