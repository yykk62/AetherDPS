// Der verbundene Charakter: Name und Server liegen nur im localStorage dieses Browsers (kein Cookie, kein Konto).
// Das Profil kommt bei jedem Besuch neu vom AetherDPS-Server (server/src/character.ts), der es bei NCSOFT abfragt
// und bis zu einer Stunde zwischenspeichert. Auch für Guides gedacht, die den Build prüfen (Skill-Stufen, Stigmas).

export interface Linked { name: string; server: number }

export interface Item { slot: string; name: string; grade: string; enchant: number; exceed: number; icon: string | null }
export interface Skill { name: string; category: string; level: number; equipped: boolean; icon: string | null }
export interface Stat { type: string; name: string; value: number; effects: string[] }
export interface Board { id: number; name: string; icon: string | null; open: number; total: number; percent: number }
export interface Title { category: string; name: string; grade: string; owned: number; total: number }

export interface Profile {
  name: string; server: string; serverId: number; className: string; classId: number; race: string; raceId: number;
  level: number; itemLevel: number; combatPower: number; portrait: string | null;
  title: { name: string; grade: string } | null;
  stats: Stat[]; boards: Board[]; titles: { owned: number; list: Title[] };
  equipment: Item[]; skills: Skill[];
  pet: { name: string; level: number; icon: string | null } | null;
  wing: { name: string; grade: string; enchant: number; icon: string | null } | null;
  loadedAt: string;
}

/** Fehler vom Server: input, notfound, rate, busy, upstream; offline = keine Verbindung. */
export type LoadError = 'input' | 'notfound' | 'rate' | 'busy' | 'upstream' | 'offline';

const KEY = 'aetherdps.character';
const API = (import.meta.env.PUBLIC_API_BASE as string | undefined) || 'https://aetherdps-api.pixel6269.workers.dev';

/** Waffe und Rüstung (wie Armory.IsArmorSlot); der Rest ist Schmuck, Runen, Amulett, Arkana. */
export const ARMOR_SLOTS = ['MainHand', 'SubHand', 'Torso', 'Pants', 'Helmet', 'Shoulder', 'Gloves', 'Boots', 'Cape'];

export function linked(): Linked | null {
  try {
    const v = JSON.parse(localStorage.getItem(KEY) ?? 'null') as Linked | null;
    return v && typeof v.name === 'string' && Number.isInteger(v.server) ? v : null;
  } catch {
    return null;
  }
}

export function link(value: Linked | null): void {
  try {
    if (value) localStorage.setItem(KEY, JSON.stringify(value));
    else localStorage.removeItem(KEY);
  } catch {
    /* gesperrter Speicher (privates Fenster): gilt dann nur bis zum Neuladen */
  }
}

export async function loadProfile(c: Linked): Promise<Profile | LoadError> {
  const url = `${API}/api/character?name=${encodeURIComponent(c.name)}&server=${c.server}`;
  try {
    const res = await fetch(url, { credentials: 'omit', referrerPolicy: 'no-referrer' });
    if (res.ok) return (await res.json()) as Profile;
    const body = (await res.json().catch(() => ({}))) as { error?: LoadError };
    return body.error ?? 'upstream';
  } catch {
    return 'offline';
  }
}

/** Mittlere Verzauberung über Waffe und Rüstung (wie ArmoryProfile.AverageEnchant). */
export function averageEnchant(p: Profile): number {
  const gear = p.equipment.filter((i) => ARMOR_SLOTS.includes(i.slot));
  return gear.length ? gear.reduce((s, i) => s + i.enchant + i.exceed, 0) / gear.length : 0;
}

/** Anteil offener Daevanion-Knoten über alle Bretter. */
export function daevanionPercent(p: Profile): number {
  const total = p.boards.reduce((s, b) => s + b.total, 0);
  return total ? Math.round((p.boards.reduce((s, b) => s + b.open, 0) * 100) / total) : 0;
}
