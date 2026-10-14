# Datenschutzerklärung AetherDPS (Entwurf)

> Entwurf für die Webseite. Keine Rechtsberatung, vor dem Veröffentlichen prüfen lassen oder mit einem Generator abgleichen.
> Platzhalter in [eckigen Klammern] ausfüllen. Hält sich an das, was die App wirklich tut
> (`AetherDPS.Engine/Community/CommunityService.cs`, Server in `server/`). Ändert sich dort etwas, hier nachziehen.

## 1. Verantwortlicher

[Vorname Nachname]
[Anschrift]
E-Mail: [Adresse]

## 2. Grundsatz

AetherDPS läuft lokal auf deinem PC. Kampfdaten, Läufe, Einstellungen und Mitschnitte werden ausschließlich dort gespeichert
(`%LOCALAPPDATA%\AetherDPS`) und nie übertragen. Es gibt kein Konto, keine Bestenliste und keine Werbung.

Die App liest den Netzwerkverkehr des Spiels AION 2 passiv mit (über Npcap), um Schaden auszuwerten. Diese Daten, darunter
auch Namen anderer Spieler in deiner Umgebung, verarbeitet sie nur im Arbeitsspeicher und in lokalen Dateien auf deinem PC.

## 3. Was die App ins Internet schickt

### a) Update-Prüfung und Neuigkeiten (GitHub)
Beim Start und danach stündlich fragt die App bei GitHub (GitHub Inc., USA) die neueste Version ab und lädt die Datei
`status.json` (Nachrichten des Entwicklers, Discord-Link). Dabei wird nichts über dich gesendet; GitHub erhält technisch
bedingt deine IP-Adresse. Rechtsgrundlage: Art. 6 Abs. 1 lit. f DSGVO (berechtigtes Interesse an sicheren, aktuellen Versionen).
Die Update-Prüfung lässt sich in den Einstellungen abschalten. Datenschutzhinweise von GitHub: https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement

### b) Anonyme Nutzungszahl (nur mit Einwilligung)
Nur wenn du beim ersten Start zustimmst, sendet die App höchstens einmal pro Tag ein Signal mit genau diesem Inhalt:
`{"v":"0.6.0","lang":"de","new":false}`, also App-Version, Sprache und ob es die erste Nutzung ist. Es gibt keine
Kennung, keinen Namen und keinen Server. Auf dem Server wird daraus nur eine Tageszählung pro Version und Sprache.
IP-Adressen werden nicht gespeichert.
Rechtsgrundlage: deine Einwilligung (Art. 6 Abs. 1 lit. a DSGVO, § 25 Abs. 1 TDDDG). Du kannst sie jederzeit in den
Einstellungen unter „Datenschutz“ widerrufen.

### c) Fehlerberichte (nur auf Klick)
Wenn du „Problem melden“ nutzt oder nach einem Absturz „Bericht senden“ klickst, werden übertragen: deine Beschreibung,
auf Wunsch das Log der App, die App-Version, die Sprache und die Windows-Version. Namen von Spielern und dein
Windows-Benutzername werden vorher aus dem Log entfernt; du kannst dir vor dem Senden ansehen, was übertragen wird.
Rechtsgrundlage: Art. 6 Abs. 1 lit. a DSGVO (du sendest selbst). Gespeichert bis der Fehler bearbeitet ist,
höchstens [12 Monate].

### d) Empfänger
Nutzungszahl und Fehlerberichte verarbeitet ein Server bei Cloudflare, Inc. (Cloudflare Workers und D1) als
Auftragsverarbeiter. Cloudflare erhält technisch bedingt deine IP-Adresse, um die Anfrage zuzustellen, und begrenzt
damit Fehlerberichte (höchstens 5 pro Minute, dafür nur kurzzeitig zwischengespeichert); dauerhaft gespeichert wird sie nicht. Datenschutzhinweise von Cloudflare:
https://www.cloudflare.com/privacypolicy/
Der Quellcode des Servers ist öffentlich: https://github.com/yykk62/AetherDPS/tree/main/server

## 4. Deine Rechte
Auskunft, Berichtigung, Löschung, Einschränkung, Widerspruch, Datenübertragbarkeit und Widerruf einer Einwilligung
(Art. 15–21 DSGVO, Art. 7 Abs. 3 DSGVO) sowie Beschwerde bei einer Datenschutz-Aufsichtsbehörde. Da die Nutzungszahl
keine Kennung enthält, lassen sich einzelne Signale keiner Person zuordnen.

## 5. Webseite
[Abschnitt für die Webseite selbst ergänzen: Hosting, Server-Logs, ggf. Schriftarten. Möglichst ohne Cookies und ohne Tracking.]

Stand: [Datum]
