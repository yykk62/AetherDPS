// GET /api/character?name=&server= – öffentliches Charakterprofil von NCSOFT für die Seite „Mein Charakter“
// auf aetherdps.com. Der Browser darf die NCSOFT-API wegen CORS nicht selbst abfragen, darum reicht dieser
// Worker genau das weiter, was die App (AetherDPS.Engine/Community/Armory.cs) abfragt: Suche über
// api-search.plaync.com, Profil und Ausrüstung über aion2.plaync.com, immer region=eu und lang=de-DE.
//
// Regeln (NCSOFT-ToS §6: kein Scraping, nur auf Nutzer-Aktion, cachen, höchstens 1 Anfrage pro Sekunde):
// - Eine Anfrage hier entsteht nur, wenn jemand die Seite mit verbundenem Charakter öffnet oder verbindet.
// - Ergebnisse liegen eine Stunde im Cache (Cache API, sonst kurz im Arbeitsspeicher); dann keine Anfrage an NCSOFT.
// - Zu NCSOFT höchstens 1 Anfrage pro Sekunde: innerhalb eines Abrufs mit Pause, über alle Abrufe per Rate Limiter.
// - Je IP höchstens 10 Abrufe pro Minute; die IP ist nur ein flüchtiger Schlüssel und wird nirgends gespeichert.
// - Der Discord-Bot (/charakter) fragt über ein Service Binding mit BOT_KEY und begrenzt selbst je Discord-Nutzer.
// - Gespeichert wird nichts über den Cache hinaus, protokolliert wird nichts.

interface RateLimiter {
  limit(options: { key: string }): Promise<{ success: boolean }>;
}

export interface CharacterEnv {
  CHARACTER_LIMIT: RateLimiter;
  NCSOFT_LIMIT: RateLimiter;
  /** Gemeinsamer Schlüssel mit dem Discord-Bot (Worker aetherdps-discord, per Service Binding). */
  BOT_KEY?: string;
}

const SEARCH_URL = "https://api-search.plaync.com/aion2global/search/v2/character";
const SITE_URL = "https://aion2.plaync.com/api/character";
const LANG = "de-DE";
const USER_AGENT = "AetherDPS-Web/1.0 (+https://aetherdps.com)";

const PROFILE_TTL = 3600; // Sekunden
const ID_TTL = 86_400; // Name → characterId ändert sich praktisch nie
const MISSING_TTL = 600; // „nicht gefunden“ kurz merken, damit Tippfehler NCSOFT nicht wiederholt treffen
const GAP_MS = 1000; // Pause zwischen zwei Anfragen an NCSOFT

/** Seiten, die die Antwort im Browser lesen dürfen: aetherdps.com und lokal `npm run dev` (beliebiger Port). */
const ORIGINS = new Set(["https://aetherdps.com", "https://www.aetherdps.com"]);
const DEV_ORIGIN = /^http:\/\/(localhost|127\.0\.0\.1):\d{2,5}$/;

/** Charaktername: nur Buchstaben und Ziffern (wie im Spiel), 2 bis 24 Zeichen. */
const NAME_RE = /^[\p{L}\p{N}]{2,24}$/u;

/** EU-Server: 13xx Elyos, 23xx Asmodier (siehe AetherDPS.Engine/GameData/servers.json). */
const isEuServer = (id: number) => (id >= 1301 && id <= 1399) || (id >= 2301 && id <= 2399);

/** Nur Bilder von diesen NCSOFT-Servern (wie Armory.IsOfficialImage). */
const officialImage = (url: unknown): string | null =>
  typeof url === "string" &&
  (url.startsWith("https://assets.playnccdn.com/") || url.startsWith("https://profileimg.plaync.com/"))
    ? url
    : null;

// ---------- Cache ----------

// Die Cache API wirkt nur auf eigenen Domains (nicht auf *.workers.dev); dort hilft der kleine Speicher je Isolat.
const memory = new Map<string, { until: number; body: string; status: number }>();
const MEMORY_MAX = 300;

const cacheKey = (kind: string, server: number, name: string) =>
  new Request(`https://cache.aetherdps.invalid/${kind}/${server}/${encodeURIComponent(name.toLowerCase())}`);

async function cacheGet(key: Request): Promise<{ body: string; status: number } | null> {
  const hit = memory.get(key.url);
  if (hit && hit.until > Date.now()) return hit;
  if (hit) memory.delete(key.url);
  const res = await caches.default.match(key);
  return res ? { body: await res.text(), status: res.status } : null;
}

function cachePut(ctx: ExecutionContext, key: Request, body: string, status: number, ttl: number): void {
  if (memory.size >= MEMORY_MAX) memory.delete(memory.keys().next().value!);
  memory.set(key.url, { until: Date.now() + ttl * 1000, body, status });
  const res = new Response(body, {
    status,
    headers: { "Content-Type": "application/json; charset=utf-8", "Cache-Control": `public, max-age=${ttl}` },
  });
  ctx.waitUntil(caches.default.put(key, res).catch(() => undefined));
}

