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
- [x] Release als eigenständige Einzeldatei: `tools/release.ps1`, Pipeline `aetherdps-release.yml` (13.10.)
- [x] Auto-Update über unsere GitHub-Releases mit SHA-256-Prüfung, nur eine Instanz gleichzeitig (13.10.)
- [ ] Signatur über die SignPath Foundation beantragen (braucht das öffentliche Repo)
- [ ] Sync-Bug als Issue oder PR an RATmeter melden

## Phase 4: Autonome Wartung
- [x] Geplante Aufgabe „aetherdps-wartung“ (täglich 11 Uhr): Upstream-Merge + deutsche Daten auf Wartungs-Branch, Build+Test, nur bei Grün nach main. Kein Push. Log: docs/WARTUNGSLOG.md

## Phase 5: Ausbau
- [x] Hauptfenster mit Reitern (DPS-Meter, Verbindung, Einstellungen), Verbindungs-Monitor mit Ping-Verlauf und Diagnose (13.10.)
- [x] Timer: Risse nach Plan, Feldbosse automatisch beim Kill erkannt (Respawn-Dauer trägt der Nutzer einmal ein), eigene Timer, Erinnerungs-Popups (13.10.)
- [x] Feldboss-Liste mit Server-Zeiten (0x9101), Zeitlimit im Dungeon, Erfassungsquote, erlittener Schaden, Discord-Webhook, EU-Termine (14.10., 0.6.0)
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

## Offen: braucht Testdaten vom Nutzer (Aufnahme heute Abend, Stand 14.10.2026)
Gefunden in der Recherche (Korea/Taiwan/Global, nur offene Lizenzen), aber noch nicht an EU-Daten bestätigt.
Aufnahme: Meter offen lassen (Mitschnitte laufen im Ring mit), danach „Problem melden“ bzw. Mitschnitt aus `%LOCALAPPDATA%\AetherDPS\Mitschnitte`.
- [ ] **Bewerber-Scouter**: selbst eine Gruppe über die Gruppensuche öffnen, ein paar Bewerbungen kommen lassen, annehmen/ablehnen. Pakete `07 97` Anfrage (Name, Klasse, Kampfkraft, Server), `25 97` zurückgezogen, `0B 97` angenommen, `09 97` abgelehnt (TK-open-public MIT, Waffle MIT)
- [ ] **Heilung / Schilde** als eigene Ansicht: Dungeon mit Heiler in der Gruppe. `05 38` Effekt 0x01/0x09/0x0B = Heilung (cyberbadger GPL)
- [ ] **Boss-Zauberleiste** mit FLÄCHE/TÖDLICH: Bosskampf mit Mechaniken. `09 38` Cast mit Dauer (SkeeveAN MIT, docs/protocol/combat-frames.md)
- [ ] **Rotation / Zeitleiste** pro Spieler und Lauf-Vergleich (aus `02 38` Skill-Einsatz)
- [ ] **Kinah pro Stunde**: eine Weile farmen und Beute aufheben. `1b 56` / `11 56` (Nyerk GPL, Waffle CurrencyItemParser MIT)
- [ ] **Checkliste automatisch abhaken** über Eintritts-Tickets: Login + einen Dungeon betreten. `0b 61` Login-Liste, `0c 61` Änderungen, Ticket 60000001 = Od-Energie (SkeeveAN docs/protocol/ticket-frames.md)
- [ ] **Kampf an/aus** (`21 8D`) für exakte Pull-Zeiten und Wipes – Daten schon in der Aufnahme vom 14.10., Abgleich mit Versuchs-Erkennung
- [ ] **Abyss-Feldbosse**: Liste in der Abyss mit offener Karte (Slots 2001–2008, 2201–2205; Zuordnung Waffle vs. SkeeveAN widersprüchlich)
- [ ] Riss-Zeiten nach der Zeitumstellung am 25.10. prüfen

## Ideen aus Daeva Codex (daevacodex.de, 15.10.2026)
- [x] Charakter-Profile über die öffentliche NCSOFT-API (0.6.4, `Armory`): Suche `api-search.plaync.com/aion2global/search/v2/character`, Profil/Ausrüstung `aion2.plaync.com/api/character/{info,equipment}` (region=eu, lang=de-DE)
- [ ] Bewerber-Scouter + Profil: Bewerber automatisch nachschlagen (Itemstufe/Kampfkraft) – braucht `07 97` aus der Aufnahme
- [ ] Timer-Kandidaten von Daeva Codex, dort selbst „für EU unbestätigt“: Beritra-Luftangriff, Dimensionale Invasion (:30), Abyss-Riss/Schlachtfeld (täglich 22:00), wöchentliche Wartung (Mi 08:00). Widerspruch: Daeva Codex Belagerung/Nahma 22:00, TheLoop (aus der spieleigenen Liste) 21:00/21:05 → mit Abyss-Liste in der Aufnahme klären
- [ ] Shugo-Festival: KR :18/:48, Daeva Codex/TheLoop stündlich – EU in der Aufnahme prüfen (Event-Ankündigung im Spiel)
- [ ] Zusammenarbeit anfragen (nur nach OK des Nutzers): Daeva Codex Companion liest die Position aus der Minimap per Bildschirm und schickt sie an 127.0.0.1; AetherDPS kennt die Position exakt aus den Paketen und könnte sie ihnen liefern. Ihre Marker-Daten (Siegel, Monolith-Fragmente, Hidden Cubes) haben keine offene Lizenz → nur mit Erlaubnis

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
