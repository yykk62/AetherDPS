---
layout: ../layouts/Prose.astro
title: Datenschutz
description: Datenschutzerklärung für die Webseite und die App AetherDPS
---

<!-- Abgeleitet aus docs/DATENSCHUTZ-ENTWURF.md; ändert sich dort oder in der App etwas, hier nachziehen. -->

## 1. Verantwortlicher

Verantwortlich ist der im [Impressum](/impressum) genannte Betreiber dieser Seite und der App.
E-Mail: [kontakt@aetherdps.com](mailto:kontakt@aetherdps.com)

## 2. Diese Webseite

### Hosting
Die Seite liegt bei Cloudflare, Inc. (Cloudflare Workers). Beim Aufruf erhält Cloudflare technisch bedingt deine IP-Adresse, die aufgerufene Adresse, Zeitpunkt und Browser-Angaben, um die Seite auszuliefern und Angriffe abzuwehren. Wir werten diese Daten nicht aus. Rechtsgrundlage: Art. 6 Abs. 1 lit. f DSGVO (sichere, schnelle Auslieferung). Datenschutzhinweise von Cloudflare: <https://www.cloudflare.com/privacypolicy/>

### Keine Cookies, kein Tracking
Die Seite setzt keine Cookies, nutzt keine Analyse- oder Werbedienste und lädt keine Schriften oder Skripte von fremden Servern. Alle Bilder liegen auf unserem eigenen Server, außer den Profilbildern und Icons auf „Mein Charakter“ (siehe unten).

### Speicher im Browser
Die Checkliste speichert deine Charakternamen und Haken im Speicher deines Browsers (localStorage). Diese Daten verlassen deinen Browser nie und lassen sich über die Browser-Einstellungen löschen. „Mein Charakter“ speichert dort Name und Server deines Charakters (siehe unten). Das Speichern ist für die von dir gewünschte Funktion unbedingt erforderlich (§ 25 Abs. 2 Nr. 2 TDDDG).

### Mein Charakter
Auf der Seite „Mein Charakter“ wählst du einmal Charaktername und EU-Server; beides liegt nur im localStorage deines Browsers und lässt sich mit „Trennen“ löschen. Bei jedem Besuch der Seite fragt dein Browser damit unseren Server bei Cloudflare ab, und der fragt bei NCSOFT (api-search.plaync.com, aion2.plaync.com) die öffentlichen Profildaten ab, die NCSOFT auch auf aion2.plaync.com zeigt (Stufe, Ausrüstung, Werte, Daevanion, Titel, Skills). Übertragen werden nur Name und Server. Das Ergebnis hält der Server höchstens eine Stunde im Cache, an NCSOFT geht höchstens eine Anfrage pro Sekunde. Gegen Missbrauch sind je IP-Adresse höchstens 10 Abrufe pro Minute möglich; die IP-Adresse dient dafür nur kurz im Arbeitsspeicher als Schlüssel und wird nicht gespeichert, protokolliert wird nichts. Profilbild, Item- und Skill-Icons lädt dein Browser direkt von NCSOFT (assets.playnccdn.com, profileimg.plaync.com, ohne Referrer); NCSOFT erhält dabei technisch bedingt deine IP-Adresse. Rechtsgrundlage: Art. 6 Abs. 1 lit. f DSGVO (von dir gewünschte Anzeige öffentlich abrufbarer Spieldaten), für den Speicher im Browser § 25 Abs. 2 Nr. 2 TDDDG. Der Quellcode des Servers ist öffentlich: <https://github.com/yykk62/AetherDPS/tree/main/server>

### E-Mail
Schreibst du an eine Adresse unter aetherdps.com, nimmt Cloudflare (Email Routing) die Nachricht an und leitet sie an ein Postfach bei Google (Gmail) weiter. Wir nutzen deine Nachricht und Adresse nur, um zu antworten, und löschen sie, wenn sie nicht mehr gebraucht werden. Rechtsgrundlage: Art. 6 Abs. 1 lit. f DSGVO (Beantwortung deiner Anfrage). Datenschutzhinweise von Google: <https://policies.google.com/privacy>

### Externe Links
Links zu GitHub, YouTube, Twitch, Wakayashi oder QuestLog.gg sind normale Links. Erst wenn du sie anklickst, wird die jeweilige Seite geladen.

## 3. Die App AetherDPS

AetherDPS läuft lokal auf deinem PC. Kampfdaten, Läufe, Einstellungen und Mitschnitte werden ausschließlich dort gespeichert (`%LOCALAPPDATA%\AetherDPS`) und nie übertragen. Es gibt kein Konto, keine Bestenliste und keine Werbung. Die App liest den Netzwerkverkehr von AION 2 passiv mit (über Npcap). Diese Daten, darunter auch Namen anderer Spieler, verarbeitet sie nur im Arbeitsspeicher und in lokalen Dateien.

### a) Update-Prüfung und Neuigkeiten (GitHub)
Beim Start und danach stündlich fragt die App bei GitHub (GitHub Inc., USA) die neueste Version ab und lädt die Datei `status.json`. Dabei wird nichts über dich gesendet; GitHub erhält technisch bedingt deine IP-Adresse. Rechtsgrundlage: Art. 6 Abs. 1 lit. f DSGVO. Abschaltbar in den Einstellungen.

### b) Anonyme Nutzungszahl (nur mit Einwilligung)
Nur wenn du beim ersten Start zustimmst, sendet die App höchstens einmal pro Tag `{"v":"0.7.10","lang":"de","new":false}`: App-Version, Sprache und ob es die erste Nutzung ist. Keine Kennung, kein Name, kein Server. Rechtsgrundlage: Einwilligung (Art. 6 Abs. 1 lit. a DSGVO, § 25 Abs. 1 TDDDG), jederzeit widerrufbar unter Einstellungen → Datenschutz.

