<p align="center">
  <img src="AetherDPS.App/Assets/icon.png" width="72" alt="" />
</p>

<h1 align="center">AetherDPS</h1>

<p align="center">
  <b>Der kostenlose DPS-Meter und Begleiter für AION 2, für die EU-Server gebaut.</b><br />
  Ohne Abo, ohne Konto, ohne Werbung. Deine Kampfdaten bleiben auf deinem PC.
</p>

<p align="center">
  <a href="https://github.com/yykk62/AetherDPS/releases/latest"><img src="https://img.shields.io/github/v/release/yykk62/AetherDPS?label=Version&color=e0a84a&style=flat-square" alt="Version" /></a>
  <a href="https://github.com/yykk62/AetherDPS/releases"><img src="https://img.shields.io/github/downloads/yykk62/AetherDPS/total?label=Downloads&color=3d8bd4&style=flat-square" alt="Downloads" /></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-2b3240?style=flat-square" alt="Windows 10 | 11" />
  <a href="LICENSE"><img src="https://img.shields.io/github/license/yykk62/AetherDPS?color=2b3240&style=flat-square" alt="Lizenz" /></a>
</p>

<p align="center">
  <a href="https://github.com/yykk62/AetherDPS/releases/latest"><b>⬇ AetherDPS.exe herunterladen</b></a>
  ·
  <a href="#funktionen">Funktionen</a>
  ·
  <a href="#bedienung">Bedienung</a>
  ·
  <a href="#häufige-fragen">Häufige Fragen</a>
  ·
  <a href="#in-english">English</a>
</p>

<p align="center">
  <img src="docs/media/hero.png" width="100%" alt="AetherDPS: Hauptfenster mit Pet-Finder, Live-Meter im Bosskampf und Timer-Pills" />
</p>

<p align="center"><sub>🌍 Deutsch · English · Français · Español · Português · Русский · 日本語 · 한국어 · 简体中文 · 繁體中文</sub></p>

## Auf einen Blick

