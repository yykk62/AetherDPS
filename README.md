# AION 2 Meter

Kostenloser, quelloffener DPS-Meter für **AION 2**, deutsch zuerst und für die EU-Server gebaut.

- Live-Overlay: Schaden, DPS und Anteil pro Gruppenmitglied, HP des Ziels und Kampfdauer
- Skill-Details per Klick auf einen Spieler
- Deutsche Klassen-, Skill- und Mob-Namen
- **Alles bleibt lokal**: kein Login, kein Upload und keine Bestenliste
- „Problem melden“ speichert die letzten 5 Minuten als Mitschnitt, damit Fehler ohne extra Test-Session behoben werden können

> Inoffizielles Fan-Projekt, nicht mit NCSOFT verbunden. Das Tool liest den Netzwerkverkehr nur **passiv** mit. Es verändert nichts am Spiel und liest keinen Spielspeicher. Die Nutzung erfolgt auf eigene Verantwortung.

## Installation

1. **Npcap** installieren: <https://npcap.com/#download> (Standardoptionen; „WinPcap API-compatible Mode“ anhaken)
2. Aion2Meter starten, dann AION 2 starten und einloggen. Das Overlay findet die Verbindung von selbst.

Die Einstellungen, die Kampf-Historie, die Logs und die Mitschnitte liegen unter `%LOCALAPPDATA%\Aion2Meter`.

## Bedienung

| Was | Wie |
|---|---|
| Verschieben | Kopfzeile ziehen |
| Größe ändern | rechten oder unteren Rand ziehen |
| Zurücksetzen | ⟲ |
| Skill-Details | ☰ oder auf einen Spieler klicken |
| Klick-durch (Mausklicks gehen ins Spiel) | ⇣, zum Ausschalten im Tray-Symbol „Klick-durch an / aus“ wählen |
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

## Lizenz

GPL-3.0, siehe [LICENSE](LICENSE).
