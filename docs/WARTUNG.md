# Wartung

## Aufbau

| Ordner | Herkunft | Inhalt |
|---|---|---|
| `AionDpsMeter.Core`, `AionDpsMeter.Services` | **Upstream** (RATmeter) | Modelle, Spieldaten, Capture, Paketverarbeitung, Sessions |
| `AionDpsMeter.UI`, `AionDpsMeter.Updater` | Upstream | WPF-Oberfläche von RATmeter. Bleibt **unangetastet** im Repo, damit Merges sauber bleiben, und wird von uns nicht gebaut |
| `AetherDPS.Engine` | unser Code | Verdrahtung, Ringpuffer, Wiedergabe, deutsche Namen, synthetische Pakete |
| `AetherDPS.App` | unser Code | Avalonia-Overlay, Texte in `Lang/*.json` |
| `AetherDPS.Cli` | unser Code | `replay` (Mitschnitt auswerten), `demo` (künstlicher Kampf) |
| `AetherDPS.Tests` | unser Code | Tests der gesamten Pipeline |

## Upstream-Fixes übernehmen (nach jedem AION-2-Patch)

```
git fetch upstream
git log --oneline main..upstream/master        # was ist neu?
git merge upstream/master
dotnet build AetherDPS.slnx && dotnet test AetherDPS.Tests
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
| `AionDpsMeter.Services/PacketCapture/CaptureDevice.cs` | `ServerAddress`, Zähler `TcpSegments`/`TcpGaps`/`TcpRetransmits` | Verbindungs-Monitor |
| `AionDpsMeter.Services/Services/Session/CombatSessionManager.cs` | Events `BuffRecorded`, `SkillCooldownRecorded`, `PlayerDied` | Buff-Leiste, Kampfanalyse |
| `AionDpsMeter.Services/PacketProcessing/Processors/EntityDeathProcessor.cs` + `EntityTracker.cs` | `RaiseEntityDied` / Event `EntityDied` für jede Einheit | Boss-Timer, Boss-Kill-Zeiten im Lauf |
| `AionDpsMeter.Services/Services/Entity/EntityTracker.cs` | Event `UserIdentified` in `SetSessionPlayerName` | Charakter dauerhaft merken (`SelfIdentity`) |

| `AionDpsMeter.Core/Data/ServerMap.cs` | `GetName` liefert `#<id>` statt `""` für unbekannte IDs | EU-Server fehlen in der Liste |

| `AionDpsMeter.Services/PacketProcessing/Routing/PacketDispatchService.cs` | Property `Observer` (sieht jedes Paket vor dem Gate) | Dungeon-Erkennung (0x3616), Stagger-Leiste (0xE005) |
| `AionDpsMeter.Services/Services/Session/CombatSessionManager.cs` | Event `RawDamage` + `RaiseRawDamage` | Todes-Rückblick, Stagger-Zuordnung (auch Treffer von Mobs) |
| `AionDpsMeter.Services/PacketProcessing/Processors/DamagePacketProcessor.cs` | `RaiseRawDamage` nach dem Parsen; Skill-Code 0 nicht mehr verworfen, aber nicht als Spielerschaden gezählt | Mob-Angriffe auf dich |

So wenig wie möglich in Upstream-Dateien ändern. Lieber in der Engine erweitern.

## Spieldaten

- Englische Skills und Mobs kommen mit jedem Upstream-Merge (`AionDpsMeter.Core/GameData/Assets`).
- Die deutschen Namen aktualisiert `powershell -File tools\update-german-data.ps1` (Quelle: A2Tools).
- Fehlt ein deutscher Name, wird der englische angezeigt.

## Fehler aus „Problem melden“ bearbeiten

1. Der Nutzer schickt `%LOCALAPPDATA%\AetherDPS\Mitschnitte\problem_*.txt` und die Logs aus `...\logs`.
2. Zum Reproduzieren: `aetherdps-cli replay problem_….txt` oder die App mit `--replay`.
3. Den Mitschnitt nach `AetherDPS.Tests/Captures/` legen. Der Test `Echte_Mitschnitte_laufen_fehlerfrei_durch` spielt alle Mitschnitte dort ab.
   **Der Ordner ist per .gitignore ausgeschlossen**, weil Mitschnitte Spielernamen enthalten. Er wird nie committet.

## Release

1. Version in `AetherDPS.App/AetherDPS.App.csproj` (`<Version>`) erhöhen und einen Abschnitt `## x.y.z` oben in `CHANGELOG.md` schreiben.
2. Committen, dann `git tag vx.y.z` und `git push origin main vx.y.z`.
3. `.github/workflows/aetherdps-release.yml` baut mit `tools/release.ps1` (Tests, `dotnet publish`, eine einzige exe), schreibt `AetherDPS.exe.sha256` und legt das GitHub-Release mit dem Text aus dem CHANGELOG an.

**Auto-Update** (`AetherDPS.Engine/Updates/UpdateService.cs`, `AetherDPS.App/Services/AppUpdate.cs`):
- Fragt `api.github.com/repos/yykk62/AetherDPS/releases/latest` ab (3 s nach dem Start, dann stündlich). Wird beim Start (erste 3 Min.) eine neue Version fertig geladen und läuft kein Lauf, installiert der Meter sie sofort und startet neu. Drafts und Pre-Releases werden ignoriert.
- Lädt `AetherDPS.exe` nach `%LOCALAPPDATA%\AetherDPS\Update` und übernimmt sie nur, wenn der SHA-256 zu `AetherDPS.exe.sha256` **und** zum `digest`, den GitHub für das Asset meldet, passt.
- Installiert wird beim Beenden über das Tray, beim nächsten Start oder per Knopf „Neu starten“: Die laufende exe wird in `.old` umbenannt, die neue an ihre Stelle kopiert. Bei einem Fehler wird zurückgerollt.
- Nur Builds aus `dotnet publish` aktualisieren sich selbst (Konstante `SELF_UPDATE`). Debug-Builds aus dem Repo tun es nie.
- Testen ohne GitHub: Umgebungsvariable `AETHERDPS_UPDATE_API` auf eine lokale `latest.json` zeigen lassen. Am 13.10.2026 so geprüft: 0.1.0 → 0.1.1 per Knopf und 0.1.1 → 0.1.2 beim nächsten Start.

Die exe entpackt Sprachdateien und Spieldaten beim ersten Start nach `%TEMP%\.net\AetherDPS\<hash>`. Ordner älterer Versionen räumt der Meter beim Start weg.

**Nur ein Meter gleichzeitig:** Ein zweiter Start holt das Hauptfenster der laufenden Instanz nach vorn (Mutex `Local\AetherDPS`). Wiedergaben mit `--replay` sind davon ausgenommen.

**Signatur:** Die exe ist noch nicht signiert, deshalb warnt SmartScreen. Kostenlos für Open-Source-Projekte geht das über die SignPath Foundation (Antrag mit öffentlichem Repo nötig). Wäre ein Schritt in der Release-Pipeline.