function cacheDelete(ctx: ExecutionContext, key: Request): void {
  memory.delete(key.url);
  ctx.waitUntil(caches.default.delete(key).catch(() => false));
}

// ---------- NCSOFT ----------

class Busy extends Error {}

/**
 * Wartet, bis die nächste Anfrage an NCSOFT erlaubt ist: mindestens GAP_MS nach dem Start der vorigen
 * und mit Platz im gemeinsamen Rate Limiter. NCSOFT antwortet oft erst nach Sekunden, darum zählt der Start.
 */
async function turn(env: CharacterEnv, last: { at: number }): Promise<void> {
  const wait = last.at + GAP_MS - Date.now();
  if (wait > 0) await new Promise((r) => setTimeout(r, wait));
  const { success } = await env.NCSOFT_LIMIT.limit({ key: "ncsoft" });
  if (!success) throw new Busy();
  last.at = Date.now();
}

/** Holt JSON von NCSOFT; null bei Fehler (wirft nie, damit gestaffelte Anfragen nicht hängen bleiben). */
async function getJson(url: string): Promise<unknown | null> {
  try {
    const res = await fetch(url, {
      headers: { "User-Agent": USER_AGENT, Accept: "application/json" },
      signal: AbortSignal.timeout(15_000),
    });
    return res.ok ? await res.json() : null;
  } catch {
    return null;
  }
}

type Obj = Record<string, unknown>;
const obj = (x: unknown): Obj => (x !== null && typeof x === "object" && !Array.isArray(x) ? (x as Obj) : {});
const arr = (x: unknown): Obj[] => (Array.isArray(x) ? x.map(obj) : []);
const str = (x: unknown): string => (typeof x === "string" ? x : typeof x === "number" ? String(x) : "");
const int = (x: unknown): number => (typeof x === "number" && Number.isFinite(x) ? Math.trunc(x) : 0);

/** Sucht die characterId (schon URL-kodiert) per Name und Server, wie Armory.FindAsync. */
async function findId(env: CharacterEnv, name: string, server: number, last: { at: number }): Promise<string | null> {
  const q = `keyword=${encodeURIComponent(name)}&region=eu&localeInfo=${LANG}&serverId=${server}`;
  await turn(env, last);
  const doc = obj(await getJson(`${SEARCH_URL}?${q}`));
  for (const row of arr(doc.list)) {
    const found = str(row.name).replace(/<[^>]+>/g, "");
    if (found.toLowerCase() === name.toLowerCase() && int(row.serverId) === server) {
      const id = str(row.characterId);
      return /^[A-Za-z0-9%._-]{8,200}$/.test(id) ? id : null;
    }
  }
  return null;
}

/** Baut die Antwort aus Profil und Ausrüstung, mit denselben Feldern wie Armory.Parse. */
function shape(infoRaw: unknown, equipmentRaw: unknown) {
  const info = obj(infoRaw);
  const p = obj(info.profile);
  let itemLevel = 0;
  const stats: { type: string; name: string; value: number; effects: string[] }[] = [];
  for (const s of arr(obj(info.stat).statList)) {
    if (str(s.type) === "ItemLevel") {
      itemLevel = int(s.value);
      continue;
    }
    const effects = Array.isArray(s.statSecondList) ? s.statSecondList.filter((x) => typeof x === "string") : [];
    stats.push({ type: str(s.type), name: str(s.name), value: int(s.value), effects });
  }

  const boards = arr(obj(info.daevanion).boardList).map((b) => ({
    id: int(b.id),
    name: str(b.name),
    icon: officialImage(b.icon),
    open: int(b.openNodeCount),
    total: int(b.totalNodeCount),
    percent: int(b.openPercent),
  }));

  const t = obj(info.title);
  const titles = {
    owned: int(t.ownedCount),
    list: arr(t.titleList).map((x) => ({
      category: str(x.equipCategory),
      name: str(x.name),
      grade: str(x.grade),
      owned: int(x.ownedCount),
      total: int(x.totalCount),
    })),
  };

  const eq = obj(equipmentRaw);
  const equipment = arr(obj(eq.equipment).equipmentList).map((i) => ({
    slot: str(i.slotPosName),
    name: str(i.name),
    grade: str(i.grade),
    enchant: int(i.enchantLevel),
    exceed: int(i.exceedLevel),
    icon: officialImage(i.icon),
  }));
  const skills = arr(obj(eq.skill).skillList)
    .filter((s) => int(s.skillLevel) > 0)
    .map((s) => ({
      name: str(s.name),
      category: str(s.category),
      level: int(s.skillLevel),
      equipped: int(s.equip) === 1,
      icon: officialImage(s.icon),
    }))
    .sort((a, b) => b.level - a.level);

  const pw = obj(eq.petwing);
  const pe = obj(pw.pet);
  const w = obj(pw.wing);
  return {
    name: str(p.characterName),
    server: str(p.serverName),
    serverId: int(p.serverId),
    className: str(p.className),
    classId: int(p.pcId),
    race: str(p.raceName),
    raceId: int(p.raceId),
    level: int(p.characterLevel),
    itemLevel,
    combatPower: int(p.combatPower),
    portrait: officialImage(p.profileImage),
    title: str(p.titleName) ? { name: str(p.titleName), grade: str(p.titleGrade) } : null,
    stats,
    boards,
    titles,
    equipment,
    skills,
    pet: str(pe.name) ? { name: str(pe.name), level: int(pe.level), icon: officialImage(pe.icon) } : null,
    wing: str(w.name)
      ? { name: str(w.name), grade: str(w.grade), enchant: int(w.enchantLevel), icon: officialImage(w.icon) }
      : null,
    loadedAt: new Date().toISOString().slice(0, 19) + "Z",
  };
}

