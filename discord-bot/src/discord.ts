// Gemeinsames: Umgebung, Discord-REST, Antworten auf Interaktionen.

interface RateLimiter {
  limit(options: { key: string }): Promise<{ success: boolean }>;
}

export interface Env {
  DISCORD_BOT_TOKEN: string;
  DISCORD_PUBLIC_KEY: string;
  /** Gleicher Wert wie BOT_KEY im Worker aetherdps-api (Charakterprofile über das Service Binding API). */
  BOT_KEY: string;
  APP_ID: string;
  GUILD_ID: string;
  NEWS_CHANNEL: string;
  ANNOUNCE_CHANNEL: string;
  ROLES: { update: string; news: string; tester: string; class: Record<string, string> };
  /** App-Emojis der Klassen, z. B. ranger → "<:c_ranger:123>". */
  EMOJIS: Record<string, string>;
  API: Fetcher;
  /** Discord-Nutzer → verknüpfter Charakter ({ name, server }). */
  LINKS: KVNamespace;
  /** Abfragen bei NCSOFT je Discord-Nutzer. */
  USER_LIMIT: RateLimiter;
}

export const GOLD = 0xe0a84a;
export const VIOLET = 0xc9a8f2;
export const GREEN = 0x5cc26a;
export const RED = 0xe2554f;
export const WEB = "https://aetherdps.com";
export const REPO = "yykk62/AetherDPS";
export const DOWNLOAD = `https://github.com/${REPO}/releases/latest`;

const API = "https://discord.com/api/v10";

export async function discord(env: Env, method: string, path: string, body?: unknown): Promise<any> {
  for (let attempt = 0; attempt < 3; attempt++) {
    const res = await fetch(API + path, {
      method,
      headers: {
        Authorization: `Bot ${env.DISCORD_BOT_TOKEN}`,
        "User-Agent": "DiscordBot (https://aetherdps.com, 1.0)",
        ...(body ? { "Content-Type": "application/json" } : {}),
      },
      body: body ? JSON.stringify(body) : undefined,
    });
    if (res.status === 429) {
      const wait = Number((await res.json<any>()).retry_after ?? 1);
      await new Promise((r) => setTimeout(r, (wait + 0.2) * 1000));
      continue;
    }
    if (!res.ok) throw new Error(`${method} ${path}: ${res.status} ${await res.text()}`);
    return res.status === 204 ? null : res.json();
  }
  throw new Error(`${method} ${path}: rate limited`);
}

export type Message = {
  content?: string;
  embeds?: unknown[];
  components?: unknown[];
  flags?: number;
  allowed_mentions?: unknown;
};

export const EPHEMERAL = 64;
const quiet = { parse: [] as string[] };

/** Sofortige Antwort (Typ 4). */
export const respond = (data: Message) => Response.json({ type: 4, data: { allowed_mentions: quiet, ...data } });
/** Nur für den Aufrufer sichtbar. */
export const whisper = (content: string, extra: Message = {}) => respond({ content, flags: EPHEMERAL, ...extra });
/** „Denkt nach …“ (Typ 5), die Antwort folgt per {@link finish}. */
export const deferred = (ephemeral = false) => Response.json({ type: 5, data: ephemeral ? { flags: EPHEMERAL } : {} });
/** Klick auf einen Knopf: Nachricht gleich ändern (Typ 6), die neue Fassung folgt per {@link finish}. */
export const deferredUpdate = () => Response.json({ type: 6 });

/** Ersetzt die (verzögerte) Antwort einer Interaktion. */
export async function finish(env: Env, token: string, data: Message): Promise<void> {
  await fetch(`${API}/webhooks/${env.APP_ID}/${token}/messages/@original`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ allowed_mentions: quiet, ...data }),
  });
}

/** Kurztext mit „…“ an Wortgrenze. */
export const cut = (s: string, n: number) => (s.length > n ? s.slice(0, n - 1).replace(/\s+\S*$/, "") + " …" : s);

/** Discord-Zeitstempel, den jeder in seiner Ortszeit sieht (R = „in 2 Stunden“, t = „21:00“). */
export const ts = (ms: number, style: "R" | "t" | "f" | "F" | "D" = "R") => `<t:${Math.floor(ms / 1000)}:${style}>`;

export const nf = new Intl.NumberFormat("de-DE");

/** Unterbefehle und Optionen einer Interaktion als flaches Objekt. */
export function options(data: any): { sub?: string; opts: Record<string, any>; focused?: string } {
  let list: any[] = data.options ?? [];
  let sub: string | undefined;
  if (list[0]?.type === 1) {
    sub = list[0].name;
    list = list[0].options ?? [];
  }
  const opts: Record<string, any> = {};
  let focused: string | undefined;
  for (const o of list) {
    opts[o.name] = o.value;
    if (o.focused) focused = o.name;
  }
  return { sub, opts, focused };
}
