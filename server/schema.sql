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
