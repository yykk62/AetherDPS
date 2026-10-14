# AetherDPS API (Cloudflare Worker)

Kleiner Server für zwei Dinge:

1. **Anonyme Nutzungszahlen:** Die App meldet einmal am Tag Version, Sprache und ob es eine Neuinstallation ist (`POST /v1/ping`). Gespeichert werden nur Tagessummen.
2. **Fehlerberichte:** Freiwillig gesendete Meldungen oder Absturzberichte (`POST /v1/report`).

Dazu gibt es eine passwortgeschützte Admin-Seite (`/admin?key=…`) mit Statistik und Berichtsliste.

## Was gespeichert wird und was nicht

**Gespeichert:**

| Tabelle | Inhalt |
|---|---|
| `daily` | UTC-Datum, App-Version, Sprache, Zähler „Nutzer“, Zähler „Neuinstallationen“. Eine Zeile pro Tag, Version und Sprache, **keine Zeile pro Nutzer**. |
| `reports` | Zeitpunkt (UTC), Art (Meldung/Absturz), App-Version, Sprache, Windows-Version, Nachrichtentext, optional ein Log-Auszug, Bearbeitungsstatus und interne Notiz. |

**Nicht gespeichert und nicht protokolliert:** IP-Adressen, User-Agents, Geräte- oder Nutzerkennungen, Cookies. Der Worker setzt keine Cookies, hat keine `console`-Ausgaben, und Workers Logs/Logpush sind in `wrangler.jsonc` abgeschaltet.

Die IP-Adresse wird nur als flüchtiger Schlüssel für die Begrenzung verwendet (`/v1/report`: 5 pro Minute, `/v1/ping`: 3 pro Minute) (Cloudflare Rate Limiting API, Zähler nur im Arbeitsspeicher des Standorts). Sie wird nirgends abgelegt.

Hinweise:

- Cloudflare verarbeitet als Infrastruktur-Anbieter technisch bedingt die Verbindungsdaten. Im Cloudflare-Dashboard gibt es außerdem anonyme Gesamtzahlen (Anfragen, Fehler, CPU-Zeit) ohne IPs.
- Was in `message` und `log` steht, bestimmt die App. Sie muss vor dem Senden Spielernamen, Benutzernamen und Pfade mit Benutzernamen entfernen.
- „Nutzer“ heißt: Anzahl Pings an diesem Tag. Ohne Kennung kann der Server nicht entduplizieren. Die App darf darum höchstens einmal pro UTC-Tag pingen.

## Endpunkte

| Methode und Pfad | Zweck |
|---|---|
| `POST /v1/ping` | `{ "v": "0.6.0", "lang": "de", "new": false }`, max. 1 KB. Antwort 204, ungültig 400. |
| `POST /v1/report` | `{ "kind": "manual"\|"crash", "v", "lang", "os", "message", "log"? }`, max. 256 KB. Antwort 201 `{ "id": n }`, 400 ungültig, 413 zu groß, 429 zu viele. |
| `GET /admin?key=` | Admin-Seite |
| `GET /admin/api/stats?key=&days=60` | Tageswerte und Verteilung nach Version und Sprache |
| `GET /admin/api/reports?key=&status=open\|closed\|all&limit=100` | Berichtsliste ohne Log (mit `has_log`, `log_size`) |
| `GET /admin/api/reports/:id?key=` | Ein Bericht mit Log |
| `POST /admin/api/reports/:id/status?key=` | `{ "status": "open"\|"closed", "note"?: "…" }` |

Erlaubte Sprachen: `de, en, fr, es, pt, ru, ja, ko, zh-Hans, zh-Hant`. Version: `x.y.z`. Ohne gültigen Schlüssel antworten alle `/admin`-Pfade mit 404. Alles andere ergibt ebenfalls 404.

## Einrichtung

```sh
cd server
npm install

# 1. Datenbank anlegen und die ausgegebene database_id in wrangler.jsonc eintragen
npx wrangler d1 create aetherdps

# 2. Tabellen anlegen
npx wrangler d1 execute aetherdps --remote --file schema.sql

# 3. Admin-Schlüssel setzen (lang und zufällig, z. B. 32+ Zeichen)
npx wrangler secret put ADMIN_KEY

# 4. Veröffentlichen
npx wrangler deploy
```

Die `database_id` ist kein Geheimnis (Zugriff nur mit Cloudflare-Konto) und darf im öffentlichen Repo stehen. Der `ADMIN_KEY` gehört **nie** in eine Datei im Repo. Die `namespace_id` des Rate-Limiters (`1001`) ist eine frei gewählte Zahl, die im Cloudflare-Konto eindeutig sein muss.

Prüfen ohne Veröffentlichen: `npm run check` (TypeScript) und `npx wrangler deploy --dry-run`.

Telemetrie von Wrangler ist über `"send_metrics": false` abgeschaltet. Zusätzlich kann man `WRANGLER_SEND_METRICS=false` setzen.

## Kosten

Pro Nutzer und Tag fällt **eine** Anfrage an (der Ping) und meist eine geschriebene D1-Zeile. Der Free-Plan von Workers erlaubt 100.000 Anfragen pro Tag, D1 im Free-Plan 100.000 geschriebene Zeilen pro Tag (Stand der Cloudflare-Doku, bitte vor dem Start prüfen). Das reicht für deutlich über 50.000 täglich aktive Nutzer. Fehlerberichte fallen daneben kaum ins Gewicht.
