// /charakter, /vergleich, /ich und Rechtsklick „AION-Charakter“: öffentliche Profile von NCSOFT über den
// Worker aetherdps-api (dieselbe Abfrage wie „Mein Charakter“ auf aetherdps.com, 1 Std. Cache, ToS-Grenzen).

import data from "./gamedata.json";
import { cut, deferred, discord, Env, finish, GOLD, Message, nf, WEB, whisper } from "./discord";

interface Item { slot: string; name: string; grade: string; enchant: number; exceed: number }
interface Profile {
  name: string; server: string; serverId: number; className: string; race: string; level: number;
  itemLevel: number; combatPower: number; portrait: string | null; title: { name: string; grade: string } | null;
  stats: { type: string; name: string; value: number }[];
  boards: { name: string; open: number; total: number; percent: number }[];
  titles: { owned: number };
  equipment: Item[];
  skills: { name: string; category: string; level: number; equipped: boolean }[];
  pet: { name: string; level: number } | null;
  wing: { name: string; grade: string; enchant: number } | null;
  loadedAt: string;
}
type Linked = { name: string; server: number };

const NAME_RE = /^[\p{L}\p{N}]{2,24}$/u;
const SERVERS: { id: number; name: string; faction: string }[] = data.servers;

// ---------- Klassen, Seltenheit, Slots ----------

const CLASSES: [RegExp, string, number][] = [
  [/gladiator/i, "gladiator", 0x4fc1c9], [/templer|templar/i, "templar", 0x5b8def], [/assassin/i, "assassin", 0x8bc34a],
  [/waldl|ranger/i, "ranger", 0x3fae7a], [/magier|zauberer|sorcer/i, "sorcerer", 0xa15ce0],
  [/beschw|element|spirit/i, "elementalist", 0xd65cc8], [/kleriker|cleric/i, "cleric", 0xe0b84a],
  [/kantor|chanter/i, "chanter", 0xe08a3c], [/faust|brawler|fighter/i, "brawler", 0xd64545],
];
const classOf = (name: string) => {
  const c = CLASSES.find(([re]) => re.test(name));
  return c ? { key: c[1], color: c[2] } : { key: "", color: GOLD };
};

// Wie im Spiel (EU-Client): Unique = Einzigartig gold, Legend blau
const GRADES: Record<string, string> = { Unique: "🟨", Legend: "🟦", Epic: "🟪", Special: "🟩", Rare: "🟢" };
const grade = (g: string) => GRADES[g] ?? "⬜";

const SLOTS: Record<string, string> = {
  MainHand: "Waffe", SubHand: "Nebenhand", Torso: "Brust", Pants: "Beine", Helmet: "Kopf", Shoulder: "Schultern",
  Gloves: "Hände", Boots: "Füße", Cape: "Umhang", Belt: "Gürtel", Necklace: "Halskette", Earring1: "Ohrring",
  Earring2: "Ohrring 2", Ring1: "Ring", Ring2: "Ring 2", Bracelet1: "Armband", Bracelet2: "Armband 2",
  Rune1: "Rune", Rune2: "Rune 2", Amulet: "Amulett",
};
const ARMOR = ["MainHand", "SubHand", "Torso", "Pants", "Helmet", "Shoulder", "Gloves", "Boots", "Cape"];
const slotName = (s: string) => SLOTS[s] ?? (/^Arcana(\d+)$/.exec(s) ? `Arkanum ${/\d+/.exec(s)![0]}` : s);
const plus = (i: { enchant: number; exceed?: number }) =>
  i.enchant || i.exceed ? ` **+${i.enchant}**${i.exceed ? ` (+${i.exceed})` : ""}` : "";

const averageEnchant = (p: Profile) => {
  const gear = p.equipment.filter((i) => ARMOR.includes(i.slot));
  return gear.length ? gear.reduce((s, i) => s + i.enchant + i.exceed, 0) / gear.length : 0;
};
const daevanion = (p: Profile) => {
  const total = p.boards.reduce((s, b) => s + b.total, 0);
  return total ? Math.round((p.boards.reduce((s, b) => s + b.open, 0) * 100) / total) : 0;
};
const bar = (percent: number, width = 10) => {
  const full = Math.round((percent / 100) * width);
  return "▰".repeat(full) + "▱".repeat(width - full);
};

// ---------- Server ----------

