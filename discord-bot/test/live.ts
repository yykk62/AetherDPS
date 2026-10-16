// Live-Test mit den echten Bindings (wrangler dev --remote test/live.ts): Profil über den API-Worker, KV, Rate Limit.
import { load, render } from "../src/character";
import type { Env } from "../src/discord";

export default {
  async fetch(req: Request, env: Env): Promise<Response> {
    const u = new URL(req.url);
    const name = u.searchParams.get("name") ?? "";
    const server = Number(u.searchParams.get("server"));
    const t0 = Date.now();
    const p = await load(env, { name, server });
    const ms = Date.now() - t0;
    await env.LINKS.put("u:test", JSON.stringify({ name: "Test", server: 1317 }));
    const kv = await env.LINKS.get("u:test", "json");
    await env.LINKS.delete("u:test");
    const gone = (await env.LINKS.get("u:test")) === null;
    const limits = [];
    for (let i = 0; i < 8; i++) limits.push((await env.USER_LIMIT.limit({ key: "test-user" })).success);
    return Response.json({
      ms, error: typeof p === "string" ? p : null,
      fields: typeof p === "string" ? null : (render(env, p, "main").embeds as any)[0].fields.length,
      kv, gone, limits,
    });
  },
};
