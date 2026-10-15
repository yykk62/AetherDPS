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
Die Seite setzt keine Cookies, nutzt keine Analyse- oder Werbedienste und lädt keine Schriften oder Skripte von fremden Servern. Alle Bilder liegen auf unserem eigenen Server.

### Speicher im Browser
Die Checkliste speichert deine Charakternamen und Haken im Speicher deines Browsers (localStorage). Diese Daten verlassen deinen Browser nie und lassen sich über die Browser-Einstellungen löschen. Das Speichern ist für die von dir gewünschte Funktion unbedingt erforderlich (§ 25 Abs. 2 Nr. 2 TDDDG).

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
Die App fragt bei NCSOFT (api-search.plaync.com, aion2.plaync.com) Charaktername und Server ab und zeigt die öffentlichen Profildaten, die NCSOFT auch auf seiner Webseite zeigt. Das passiert nur, wenn du den Reiter Profil öffnest, selbst suchst oder klickst, oder (abschaltbar) wenn jemand deiner Gruppe beitritt. Gespeichert wird nur 15 Minuten im Arbeitsspeicher.

### e) Discord-Webhook (nur auf Klick)
Teilst du einen Lauf in deinen Discord-Kanal, geht der Bericht an die von dir eingetragene Webhook-Adresse.

### f) Empfänger
Nutzungszahl und Fehlerberichte verarbeitet ein Server bei Cloudflare, Inc. als Auftragsverarbeiter. Die IP-Adresse wird nicht gespeichert; gegen Spam merkt sich der Server bis Tagesende nur einen gekürzten Hash mit täglich neuem Zufallswert. Der Quellcode des Servers ist öffentlich: <https://github.com/yykk62/AetherDPS/tree/main/server>

## 4. Deine Rechte

Auskunft, Berichtigung, Löschung, Einschränkung, Widerspruch, Datenübertragbarkeit und Widerruf einer Einwilligung (Art. 15–21, Art. 7 Abs. 3 DSGVO) sowie Beschwerde bei einer Datenschutz-Aufsichtsbehörde.

Stand: 15.10.2026