// ---------- Endpunkt ----------

function reply(req: Request, body: string | null, status: number, extra: Record<string, string> = {}): Response {
  const headers: Record<string, string> = {
    "X-Content-Type-Options": "nosniff",
    "Cache-Control": "no-store",
    Vary: "Origin",
    ...extra,
  };
  if (body !== null) headers["Content-Type"] = "application/json; charset=utf-8";
  const origin = req.headers.get("Origin");
  if (origin && (ORIGINS.has(origin) || DEV_ORIGIN.test(origin))) headers["Access-Control-Allow-Origin"] = origin;
  return new Response(body, { status, headers });
}

const error = (req: Request, status: number, code: string, extra: Record<string, string> = {}) =>
  reply(req, JSON.stringify({ error: code }), status, extra);

export async function character(req: Request, env: CharacterEnv, ctx: ExecutionContext, url: URL): Promise<Response> {
  if (req.method === "OPTIONS") {
    return reply(req, null, 204, { "Access-Control-Allow-Methods": "GET", "Access-Control-Max-Age": "86400" });
  }
  if (req.method !== "GET") return error(req, 405, "method");

  const name = (url.searchParams.get("name") ?? "").trim();
  const server = Number.parseInt(url.searchParams.get("server") ?? "", 10);
  if (!NAME_RE.test(name) || !isEuServer(server)) return error(req, 400, "input");

  // Die IP dient nur als flüchtiger Schlüssel für die Begrenzung und wird nirgends gespeichert.
  // Der Discord-Bot begrenzt selbst je Discord-Nutzer; die Grenze zu NCSOFT (NCSOFT_LIMIT) gilt auch für ihn.
  const fromBot = !!env.BOT_KEY && req.headers.get("X-AetherDPS-Bot") === env.BOT_KEY;
  const { success } = fromBot
    ? { success: true }
    : await env.CHARACTER_LIMIT.limit({ key: req.headers.get("CF-Connecting-IP") ?? "unknown" });
  if (!success) return error(req, 429, "rate", { "Retry-After": "60" });

  const profileKey = cacheKey("profile", server, name);
  const cached = await cacheGet(profileKey);
  if (cached) return reply(req, cached.body, cached.status, { "X-Cache": "HIT" });

  const last = { at: 0 };
  const idKey = cacheKey("id", server, name);
  try {
    let id = (await cacheGet(idKey))?.body ?? null;
    if (id === null) {
      id = await findId(env, name, server, last);
      if (id === null) {
        const body = JSON.stringify({ error: "notfound" });
        cachePut(ctx, profileKey, body, 404, MISSING_TTL);
        return reply(req, body, 404);
      }
      cachePut(ctx, idKey, id, 200, ID_TTL);
    }

    const q = `lang=${LANG}&region=eu&serverId=${server}&characterId=${id}`;
    // Gestaffelt: Ausrüstung startet eine Sekunde nach dem Profil, ohne auf dessen Antwort zu warten.
    await turn(env, last);
    const infoReq = getJson(`${SITE_URL}/info?${q}`);
    await turn(env, last);
    const [info, equipment] = await Promise.all([infoReq, getJson(`${SITE_URL}/equipment?${q}`)]);
    if (info === null || !("profile" in obj(info))) {
      cacheDelete(ctx, idKey); // vielleicht umbenannt oder gelöscht: beim nächsten Mal neu suchen
      return error(req, 502, "upstream");
    }
    const body = JSON.stringify(shape(info, equipment));
    cachePut(ctx, profileKey, body, 200, PROFILE_TTL);
    return reply(req, body, 200, { "X-Cache": "MISS" });
  } catch (e) {
    if (e instanceof Busy) return error(req, 503, "busy", { "Retry-After": "5" });
    return error(req, 502, "upstream");
  }
}
