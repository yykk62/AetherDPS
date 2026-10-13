# AION 2 Meter

Kostenloser, quelloffener DPS-Meter für **AION 2**, deutsch zuerst und für die EU-Server gebaut.

- Live-Overlay: Schaden, DPS und Anteil pro Gruppenmitglied, HP des Ziels und Kampfdauer
- Skill-Details per Klick auf einen Spieler
- Deutsche Klassen-, Skill- und Mob-Namen
- **Alles bleibt lokal**: kein Login, kein Upload und keine Bestenliste
- „Problem melden“ speichert die letzten 5 Minuten als Mitschnitt, damit Fehler ohne extra Test-Session behoben werden können

> Inoffizielles Fan-Projekt, nicht mit NCSOFT verbunden. Das Tool liest den Netzwerkverkehr nur **passiv** mit. Es verändert nichts am Spiel und liest keinen Spielspeicher. Die Nutzung erfolgt auf eigene Verantwortung.

## Installation

1. Aion2Meter starten.
2. Fehlt **Npcap**, öffnet sich die Einrichtung. Dort einmal auf „Npcap jetzt installieren“ klicken. Der Meter lädt Npcap von der offiziellen Seite, prüft die Signatur und startet den Installer mit den richtigen Einstellungen. Du bestätigst nur noch (Admin-Abfrage → „Ja“, dann „I Agree“ → „Install“ → „Finish“).
3. AION 2 starten und einloggen. Das Overlay findet die Verbindung von selbst.

Die Einstellungen, die Kampf-Historie, die Logs und die Mitschnitte liegen unter `%LOCALAPPDATA%\Aion2Meter`.

## Bedienung

Beim Start öffnet sich das **Hauptfenster** mit den Reitern DPS-Meter, Verbindung und Einstellungen. Wer es schließt, lässt den Meter im Tray weiterlaufen. Der Reiter **Verbindung** zeigt deinen Spiel-Ping als Verlauf mit Lag-Spitzen und neu gesendeten Paketen. „Verbindung prüfen“ sagt dir, ob ein Problem im Heimnetz, beim Internetanbieter oder auf dem Weg zum Spielserver liegt.

| Was | Wie |
|---|---|
| Meter starten / ausblenden | **STRG+W** im Spiel (in den Einstellungen änderbar). Ohne STRG+W ist der Meter in Bereitschaft und zeichnet keine Kämpfe auf |
| Verschieben | Kopfzeile ziehen |
| Größe ändern | rechten oder unteren Rand ziehen |
| Gesamt / Ziel | Knopf oben. **Gesamt** = aller Schaden seit STRG+W, zum Beispiel ein ganzer Dungeon. **Ziel** = nur der aktuelle Gegner |
| Zurücksetzen | ⟲ |
| Ausblenden (Bereitschaft) | ✕. Ganz beenden über das Tray-Symbol → „Beenden“ |
| Skill-Details | ☰ oder auf einen Spieler klicken |
| Sperren (Mausklicks gehen ins Spiel, nichts wird versehentlich angeklickt) | Schloss-Symbol. Zum Entsperren wieder aufs Schloss klicken, es bleibt immer klickbar |
| Einstellungen | ⚙ oder Tray-Symbol: Hotkey ändern, Deckkraft, nur meine Gruppe (Standard: an), „Du“ statt Name |
| Screenshot auf den Zweitbildschirm | **STRG+S** im Spiel (änderbar). Der Spiel-Bildschirm erscheint im Vollbild auf dem anderen Monitor, jeder neue ersetzt das alte Bild. Klick oder ESC blendet aus |
| Fehler melden | Tray-Symbol → „Problem melden“ |

## Für Entwickler

```
dotnet build Aion2Meter.slnx
dotnet test Aion2Meter.Tests
Aion2Meter.App\bin\Debug\net10.0\Aion2Meter.exe --replay <mitschnitt.txt> [--speed 1]
Aion2Meter.Cli\bin\Debug\net10.0\aion2meter-cli.exe replay|demo <datei>
```

Aufbau und Wartung stehen in [docs/WARTUNG.md](docs/WARTUNG.md), das Protokoll in [docs/PROTOKOLL.md](docs/PROTOKOLL.md).

## Danke an

- **[RATmeter](https://github.com/Kuroukihime/AIon2-Dps-Meter)** von Kuroukihime (GPL-3.0): Netzwerkmitschnitt, Paketverarbeitung, Kampf-Sessions und die Mob-/Skill-Datenbank. Dieses Projekt ist ein Fork davon.
- **[A2Tools](https://github.com/taengu/A2Tools-DPS-Meter)** von taengu (GPL-3.0): deutsche Skill- und Mob-Namen
- Klassen-Icons: © NCSOFT. Es sind die Icons aus AION 2, übernommen aus RATmeter und als Fan-Inhalt verwendet. Sie fallen nicht unter die GPL.

## Lizenz

GPL-3.0, siehe [LICENSE](LICENSE).
