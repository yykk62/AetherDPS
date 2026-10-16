// /pet, /feldboss, /events, /checkliste, /patchnotes, /aetherdps, /hilfe – Daten wie in App und Webseite.

import data from "./gamedata.json";
import { EVENTS } from "../../web/src/data/events";
import { DAILY, RECHARGES, WEEKLY, type Task } from "../../web/src/data/checklist";
import { DAILY_RESET, describe, next, WEEKLY_RESET, type Schedule } from "../../web/src/lib/schedule";
import { boardArticles } from "./feeds";
import { cut, deferred, DOWNLOAD, Env, finish, GOLD, GREEN, REPO, respond, ts, VIOLET, WEB, whisper } from "./discord";

interface Pet { id: number; name: string; category: string; group: string; regions: string[]; monsters: string[] }
interface Boss { slot: number; name: string; map: string; respawn?: number; fixed?: Schedule }
const PETS: Pet[] = data.pets;
const BOSSES: Boss[] = data.bosses as Boss[];

const WAKAYASHI = "[Wakayashi](https://wakayashi.gg)";

const rank = (name: string, q: string) => (name.toLowerCase().startsWith(q) ? 0 : 1);
const search = <T extends { name: string }>(list: T[], query: string) => {
  const q = query.trim().toLowerCase();
  return list.filter((x) => x.name.toLowerCase().includes(q)).sort((a, b) => rank(a.name, q) - rank(b.name, q));
};

// ---------- Pets ----------

const PET_CATEGORIES: Record<string, { name: string; color: number; emoji: string }> = {
  Intellect: { name: "Cognia", color: 0x4f9ce8, emoji: "🔵" },
  Feral: { name: "Fera", color: 0xe8894f, emoji: "🟠" },
  Nature: { name: "Natura", color: 0x5cc26a, emoji: "🟢" },
  Trans: { name: "Varia", color: 0xa77be8, emoji: "🟣" },
  Special: { name: "Specia", color: 0xe0c04a, emoji: "🟡" },
};

export const petChoices = (q: string) =>
  search(PETS, q).slice(0, 25).map((p) => ({ name: `${p.name} · ${PET_CATEGORIES[p.category]?.name ?? p.category}`, value: String(p.id) }));

export function pet(opts: Record<string, any>) {
  const v = String(opts.name ?? "");
  const p = PETS.find((x) => String(x.id) === v) ?? search(PETS, v)[0];
  if (!p) return whisper("🐾 Dieses Pet kenne ich nicht. Wähle es am besten aus der Liste beim Tippen.");
  const cat = PET_CATEGORIES[p.category] ?? { name: p.category, color: GOLD, emoji: "⚪" };
  return respond({
    embeds: [{
      color: cat.color,
      title: `🐾 ${p.name}`,
      url: `${WEB}/datenbank/pets/`,
      thumbnail: { url: `${WEB}/gen/pets/${p.id}.png` },
      fields: [
        { name: "Gattung", value: `${cat.emoji} ${cat.name}`, inline: true },
        { name: "Gebiet", value: p.group || "–", inline: true },
        { name: "📍 Fundorte", value: cut(p.regions.join(" · ") || "unbekannt", 1024) },
        { name: "💀 Seele droppt von", value: cut(p.monsters.join(" · ") || "unbekannt", 1024) },
      ],
      footer: { text: "Pet-Radar mit Karte: im AetherDPS-Meter (Reiter Pets) · Daten: SkeeveAN (MIT), NCSOFT" },
    }],
  });
}

// ---------- Feldbosse ----------

export const bossChoices = (q: string) =>
  search(BOSSES, q).slice(0, 25).map((b) => ({ name: cut(`${b.name} · ${b.map}`, 100), value: String(b.slot) }));

const duration = (min: number) => (min % 60 === 0 ? `${min / 60} Std.` : min > 60 ? `${Math.floor(min / 60)} Std. ${min % 60} Min.` : `${min} Min.`);

