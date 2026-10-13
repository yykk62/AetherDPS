# Plan

## Phase 1: Grundgerüst ✅ (13.10.2026)
- Fork von RATmeter mit Upstream-Remote, eigener Avalonia-Oberfläche und deutschen Texten und Namen
- Ringpuffer mit „Problem melden“, Wiedergabe (`--replay`), CLI, Pipeline-Tests und CI
- Sync-Bug im Upstream gefunden und gefixt
- Einrichtungsfenster: Npcap mit einem Klick (offizieller Download, Signaturprüfung, voreingestellte Optionen)

## Phase 2: Erster Live-Test auf EU
- [ ] Der Entwickler startet den Meter, installiert Npcap über die Einrichtung und startet das Spiel. Erwartet wird „Verbunden“ mit Ping.
- [ ] 2–3 Minuten am Sandsack spielen und die Zahlen mit dem DPS-Meter im Spiel vergleichen
- [ ] „Problem melden“ einmal auslösen. Das ergibt den ersten echten Mitschnitt für die Tests (lokal, nicht committen).
- [ ] Prüfen, ob Klick-durch über dem Spiel funktioniert (Vollbild im Fenstermodus)

## Phase 3: Veröffentlichen
- [ ] GitHub-Repo (öffentlich, GPL), erst nach Rücksprache
- [ ] Release als eigenständige Einzeldatei (`dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true`)
- [ ] Update-Prüfung über unsere GitHub-Releases (Hash-Prüfung, nicht wie bei Upstream ohne)
- [ ] Sync-Bug als Issue oder PR an RATmeter melden

## Phase 4: Autonome Wartung
- [x] Geplante Aufgabe „aion2meter-wartung“ (täglich 11 Uhr): Upstream-Merge + deutsche Daten auf Wartungs-Branch, Build+Test, nur bei Grün nach main. Kein Push. Log: docs/WARTUNGSLOG.md

## Phase 5: Ausbau
- [x] Hauptfenster mit Reitern (DPS-Meter, Verbindung, Einstellungen), Verbindungs-Monitor mit Ping-Verlauf und Diagnose (13.10.)
- [x] Timer: Risse nach Plan, Feldbosse automatisch beim Kill erkannt (Respawn-Dauer trägt der Nutzer einmal ein), eigene Timer, Erinnerungs-Popups (13.10.)
- [x] Lauf-Archiv mit Bestzeiten, Boss-Zeiten, Vergleich, Kampfanalyse, Teilen als Bild/Text (13.10.)
- [x] Gruppen-Check, Daily-/Weekly-Checkliste, Buff-/Abklingzeit-Leiste (13.10.)
- [ ] Im Spiel prüfen: EU-Risszeiten, Reset-Zeiten, Buff-Leiste im Spiel, Feldboss-Erkennung, Zahlen gegen den Spiel-Meter
- [ ] Item-Datenbank (Datenquelle klären: NC-Web-API mit Deutsch vs. Spieldateien)
- [ ] Interaktive Karte, vorher eine kurze Aufnahme: Welche Pakete kommen beim Einsammeln? Positionen anderer Spieler NIE anzeigen
- [x] Einstellungen: Hotkey, Deckkraft, nur meine Gruppe, „Du“ statt Name
- [ ] Kampf-Historie ansehen (Daten liegen schon in SQLite)
- [ ] Kampfanalyse nach dem Kampf (Rotation, Uptime der Buffs)
- [ ] Mitlesen ohne Npcap über das in Windows eingebaute pktmon (Idee von a2kit)
- [ ] Spendenlink (Ko-fi)

## Phase 6: Bot-Radar (Idee vom 13.10.2026)
Ziel: Bots an Farm-Spots erkennen und mit Beweisen melden. Das Tool liest nur passiv mit, es sendet nie etwas und meldet nie automatisch.
- [ ] Mitschnitt von 5–10 Minuten an einem Bot-Spot (nach Phase 2)
- [ ] Bewegungspakete finden: Positionen anderer Spieler und die eigene Position
- [ ] Sichtradius aus dem Mitschnitt messen (wo Spieler erscheinen und verschwinden) und prüfen, ob es bei vielen Spielern eine Obergrenze gibt
- [ ] Aufzeichnung je Spieler: Laufweg, Skill-Zeitpunkte, Anwesenheitsdauer
- [ ] Bewertung: Wiederholung der Route, Streuung im Skill-Timing, Reaktionszeit auf Mob-Spawns, Dauer-Anwesenheit, synchrone Gruppen. „Sicher“ nur, wenn mehrere harte Merkmale zusammenkommen.
- [ ] Verdachtsliste im Meter mit Namen zum Kopieren für die Meldung im Spiel
- [ ] Beweis-Export für den NCSoft-Support: Name, Zeitraum, Ort, Laufweg als Grafik, Timing-Statistik
- Mitschnitte und Exporte enthalten Spielernamen. Sie kommen nie ins Repo.
