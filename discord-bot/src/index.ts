/**
 * Discord-Bot für den AetherDPS-Server – läuft ohne dauerhafte Verbindung:
 *  - POST /interactions: Rollen-Knöpfe und Klassen-Auswahl (Discord schickt Klicks hierher)
 *  - Cron alle 15 Min.: offizielle AION-2-News (aion2.plaync.com, Deutsch) und AetherDPS-Releases (GitHub) posten
 *
 * Gespeichert wird nichts: Was schon gepostet ist, steht im Kanal selbst (Link im Embed).
 */

interface Env {
  DISCORD_BOT_TOKEN: string;
  DISCORD_PUBLIC_KEY: string;
  GUILD_ID: string;
  NEWS_CHANNEL: string;
  ANNOUNCE_CHANNEL: string;
  ROLES: { update: string; news: string; tester: string; class: Record<string, string> };
}

const API = "https://discord.com/api/v10";
const GOLD = 0xe0a84a;
const VIOLET = 0xc9a8f2;
const NC = "https://api-global-community.plaync.com/aion2_global/board";
const REPO = "yykk62/AetherDPS";

// ---------- Discord-REST ----------

async function discord(env: Env, method: string, path: string, body?: unknown): Promise<any> {
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

// ---------- Interaktionen (Rollen) ----------

const hexToBytes = (hex: string) => new Uint8Array(hex.match(/../g)!.map((b) => parseInt(b, 16)));

async function verify(req: Request, body: string, env: Env): Promise<boolean> {
  const sig = req.headers.get("X-Signature-Ed25519");
  const ts = req.headers.get("X-Signature-Timestamp");
  if (!sig || !ts) return false;
  const key = await crypto.subtle.importKey("raw", hexToBytes(env.DISCORD_PUBLIC_KEY), { name: "Ed25519" }, false, ["verify"]);
  return crypto.subtle.verify("Ed25519", key, hexToBytes(sig), new TextEncoder().encode(ts + body));
}

const reply = (content: string) =>
  Response.json({ type: 4, data: { content, flags: 64, allowed_mentions: { parse: [] } } });

const PINGS: Record<string, { key: "update" | "news" | "tester"; on: string; off: string }> = {
  "ping:update": {
    key: "update",
    on: "🔔 Du wirst jetzt bei **neuen AetherDPS-Versionen** erwähnt.",
    off: "🔕 Keine Erwähnung mehr bei neuen AetherDPS-Versionen.",
  },
  "ping:news": {
    key: "news",
    on: "📰 Du wirst jetzt bei **neuen AION-2-Patchnotes** erwähnt.",
    off: "🔕 Keine Erwähnung mehr bei AION-2-Patchnotes.",
  },
  "ping:tester": {
    key: "tester",
    on: "🧪 Willkommen im **Testerteam**! Du bekommst Testversionen vorab und wirst bei Testaufrufen erwähnt.",
    off: "👋 Du bist nicht mehr im Testerteam.",
  },
};

async function interaction(req: Request, env: Env): Promise<Response> {
  const body = await req.text();
  if (!(await verify(req, body, env))) return new Response("bad signature", { status: 401 });
  const i = JSON.parse(body);
  if (i.type === 1) return Response.json({ type: 1 }); // PING
  if (i.type !== 3 || i.guild_id !== env.GUILD_ID || !i.member) return reply("Das geht nur auf dem AetherDPS-Server.");

  const user: string = i.member.user.id;
  const has = new Set<string>(i.member.roles);
  const id: string = i.data.custom_id;
  const rolePath = (role: string) => `/guilds/${env.GUILD_ID}/members/${user}/roles/${role}`;

  const ping = PINGS[id];
  if (ping) {
    const role = env.ROLES[ping.key];
    const add = !has.has(role);
    await discord(env, add ? "PUT" : "DELETE", rolePath(role));
    return reply(add ? ping.on : ping.off);
  }

  if (id === "class") {
    const chosen = new Set<string>(i.data.values ?? []);
    for (const [key, role] of Object.entries(env.ROLES.class)) {
      if (chosen.has(key) && !has.has(role)) await discord(env, "PUT", rolePath(role));
      if (!chosen.has(key) && has.has(role)) await discord(env, "DELETE", rolePath(role));
    }
    const names = [...chosen].map((k) => `<@&${env.ROLES.class[k]}>`).join(" ");
    return reply(chosen.size ? `⚔️ Deine Klasse: ${names}` : "Klassen-Rolle entfernt.");
  }

  return reply("Unbekannter Knopf – bitte Bescheid sagen.");
}

// ---------- Automatische Posts ----------

/** Links (Embed-URL) und neuester Zeitstempel der letzten 50 Nachrichten eines Kanals. */
async function posted(env: Env, channel: string) {
  const messages: any[] = await discord(env, "GET", `/channels/${channel}/messages?limit=50`);
  const urls = new Set<string>();
  let latest = 0;
  for (const m of messages)
    for (const e of m.embeds ?? []) {
      if (e.url) urls.add(e.url);
      if (e.timestamp) latest = Math.max(latest, Date.parse(e.timestamp));
    }
  return { urls, latest, empty: urls.size === 0 };
}

async function publish(env: Env, channel: string, message: unknown) {
  const m = await discord(env, "POST", `/channels/${channel}/messages`, message);
  // Ankündigungskanal: an Server weitergeben, die dem Kanal folgen
  await discord(env, "POST", `/channels/${channel}/messages/${m.id}/crosspost`).catch(() => {});
}

const clean = (s: string) =>
  s
    .replace(/&nbsp;/g, " ")
    .replace(/&amp;/g, "&")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'")
    .replace(/[​‌‍﻿]/g, "")
    .replace(/\s+/g, " ")
    .trim();

const cut = (s: string, n: number) => (s.length > n ? s.slice(0, n - 1).replace(/\s+\S*$/, "") + " …" : s);

interface Article { url: string; title: string; summary: string; image?: string; at: number; patch: boolean }

async function boardArticles(board: string, patch: boolean): Promise<Article[]> {
  const res = await fetch(`${NC}/${board}/article/search/moreArticle?isVote=true&moreSize=10&moreDirection=BEFORE&previousArticleId=0`, {
    headers: { "User-Agent": "AetherDPS-Discord (+https://aetherdps.com)", Accept: "application/json" },
  });
  if (!res.ok) return [];
  const data = await res.json<any>();
  return (data.contentList ?? []).map((a: any) => ({
    url: String(a.rootBoard?.board?.boardUrlPattern ?? "").replace("{articleId}", a.id),
    title: clean(a.title ?? ""),
    summary: clean(a.summary ?? ""),
    image: a.thumbnailUrl || undefined,
    at: Date.parse(a.timestamps?.postedAt ?? "") || 0,
    patch,
  })).filter((a: Article) => a.url.startsWith("https://aion2.plaync.com/"));
}

async function postNews(env: Env) {
  const all = [...(await boardArticles("update_de", true)), ...(await boardArticles("notice_de", false))].sort((a, b) => a.at - b.at);
  const seen = await posted(env, env.NEWS_CHANNEL);
  // Erster Lauf: nur die drei neuesten, kein Nachholen von allem
  const fresh = seen.empty ? all.slice(-3) : all.filter((a) => !seen.urls.has(a.url) && a.at > seen.latest);
  for (const a of fresh) {
    await publish(env, env.NEWS_CHANNEL, {
      content: a.patch && !seen.empty ? `<@&${env.ROLES.news}> Neue Patch-Notizen sind da!` : undefined,
      allowed_mentions: { roles: [env.ROLES.news] },
      embeds: [{
        author: { name: a.patch ? "🛠️ AION 2 · Patch-Notizen" : "📣 AION 2 · Ankündigung" },
        title: cut(a.title, 250),
        url: a.url,
        description: `${cut(a.summary, 320)}\n\n**[➜ Ganzen Beitrag lesen](${a.url})**`,
        color: a.patch ? GOLD : VIOLET,
        image: a.image ? { url: a.image } : undefined,
        footer: { text: "Offiziell von aion2.plaync.com · automatisch geteilt" },
        timestamp: new Date(a.at).toISOString(),
      }],
    });
  }
}

async function postReleases(env: Env) {
  const res = await fetch(`https://api.github.com/repos/${REPO}/releases?per_page=5`, {
    headers: { "User-Agent": "AetherDPS-Discord", Accept: "application/vnd.github+json" },
  });
  if (!res.ok) return;
  const releases = (await res.json<any[]>()).filter((r) => !r.draft && !r.prerelease)
    .sort((a, b) => Date.parse(a.published_at) - Date.parse(b.published_at));
  const seen = await posted(env, env.ANNOUNCE_CHANNEL);
  const fresh = releases.filter((r) => !seen.urls.has(r.html_url) && Date.parse(r.published_at) > seen.latest);
  // Ohne bisherige Release-Posts nur die neueste Version
  for (const r of seen.empty ? fresh.slice(-1) : fresh) {
    const exe = (r.assets ?? []).find((a: any) => a.name === "AetherDPS.exe");
    const notes = String(r.body ?? "").replace(/\r/g, "").trim();
    await publish(env, env.ANNOUNCE_CHANNEL, {
      content: seen.empty ? undefined : `<@&${env.ROLES.update}> Neue Version!`,
      allowed_mentions: { roles: [env.ROLES.update] },
      embeds: [{
        author: { name: "✨ Neues Update" },
        title: `AetherDPS ${String(r.tag_name).replace(/^v/, "")} ist da`,
        url: r.html_url,
        description: `${cut(notes, 3500)}\n\n🔄 **Schon installiert?** Das Update kommt beim nächsten Start von selbst.`,
        color: GOLD,
        footer: { text: "AetherDPS · kostenlos & Open Source" },
        timestamp: r.published_at,
      }],
      components: [{
        type: 1,
        components: [
          ...(exe ? [{ type: 2, style: 5, label: "Download", emoji: { name: "⬇️" }, url: exe.browser_download_url }] : []),
          { type: 2, style: 5, label: "Alle Änderungen", emoji: { name: "📋" }, url: r.html_url },
        ],
      }],
    });
  }
}

export default {
  async fetch(req: Request, env: Env): Promise<Response> {
    const url = new URL(req.url);
    if (req.method === "POST" && url.pathname === "/interactions") return interaction(req, env);
    return Response.redirect("https://aetherdps.com/", 302);
  },
  async scheduled(_: ScheduledController, env: Env, ctx: ExecutionContext) {
    ctx.waitUntil(Promise.allSettled([postNews(env), postReleases(env)]));
  },
};
