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

### d) Charakter-Profile
Die App fragt bei NCSOFT (api-search.plaync.com, aion2.plaync.com) Charaktername und Server ab und zeigt die
öffentlichen Profildaten (Stufe, Ausrüstung, Werte, Daevanion, Titel, Skills), die NCSOFT auch auf seiner Webseite
zeigt. Das passiert nur, wenn du den Reiter „Profil“ öffnest (dein eigener Charakter), selbst einen Namen suchst oder
anklickst, oder – in der Einstellung im Reiter „Gruppe“ abschaltbar – wenn jemand deiner Gruppe beitritt (dann wird
sein Profil kurz eingeblendet). Außerdem fragt die App – unter Einstellungen → Datenschutz abschaltbar – alle 4 Stunden,
solange sie läuft, dein eigenes Profil ab (Name und Server aus den Einstellungen) und hält Kampfkraft und Itemstufe als
Verlauf auf deinem PC fest (Datei profil_verlauf.json im Datenordner; nichts davon geht an uns). Icons und Porträts kommen
von assets.playnccdn.com und profileimg.plaync.com; Skill- und Item-Bilder werden einmal geladen und im Datenordner aufbewahrt
(auch für die Kampfanalyse), dabei erhält NCSOFT technisch bedingt deine IP-Adresse.
Profildaten anderer Spieler liegen nur kurz im Arbeitsspeicher (15 Minuten); die Werte einzelner Ausrüstungsteile
bewahrt die App bis zu 6 Stunden im Datenordner auf (Ordner Profile), damit ein erneuter Klick sofort antwortet. Rechtsgrundlage: Art. 6 Abs. 1 lit. f DSGVO (Anzeige
öffentlich abrufbarer Spieldaten für Gruppe und Spieler; abschaltbar).

### e) Discord-Profil (nur mit Einwilligung)
Schaltest du unter Einstellungen → Datenschutz „Im Discord-Profil anzeigen“ ein, teilt die App dem Discord-Programm auf deinem PC über dessen lokale Schnittstelle mit, was du gerade machst (z. B. „AION 2 · Dungeon: Draupnir“, „Bosskampf: …“ oder „In Bereitschaft“, mit Startzeit). Die App selbst sendet dabei nichts ins Internet und nichts an uns. Discord (Discord Inc., USA) zeigt diese Angaben in deinem Profil, so wie bei jedem Spiel, nach deinen Discord-Einstellungen und der Datenschutzerklärung von Discord: <https://discord.com/privacy>. Spieler- und Charakternamen werden nie übermittelt, Dungeon- und Bossnamen lassen sich ausblenden. Rechtsgrundlage: Einwilligung (Art. 6 Abs. 1 lit. a DSGVO), standardmäßig aus und jederzeit abschaltbar.

### f) Empfänger
Nutzungszahl und Fehlerberichte verarbeitet ein Server bei Cloudflare, Inc. (Cloudflare Workers und D1) als
Auftragsverarbeiter. Cloudflare erhält technisch bedingt deine IP-Adresse, um die Anfrage zuzustellen, und begrenzt
damit Anfragen (höchstens 5 Fehlerberichte bzw. 3 Signale pro Minute, dafür nur kurzzeitig im Arbeitsspeicher). Damit
niemand die Zahlen künstlich aufbläht, zählt je Absender nur ein Signal pro Tag und es gehen höchstens 5 Fehlerberichte
pro Tag ein. Dafür speichert der Server bis zum Ende des Tages nur einen gekürzten Hash aus IP-Adresse und einem
zufälligen, täglich neuen Wert; am nächsten Tag werden dieser Wert und alle Hashes gelöscht, danach ist keine Zuordnung
zu einer IP-Adresse mehr möglich. Die IP-Adresse selbst wird nicht gespeichert. Datenschutzhinweise von Cloudflare:
https://www.cloudflare.com/privacypolicy/
Der Quellcode des Servers ist öffentlich: https://github.com/yykk62/AetherDPS/tree/main/server

## 4. Deine Rechte
Auskunft, Berichtigung, Löschung, Einschränkung, Widerspruch, Datenübertragbarkeit und Widerruf einer Einwilligung
(Art. 15–21 DSGVO, Art. 7 Abs. 3 DSGVO) sowie Beschwerde bei einer Datenschutz-Aufsichtsbehörde. Da die Nutzungszahl
keine Kennung enthält, lassen sich einzelne Signale keiner Person zuordnen.

## 5. Webseite
[Abschnitt für die Webseite selbst ergänzen: Hosting, Server-Logs, ggf. Schriftarten. Möglichst ohne Cookies und ohne Tracking.
Der aktuelle Stand für die Webseite steht in `web/src/pages/datenschutz.md`.]

### Mein Charakter
Auf der Seite „Mein Charakter“ wählst du einmal Charaktername und EU-Server. Beides speichert dein Browser im
localStorage (kein Cookie); löschen kannst du es mit „Trennen“ oder über die Browser-Einstellungen. Bei jedem Besuch
der Seite fragt dein Browser damit unseren Server (Cloudflare Worker, siehe 3e) ab, und der fragt bei NCSOFT
(api-search.plaync.com, aion2.plaync.com) dieselben öffentlichen Profildaten ab wie die App (Stufe, Ausrüstung, Werte,
Daevanion, Titel, Skills). Übertragen werden nur Name und Server. Das Ergebnis hält der Server höchstens eine Stunde im
Cache (danach gelöscht), damit NCSOFT nicht bei jedem Aufruf gefragt wird; an NCSOFT geht höchstens eine Anfrage pro
Sekunde. Gegen Missbrauch sind je IP-Adresse höchstens 10 Abrufe pro Minute möglich; die IP-Adresse dient dafür nur
kurzzeitig im Arbeitsspeicher als Schlüssel und wird nicht gespeichert, protokolliert wird nichts. Bilder und Icons
lädt dein Browser direkt von NCSOFT (assets.playnccdn.com, profileimg.plaync.com, ohne Referrer); NCSOFT erhält dabei
technisch bedingt deine IP-Adresse. Rechtsgrundlage: Art. 6 Abs. 1 lit. f DSGVO (von dir gewünschte Anzeige öffentlich
abrufbarer Spieldaten), für den Speicher im Browser § 25 Abs. 2 Nr. 2 TDDDG.

Stand: [Datum]
