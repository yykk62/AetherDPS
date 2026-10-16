# AetherDPS-Discord

Bot und Einrichtung für den AetherDPS-Discord-Server.

## Bot (`src/index.ts`, Cloudflare Worker `aetherdps-discord`)

Läuft ohne dauerhafte Verbindung zu Discord. Gespeichert wird nichts; was schon gepostet ist, steht im Kanal selbst.

- **`POST https://bot.aetherdps.com/interactions`**: Discord schickt Klicks auf die Rollen-Knöpfe in #willkommen hierher. Klassen-Auswahl (bis zu 3) und die Pings Updates, AION-2-News und Tester werden an- und abgeschaltet. Die Signatur wird geprüft (Ed25519).
- **Cron alle 15 Minuten**:
  - offizielle AION-2-Patch-Notizen (`update_de`) und Ankündigungen (`notice_de`) von `api-global-community.plaync.com` → #aion2-news (Patch-Notizen mit Erwähnung von @AION-News-Ping)
  - neue Releases von `yykk62/AetherDPS` (GitHub) → #ankündigungen mit Erwähnung von @Update-Ping und Download-Knopf. Die Release-Notizen kommen aus `CHANGELOG.md`, also wie immer pflegen
  - beides wird in Server weitergegeben, die dem Kanal folgen

Ausrollen: `npm install`, dann `npx wrangler deploy` (aus diesem Ordner). Secrets: `DISCORD_BOT_TOKEN`, `DISCORD_PUBLIC_KEY` (`wrangler secret put …`). Die Server-, Kanal- und Rollen-IDs stehen in `wrangler.jsonc`.

## Einrichtung (`setup/`)

Python-Skripte, mit denen der Server eingerichtet wurde (über die Discord-API mit dem Bot-Token aus `~/.secrets/aetherdps.env`). Die IDs stehen in `setup/cfg.json`. Die Skripte erkennen Vorhandenes und lassen sich erneut ausführen. Texte ändern: `content.py` anpassen und ausführen, dann werden die bestehenden Nachrichten bearbeitet statt neu gepostet.

- `setup1.py`: Namen, Grundeinstellungen, Rollen, Klassen-Emojis
- `setup2.py`: Kategorien, Kanäle, Forum mit Tags, Community-Modus
- `setup3.py`: AutoMod (Spam, Massen-Erwähnungen, anstößige Wörter, fremde Einladungen und Betrugs-Links), Meldungen in #team-log
- `setup4.py`: Profilbild, Banner und Beschreibung des Bots, Server-Icon
- `content.py`: Inhalte von #willkommen (Regeln, Rollen-Knöpfe), #ankündigungen, #aion2-news, #anleitung, Forum-Leitfaden, #allgemein
- `banner.py`: setzt die Überschriften auf die Banner (Hintergründe mit Nano Banana 2.1 erzeugt)

Die fertigen Grafiken liegen in `assets/`.
