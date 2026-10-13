# Wartung

## Aufbau

| Ordner | Herkunft | Inhalt |
|---|---|---|
| `AionDpsMeter.Core`, `AionDpsMeter.Services` | **Upstream** (RATmeter) | Modelle, Spieldaten, Capture, Paketverarbeitung, Sessions |
| `AionDpsMeter.UI`, `AionDpsMeter.Updater` | Upstream | WPF-Oberfläche von RATmeter. Bleibt **unangetastet** im Repo, damit Merges sauber bleiben, und wird von uns nicht gebaut |
| `Aion2Meter.Engine` | unser Code | Verdrahtung, Ringpuffer, Wiedergabe, deutsche Namen, synthetische Pakete |
| `Aion2Meter.App` | unser Code | Avalonia-Overlay, Texte in `Lang/*.json` |
| `Aion2Meter.Cli` | unser Code | `replay` (Mitschnitt auswerten), `demo` (künstlicher Kampf) |
| `Aion2Meter.Tests` | unser Code | Tests der gesamten Pipeline |

## Upstream-Fixes übernehmen (nach jedem AION-2-Patch)

```
git fetch upstream
git log --oneline main..upstream/master        # was ist neu?
git merge upstream/master
dotnet build Aion2Meter.slnx && dotnet test Aion2Meter.Tests
```

Für `README.md` gilt `merge=ours` (siehe `.gitattributes`). Einmal pro Klon muss dafür dieser Befehl laufen:
`git config merge.ours.driver true`

### Unsere Änderungen an Upstream-Dateien (bei Konflikten beachten)

| Datei | Änderung | Grund |
|---|---|---|
| `AionDpsMeter.Services/PacketCapture/TcpStreamBuffer.cs` | Event `RawDataAdded` | Ringpuffer für „Problem melden“ |
| `AionDpsMeter.Services/PacketCapture/PacketAccumulator.cs` | Sync-Suche über `buffer.AsSpan(0, length)` | **Bug in Upstream:** `IndexOfArray(pattern, length)` nimmt `length` als Start-Offset und sucht daher nur hinter den gültigen Daten. Die Synchronisierung klappte nur zufällig über alte Pufferreste. → Bug bei RATmeter melden |
| `AionDpsMeter.Services/PacketProcessing/Routing/PacketDispatchService.cs` | Property `Gate` (Filter pro Opcode) | Bereitschaftsmodus: ohne STRG+W wird kein Kampf erfasst, nur Namen und Gruppe |
| `AionDpsMeter.Services/Services/Session/CombatSessionManager.cs` | Event `DamageRecorded` vor `RouteToTargetEntry` | Lauf-Modus (`RunTracker`): Upstream zählt pro Ziel, wir zählen den ganzen Lauf seit STRG+W |
| `AionDpsMeter.Services/Services/Entity/EntityTracker.cs` + `PacketProcessing/Processors/PartyProcessor.cs` | Event `PartyUpdated` und `RaisePartyUpdated(list)` nach dem Gruppen-Paket | Filter „nur meine Gruppe“ (`PartyRoster`) |

So wenig wie möglich in Upstream-Dateien ändern. Lieber in der Engine erweitern.

## Spieldaten

- Englische Skills und Mobs kommen mit jedem Upstream-Merge (`AionDpsMeter.Core/GameData/Assets`).
- Die deutschen Namen aktualisiert `powershell -File tools\update-german-data.ps1` (Quelle: A2Tools).
- Fehlt ein deutscher Name, wird der englische angezeigt.

## Fehler aus „Problem melden“ bearbeiten

1. Der Nutzer schickt `%LOCALAPPDATA%\Aion2Meter\Mitschnitte\problem_*.txt` und die Logs aus `...\logs`.
2. Zum Reproduzieren: `aion2meter-cli replay problem_….txt` oder die App mit `--replay`.
3. Den Mitschnitt nach `Aion2Meter.Tests/Captures/` legen. Der Test `Echte_Mitschnitte_laufen_fehlerfrei_durch` spielt alle Mitschnitte dort ab.
   **Der Ordner ist per .gitignore ausgeschlossen**, weil Mitschnitte Spielernamen enthalten. Er wird nie committet.

## Release

Noch offen, siehe [PLAN.md](PLAN.md).
