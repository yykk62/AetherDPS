-- AetherDPS – D1-Schema. Anwenden mit:
--   wrangler d1 execute aetherdps --remote --file schema.sql

-- Tageszähler. Keine Zeile pro Nutzer, nur Summen je Tag, Version und Sprache.
CREATE TABLE IF NOT EXISTS daily (
  day          TEXT    NOT NULL,             -- UTC-Datum, YYYY-MM-DD
  version      TEXT    NOT NULL,
  lang         TEXT    NOT NULL,
  users        INTEGER NOT NULL DEFAULT 0,
  new_installs INTEGER NOT NULL DEFAULT 0,
  PRIMARY KEY (day, version, lang)
);

-- Fehlerberichte (manuell oder nach Absturz).
CREATE TABLE IF NOT EXISTS reports (
  id         INTEGER PRIMARY KEY AUTOINCREMENT,
  created_at TEXT NOT NULL,                  -- UTC, ISO 8601
  kind       TEXT NOT NULL,                  -- 'manual' | 'crash'
  version    TEXT NOT NULL,
  lang       TEXT NOT NULL,
  os         TEXT NOT NULL,
  message    TEXT NOT NULL,
  log        TEXT,
  status     TEXT NOT NULL DEFAULT 'open',   -- 'open' | 'closed'
  note       TEXT
);

CREATE INDEX IF NOT EXISTS reports_status_id ON reports (status, id);

CREATE INDEX IF NOT EXISTS reports_created ON reports (created_at);

-- Grenzen gegen Spam (nur für den laufenden Tag, ältere Zeilen löscht der Worker beim Tageswechsel).
-- Zufälliges Salz je Tag; ohne es lässt sich eine Kennung keiner IP mehr zuordnen.
CREATE TABLE IF NOT EXISTS day_salt (
  day  TEXT PRIMARY KEY,                     -- UTC-Datum
  salt TEXT NOT NULL
);

-- Je Absender (gekürzter Hash aus Salz und IP): wie oft heute gepingt bzw. gemeldet.
CREATE TABLE IF NOT EXISTS senders (
  day     TEXT    NOT NULL,
  id      TEXT    NOT NULL,
  pings   INTEGER NOT NULL DEFAULT 0,
  reports INTEGER NOT NULL DEFAULT 0,
  PRIMARY KEY (day, id)
);
