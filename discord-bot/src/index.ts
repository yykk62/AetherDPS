/**
 * Discord-Bot für AetherDPS – läuft ohne dauerhafte Verbindung (HTTP-Interaktionen + Cron):
 *  - POST /interactions: Slash-Befehle (/charakter, /vergleich, /ich, /pet, /feldboss, /events, …),
 *    Rechtsklick „AION-Charakter“, Knöpfe unter Profilen und die Rollen-Knöpfe in #willkommen
 *  - Cron alle 15 Min.: offizielle AION-2-News und AetherDPS-Releases posten (src/feeds.ts)
 *
 * Die Befehle funktionieren auf dem AetherDPS-Server, in anderen Servern (wenn der Bot dort ist) und
 * als „App hinzufügen“ in jedem Server und in DMs. Gespeichert wird nur die Verknüpfung Discord → Charakter
 * (/ich verknüpfen, KV LINKS), protokolliert wird nichts.
 */

import { bossChoices, aetherdps, checklist, events, fieldBoss, help, patchnotes, pet, petChoices } from "./info";
import { character, compare, me, serverChoices, switchView, userContext } from "./character";
import { isRoleButton, roleButton } from "./roles";
import { postNews, postReleases } from "./feeds";
import { Env, options, promo, whisper } from "./discord";

/** Sofort-Antwort um die Knopfzeile ergänzen (außerhalb des AetherDPS-Servers, siehe promo). */
async function promoted(env: Env, i: any, res: Response): Promise<Response> {
  const j = await res.json<any>();
  if (j.type === 4) j.data = promo(env, i, j.data);
  return Response.json(j);
}

const hexToBytes = (hex: string) => new Uint8Array(hex.match(/../g)!.map((b) => parseInt(b, 16)));

async function verify(req: Request, body: string, env: Env): Promise<boolean> {
  const sig = req.headers.get("X-Signature-Ed25519");
  const ts = req.headers.get("X-Signature-Timestamp");
  if (!sig || !ts || !/^[0-9a-f]{128}$/i.test(sig)) return false;
  const key = await crypto.subtle.importKey("raw", hexToBytes(env.DISCORD_PUBLIC_KEY), { name: "Ed25519" }, false, ["verify"]);
  return crypto.subtle.verify("Ed25519", key, hexToBytes(sig), new TextEncoder().encode(ts + body));
}

async function interaction(req: Request, env: Env, ctx: ExecutionContext): Promise<Response> {
  const body = await req.text();
  if (!(await verify(req, body, env))) return new Response("bad signature", { status: 401 });
  const i = JSON.parse(body);
  const user: string = i.member?.user?.id ?? i.user?.id;

  if (i.type === 1) return Response.json({ type: 1 }); // PING

  if (i.type === 4) { // Autovervollständigung
    const { opts, focused } = options(i.data);
    const q = String(opts[focused ?? ""] ?? "");
    const choices = focused?.startsWith("server") ? serverChoices(q)
      : i.data.name === "pet" ? petChoices(q)
      : i.data.name === "feldboss" ? bossChoices(q)
      : [];
    return Response.json({ type: 8, data: { choices } });
  }

  if (i.type === 3) { // Knöpfe und Auswahlmenüs
    const id: string = i.data.custom_id;
    if (isRoleButton(id)) return roleButton(env, i);
    if (id.startsWith("cv:")) return switchView(env, ctx, i, user, id);
    return whisper("Dieser Knopf ist veraltet.");
  }

  if (i.type === 2) { // Befehle
    if (i.data.type === 2) return userContext(env, ctx, i, user);
    const { sub, opts } = options(i.data);
    switch (i.data.name) {
      case "charakter": return character(env, ctx, i, user, opts);
      case "vergleich": return compare(env, ctx, i, user, opts);
      case "ich": return me(env, ctx, i, user, sub, opts);
      case "pet": return promoted(env, i, pet(opts));
      case "feldboss": return promoted(env, i, fieldBoss(opts, user));
      case "events": return promoted(env, i, events());
      case "checkliste": return checklist();
      case "patchnotes": return patchnotes(env, ctx, i);
      case "aetherdps": return aetherdps(env, ctx, i);
      case "hilfe": return help(env);
    }
  }
  return whisper("Unbekannter Befehl.");
}

export default {
  async fetch(req: Request, env: Env, ctx: ExecutionContext): Promise<Response> {
    const url = new URL(req.url);
    if (req.method === "POST" && url.pathname === "/interactions") {
      try {
        return await interaction(req, env, ctx);
      } catch (e) {
        console.error(String(e)); // nur im Live-Log (wrangler tail)
        return whisper("😵 Da ist etwas schiefgelaufen. Bitte nochmal versuchen.");
      }
    }
    return Response.redirect("https://aetherdps.com/", 302);
  },
  async scheduled(_: ScheduledController, env: Env, ctx: ExecutionContext) {
    ctx.waitUntil(Promise.allSettled([postNews(env), postReleases(env)]).then((results) => {
      for (const r of results) if (r.status === "rejected") console.error(String(r.reason));
    }));
  },
};