export function serverChoices(query: string) {
  const q = query.trim().toLowerCase();
  return SERVERS.filter((s) => !q || s.name.toLowerCase().includes(q) || String(s.id).startsWith(q))
    .sort((a, b) => Number(!a.name.toLowerCase().startsWith(q)) - Number(!b.name.toLowerCase().startsWith(q)))
    .slice(0, 25)
    .map((s) => ({ name: `${s.name} (${s.faction})`, value: String(s.id) }));
}

/** Server aus der Auswahl (ID) oder getippt (Name, Groß-/Kleinschreibung egal). */
function server(value: unknown): number | null {
  const v = String(value ?? "").trim();
  const byId = SERVERS.find((s) => String(s.id) === v);
  if (byId) return byId.id;
  const byName = SERVERS.filter((s) => s.name.toLowerCase() === v.toLowerCase().replace(/\s*\(.*\)$/, ""));
  return byName.length === 1 ? byName[0].id : null;
}
const serverName = (id: number) => SERVERS.find((s) => s.id === id)?.name ?? `#${id}`;

// ---------- Laden ----------

const ERRORS: Record<string, string> = {
  notfound: "🔍 Diesen Charakter gibt es auf dem Server nicht. Stimmt die Schreibweise?",
  rate: "⏳ Gerade zu viele Abfragen. Bitte in einer Minute nochmal.",
  busy: "⏳ NCSOFT wird gerade oft gefragt. Bitte in ein paar Sekunden nochmal.",
  input: "✏️ Name oder Server ungültig.",
  upstream: "🛠️ NCSOFT antwortet gerade nicht (Wartung?). Bitte später nochmal.",
};

export async function load(env: Env, c: Linked): Promise<Profile | string> {
  try {
    const res = await env.API.fetch(`https://api/api/character?name=${encodeURIComponent(c.name)}&server=${c.server}`, {
      headers: { "X-AetherDPS-Bot": env.BOT_KEY },
    });
    const body = await res.json<any>();
    return res.ok ? (body as Profile) : ERRORS[body?.error] ?? ERRORS.upstream;
  } catch {
    return ERRORS.upstream;
  }
}

async function allowed(env: Env, user: string) {
  return (await env.USER_LIMIT.limit({ key: user })).success;
}

// ---------- Ansichten ----------

export type View = "main" | "gear" | "stats" | "skills" | "daeva";

function header(env: Env, p: Profile) {
  const cls = classOf(p.className);
  return {
    color: cls.color,
    author: {
      name: `${p.className} · Stufe ${p.level} · ${p.race} · ${p.server}`,
      ...(cls.key ? { icon_url: `${WEB}/gen/classes/${cls.key}.png` } : {}),
    },
    title: `${cls.key ? "" : "🧙 "}${p.name}`,
    thumbnail: p.portrait ? { url: p.portrait } : undefined,
    footer: { text: "Öffentliche Daten von NCSOFT · bis 1 Std. zwischengespeichert · AetherDPS" },
    timestamp: p.loadedAt,
  };
}

