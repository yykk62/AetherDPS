// Rollen-Knöpfe in #willkommen: Klassen-Auswahl (bis zu 3) und Pings an/aus.

import { discord, Env, whisper } from "./discord";

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

export const isRoleButton = (id: string) => id in PINGS || id === "class";

export async function roleButton(env: Env, i: any): Promise<Response> {
  if (i.guild_id !== env.GUILD_ID || !i.member) return whisper("Das geht nur auf dem AetherDPS-Server.");
  const user: string = i.member.user.id;
  const has = new Set<string>(i.member.roles);
  const id: string = i.data.custom_id;
  const rolePath = (role: string) => `/guilds/${env.GUILD_ID}/members/${user}/roles/${role}`;

  const ping = PINGS[id];
  if (ping) {
    const role = env.ROLES[ping.key];
    const add = !has.has(role);
    await discord(env, add ? "PUT" : "DELETE", rolePath(role));
    return whisper(add ? ping.on : ping.off);
  }

  const chosen = new Set<string>(i.data.values ?? []);
  for (const [key, role] of Object.entries(env.ROLES.class)) {
    if (chosen.has(key) && !has.has(role)) await discord(env, "PUT", rolePath(role));
    if (!chosen.has(key) && has.has(role)) await discord(env, "DELETE", rolePath(role));
  }
  const names = [...chosen].map((k) => `<@&${env.ROLES.class[k]}>`).join(" ");
  return whisper(chosen.size
    ? `⚔️ Deine Klasse: ${names}\n💡 Mit \`/ich verknüpfen\` setzt der Bot sie automatisch passend zu deinem Charakter.`
    : "Klassen-Rolle entfernt.");
}
