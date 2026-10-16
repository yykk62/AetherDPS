// Automatische Posts alle 15 Minuten: offizielle AION-2-News (aion2.plaync.com, Deutsch) und AetherDPS-Releases.
// Gespeichert wird nichts: Was schon gepostet ist, steht im Kanal selbst (Link im Embed).

import { cut, discord, Env, GOLD, REPO, VIOLET } from "./discord";

const NC = "https://api-global-community.plaync.com/aion2_global/board";

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
    // Absätze fehlen in der Kurzfassung: „Daevas,Damit“ / „RuhmHallo“ wieder trennen
    .replace(/([,.!?:;])(?=[A-ZÄÖÜ])/g, "$1 ")
    .replace(/([a-zäöüß])(?=[A-ZÄÖÜ][a-zäöüß])/g, "$1 ")
    .replace(/\s+/g, " ")
    .trim();

export interface Article { url: string; title: string; summary: string; image?: string; at: number; patch: boolean }

export async function boardArticles(board: string, patch: boolean): Promise<Article[]> {
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

export async function postNews(env: Env) {
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

export async function postReleases(env: Env) {
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

