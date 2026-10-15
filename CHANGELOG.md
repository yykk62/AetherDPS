# Änderungen

Neue Versionen oben. Der Abschnitt einer Version wird automatisch zum Text des GitHub-Releases.

## 0.7.2

- **Pet-Radar behoben:** Es ging nicht auf, wenn der Meter nach dem Einloggen gestartet wurde (z. B. nach einem Update). Jetzt reicht es, die Karte zu öffnen
- **Pet-Radar „Immer“:** Neben „solange die Spielkarte offen ist“ kann das Radar dauerhaft stehen bleiben und dir folgen (Reiter Pets)
- **Fehlende Pets im Radar:** Unter der Karte eine aufklappbare Liste der nächsten fehlenden Pets (nach deinem Filter im Reiter Pets). Klick auf ein Pet zeigt alle seine Fundorte auf der Karte, herausgezoomt – so siehst du gleich, wohin du teleportieren musst
- **Bilder für alle 207 Pets** in Liste, Detail und Radar
- **Karte im Reiter Pets:** größer, und Ziehen funktioniert jetzt wie gewohnt (nur solange die linke Taste gedrückt ist – vorher konnte die Karte am Mauszeiger kleben bleiben)
- Der Filter im Reiter Pets wird gemerkt und gilt auch fürs Radar

## 0.7.1

- **Timer als einzelne Pills:** Der Timer-Kasten ist weg. Jeder Timer ist eine eigene, gleich große Pill, die du frei ziehen kannst. Leg mehrere grob nebeneinander, untereinander oder als Block hin und klick rechts auf **Gruppieren**: Die Anordnung wird übernommen (1×3, 3×1, 2×2 …) und die Gruppe bewegt sich zusammen. Rechtsklick auch zum **Lösen**, **Auflösen**, für **Spalten**, zum **Entfernen** einer Pill (ohne die App zu öffnen) und zum **Sperren** (entsperren im Reiter Timer)
- **Pet-Radar:** Öffnest du im Spiel die Karte, erscheint am linken Rand ein kleines Fenster mit den Spawnpunkten der Pets, die dir noch fehlen, rund um deine Position, und den nächsten mit Entfernung und Richtung. Verschiebbar, abschaltbar im Reiter Pets
- **Pet-Finder:** Filter „Noch nicht gesammelt“ und „Noch nicht auf Höchststufe“ statt des einen Hakens
- Profil: Stigma-Skills heißen jetzt so

## 0.7.0

- **Neuer Reiter Profil:** Dein Charakter auf einen Blick, auch ohne laufendes Spiel – Kampfkraft, Itemstufe, mittlere Verzauberung, Ausrüstung (Klick auf ein Teil zeigt Werte, Manasteine und Götterstein), alle Werte mit ihren Wirkungen, Daevanion-Fortschritt je Brett, Titel, Pet, Flügel und Skills. Jeden anderen Charakter auf einem EU-Server kannst du per Name suchen
- **Neu in der Gruppe:** Tritt jemand deiner Gruppe bei, erscheint am rechten Rand seine Karte mit Kampfkraft, Itemstufe, Verzauberung, Daevanion und Waffe. Klick öffnet das ganze Profil. Abschaltbar im Reiter Gruppe
- **Pet-Finder (Reiter Pets):** Alle Pets von Verteron, Altgard und der Abyss mit deiner Stufe und dem Fortschritt (liest der Meter beim Einloggen mit) und einer Karte, auf der alle Spawnpunkte ihrer Monster eingezeichnet sind, samt Gebieten und deiner Position. Filter nach Gattung, Suche nach Pet oder Monster, „nur noch nicht fertige“

## 0.6.4

- **Charakter-Profile im Reiter Gruppe:** Klick auf dich („Mein Charakter“) oder ein Gruppenmitglied zeigt Stufe, Itemstufe, Kampfkraft, die komplette Ausrüstung mit Verzauberung und die Skills – direkt von NCSOFT (dieselben öffentlichen Daten wie auf aion2.plaync.com). Abgefragt wird nur auf Klick

## 0.6.3

- **Timer als Kacheln:** Im Overlay ist jeder Timer eine kleine Kachel (Name oben, Zeit groß). Rechtsklick auf den Kopf: **Spalten** (1–4, z. B. 2×2 oder 3×2), **alle einzeln lösen** oder **alle zurück**. Einzelne Kacheln frei ziehen – zum Zurücklegen einfach wieder auf die Gruppe ziehen
- **Overlay nur kurz vorher:** Neben „immer“ und „aus“ gibt es „nur kurz vorher anzeigen“ (Standard 5 Minuten). Erinnerungen kommen in jedem Fall
- **Feste Termine statt Zeitplan-Editor:** Raumzeit-Riss und Artefakt-Belagerung sind fest hinterlegt und nur noch an-/abwählbar. Für alles andere gibt es eigene Timer
- **Feldbosse nur der eigenen Seite:** Elyos sehen Verteron, Asmodier Altgard, beide die Abyss