| | |
|---|---|
| ⚔ **[Live-DPS-Meter](#-live-dps-meter)** | Boss mit Lebens- und Stagger-Balken, Schaden, DPS und Anteil deiner Gruppe, Skill-Details per Klick |
| 🏆 **[Läufe-Archiv](#-läufe-archiv)** | Jeder Dungeon mit Boss-Zeiten, Bestzeit und Kampfanalyse, als Bild oder Text für Discord |
| 🧙 **[Profil](#-profil)** | Dein Charakter (oder jeder auf einem EU-Server) ohne laufendes Spiel: Ausrüstung, Werte, Daevanion, Skills |
| 👥 **[Neu in der Gruppe](#-neu-in-der-gruppe)** | Tritt jemand bei, siehst du sofort Kampfkraft, Itemstufe, Verzauberung und Waffe |
| 🐾 **[Pet-Finder & Radar](#-pet-finder--pet-radar)** | Alle Pets mit deiner Stufe, ihre Spawnpunkte auf der Karte und ein Radar zu den fehlenden in deiner Nähe |
| ⏱ **[Timer & Feldbosse](#-timer--feldbosse)** | Echte Respawn-Zeiten vom Server, Risse, Belagerung, eigene Timer als frei verschiebbare Pills |
| 📶 **[Verbindung](#-verbindung--mehr)** | Ping-Verlauf, Lag-Spitzen und die Antwort, ob es an dir, am Anbieter oder am Spielserver liegt |
| ✅ **Checkliste** | Tägliches und Wöchentliches mit automatischem Reset und Erinnerung |
| 🖼 **Screenshot** | STRG+S zeigt das Spielbild sofort auf deinem zweiten Monitor |
| 🌍 **10 Sprachen** | Oberfläche, Skills, Monster, Klassen, Dungeons und Servernamen in deiner Sprache |
| 🔄 **Updates** | Kommen von selbst, geprüft per Prüfsumme. Nach einem Spiel-Patch sagt dir der Meter Bescheid |

## Funktionen

### ⚔ Live-DPS-Meter

<table>
  <tr>
    <td width="52%"><img src="docs/media/overlay.gif" width="100%" alt="Live-Meter im Bosskampf: Balken, DPS, Anteil, Lebens- und Stagger-Balken" /></td>
    <td>

Das Overlay liegt über dem Spiel und zeigt **nur deine Gruppe**: Fremde in der Nähe erscheinen nie, Beschwörungen zählen zu ihrem Besitzer.

- **Boss-Karte** mit Lebensbalken und **Stagger-Leiste**
- Schaden, **DPS** und **Anteil** jedes Mitglieds, Klassen-Icons und -Farben
- **Gesamt** (ganzer Dungeon seit STRG+W) oder nur das **aktuelle Ziel**
- **Auto-Modus:** klappt bei jedem Boss von selbst auf
- **Sperren:** Klicks gehen ins Spiel, nichts wird versehentlich angeklickt
- Der Dungeon wird erkannt (mit Schwierigkeit)

</td>
  </tr>
  <tr>
    <td><img src="docs/media/overlay-skills.png" width="100%" alt="Skill-Details eines Spielers im Overlay" /></td>
    <td>

**☰ oder Klick auf einen Spieler** zeigt seine Skills mit Schaden, Treffern und Krit-Quote, direkt im Overlay.

Nach deinem Tod zeigt der Meter, **woran du gestorben bist**, und läuft ein langer Buff aus, sagt er Bescheid.

</td>
  </tr>
</table>

### 🏆 Läufe-Archiv

Jeder Lauf wird nach dem Dungeon benannt und mit Boss-Zeiten gespeichert. 🏆 markiert deine **Bestzeit**, daneben steht der Vergleich mit deinem Durchschnitt. Die **Kampfanalyse** zeigt pro Spieler Aktivität, Tode, erlittenen Schaden, Schaden nach Skill und Buff-Laufzeit. **Als Bild kopieren**, als Text oder per Klick in deinen eigenen Discord-Kanal.

<p align="center"><img src="docs/media/runs.png" width="100%" alt="Läufe-Archiv mit Bestzeiten und Kampfanalyse" /></p>

### 🧙 Profil

Dein Charakter auf einen Blick, **auch ohne laufendes Spiel**: Kampfkraft, Itemstufe, mittlere Verzauberung, die komplette Ausrüstung, alle Werte mit ihren Wirkungen, Daevanion-Fortschritt je Brett, Titel, Pet, Flügel, Skills und Stigma-Skills. Jeden anderen Charakter auf einem EU-Server findest du per Name. Die Daten sind dieselben öffentlichen wie auf aion2.plaync.com.

<p align="center"><img src="docs/media/profile.png" width="100%" alt="Profil eines Charakters mit Ausrüstung und Werten" /></p>

<details>
<summary><b>Klick auf ein Teil:</b> Werte, Manasteine und Götterstein</summary>
<br />
<p align="center"><img src="docs/media/profile-item.png" width="100%" alt="Details eines Items mit Werten, Manasteinen und Götterstein" /></p>
</details>

<details>
<summary><b>Weiter unten:</b> Daevanion, Titel, Skills und Stigma-Skills</summary>
<br />
<p align="center"><img src="docs/media/profile-skills.png" width="100%" alt="Daevanion-Bretter, Titel, Skills und Stigma-Skills" /></p>
</details>

### 👥 Neu in der Gruppe

Tritt jemand deiner Gruppe bei, erscheint am Bildschirmrand seine Karte mit **Kampfkraft, Itemstufe, Verzauberung, Daevanion und Waffe**. Ein Klick öffnet das ganze Profil. Abschaltbar im Reiter Gruppe. Verfehlt die Gruppe einen Stagger, zeigt der Meter, wer wie viel beigetragen hat.

<p align="center"><img src="docs/media/popups.png" width="100%" alt="Karte „Neu in der Gruppe“ und Stagger-Auswertung" /></p>

### 🐾 Pet-Finder & Pet-Radar

Alle Pets von Verteron, Altgard und der Abyss mit **deiner Stufe und deinem Fortschritt** (liest der Meter beim Einloggen mit), mit Bild und einer Karte aller Spawnpunkte ihrer Monster, samt Gebieten und deiner Position. Filter nach Gattung und Status, Suche nach Pet oder Monster. Jede Gattung hat ihre Farbe: Cognia blau, Fera orange, Natura grün, Varia lila, Specia gelb.

<p align="center"><img src="docs/media/pets.png" width="100%" alt="Pet-Finder mit Liste und Spawnkarte" /></p>

Das **Pet-Radar** erscheint, sobald du im Spiel die Karte öffnest, genau darüber und so groß wie sie, oder bleibt auf Wunsch dauerhaft stehen. Es ist eine **Minimap wie im Spiel**: Du bist immer in der Mitte, sie folgt dir (auch im Flug) und merkt sich deinen Zoom. Darauf die fehlenden Pets in deiner Nähe mit **Entfernung, Richtung und dem Monster, das die Seele droppt**. Klick auf ein Pet zeigt seine Fundorte; liegt keiner im Bild, zoomt die Karte nur so weit heraus, dass der nächste zu sehen ist. Größe per Ecke ziehen, ▾ klappt es zu einer kleinen Leiste ein.

<p align="center"><img src="docs/media/pet-radar.png" width="100%" alt="Pet-Radar: je Gattung, eine Gattung gewählt, ein Pet angeklickt" /></p>

### ⏱ Timer & Feldbosse

Öffnest du im Spiel die Karte, schickt der Server die Feldbosse mit ihren **genauen Respawn-Zeiten**, und der Meter merkt sie sich. Dazu Raumzeit-Risse, Artefakt-Belagerung und eigene Countdowns, alle mit Erinnerung. Im Dungeon läuft das Zeitlimit mit.

<table>
  <tr>
    <td width="50%"><img src="docs/media/timers.png" width="100%" alt="Reiter Timer mit Feldbossen" /></td>
    <td>

Jeder Timer ist eine eigene **Pill** über dem Spiel:

- frei ziehen, grob **nebeneinander, untereinander oder als Block** hinlegen
- Rechtsklick → **Gruppieren**: Die Anordnung wird übernommen (1×3, 3×1, 2×2 …)
- Rechtsklick auch zum **Lösen**, **Entfernen** oder **Sperren**, ohne die App zu öffnen

<img src="docs/media/pills.gif" width="100%" alt="Timer-Pills hinlegen und per Rechtsklick gruppieren" />

</td>
  </tr>
</table>

### 📶 Verbindung & mehr

Der Reiter Verbindung zeigt deinen Spiel-Ping als Verlauf mit Lag-Spitzen und neu gesendeten Paketen. **Verbindung prüfen** sagt dir, ob ein Problem im Heimnetz, beim Internetanbieter oder auf dem Weg zum Spielserver liegt.

<p align="center"><img src="docs/media/connection.png" width="100%" alt="Ping-Verlauf mit Lag-Spitze" /></p>

## Häufige Fragen

**Ist das erlaubt?** Der Meter liest den Netzwerkverkehr nur **passiv** mit, so wie die Spieltools anderer Anbieter auch. Er verändert nichts am Spiel, liest keinen Spielspeicher und sendet nichts ans Spiel. Es ist ein inoffizielles Fan-Projekt, nicht mit NCSOFT verbunden. Die Nutzung erfolgt auf eigene Verantwortung.

**Was kostet das?** Nichts. Der Quellcode ist offen (GPL-3.0), jeder kann nachsehen, was das Programm tut.

**Werden meine Daten hochgeladen?** Deine Kampfdaten, Läufe und Mitschnitte nie. Es gibt kein Konto und keine Bestenliste. Nach außen geht nur:
- die **Update-Prüfung** und das Lesen von Neuigkeiten auf GitHub (es wird nichts gesendet),
- eine **anonyme Nutzungszahl**, nur wenn du beim ersten Start zustimmst: einmal am Tag `{"v":"0.7.4","lang":"de","new":false}`, also Version, Sprache und ob es die erste Nutzung ist. Keine Namen, kein Server, keine Kennung. Jederzeit abschaltbar unter Einstellungen → Datenschutz,
- **Fehlerberichte**, nur wenn du selbst auf „Senden“ klickst. Namen von Spielern werden vorher entfernt, und du siehst vorher, was rausgeht.
- **Charakter-Profile** von NCSOFT (dieselben öffentlichen Daten wie auf aion2.plaync.com): wenn du den Reiter Profil öffnest (dein Charakter), auf jemanden klickst oder sucht, und – abschaltbar im Reiter Gruppe – wenn jemand deiner Gruppe beitritt. Dabei gehen nur Name und Server an NCSOFT.

Der Server dafür ist offen einsehbar im Ordner [`server/`](server/) und speichert keine IP-Adressen. Gegen Spam zählt er je Absender höchstens einen Ping und fünf Berichte pro Tag; dafür merkt er sich bis Tagesende nur einen Hash mit täglich neuem Zufallswert, der danach gelöscht wird.

**Warum warnt Windows beim ersten Start?** Die Datei ist noch nicht digital signiert, das ist in Arbeit. Klick auf „Weitere Informationen“ → „Trotzdem ausführen“.

**Was ist Npcap?** Ein kostenloser Treiber, mit dem Programme den Netzwerkverkehr mitlesen können (auch Wireshark nutzt ihn). Der Meter richtet ihn beim ersten Start mit einem Klick ein.

**Etwas stimmt nicht?** Hauptfenster oder Tray-Symbol → „Problem melden“. Kurz beschreiben, Senden klicken, fertig. Kein Konto und keine E-Mail nötig. Alternativ ein [Issue auf GitHub öffnen](https://github.com/yykk62/AetherDPS/issues).

**Nach einem Spiel-Patch zeigt der Meter nichts mehr?** Das passiert, wenn sich die Daten des Spiels ändern. Der Meter erkennt das und sagt Bescheid. Die angepasste Version kommt meist schnell und installiert sich von selbst.

## Download

**[AetherDPS.exe von der Release-Seite laden](https://github.com/yykk62/AetherDPS/releases/latest)**: eine einzige Datei, keine Installation und kein .NET nötig.

- **Voraussetzung:** Windows 10 oder 11 (64 Bit) und Npcap (richtet der Meter beim ersten Start selbst ein)
- **Wohin damit?** In einen eigenen Ordner, zum Beispiel `Dokumente\AetherDPS`, nicht in den Download-Ordner und nicht nach `C:\Programme` (dort darf sich die Datei nicht selbst aktualisieren)
- **Windows-Warnung „Der Computer wurde durch Windows geschützt“:** Die Datei ist noch nicht digital signiert. Auf „Weitere Informationen“ → „Trotzdem ausführen“ klicken. Wer sichergehen will, vergleicht die SHA-256-Prüfsumme mit der Angabe auf der Release-Seite: `Get-FileHash AetherDPS.exe`
- **Updates kommen automatisch:** Der Meter sieht bei jedem Start auf GitHub nach und installiert eine neue Version gleich dort (er startet kurz neu). Läuft er länger, schaut er stündlich nach und installiert beim nächsten Start. Abschaltbar unter Einstellungen → Updates. Dabei wird nur die öffentliche Release-Seite abgefragt, von dir wird nichts gesendet

## Installation

1. AetherDPS starten.
2. Fehlt **Npcap**, öffnet sich die Einrichtung. Dort einmal auf „Npcap jetzt installieren“ klicken. Der Meter lädt Npcap von der offiziellen Seite, prüft die Signatur und startet den Installer mit den richtigen Einstellungen. Du bestätigst nur noch (Admin-Abfrage → „Ja“, dann „I Agree“ → „Install“ → „Finish“).
3. AION 2 starten und einloggen. Das Overlay findet die Verbindung von selbst.

Die Einstellungen, die Kampf-Historie, die Logs und die Mitschnitte liegen unter `%LOCALAPPDATA%\AetherDPS`.

## Bedienung

**Reiter im Hauptfenster:**
- **DPS-Meter:** Status und aktueller Lauf
- **Läufe:** Archiv aller Läufe mit Boss-Zeiten, Bestzeit 🏆, Vergleich mit deinem Durchschnitt, Kampfanalyse pro Spieler sowie „Als Bild/Text kopieren“ für Discord
- **Gruppe:** Mitglieder mit Server, Stufe und Kampfkraft (nur für dich sichtbar), Klick öffnet das Profil. Hier lässt sich „Neu in der Gruppe“ abschalten
- **Profil:** dein Charakter oder jeder andere auf einem EU-Server
- **Pets:** Pet-Finder mit Karte und die Einstellung fürs Pet-Radar (solange die Spielkarte offen ist, immer oder aus)
- **Timer:** Feldbosse (★ = als Pill anzeigen und erinnern), Risse, Artefakt-Belagerung, eigene Countdowns, Timer-Pills an/aus und entsperren
- **Checkliste:** Tägliches und Wöchentliches mit automatischem Reset und Erinnerung
- **Verbindung** und **Einstellungen**

Wer das Hauptfenster schließt, lässt den Meter im Tray weiterlaufen.

| Was | Wie |
|---|---|
| Meter starten / ausblenden | **STRG+W** im Spiel (in den Einstellungen änderbar). Ohne STRG+W ist der Meter in Bereitschaft und zeichnet keine Kämpfe auf |
| Automatisch erfassen | Einstellungen → „Automatisch erfassen“. Der Meter bleibt als schmale Leiste mit Name und Server stehen und klappt bei jedem Bosskampf von selbst auf. STRG+W blendet ihn dann nur ein und aus |
| Knöpfe | erscheinen, sobald die Maus über dem Meter ist |
| Verschieben | Boss-Karte oder Leiste ziehen |
| Breite ändern | rechten Rand ziehen. Die Höhe passt sich der Gruppe an |
| Gesamt / Ziel | Knopf oben. **Gesamt** = aller Schaden seit STRG+W, zum Beispiel ein ganzer Dungeon. **Ziel** = nur der aktuelle Gegner |
| Zurücksetzen | ⟲ |
| Ausblenden (Bereitschaft) | ✕. Ganz beenden über das Tray-Symbol → „Beenden“ |
| Skill-Details | ☰ oder auf einen Spieler klicken |
| Sperren (Mausklicks gehen ins Spiel) | Schloss-Symbol. Zum Entsperren wieder aufs Schloss klicken, es bleibt immer klickbar |
| Timer-Pills | ziehen; Rechtsklick → Gruppieren, Lösen, Entfernen, Sperren. Entsperren im Reiter Timer |
| Pet-Radar | erscheint mit der Spielkarte (Reiter Pets: auch „Immer“). Mausrad zoomt (wird gemerkt), Ziehen schaut kurz woanders hin, nach 5 s ist die Karte wieder bei dir. ✕ blendet es bis zum nächsten Öffnen aus |
| Einstellungen | ⚙ oder Tray-Symbol: Hotkey ändern, Deckkraft, nur meine Gruppe (Standard: an), „Du“ statt Name |
| Screenshot auf den Zweitbildschirm | **STRG+S** im Spiel (änderbar). Der Spiel-Bildschirm erscheint im Vollbild auf dem anderen Monitor, jeder neue ersetzt das alte Bild. Klick oder ESC blendet aus |
| AION 2 starten | Knopf im Hauptfenster oder Einstellung „AION 2 mit dem Meter starten“ (Steam). Läuft der Meter beim Login schon, kennt er deinen Charakter sofort |
| Fehler melden | Hauptfenster oder Tray-Symbol → „Problem melden“ |
| Sprache | Einstellungen → Sprache (Standard: wie Windows) |

## Für Entwickler

```
dotnet build AetherDPS.slnx
dotnet test AetherDPS.Tests
dotnet test AetherDPS.UiTests      # rendert alle Fenster unsichtbar; AETHERDPS_SHOTS=<ordner> speichert Bilder
AetherDPS.App\bin\Debug\net10.0\AetherDPS.exe --replay <mitschnitt.txt> [--speed 1]
AetherDPS.Cli\bin\Debug\net10.0\aetherdps-cli.exe replay|demo <datei>
```

Aufbau und Wartung stehen in [docs/WARTUNG.md](docs/WARTUNG.md), das Protokoll in [docs/PROTOKOLL.md](docs/PROTOKOLL.md). Der kleine Server für Nutzungszahl und Fehlerberichte liegt in [`server/`](server/) (Cloudflare Worker), Nachrichten an alle stehen in [`status.json`](status.json).

**Bilder fürs README** (`docs/media/`): `AETHERDPS_README=<ordner>` setzen (optional `AETHERDPS_README_FROM=%LOCALAPPDATA%\AetherDPS` für den eigenen Charakter mit echtem Profil), `dotnet test AetherDPS.UiTests --filter ReadmeMedia`, dann `python tools/readme-media.py <ordner>` (Pillow, ffmpeg).

**Release bauen:** `powershell -ExecutionPolicy Bypass -File tools\release.ps1` → `artifacts\release\AetherDPS.exe` + `.sha256`.
**Veröffentlichen:** Version in `AetherDPS.App.csproj` und `CHANGELOG.md` eintragen, dann `git tag v0.2.0` und `git push origin v0.2.0`. GitHub Actions baut, testet und legt das Release an.

## Danke an

- **[RATmeter](https://github.com/Kuroukihime/AIon2-Dps-Meter)** von Kuroukihime (GPL-3.0): die Grundlage. AetherDPS ist ein Fork davon (Netzwerkmitschnitt, Paketverarbeitung, Kampf-Sessions, Skill- und Mob-Daten)
- **[A2Tools](https://github.com/taengu/A2Tools-DPS-Meter)** von taengu (GPL-3.0): Spieltexte in 10 Sprachen und die Dungeon-Erkennung
- **[Aion-DPS-Meter](https://github.com/SkeeveAN/Aion-DPS-Meter)** von SkeeveTV (MIT): Pet-Daten, Spawnpunkte, Weltkarten und Feldboss-Positionen

Alles mit Lizenzen steht in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Klassen-Icons, Weltkarten, Pet-Porträts sowie Item- und Skill-Icons: © NCSOFT, als Fan-Inhalt verwendet; sie fallen nicht unter die GPL.

## In English

**AetherDPS** is a free, open-source DPS meter and companion overlay for AION 2, built for the EU servers and available in 10 languages (Settings → Language). The screenshots above show the German interface.

- **Live DPS meter** with boss card, health and stagger bars, damage, DPS and share for your party only, skill details and death recap
- **Run archive** named after the dungeon, with boss times, best time and combat analysis, copyable as image or text for Discord
- **Profile** of your character (or anyone on an EU server) even without the game running: gear with manastones and godstone, stats, Daevanion, titles, skills and stigmas
- **Party join cards:** when someone joins, their combat power, item level, enchant level and weapon pop up
- **Pet finder:** every pet with your level and a map of where its monsters spawn, plus your position. The **pet radar** opens with the in-game map (or stays on) and shows the missing pets around you with distance and direction, colour-coded by species
- **Field bosses with the server's real respawn times** (just open the map in game), rifts, Artifact Siege, dungeon time limit, custom timers as **draggable pills** you can group with a right-click
- **Checklist**, **connection monitor**, **second-screen screenshot**, **automatic updates**, a warning after game patches, and **Report a problem** straight from the app

**Download:** [AetherDPS.exe](https://github.com/yykk62/AetherDPS/releases/latest), a single file with no installation needed (Windows 10/11, 64-bit). On first start it sets up Npcap with one click.

**Privacy:** your combat data never leaves your PC. The only outgoing data is the update check, an optional anonymous daily usage count (version and language only, opt-in), problem reports you send yourself and character profile lookups at NCSOFT (name and server only: when you open the Profile tab, look someone up, or – switchable – when someone joins your party). The server code is in [`server/`](server/); it stores no IP addresses and counts at most one ping per sender per day.

**Is it allowed?** The meter only reads network traffic passively. It does not modify the game, read game memory or send anything to the game. It is an unofficial fan project, not affiliated with NCSOFT. Use at your own risk.

## Lizenz

GPL-3.0, siehe [LICENSE](LICENSE).
