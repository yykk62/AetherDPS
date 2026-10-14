// AetherDPS API – anonyme Tageszähler und Fehlerberichte.
// Datenschutz: Dieser Worker speichert und protokolliert keine IP-Adressen, User-Agents,
// Cookies oder sonstigen Kennungen. Es gibt bewusst keine console.*-Ausgaben.

import { ADMIN_HTML } from "./admin.html";

interface RateLimiter {
  limit(options: { key: string }): Promise<{ success: boolean }>;
}

export interface Env {
  DB: D1Database;
  REPORT_LIMIT: RateLimiter;
  ADMIN_KEY?: string;
}

type Json = Record<string, unknown>;

const KB = 1024;
const VERSION_RE = /^\d{1,3}\.\d{1,3}\.\d{1,3}$/;
const LANGS = new Set(["de", "en", "fr", "es", "pt", "ru", "ja", "ko", "zh-Hans", "zh-Hant"]);
const BASE_HEADERS: Record<string, string> = {
  "X-Content-Type-Options": "nosniff",
  "Cache-Control": "no-store",
};

// ---------- Helfer ----------

function respond(body: BodyInit | null, status: number, headers: Record<string, string> = {}): Response {
  return new Response(body, { status, headers: { ...BASE_HEADERS, ...headers } });
}
const empty = (status: number) => respond(null, status);
const json = (data: unknown, status = 200) =>
  respond(JSON.stringify(data), status, { "Content-Type": "application/json; charset=utf-8" });

const isVersion = (x: unknown): x is string => typeof x === "string" && VERSION_RE.test(x);
const isLang = (x: unknown): x is string => typeof x === "string" && LANGS.has(x);
const isStr = (x: unknown, max: number): x is string => typeof x === "string" && x.length <= max;

/** UTC-Datum YYYY-MM-DD, optional um n Tage verschoben. */
const utcDay = (offsetDays = 0) => new Date(Date.now() + offsetDays * 86_400_000).toISOString().slice(0, 10);

function clampInt(raw: string | null, min: number, max: number, fallback: number): number {
  const n = raw === null ? NaN : Number.parseInt(raw, 10);
  return Number.isFinite(n) ? Math.min(max, Math.max(min, n)) : fallback;
}

/**
 * Liest höchstens maxBytes als JSON-Objekt. Prüft Content-Length und die tatsächlich
 * gelesene Länge. Rückgabe: Objekt oder HTTP-Fehlerstatus (413 zu groß, 400 ungültig).
 */
async function readJson(req: Request, maxBytes: number): Promise<Json | number> {
  const cl = req.headers.get("Content-Length");
  if (cl !== null && !(Number(cl) <= maxBytes)) return 413;
  if (!req.body) return 400;

  const reader = (req.body as ReadableStream<Uint8Array>).getReader();
  const chunks: Uint8Array[] = [];
  let size = 0;
  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    size += value.byteLength;
    if (size > maxBytes) {
      await reader.cancel();
      return 413;
    }
    chunks.push(value);
  }
  const buf = new Uint8Array(size);
  let offset = 0;
  for (const c of chunks) {
    buf.set(c, offset);
    offset += c.byteLength;
  }
  try {
    const v: unknown = JSON.parse(new TextDecoder("utf-8", { fatal: true }).decode(buf));
    return v !== null && typeof v === "object" && !Array.isArray(v) ? (v as Json) : 400;
  } catch {
    return 400;
  }
}

/** Vergleich in konstanter Zeit (über SHA-256, damit die Länge nichts verrät). */
async function keyMatches(given: string | null, expected: string | undefined): Promise<boolean> {
  if (!expected || !given) return false;
  const enc = new TextEncoder();
  const [a, b] = await Promise.all([
    crypto.subtle.digest("SHA-256", enc.encode(given)),
    crypto.subtle.digest("SHA-256", enc.encode(expected)),
  ]);
  return crypto.subtle.timingSafeEqual(a, b);
}

// ---------- Öffentliche Endpunkte ----------

async function ping(req: Request, env: Env): Promise<Response> {
  const b = await readJson(req, 1 * KB);
  if (typeof b === "number") return empty(400);
  const isNew = b.new ?? false;
  if (!isVersion(b.v) || !isLang(b.lang) || typeof isNew !== "boolean") return empty(400);

  await env.DB.prepare(
    `INSERT INTO daily (day, version, lang, users, new_installs) VALUES (?1, ?2, ?3, 1, ?4)
     ON CONFLICT (day, version, lang) DO UPDATE
     SET users = users + 1, new_installs = new_installs + excluded.new_installs`,
  )
    .bind(utcDay(), b.v, b.lang, isNew ? 1 : 0)
    .run();
  return empty(204);
}

async function report(req: Request, env: Env): Promise<Response> {
  // Die IP dient nur als flüchtiger Schlüssel für die Begrenzung und wird nirgends gespeichert.
  const { success } = await env.REPORT_LIMIT.limit({ key: req.headers.get("CF-Connecting-IP") ?? "unknown" });
  if (!success) return empty(429);

  const b = await readJson(req, 256 * KB);
  if (typeof b === "number") return empty(b);
  const { kind, v, lang, os, message } = b;
  const log = b.log ?? null;
  if (
    (kind !== "manual" && kind !== "crash") ||
    !isVersion(v) ||
    !isLang(lang) ||
    !isStr(os, 100) ||
    !isStr(message, 4000) ||
    (kind === "manual" && message.trim() === "") ||
    (log !== null && !isStr(log, 200_000))
  ) {
    return empty(400);
  }

  const createdAt = new Date().toISOString().slice(0, 19) + "Z";
  const res = await env.DB.prepare(
    "INSERT INTO reports (created_at, kind, version, lang, os, message, log) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7)",
  )
    .bind(createdAt, kind, v, lang, os, message, log || null)
    .run();
  return json({ id: res.meta.last_row_id }, 201);
}