export function fieldBoss(opts: Record<string, any>, user: string) {
  const v = String(opts.name ?? "");
  const b = BOSSES.find((x) => String(x.slot) === v) ?? search(BOSSES, v)[0];
  if (!b) return whisper("👹 Diesen Feldboss kenne ich nicht. Wähle ihn am besten aus der Liste beim Tippen.");
  const now = Date.now();
  if (opts["getötet"]) {
    if (b.fixed) {
      const n = next(b.fixed, now + 60_000);
      return respond({ content: `💀 **${b.name}** (${b.map}) liegt! Er kommt zu festen Zeiten (${describe(b.fixed)}), der nächste: ${n ? `${ts(n.at, "t")} · ${ts(n.at)}` : "unbekannt"}.` });
    }
    const minutes = Number(opts.respawn) || b.respawn;
    if (!minutes) return whisper(`Die Respawn-Zeit von **${b.name}** ist noch nicht bekannt. Gib sie mit der Option **respawn** (Minuten) mit an, dann startet der Countdown.`);
    const at = now + minutes * 60_000;
    return respond({
      content: `💀 **${b.name}** (${b.map}) wurde von <@${user}> gelegt.\n⏰ Respawn ${ts(at, "t")} · **${ts(at)}**`,
      allowed_mentions: { parse: [] },
    });
  }
  const lines = [`📍 **${b.map}**`];
  if (b.fixed) {
    const n = next(b.fixed, now);
    lines.push(`🗓️ Feste Zeiten: ${describe(b.fixed)} (Serverzeit)`, n ? (n.live ? "🟢 **Gerade offen!**" : `⏰ Nächster: ${ts(n.at, "t")} · ${ts(n.at)}`) : "");
  } else if (b.respawn) {
    lines.push(`🔁 Respawn **${duration(b.respawn)}** nach dem Tod`, "💡 Gerade gelegt? `/feldboss` mit **getötet: True** startet einen Countdown für alle.");
  } else lines.push("🔁 Respawn-Zeit noch unbekannt", "💡 Gerade gelegt? `/feldboss` mit **getötet: True** und **respawn** (Minuten) startet einen Countdown für alle.");
  return respond({
    embeds: [{
      color: 0xd64545, title: `👹 ${b.name}`, url: `${WEB}/datenbank/feldbosse/`, description: lines.filter(Boolean).join("\n"),
      footer: { text: "Echte Respawn-Timer vom Server: im AetherDPS-Meter (Reiter Timer)" },
    }],
  });
}

// ---------- Events und Checkliste ----------

const TYPE_EMOJI: Record<string, string> = { pvp: "⚔️", boss: "👹", event: "🎪" };

export function events() {
  const now = Date.now();
  const rows = EVENTS.map((e) => ({ e, n: next(e.schedule, now) }))
    .filter((x) => x.n)
    .sort((a, b) => a.n!.at - b.n!.at)
    .map(({ e, n }) => `${TYPE_EMOJI[e.type] ?? "📅"} **${e.name}** · ${n!.live ? "🟢 **läuft gerade**" : `${ts(n!.at, "t")} · ${ts(n!.at)}`}` +
      `${e.confirmed ? "" : " · *EU unbestätigt*"}\n-# ${describe(e.schedule)} · ${e.note}`);
  const daily = next(DAILY_RESET, now)!.at;
  const weekly = next(WEEKLY_RESET, now)!.at;
  return respond({
    embeds: [{
      color: VIOLET,
      title: "📅 Nächste Events",
      description: cut(rows.join("\n"), 3800),
      fields: [
        { name: "🔄 Täglicher Reset", value: `${ts(daily, "t")} · ${ts(daily)}`, inline: true },
        { name: "🗓️ Wöchentlicher Reset", value: `${ts(weekly, "f")} · ${ts(weekly)}`, inline: true },
      ],
      footer: { text: "Zeiten nach Wakayashi (wakayashi.gg) · Discord zeigt sie in deiner Ortszeit" },
    }],
    components: [{ type: 1, components: [{ type: 2, style: 5, label: "Alle Timer", emoji: { name: "⏱️" }, url: `${WEB}/timer/` }] }],
  });
}

export function checklist() {
  const now = Date.now();
  const task = (t: Task) => `☐ ${t.name}${t.count ? ` **${t.count}×**` : t.label ? ` **${t.label}**` : ""}${t.who === "main" ? " · *Main*" : ""}`;
  const daily = next(DAILY_RESET, now)!.at;
  const weekly = next(WEEKLY_RESET, now)!.at;
  return respond({
    flags: 64,
    embeds: [{
      color: GREEN,
      title: "✅ Checkliste",
      description: `Inhalte von ${WAKAYASHI}, mit Erlaubnis. Zum Abhaken: [aetherdps.com/checkliste](${WEB}/checkliste/) oder der Reiter Checkliste im Meter.`,
      fields: [
        { name: `☀️ Täglich · Reset ${ts(daily)}`, value: DAILY.map(task).join("\n") },
        { name: `🗓️ Wöchentlich · Reset ${ts(weekly)}`, value: WEEKLY.map(task).join("\n") },
        { name: "🔋 Lädt sich auf", value: RECHARGES.map((r) => `• ${r.name}: **${r.amount}**${r.note ? ` · *${r.note}*` : ""}`).join("\n") },
      ],
    }],
  });
}

