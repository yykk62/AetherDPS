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

## Recherche Karten-Daten und APIs (15.10.2026)
Karten-Daten (Fundorte, Spawns):
- Alle Live-Maps stammen letztlich aus dem Spielclient (UE 5.3, verschlüsselte `.dat` unter `Content/Data/{Table,Map,WorldMap}`, CUE4Parse kann es seit 06/2026). Wurzel der meisten GitHub-Maps: tc-imba (`aion2-interactive-map`, jetzt `arkive-games/arkive`), Daten CC BY-NC 4.0 → nicht GPL-kompatibel, höchstens als getrennter Laufzeit-Download mit Namensnennung.
- Questlog.gg (92.854 Marker), interactivemap.app, aion2maps.com, gaming.tools: keine Lizenz → nicht verwenden.
- SkeeveAN/Aion-DPS-Meter (MIT, Daten „Eigentum NCSOFT“): `pets/spawns.json` (20.791 Spawns), `maps/gather.json`, `kibelisks.json`, `npc_names.json`, Kalibrierung. Weltkoordinaten in cm = dieselben Einheiten wie Spawn-Paket 0x4136 (Median < 1 m). Karte: linear, Parameter aus `Data/WorldMap` (WorldBoundBox).
- Kartenbilder sind © NCSOFT, nirgends frei. NCSOFT hat keine offizielle Karte.
- Selbst extrahieren widerspricht den ToS (Reverse Engineering) → nicht machen.
Noch ungenutzt aus A2Tools/Daevalog (gleiche GPL-Quelle): `player_summon_npcs.json` (SummonMerger), `abnormals.json` (Buff-Icons, max. Stacks), `dot_skill_ids.json`, `skill_icons.json`; Daevalog: `instance_maps.json`, `attack_skills.json`, `skill_groups.json`.

Offizielle APIs (ohne Login, region=eu, geprüft):
- `aion2.plaync.com/de-de/api/gameinfo/{servers,classes,pcdata}`; `api/character/{info,equipment,equipment/item,daevanion/detail}` (Item-Detail mit Steinen/Substats, Daevanion-Knoten, Titel x/297)
- `api/ranking/list`: seit 09/2026 leer (`season:null`) → später
- Patch-Notes/Ankündigungen: `api-global-community.plaync.com/aion2_global/board/{notice,update,cm_story}_de/article/search/moreArticle?moreSize=5&moreDirection=BEFORE`
- Nicht vorhanden: Legion, Serverstatus/Bevölkerung, Inventar/Kinah, Item-DB für Global (nur TW), Marktpreise (nur Drittseite aion2.exchange, Einzelbetreiber, keine Bedingungen)
- ToS §6: kein Scraping/Crawling, nichts Kommerzielles → nur auf Nutzer-Klick, cachen, ≤1 Anfrage/s

Ideen daraus (offen, neue Verbindungen nach außen → CLAUDE.md/README/Datenschutz mitziehen):
- [x] Reiter Profil (0.7.0): Werte, Daevanion je Brett, Titel, Pet, Flügel, Item-Details (Manasteine, Götterstein)
- [x] Popup beim Gruppenbeitritt (0.7.0, abschaltbar)
- [x] Pet-Finder mit Karte (0.7.0): Daten + Kacheln aus SkeeveAN (MIT) über `tools/update-pet-data.py`, Pet-Liste 0x9000 und Position 0x373C an EU-Aufnahme bestätigt
- [ ] Gear-Vergleich zweier Charaktere (`equipment/item`: Steine, Substats)
- [ ] DPS pro 1000 Kampfkraft im Lauf (Kampfkraft kommt schon im Gruppen-Paket, keine Abfrage nötig)
- [ ] Feldbosse auf der Pet-Karte (Koordinaten in field_bosses.json, Lebt/Tot aus 0x9101)
- Patch-Notes-Wächter: vom Nutzer abgelehnt (15.10.)

Skill-Slots (Frage 15.10.): Die NCSOFT-API (`character/equipment`) kennt nur characterId/serverId/lang, keinen Skill-Slot – sie liefert den Stand, den NCSOFT gespeichert hat (vermutlich der beim Ausloggen aktive Slot), genau wie aion2.plaync.com. Mit der Aufnahme prüfen: Slot 1→2→3 wechseln (je F8) und schauen, ob das Skill-Paket (SkeeveAN `skills` 0x0051, bei uns 0x5100) die Slots enthält.
- [ ] Pet-Radar: Öffnet die Karte zuverlässig 0x9101 auch außerhalb von Feldboss-Gebieten? Position aktualisiert sich nur bei Skills

Pet-Finder, mit der Aufnahme heute Abend prüfen:
- [ ] Pet-Fortschritt live ohne Neu-Einloggen: SkeeveAN nennt `petProgress` 0x0D90 (bei uns 0x900D), `petLevel` 0x1490 (0x9014), `petAdded` 0x0990 (0x9009). Seele einsammeln und Pet hochstufen, dann vergleichen
- [ ] Position: kommt nur bei Skills/Korrekturen. Im offenen Gelände ein paar Skills benutzen und prüfen, ob der Punkt auf der Karte stimmt (Ebenen 101xxx → Verteron 1010 ist geraten)
- [ ] Gebiete (`regions.json`) zeigen teils Zonen, in denen das Monster laut Spawns gar nicht steht → stichprobenartig prüfen

A2Tools, noch ungenutzt (nur intern, erst mit Aufnahme prüfen): `player_summon_npcs.json` (Beschwörungen sicher dem Besitzer zuordnen statt per Klasse), `dot_skill_ids.json`, `resource_restore_skills.json` (MP-/SP-Wiederherstellung nicht als Heilung zählen), `abnormals.json` (max. Stapel, Icon-Namen – Bilder selbst sind nicht dabei), `skill_icons.json` (nur Namen)

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