export function render(env: Env, p: Profile, view: View): Message {
  const e: any = header(env, p);
  if (view === "main") {
    const weapon = p.equipment.find((i) => i.slot === "MainHand");
    // Lange Namen als volle Zeilen oben, in den 3er-Spalten nur kurze Zahlen (sonst bricht Discord um)
    e.description = [
      p.title ? `🏅 *„${p.title.name}“*` : "",
      weapon ? `🗡️ ${grade(weapon.grade)} ${weapon.name}${plus(weapon)}` : "",
      p.wing ? `🪽 ${grade(p.wing.grade)} ${p.wing.name}${plus(p.wing)}` : "",
      p.pet ? `🐾 ${p.pet.name} · Stufe ${p.pet.level}` : "",
    ].filter(Boolean).join("\n") || undefined;
    e.fields = [
      { name: "⚔️ Kampfkraft", value: `**${nf.format(p.combatPower)}**`, inline: true },
      { name: "🎒 Itemstufe", value: `**${nf.format(p.itemLevel)}**`, inline: true },
      { name: "✨ Ø Verzaub.", value: `**+${averageEnchant(p).toFixed(1).replace(".", ",")}**`, inline: true },
      { name: "🔮 Daevanion", value: `**${daevanion(p)} %**`, inline: true },
      { name: "🏆 Titel", value: `**${nf.format(p.titles.owned)}**`, inline: true },
      { name: "📚 Skills", value: `**${p.skills.length}**`, inline: true },
    ];
  } else if (view === "gear") {
    const row = (i: Item) => `${grade(i.grade)} ${slotName(i.slot)} · ${i.name}${plus(i)}`;
    const armor = p.equipment.filter((i) => ARMOR.includes(i.slot));
    const rest = p.equipment.filter((i) => !ARMOR.includes(i.slot));
    e.fields = [
      { name: "🛡️ Waffe und Rüstung", value: cut(armor.map(row).join("\n") || "–", 1024) },
      { name: "💍 Schmuck und Weiteres", value: cut(rest.map(row).join("\n") || "–", 1024) },
    ];
    e.description = `Ø Verzauberung (Waffe + Rüstung): **+${averageEnchant(p).toFixed(1).replace(".", ",")}** · Itemstufe **${nf.format(p.itemLevel)}**`;
  } else if (view === "stats") {
    // Volle Breite statt schmaler Spalten: nichts bricht mitten im Namen um
    const base = p.stats.filter((s) => !s.name.includes("["));
    const gods = p.stats.filter((s) => s.name.includes("["));
    const god = (s: Profile["stats"][number]) => {
      const [, name, deity] = /^(.*?)\s*\[(.*)\]$/.exec(s.name) ?? [, s.name, ""];
      return `**${nf.format(s.value)}** ${name}${deity ? ` · *${deity}*` : ""}`;
    };
    const half = Math.ceil(gods.length / 2);
    e.fields = [
      { name: "💪 Grundwerte", value: cut(base.map((s) => `${s.name} **${nf.format(s.value)}**`).join("  ·  ") || "–", 1024) },
      ...(gods.length ? [
        { name: "✴️ Götter-Werte", value: cut(gods.slice(0, half).map(god).join("\n"), 1024), inline: true },
        { name: "​", value: cut(gods.slice(half).map(god).join("\n") || "​", 1024), inline: true },
      ] : []),
    ];
  } else if (view === "skills") {
    // Je Gruppe eine Zeile über die volle Breite: ausgerüstete zuerst, die übrigen klein darunter
    const group = (cat: string) => {
      const list = p.skills.filter((s) => s.category === cat);
      const fmt = (x: typeof list) => x.map((s) => `${s.name} **${s.level}**`).join("  ·  ");
      // Passive markiert NCSOFT nie als ausgerüstet (sie wirken immer): dann alle normal zeigen
      const marked = list.some((s) => s.equipped);
      const on = marked ? list.filter((s) => s.equipped) : list;
      const off = marked ? list.filter((s) => !s.equipped) : [];
      return [on.length ? `${marked ? "🔹 " : ""}${fmt(on)}` : "",off.length ? `-# Nicht ausgerüstet: ${fmt(off)}` : ""].filter(Boolean).join("\n");
    };
    const NAMES: [string, string][] = [["Active", "⚡ Aktiv"], ["Passive", "🛡️ Passiv"], ["Dp", "🌟 DP-Skills"]];
    e.fields = NAMES.filter(([cat]) => p.skills.some((s) => s.category === cat))
      .map(([cat, name]) => ({ name, value: cut(group(cat), 1024) }));
    e.description = "🔹 ausgerüstet · Zahl = Skill-Stufe\n-# NCSOFT zeigt die Skills von Slot 1. Wer mit einem anderen Slot spielt, sieht hier andere Werte.";
  } else {
    e.description = p.boards.length
      ? p.boards.map((b) => `**${b.name}** ${bar(b.percent)} ${b.percent} % · ${b.open}/${b.total}`).join("\n")
      : "Noch keine Daevanion-Bretter.";
    e.fields = [{ name: "🔮 Gesamt", value: `**${daevanion(p)} %** der Knoten offen` }];
  }
  return { embeds: [e], components: [buttons(p, view)] };
}

const VIEWS: [View, string, string][] = [
  ["main", "Übersicht", "🧙"], ["gear", "Ausrüstung", "🛡️"], ["stats", "Werte", "💪"], ["skills", "Skills", "⚡"], ["daeva", "Daevanion", "🔮"],
];
function buttons(p: Profile, current: View) {
  return {
    type: 1,
    components: VIEWS.map(([view, label, emoji]) => ({
      type: 2, style: view === current ? 1 : 2, label, emoji: { name: emoji },
      custom_id: `cv:${view}:${p.serverId}:${p.name}`, disabled: view === current,
    })),
  };
}

