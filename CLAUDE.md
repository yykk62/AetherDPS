# AetherDPS – Hinweise für Claude

Ein quelloffener DPS-Meter für AION 2 (EU), deutsch zuerst, in 10 Sprachen, als Fork von RATmeter (GPL-3.0). Hieß bis 0.5.0 „Aion2Meter“ (Übergang: `LegacyMigration`, Mutex-Name, zusätzliches Release-Asset `Aion2Meter.exe`).

- **Lesen:** `docs/WARTUNG.md` (Aufbau, Upstream-Merge, Patch-Liste), `docs/PROTOKOLL.md`, `docs/PLAN.md` (nächste Schritte)
- **Bauen und testen:** `dotnet build AetherDPS.slnx`, `dotnet test AetherDPS.Tests`. Es braucht das .NET-10-SDK. `dotnet` liegt nicht im PATH, also den vollen Pfad `C:\Program Files\dotnet\dotnet.exe` verwenden.
- **Oberfläche ohne Bildschirm prüfen:** `dotnet test AetherDPS.UiTests` rendert alle Fenster unsichtbar. Mit `AETHERDPS_SHOTS=<ordner>` werden die Bilder gespeichert. So lässt sich das Aussehen prüfen, ohne dass Fenster über einem laufenden Spiel aufgehen
- **README-Bilder** (`docs/media/`): `AETHERDPS_README=<ordner>` (+ `AETHERDPS_README_FROM=%LOCALAPPDATA%\AetherDPS` für den eigenen Charakter mit echtem Profil, ausdrücklich so gewünscht), `dotnet test AetherDPS.UiTests --filter ReadmeMedia`, dann `python tools/readme-media.py <ordner>`. Mitspieler nur mit Pseudonymen, nie fremde echte Namen; der eigene Name steht nie im Code
- **Nie echte Fenster öffnen, solange AION2.exe läuft** (der Nutzer spielt dann). Wiedergaben laufen mit eigenem Datenordner
- **Ohne Spiel testen:** `aetherdps-cli demo x.txt`, danach `AetherDPS.exe --replay x.txt`
- **Upstream-Dateien** (`AionDpsMeter.*`) nur minimal ändern und jede Änderung in die Tabelle in `docs/WARTUNG.md` eintragen
- **Mitschnitte** enthalten Spielernamen. Sie werden nie committet und nie öffentlich geteilt.
- **Texte** in der Oberfläche stehen in `AetherDPS.App/Lang/de.json` (Quelle) und den anderen Sprachdateien und werden über `{l:T schlüssel}` bzw. `Loc.T()` eingebunden (auch in der Engine). Keine fest eingebauten Texte. Neue Schlüssel in **alle** Sprachdateien eintragen, prüfen mit `python tools/check-lang.py`.
- **Was nach außen geht, ist abschließend:** Update-Prüfung, status.json lesen, der tägliche Nutzungszähler (nur mit Zustimmung, nur `{v, lang, new}`), Fehlerberichte (nur per Klick, Namen entfernt), Discord-Webhook (nur per Klick, eigene URL) und Charakter-Profile bei NCSOFT (`Community/Armory.cs`: Name + Server an die offizielle Such-/Profil-API, nur wenn der Nutzer den Reiter Profil öffnet, sucht/klickt oder – abschaltbar, `PartyJoinPopup` – jemand der Gruppe beitritt; Bilder nur von assets.playnccdn.com und profileimg.plaync.com). Alles in `Engine/Community/CommunityService.cs`, der Server in `server/`. Nichts darüber hinaus (keine Kampfdaten, keine Bestenlisten, keine IDs), und jede Änderung daran in README und Datenschutzerklärung nachziehen.
- **Server (`server/`)** ist ein Cloudflare Worker (Konto des Nutzers, `wrangler deploy` aus `server/`). Statistik und Fehlerberichte: `/admin?key=…`, Schlüssel in `~/.secrets/aetherdps.env` (nie ins Repo). Offene Berichte abarbeiten: `GET /admin/api/reports?key=…&status=open`.
- **Nachrichten an alle** (z. B. „Update kommt heute Abend“): Eintrag in `status.json` auf main pushen (`messages[]` mit `id`, `level` info/warn, `text` je Sprache, optional `until`, `minVersion`, `maxVersion`); Discord-Link als `discord`. Nur nach Rückfrage beim Nutzer pushen.
- **Beim Testen** kein hörbarer Ton, und die Test-Instanz der App danach beenden