// ---------- News und AetherDPS ----------

export async function patchnotes(env: Env, ctx: ExecutionContext, i: any) {
  ctx.waitUntil((async () => {
    const [up, notes] = await Promise.all([boardArticles("update_de", true), boardArticles("notice_de", false)]);
    const line = (a: { title: string; url: string; at: number }) => `[${cut(a.title, 90)}](${a.url}) · ${ts(a.at)}`;
    await finish(env, i, {
      embeds: [{
        color: GOLD,
        title: "📰 AION 2 · Offizielle News",
        fields: [
          { name: "🛠️ Patch-Notizen", value: up.slice(-3).reverse().map(line).join("\n") || "–" },
          { name: "📣 Ankündigungen", value: notes.slice(-4).reverse().map(line).join("\n") || "–" },
        ],
        footer: { text: "Quelle: aion2.plaync.com (EU, Deutsch)" },
      }],
    });
  })());
  return deferred();
}

export async function aetherdps(env: Env, ctx: ExecutionContext, i: any) {
  ctx.waitUntil((async () => {
    let version = "";
    let date = 0;
    try {
      const r = await fetch(`https://api.github.com/repos/${REPO}/releases/latest`, { headers: { "User-Agent": "AetherDPS-Discord", Accept: "application/vnd.github+json" } });
      if (r.ok) {
        const j = await r.json<any>();
        version = String(j.tag_name).replace(/^v/, "");
        date = Date.parse(j.published_at);
      }
    } catch { /* ohne Version */ }
    await finish(env, i, {
      embeds: [{
        color: GOLD,
        title: "📊 AetherDPS",
        url: WEB,
        description: "Der **kostenlose, quelloffene DPS-Meter für AION 2** (EU), auf Deutsch und in 10 Sprachen.\n" +
          (version ? `\n✨ Neueste Version: **${version}** (${ts(date, "D")})` : ""),
        fields: [
          { name: "⚔️ Live-DPS", value: "Boss-Leben und Stagger, DPS je Spieler, Skill-Details", inline: true },
          { name: "🏆 Läufe", value: "Boss-Zeiten, Bestzeiten, als Bild für Discord", inline: true },
          { name: "🐾 Pet-Radar", value: "Fehlende Pets live auf deiner Spielkarte", inline: true },
          { name: "⏱️ Timer", value: "Echte Feldboss-Respawns vom Server", inline: true },
          { name: "👥 Gruppe", value: "Kampfkraft und Waffe neuer Mitglieder", inline: true },
          { name: "📶 Verbindung", value: "Woran liegt der Lag?", inline: true },
        ],
        footer: { text: "Windows 10/11 · keine Installation · Updates automatisch" },
      }],
      components: [{ type: 1, components: [
        { type: 2, style: 5, label: "Download", emoji: { name: "⬇️" }, url: DOWNLOAD },
        { type: 2, style: 5, label: "Webseite", emoji: { name: "🌐" }, url: WEB },
        { type: 2, style: 5, label: "Discord", emoji: { name: "💬" }, url: "https://discord.gg/xExgsyDEP2" },
      ] }],
    });
  })());
  return deferred();
}

export function help(env: Env) {
  return respond({
    flags: 64,
    embeds: [{
      color: GOLD,
      title: "🤖 Befehle des AetherDPS-Bots",
      description: [
        "**🧙 Charaktere** (öffentliche Daten von NCSOFT)",
        "`/charakter` Profil mit Kampfkraft, Ausrüstung, Werten, Skills und Daevanion zum Durchblättern",
        "`/vergleich` zwei Charaktere nebeneinander",
        "`/ich verknüpfen` deinen Charakter verknüpfen. Danach reicht `/charakter`, auf diesem Server bekommst du die Klassen-Rolle, und andere sehen ihn per **Rechtsklick auf dich → Apps → AION-Charakter**",
        "",
        "**🗺️ Spielinfos**",
        "`/events` nächste Events und Resets als Countdown",
        "`/feldboss` Gebiet und Respawn, mit *getötet* ein Countdown für alle",
        "`/pet` Fundorte und welches Monster die Seele droppt",
        "`/checkliste` Tägliches und Wöchentliches",
        "`/patchnotes` neueste offizielle News",
        "",
        "**📊 AetherDPS**",
        "`/aetherdps` neueste Version und Download",
        "",
        "💡 **Tipp:** Über das Bot-Profil → **App hinzufügen** kannst du die Befehle in jedem Server und in DMs nutzen.",
      ].join("\n"),
    }],
  });
}
