// Vorschau ohne Discord-Client: erzeugt die Antworten der Befehle als JSON (node dist/preview.mjs <profil.json>).
// Aufruf: npm run preview -- <profil.json> > vorschau.json
import { readFileSync } from "node:fs";
import { render } from "../src/character";
import { serverChoices } from "../src/character";
import { checklist, events, fieldBoss, help, pet, petChoices, bossChoices } from "../src/info";

const env: any = { EMOJIS: {}, ROLES: { class: {} } };
const profile = JSON.parse(readFileSync(process.argv[2], "utf8"));
const body = async (r: Response) => (await r.json() as any).data;

const out: Record<string, unknown> = {};
for (const v of ["main", "gear", "stats", "skills", "daeva"] as const) out[`charakter_${v}`] = render(env, profile, v);
out.events = await body(events());
out.checkliste = await body(checklist());
out.hilfe = await body(help(env));
const firstPet = petChoices("drache")[0];
out.pet = await body(pet({ name: firstPet?.value }));
const boss = bossChoices("")[0];
out.feldboss = await body(fieldBoss({ name: boss.value }, "0"));
out.feldboss_getoetet = await body(fieldBoss({ name: bossChoices("").find((b) => true)!.value, "getötet": true }, "0"));
out.autocomplete = { server_ho: serverChoices("ho"), pet_drache: petChoices("drache").slice(0, 5), boss_leer: bossChoices("").slice(0, 3) };
console.log(JSON.stringify(out));
