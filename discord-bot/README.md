# AetherDPS-Discord

Bot und Einrichtung für den AetherDPS-Discord-Server.

## Befehle

| Befehl | Was |
|---|---|
| `/charakter [name] [server]` | Profil von NCSOFT mit Knöpfen für Übersicht, Ausrüstung, Werte, Skills, Daevanion. Ohne Angaben: der verknüpfte Charakter |
| `/vergleich` | zwei Charaktere nebeneinander (zweiter leer = der eigene) |
| `/ich verknüpfen · anzeigen · lösen` | Discord-Nutzer ↔ Charakter (KV `LINKS`), setzt auf unserem Server die Klassen-Rolle |
| Rechtsklick → Apps → **AION-Charakter** | verknüpften Charakter eines Mitglieds zeigen |
| `/events` | nächste Events und Resets als Discord-Zeitstempel (jeder sieht seine Ortszeit), Daten aus `web/src/data/events.ts` (Wakayashi) |
| `/feldboss name [getötet] [respawn]` | Gebiet und Respawn; mit *getötet* ein öffentlicher Countdown |
| `/pet name` | Fundorte, Gattung, Monster mit der Seele |
| `/checkliste` · `/patchnotes` · `/aetherdps` · `/hilfe` | Checkliste (Wakayashi), offizielle News, Version + Download, Übersicht |

Die Befehle stehen in `src/commands.json` und werden mit `python setup/commands.py` bei Discord angemeldet (global, für Server und „App hinzufügen“ bei Nutzern). Spieldaten: `python tools/build-data.py` erzeugt `src/gamedata.json` aus `AetherDPS.Engine/GameData`; Zeitpläne und Checkliste importiert der Bot direkt aus `web/src`, damit Webseite und Bot gleich sind.

Charakterprofile holt der Bot über das Service Binding `API` vom Worker `aetherdps-api` (`server/src/character.ts`, gemeinsamer Schlüssel `BOT_KEY`): dieselbe Grenze zu NCSOFT (1 Anfrage/s), derselbe Cache, dazu höchstens 6 Abfragen pro Minute je Discord-Nutzer (`USER_LIMIT`).

Prüfen ohne Discord-Client: `npx esbuild test/preview.ts --bundle --platform=node --format=esm --outfile=dist/preview.mjs`, dann `node dist/preview.mjs <profil.json>` (alle Antworten als JSON). Live mit den echten Bindings: `npx wrangler dev --remote test/live.ts`.

## Bot (`src/index.ts`, Cloudflare Worker `aetherdps-discord`)

Läuft ohne dauerhafte Verbindung zu Discord. Gespeichert werden nur die Verknüpfungen aus `/ich verknüpfen` (KV `LINKS`); was schon gepostet ist, steht im Kanal selbst.

- **`POST https://bot.aetherdps.com/interactions`**: Discord schickt Klicks auf die Rollen-Knöpfe in #willkommen hierher. Klassen-Auswahl (bis zu 3) und die Pings Updates, AION-2-News und Tester werden an- und abgeschaltet. Die Signatur wird geprüft (Ed25519).
- **Cron alle 15 Minuten**:
  - offizielle AION-2-Patch-Notizen (`update_de`) und Ankündigungen (`notice_de`) von `api-global-community.plaync.com` → #aion2-news (Patch-Notizen mit Erwähnung von @AION-News-Ping)
  - neue Releases von `yykk62/AetherDPS` (GitHub) → #ankündigungen mit Erwähnung von @Update-Ping und Download-Knopf. Die Release-Notizen kommen aus `CHANGELOG.md`, also wie immer pflegen
  - beides wird in Server weitergegeben, die dem Kanal folgen

Ausrollen: `npm install`, dann `npx wrangler deploy` (aus diesem Ordner). Secrets: `DISCORD_BOT_TOKEN`, `DISCORD_PUBLIC_KEY`, `BOT_KEY` (`wrangler secret put …`). Die Server-, Kanal- und Rollen-IDs stehen in `wrangler.jsonc`.

## Einrichtung (`setup/`)

Python-Skripte, mit denen der Server eingerichtet wurde (über die Discord-API mit dem Bot-Token aus `~/.secrets/aetherdps.env`). Die IDs stehen in `setup/cfg.json`. Die Skripte erkennen Vorhandenes und lassen sich erneut ausführen. Texte ändern: `content.py` anpassen und ausführen, dann werden die bestehenden Nachrichten bearbeitet statt neu gepostet.

- `setup1.py`: Namen, Grundeinstellungen, Rollen, Klassen-Emojis
- `setup2.py`: Kategorien, Kanäle, Forum mit Tags, Community-Modus
- `setup3.py`: AutoMod (Spam, Massen-Erwähnungen, anstößige Wörter, fremde Einladungen und Betrugs-Links), Meldungen in #team-log
- `setup4.py`: Profilbild, Banner und Beschreibung des Bots, Server-Icon
- `content.py`: Inhalte von #willkommen (Regeln, Rollen-Knöpfe), #ankündigungen, #aion2-news, #anleitung, Forum-Leitfaden, #allgemein
- `banner.py`: setzt die Überschriften auf die Banner (Hintergründe mit Nano Banana 2.1 erzeugt)

Die fertigen Grafiken liegen in `assets/`.