// ---------- Befehle ----------

const linkOf = async (env: Env, user: string): Promise<Linked | null> => env.LINKS.get<Linked>(`u:${user}`, "json");
const NOT_LINKED = "Du hast noch keinen Charakter verknüpft. Gib Name und Server an oder verknüpfe ihn einmal mit `/ich verknüpfen`.";

/** Wer ist gemeint? Angaben im Befehl, sonst der verknüpfte Charakter. */
async function target(env: Env, user: string, name: unknown, srv: unknown): Promise<Linked | string> {
  if (!name && !srv) return (await linkOf(env, user)) ?? NOT_LINKED;
  if (!name || !srv) return "Bitte **Name und Server** angeben (der Server lässt sich beim Tippen auswählen).";
  const id = server(srv);
  if (!id) return "Diesen Server kenne ich nicht. Wähle ihn am besten aus der Liste beim Tippen.";
  const n = String(name).trim();
  if (!NAME_RE.test(n)) return ERRORS.input;
  return { name: n, server: id };
}

export async function character(env: Env, ctx: ExecutionContext, i: any, user: string, opts: Record<string, any>) {
  const who = await target(env, user, opts.name, opts.server);
  if (typeof who === "string") return whisper(who);
  return show(env, ctx, i, user, who);
}

async function show(env: Env, ctx: ExecutionContext, i: any, user: string, who: Linked, ephemeral = false) {
  if (!(await allowed(env, user))) return whisper(ERRORS.rate);
  ctx.waitUntil((async () => {
    const p = await load(env, who);
    await finish(env, i.token, typeof p === "string" ? { content: p, embeds: [] } : render(env, p, "main"));
  })());
  return deferred(ephemeral);
}

/** Knopf unter dem Profil: andere Ansicht (jeder darf blättern, der Cache verhindert neue NCSOFT-Abfragen). */
export async function switchView(env: Env, ctx: ExecutionContext, i: any, user: string, id: string) {
  const [, view, srv, ...rest] = id.split(":");
  const who = { name: rest.join(":"), server: Number(srv) };
  if (!(await allowed(env, user))) return whisper(ERRORS.rate);
  ctx.waitUntil((async () => {
    const p = await load(env, who);
    if (typeof p !== "string") await finish(env, i.token, render(env, p, view as View));
  })());
  return Response.json({ type: 6 });
}

export async function compare(env: Env, ctx: ExecutionContext, i: any, user: string, opts: Record<string, any>) {
  const a = await target(env, user, opts.name1, opts.server1);
  if (typeof a === "string") return whisper(a);
  const b = await target(env, user, opts.name2, opts.server2);
  if (typeof b === "string") return whisper(b === NOT_LINKED ? "Für den zweiten Charakter Name und Server angeben, oder deinen mit `/ich verknüpfen` verknüpfen." : b);
  if (!(await allowed(env, user))) return whisper(ERRORS.rate);
  ctx.waitUntil((async () => {
    const pa = await load(env, a);
    const pb = await load(env, b);
    if (typeof pa === "string" || typeof pb === "string") {
      await finish(env, i.token, { content: typeof pa === "string" ? `**${a.name}:** ${pa}` : `**${b.name}:** ${pb}` });
      return;
    }
    const rows: [string, (p: Profile) => number, (v: number) => string][] = [
      ["⚔️ Kampfkraft", (p) => p.combatPower, (v) => nf.format(v)],
      ["🎒 Itemstufe", (p) => p.itemLevel, (v) => nf.format(v)],
      ["✨ Ø Verzauberung", averageEnchant, (v) => `+${v.toFixed(1).replace(".", ",")}`],
      ["🔮 Daevanion", daevanion, (v) => `${v} %`],
      ["📈 Stufe", (p) => p.level, (v) => String(v)],
      ["🏆 Titel", (p) => p.titles.owned, (v) => nf.format(v)],
    ];
    const col = (p: Profile, other: Profile) => rows.map(([label, f, fmt]) => {
      const v = f(p), o = f(other);
      return `${label}\n**${fmt(v)}**${v > o ? " 🔺" : ""}`;
    }).join("\n\n");
    const cls = (p: Profile) => {
      const k = classOf(p.className).key;
      return `${k && env.EMOJIS[k] ? env.EMOJIS[k] + " " : ""}${p.className} · ${p.server}`;
    };
    const diff = pa.combatPower - pb.combatPower;
    await finish(env, i.token, {
      embeds: [{
        color: GOLD,
        title: `⚔️ ${pa.name} vs. ${pb.name}`,
        description: diff === 0 ? "Gleiche Kampfkraft!" :
          `**${diff > 0 ? pa.name : pb.name}** liegt **${nf.format(Math.abs(diff))}** Kampfkraft vorn (${Math.round(Math.abs(diff) * 100 / Math.max(1, Math.min(pa.combatPower, pb.combatPower)))} %).`,
        fields: [
          { name: pa.name, value: `${cls(pa)}\n\n${col(pa, pb)}`, inline: true },
          { name: pb.name, value: `${cls(pb)}\n\n${col(pb, pa)}`, inline: true },
        ],
        footer: { text: "Öffentliche Daten von NCSOFT · AetherDPS" },
      }],
    });
  })());
  return deferred();
}