## 0.6.2

- **Behoben:** Startete der Meter vor dem Spiel oder lud Steam gerade ein Update, konnte er sich auf den falschen Netzwerkverkehr festlegen und zeigte dann gar nichts mehr an (auch keine Feldbosse). Jetzt erkennt er den Spielserver zuverlässig und sucht von selbst neu, wenn vom Spiel nichts kommt

## 0.6.1

- **Behoben:** Nach dem Update von 0.5.0 startete der Meter nicht mehr (er räumte beim Start seine eigenen entpackten Dateien weg). Wer davon betroffen ist: [AetherDPS.exe neu herunterladen](https://github.com/yykk62/AetherDPS/releases/latest) – Einstellungen und Läufe bleiben erhalten

## 0.6.0

- **Neuer Name: AetherDPS.** Einstellungen, Läufe und Timer werden beim ersten Start übernommen, Updates laufen wie gewohnt weiter
- **Dungeons mit Namen:** Der Meter erkennt, in welchem Dungeon du bist (inklusive Schwierigkeit). Läufe heißen nach dem Dungeon, die Mini-Leiste zeigt ihn an
- **10 Sprachen:** Deutsch, Englisch, Französisch, Spanisch, Portugiesisch, Russisch, Japanisch, Koreanisch und Chinesisch (vereinfacht und traditionell), für die Oberfläche und für Skills, Monster, Klassen, Dungeons und Server (Einstellungen → Sprache, Standard: wie Windows)
- **Alle EU-Servernamen** statt Nummern
- **Problem melden** schickt deine Beschreibung und auf Wunsch das Log direkt an den Entwickler, ohne Konto und ohne E-Mail. Namen von Spielern werden vorher entfernt
- **Absturzberichte:** Ist der Meter abgestürzt, fragt er beim nächsten Start, ob er einen Bericht senden darf
- **Spiel-Update erkannt:** Versteht der Meter die Spieldaten nach einem Patch nicht mehr, sagt er das, statt einfach leer zu bleiben
- **Nachrichten vom Entwickler** (z. B. „Update kommt heute Abend“) erscheinen im Hauptfenster
- **Anonyme Nutzungszahl** (freiwillig, fragt beim Start): einmal am Tag ein Signal mit Version und Sprache, ohne Namen oder Kennung
- „Was ist neu“ nach jedem Update
- **Feldbosse mit den echten Zeiten des Servers:** Öffnest du im Spiel die Karte, kennt der Meter alle Feldbosse dieser Karte: wer lebt, wer wann wiederkommt (Verteron, Altgard, Abyss). Mit ★ landen Bosse im Timer-Overlay und erinnern dich vor dem Respawn. Die Respawn-Dauer lernt der Meter mit
- **Zeitlimit im Dungeon** als Countdown auf der Boss-Karte und in der Mini-Leiste (vom Server, rot in der letzten Minute)
- **Erfassungsquote:** Bei besiegten Bossen steht im Läufe-Archiv, wie viel ihres Lebens der Meter erfasst hat – so siehst du, dass nichts fehlt
- **Erlittener Schaden** pro Spieler in der Kampfanalyse
- **An Discord senden:** Webhook in den Einstellungen eintragen, dann postet ein Klick den Lauf als Bild und Text in deinen Kanal
- **EU-Termine:** Artefakt-Belagerung (Mo, Do, Sa 21:00) statt der bisherigen geschätzten Vorlagen; Abyss-Bosse stehen jetzt bei den Feldbossen

## 0.5.0

- **Timer neu:** Die Timer-Gruppe ist eine schmale Leiste, die so groß ist wie ihr Inhalt. Jeder Timer lässt sich per Rechtsklick als **eigene Leiste lösen** und frei auf dem Bildschirm verteilen, die Gruppe verschiebt man als Ganzes. Das Schloss sperrt Gruppe und Leisten zusammen
- **Dungeon-Bosse** erscheinen nicht mehr als Feldboss-Timer. Bosse ohne Respawn-Zeit stehen nur noch im Reiter „Timer“
- **Große Stagger-Warnung** (Einstellungen → Anzeige, standardmäßig aus): oben in der Bildmitte, halbtransparent, mit Stagger-Leiste und geschätzter Restzeit

## 0.4.1

- **Aufgeräumt:** Einstellungen in Abschnitte gegliedert (Bedienung, Erfassung, Anzeige, Start, Updates), Texte überarbeitet
- Boss-Karte: voller Bossname, Leben kompakt am Balken
- Läufe: Boss-Zeilen zweizeilig und gut lesbar
- Stabiler: Fehler im Hintergrund werden geloggt statt den Meter zu beenden
- Die geführte Aufnahme (Werkzeug für die Entwicklung) ist aus der Oberfläche entfernt

## 0.4.0

- **Stagger-Anzeige:** Die Stagger-Leiste des Bosses erscheint auf der Boss-Karte. Danach siehst du, ob es geklappt hat und wer wie viel beigetragen hat
- **Todes-Rückblick:** Nach deinem Tod zeigt der Meter, wer dir in den letzten Sekunden wie viel Schaden gemacht hat, oder dass es ein Sofort-Tod durch eine Boss-Mechanik war (z. B. nach einer verfehlten Stagger-Phase)
- **Dungeon-Erkennung:** Im Auto-Modus beginnt der Lauf beim Betreten eines Dungeons und endet beim Verlassen
- **Versuche:** Neue Anläufe am Boss werden gezählt, auf der Boss-Karte und im Läufe-Archiv
- **Buff-Hinweis**, wenn Essen oder Rollen auslaufen
- Alle Hinweise lassen sich unter Einstellungen → „Kampf-Hinweise“ abschalten

## 0.3.1

- **Updates ohne Umweg:** Der Meter sucht bei jedem Start nach einer neuen Version und installiert sie gleich (er startet kurz neu). Läuft er länger, sieht er stündlich nach

## 0.3.0

- **Nur eigene Kämpfe:** Die Boss-Karte zeigt nur Gegner, die du oder deine Gruppe angreifen. Fremde Kämpfe in der Nähe lassen sie nicht mehr springen
- **Auto-Modus** startet nur, wenn du oder deine Gruppe einen Boss angreifen
- **Läufe:** Boss-Zeilen sind beschriftet („ab 1:04 · 0:08 lang · 2,7K Schaden“). Bosse mit ein paar Streiftreffern zählen nicht mehr und geben dem Lauf keinen Namen
- **Server:** Unbekannte (EU-)Server erscheinen als Nummer, eigener Servername eintragbar
- **Geführte Aufnahme** (Hauptfenster oder Tray): Der Meter führt dich durch ein paar Schritte im Spiel und setzt mit F8 Marken, F9 überspringt einen Schritt. Hilft, neue Funktionen zu bauen. Bleibt auf deinem PC

## 0.2.0

- **Neues Overlay:** Boss-Karte mit Lebensbalken, Kampfzeit und Toden, darunter schmale Spielerzeilen mit Kampfkraft und Anteil. Knöpfe nur unter der Maus, die Höhe passt sich an
- **Mini-Leiste:** Ohne Kampf schrumpft der Meter auf Name, Server und Ping
- **Automatisch erfassen** (Einstellung): Jeder Bosskampf wird ohne Hotkey erfasst. Ein Lauf endet nach 5 Minuten ohne Kampf im Archiv
- Wiedergaben laufen in einem eigenen Datenordner und öffnen kein Hauptfenster

## 0.1.0

Erste öffentliche Version.

- **DPS-Meter** als Overlay: Schaden, DPS und Anteil pro Gruppenmitglied, Skill-Details, deutsche Klassen-, Skill- und Mob-Namen. Start und Ausblenden mit STRG+W, Klick-durch per Schloss
- **Nur deine Gruppe:** Fremde in der Nähe erscheinen nie, Beschwörungen zählen zu ihrem Besitzer
- **Läufe-Archiv** mit Boss-Zeiten, Bestzeit, Vergleich und Kampfanalyse, als Bild oder Text für Discord kopierbar
- **Timer-Overlay** für Risse, Weltbosse und Events (eigene Zeitpläne), Feldboss-Timer und eigene Countdowns, mit Erinnerungen
- **Checkliste** für Tägliches und Wöchentliches mit automatischem Reset
- **Gruppen-Check, Buff-Leiste, Verbindungs-Monitor** (Ping-Verlauf, Lag-Spitzen, Diagnose)
- **Screenshot auf den Zweitbildschirm** mit STRG+S
- **AION 2 aus dem Meter starten** (Steam), der Charakter wird gemerkt
- **Npcap-Einrichtung mit einem Klick** (offizieller Download mit Signaturprüfung)
- **Automatische Updates** aus den GitHub-Releases, geprüft per SHA-256
