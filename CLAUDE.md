# AetherDPS – Hinweise für Claude

Ein deutscher, quelloffener DPS-Meter für AION 2 (EU), als Fork von RATmeter (GPL-3.0).

- **Lesen:** `docs/WARTUNG.md` (Aufbau, Upstream-Merge, Patch-Liste), `docs/PROTOKOLL.md`, `docs/PLAN.md` (nächste Schritte)
- **Bauen und testen:** `dotnet build AetherDPS.slnx`, `dotnet test AetherDPS.Tests`. Es braucht das .NET-10-SDK. `dotnet` liegt nicht im PATH, also den vollen Pfad `C:\Program Files\dotnet\dotnet.exe` verwenden.
- **Oberfläche ohne Bildschirm prüfen:** `dotnet test AetherDPS.UiTests` rendert alle Fenster unsichtbar. Mit `AETHERDPS_SHOTS=<ordner>` werden die Bilder gespeichert. So lässt sich das Aussehen prüfen, ohne dass Fenster über einem laufenden Spiel aufgehen
- **Nie echte Fenster öffnen, solange AION2.exe läuft** (der Nutzer spielt dann). Wiedergaben laufen mit eigenem Datenordner
- **Ohne Spiel testen:** `aetherdps-cli demo x.txt`, danach `AetherDPS.exe --replay x.txt`
- **Upstream-Dateien** (`AionDpsMeter.*`) nur minimal ändern und jede Änderung in die Tabelle in `docs/WARTUNG.md` eintragen
- **Mitschnitte** enthalten Spielernamen. Sie werden nie committet und nie öffentlich geteilt.
- **Texte** in der Oberfläche stehen in `AetherDPS.App/Lang/de.json` und werden über `{l:T schlüssel}` bzw. `Loc.T()` eingebunden. Keine fest eingebauten Texte.
- **Nichts hochladen:** kein Telemetrie-Code, keine Bestenlisten. Das ist das Versprechen des Projekts.
- **Beim Testen** kein hörbarer Ton, und die Test-Instanz der App danach beenden
