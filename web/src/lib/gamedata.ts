// Liest die Spieldaten der App (AetherDPS.Engine/GameData) beim Bauen der Seite. Eine Quelle für App und Webseite.
import fs from 'node:fs';
import path from 'node:path';
import type { Schedule } from './schedule';

const ROOT = path.resolve(process.cwd(), '../AetherDPS.Engine/GameData');
const read = <T>(file: string): T => JSON.parse(fs.readFileSync(path.join(ROOT, file), 'utf8'));

type Names = Record<string, string>;
const npcs = read<Record<string, { name: string }>>('de/npcs.json');
export const npcName = (id: number | string) => npcs[String(id)]?.name ?? `#${id}`;

export interface FieldBoss { slot: number; npc: number; name: string; respawn?: number; fixed?: Schedule }
export interface FieldBossMap { id: number; key: string; name: string; bosses: FieldBoss[] }

interface RawBoss { slot: number; npc: number; respawn?: number | null; fixed?: Schedule }

export function fieldBosses(): FieldBossMap[] {
  const raw = read<{ maps: { map: number; key: string; names: Names; bosses: RawBoss[] }[] }>('field_bosses.json');
  return raw.maps.map((m) => ({
    id: m.map,
    key: m.key,
    name: m.names.de ?? m.names.en,
    bosses: m.bosses.map((b) => ({
      slot: b.slot,
      npc: b.npc,
      name: npcName(b.npc),
      respawn: b.respawn ?? undefined,
      fixed: b.fixed,
    })),
  }));
}

/** Gattungen wie im Spiel (Cognia, Fera …) mit den Farben aus der App. */
export const PET_CATEGORIES: Record<string, { name: string; color: string }> = {
  Intellect: { name: 'Cognia', color: '#4f9ce8' },
  Feral: { name: 'Fera', color: '#e8894f' },
  Nature: { name: 'Natura', color: '#5cc26a' },
  Trans: { name: 'Varia', color: '#a77be8' },
  Special: { name: 'Specia', color: '#e0c04a' },
};

export interface Pet { id: number; name: string; category: string; group: string; regions: string[]; monsters: string[] }

const GROUP_NAMES: Record<string, string> = { verteron: 'Verteron', altgard: 'Altgard', abyss: 'Abyss' };

export function pets(): Pet[] {
  const raw = read<{ pets: Record<string, { category: string; names: Names }>; monsters: Record<string, number> }>('pets/pets.json');
  const regions = read<{ groups: { id: string; regions: { names: Names; pets: number[] }[] }[] }>('pets/regions.json');
  const where = new Map<number, { group: string; regions: Set<string> }>();
  for (const g of regions.groups)
    for (const r of g.regions)
      for (const p of r.pets) {
        const e = where.get(p) ?? { group: GROUP_NAMES[g.id] ?? g.id, regions: new Set<string>() };
        e.regions.add(r.names.de ?? r.names.en);
        where.set(p, e);
      }
  const monsters = new Map<number, Set<string>>();
  for (const [npc, pet] of Object.entries(raw.monsters)) {
    const set = monsters.get(pet) ?? new Set<string>();
    set.add(npcName(npc));
    monsters.set(pet, set);
  }
  return Object.entries(raw.pets)
    .map(([id, p]) => ({
      id: +id,
      name: (p.names.de ?? p.names.en).replace(/^Pet[:：]\s*/, ''),
      category: p.category,
      group: where.get(+id)?.group ?? '',
      regions: [...(where.get(+id)?.regions ?? [])],
      monsters: [...(monsters.get(+id) ?? [])].filter((n) => !n.startsWith('#')).slice(0, 8),
    }))
    .sort((a, b) => a.name.localeCompare(b.name, 'de'));
}

/** EU-Server aus servers.json: 13xx Elyos, 23xx Asmodier (wie in der App). */
export function euServers(): { elyos: { id: number; name: string }[]; asmodian: { id: number; name: string }[] } {
  const raw = read<{ servers: Record<string, Names> }>('servers.json').servers;
  const list = (prefix: string) =>
    Object.entries(raw)
      .filter(([id]) => id.length === 4 && id.startsWith(prefix))
      .map(([id, n]) => ({ id: Number(id), name: n.de ?? n.en }))
      .sort((a, b) => a.name.localeCompare(b.name, 'de'));
  return { elyos: list('13'), asmodian: list('23') };
}