### c) Fehlerberichte (nur auf Klick)
Wenn du „Problem melden“ nutzt, werden deine Beschreibung, auf Wunsch das Log, App-Version, Sprache und Windows-Version übertragen. Namen von Spielern und dein Windows-Benutzername werden vorher entfernt. Gespeichert bis der Fehler bearbeitet ist, höchstens 12 Monate.

### d) Charakter-Profile
Die App fragt bei NCSOFT (api-search.plaync.com, aion2.plaync.com) Charaktername und Server ab und zeigt die öffentlichen Profildaten, die NCSOFT auch auf seiner Webseite zeigt. Das passiert nur, wenn du den Reiter Profil öffnest, selbst suchst oder klickst, (abschaltbar) wenn jemand deiner Gruppe beitritt und (abschaltbar) alle 4 Stunden für dein eigenes Profil, solange die App läuft; Kampfkraft und Itemstufe hält sie dann als Verlauf nur auf deinem PC fest. Skill- und Item-Bilder lädt die App einmal von NCSOFT (assets.playnccdn.com) und bewahrt sie im Datenordner auf; NCSOFT erhält dabei technisch bedingt deine IP-Adresse. Profildaten und Profilbilder anderer Spieler sowie die Werte einzelner Ausrüstungsteile bewahrt die App bis zu 6 Stunden im Datenordner auf, damit sie beim Wiedersehen nicht erneut abgefragt werden.

### e) Discord-Webhook (nur auf Klick)
Teilst du einen Lauf in deinen Discord-Kanal, geht der Bericht an die von dir eingetragene Webhook-Adresse.

### f) Discord-Profil (nur mit Einwilligung)
Schaltest du unter Einstellungen → Datenschutz „Im Discord-Profil anzeigen“ ein, teilt die App dem Discord-Programm auf deinem PC über dessen lokale Schnittstelle mit, was du gerade machst (z. B. „AION 2 · Dungeon: Draupnir“, „Bosskampf: …“ oder „In Bereitschaft“, mit Startzeit). Die App selbst sendet dabei nichts ins Internet und nichts an uns. Discord (Discord Inc., USA) zeigt diese Angaben in deinem Profil, so wie bei jedem Spiel, nach deinen Discord-Einstellungen und der Datenschutzerklärung von Discord: <https://discord.com/privacy>. Spieler- und Charakternamen werden nie übermittelt, Dungeon- und Bossnamen lassen sich ausblenden. Rechtsgrundlage: Einwilligung (Art. 6 Abs. 1 lit. a DSGVO), standardmäßig aus und jederzeit abschaltbar.

### g) Empfänger
Nutzungszahl und Fehlerberichte verarbeitet ein Server bei Cloudflare, Inc. als Auftragsverarbeiter. Die IP-Adresse wird nicht gespeichert; gegen Spam merkt sich der Server bis Tagesende nur einen gekürzten Hash mit täglich neuem Zufallswert. Der Quellcode des Servers ist öffentlich: <https://github.com/yykk62/AetherDPS/tree/main/server>

## 3a. Discord-Server und Discord-Bot

Unser Discord-Server und der Bot „AetherDPS“ laufen über Discord (Discord Inc., USA); dort gilt zusätzlich die Datenschutzerklärung von Discord: <https://discord.com/privacy>. Der Bot ist ein Programm auf unserem Server bei Cloudflare und bekommt von Discord nur die Befehle, die du selbst auslöst (mit deiner Discord-Nutzer-ID, deinen Rollen auf unserem Server und den Angaben im Befehl).

- **Rollen-Knöpfe:** Der Bot gibt oder nimmt dir die gewählte Rolle auf unserem Server. Gespeichert wird dabei nichts außerhalb von Discord.
- **/charakter, /vergleich, Rechtsklick „AION-Charakter“:** Wie bei „Mein Charakter“ (siehe oben) gehen Charaktername und Server an NCSOFT, das Ergebnis liegt höchstens eine Stunde im Cache. Je Discord-Nutzer sind höchstens 6 Abfragen pro Minute möglich; die Nutzer-ID dient dafür nur kurz im Arbeitsspeicher als Schlüssel.
- **/ich verknüpfen:** Wir speichern deine Discord-Nutzer-ID zusammen mit Charaktername und Server, damit `/charakter` ohne Angaben funktioniert und andere deinen Charakter per Rechtsklick auf dich sehen können. Auf unserem Server setzt der Bot dabei deine Klassen-Rolle. `/ich lösen` löscht die Verknüpfung sofort. Rechtsgrundlage: Einwilligung (Art. 6 Abs. 1 lit. a DSGVO).
- **Automatische Beiträge:** Alle 15 Minuten liest der Bot die öffentlichen Neuigkeiten von aion2.plaync.com und die Versionen auf GitHub und postet neue Beiträge. Dabei werden keine Daten von dir übertragen.

Protokolliert wird nichts. Der Quellcode des Bots ist öffentlich: <https://github.com/yykk62/AetherDPS/tree/main/discord-bot>

## 4. Deine Rechte

Auskunft, Berichtigung, Löschung, Einschränkung, Widerspruch, Datenübertragbarkeit und Widerruf einer Einwilligung (Art. 15–21, Art. 7 Abs. 3 DSGVO) sowie Beschwerde bei einer Datenschutz-Aufsichtsbehörde.

Stand: 16.10.2026