// ---------- Admin ----------

function adminPage(): Response {
  const nonce = crypto.randomUUID().replaceAll("-", "");
  return respond(ADMIN_HTML.replaceAll("__NONCE__", nonce), 200, {
    "Content-Type": "text/html; charset=utf-8",
    "Referrer-Policy": "no-referrer",
    "X-Robots-Tag": "noindex, nofollow",
    "X-Frame-Options": "DENY",
    "Content-Security-Policy":
      `default-src 'none'; script-src 'nonce-${nonce}'; style-src 'nonce-${nonce}'; ` +
      "connect-src 'self'; img-src data:; base-uri 'none'; form-action 'none'; frame-ancestors 'none'",
  });
}

async function stats(env: Env, url: URL): Promise<Response> {
  const days = clampInt(url.searchParams.get("days"), 1, 365, 60);
  const latest = "(SELECT MAX(day) FROM daily)";
  const [daily, versions, langs, last] = await env.DB.batch([
    env.DB.prepare(
      `SELECT day, SUM(users) AS users, SUM(new_installs) AS new_installs
       FROM daily WHERE day >= ?1 GROUP BY day ORDER BY day`,
    ).bind(utcDay(-(days - 1))),
    env.DB.prepare(
      `SELECT version, SUM(users) AS users FROM daily WHERE day = ${latest}
       GROUP BY version ORDER BY users DESC, version DESC`,
    ),
    env.DB.prepare(
      `SELECT lang, SUM(users) AS users FROM daily WHERE day = ${latest}
       GROUP BY lang ORDER BY users DESC, lang`,
    ),
    env.DB.prepare(`SELECT ${latest} AS day`),
  ]);
  return json({
    day: (last.results[0] as { day: string | null } | undefined)?.day ?? null,
    daily: daily.results,
    versions: versions.results,
    langs: langs.results,
  });
}

const REPORT_COLS = "id, created_at, kind, version, lang, os, message, status, note";

async function listReports(env: Env, url: URL): Promise<Response> {
  const status = url.searchParams.get("status") ?? "open";
  if (status !== "open" && status !== "closed" && status !== "all") return empty(400);
  const limit = clampInt(url.searchParams.get("limit"), 1, 500, 100);
  const cols = `${REPORT_COLS}, COALESCE(LENGTH(log), 0) AS log_size`;
  const stmt =
    status === "all"
      ? env.DB.prepare(`SELECT ${cols} FROM reports ORDER BY id DESC LIMIT ?1`).bind(limit)
      : env.DB.prepare(`SELECT ${cols} FROM reports WHERE status = ?1 ORDER BY id DESC LIMIT ?2`).bind(status, limit);
  const { results } = await stmt.all<Json & { log_size: number }>();
  return json(results.map((r) => ({ ...r, has_log: r.log_size > 0 })));
}

async function getReport(env: Env, id: number): Promise<Response> {
  const row = await env.DB.prepare(`SELECT ${REPORT_COLS}, log FROM reports WHERE id = ?1`).bind(id).first();
  return row ? json(row) : empty(404);
}

async function setStatus(req: Request, env: Env, id: number): Promise<Response> {
  const b = await readJson(req, 8 * KB);
  if (typeof b === "number") return empty(400);
  const { status } = b;
  const hasNote = "note" in b;
  const note = b.note ?? null;
  if ((status !== "open" && status !== "closed") || (note !== null && !isStr(note, 2000))) return empty(400);

  const row = await env.DB.prepare(
    `UPDATE reports SET status = ?1, note = CASE WHEN ?2 = 1 THEN ?3 ELSE note END
     WHERE id = ?4 RETURNING id, status, note`,
  )
    .bind(status, hasNote ? 1 : 0, note || null, id)
    .first();
  return row ? json(row) : empty(404);
}

async function admin(req: Request, env: Env, url: URL): Promise<Response> {
  // Ohne gültigen Schlüssel sieht alles unter /admin wie eine nicht vorhandene Seite aus.
  if (!(await keyMatches(url.searchParams.get("key"), env.ADMIN_KEY))) return empty(404);

  const { pathname: p } = url;
  const m = req.method;
  if (m === "GET" && p === "/admin") return adminPage();
  if (m === "GET" && p === "/admin/api/stats") return stats(env, url);
  if (m === "GET" && p === "/admin/api/reports") return listReports(env, url);

  let match = /^\/admin\/api\/reports\/(\d{1,12})$/.exec(p);
  if (m === "GET" && match) return getReport(env, Number(match[1]));
  match = /^\/admin\/api\/reports\/(\d{1,12})\/status$/.exec(p);
  if (m === "POST" && match) return setStatus(req, env, Number(match[1]));
  return empty(404);
}

// ---------- Einstieg ----------

export default {
  async fetch(req: Request, env: Env): Promise<Response> {
    try {
      const url = new URL(req.url);
      const p = url.pathname;
      if (req.method === "POST" && p === "/v1/ping") return await ping(req, env);
      if (req.method === "POST" && p === "/v1/report") return await report(req, env);
      if (p === "/admin" || p.startsWith("/admin/")) return await admin(req, env, url);
      return empty(404);
    } catch {
      // Bewusst ohne Protokollierung.
      return empty(500);
    }
  },
} satisfies ExportedHandler<Env>;