export async function me(env: Env, ctx: ExecutionContext, i: any, user: string, sub: string | undefined, opts: Record<string, any>) {
  if (sub === "lösen") {
    await env.LINKS.delete(`u:${user}`);
    if (i.guild_id === env.GUILD_ID) ctx.waitUntil(setClassRole(env, user, i.member?.roles ?? [], null));
    return whisper("🗑️ Verknüpfung gelöscht. Gespeichert ist nichts mehr.");
  }
  if (sub === "anzeigen") {
    const link = await linkOf(env, user);
    return link ? show(env, ctx, i, user, link) : whisper(NOT_LINKED);
  }
  // verknüpfen: erst prüfen, ob es den Charakter gibt
  const who = await target(env, user, opts.name, opts.server);
  if (typeof who === "string") return whisper(who);
  if (!(await allowed(env, user))) return whisper(ERRORS.rate);
  ctx.waitUntil((async () => {
    const p = await load(env, who);
    if (typeof p === "string") {
      await finish(env, i.token, { content: p });
      return;
    }
    await env.LINKS.put(`u:${user}`, JSON.stringify({ name: p.name, server: p.serverId }));
    let role = "";
    if (i.guild_id === env.GUILD_ID) {
      const key = classOf(p.className).key;
      if (key && (await setClassRole(env, user, i.member?.roles ?? [], key))) role = `\n🎭 Deine Klassen-Rolle ist jetzt <@&${env.ROLES.class[key]}>.`;
    }
    const msg = render(env, p, "main");
    await finish(env, i.token, {
      ...msg,
      content: `✅ **${p.name}** (${serverName(p.serverId)}) ist jetzt mit dir verknüpft.\n` +
        `\`/charakter\` ohne Angaben zeigt ihn, und andere sehen ihn per Rechtsklick auf dich → Apps → **AION-Charakter**.${role}\n` +
        "-# Gespeichert werden nur Name und Server, `/ich lösen` löscht das.",
    });
  })());
  return deferred(true);
}

/** Klassen-Rolle auf dem AetherDPS-Server setzen (andere Klassen-Rollen entfernen); key null = alle weg. */
async function setClassRole(env: Env, user: string, has: string[], key: string | null): Promise<boolean> {
  try {
    for (const [k, role] of Object.entries(env.ROLES.class)) {
      const path = `/guilds/${env.GUILD_ID}/members/${user}/roles/${role}`;
      if (k === key && !has.includes(role)) await discord(env, "PUT", path);
      if (k !== key && has.includes(role)) await discord(env, "DELETE", path);
    }
    return true;
  } catch {
    return false;
  }
}

/** Rechtsklick auf ein Mitglied → Apps → „AION-Charakter“. */
export async function userContext(env: Env, ctx: ExecutionContext, i: any, user: string) {
  const targetId: string = i.data.target_id;
  const link = await linkOf(env, targetId);
  if (!link) {
    const self = targetId === user;
    return whisper(self ? NOT_LINKED : `<@${targetId}> hat keinen AION-Charakter verknüpft. Mit \`/ich verknüpfen\` geht das in Sekunden.`);
  }
  return show(env, ctx, i, user, link);
}

