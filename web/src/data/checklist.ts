// Aufgaben der Checkliste nach Wakayashi (mit Erlaubnis), ergänzt um die Punkte aus der App.
// who: 'all' = jeder Charakter, 'main' = nur der Main. count = wie oft, label = Anzeige statt Zähler.

export interface Task {
  id: string;
  name: string;
  en: string;
  who: 'all' | 'main';
  count?: number;
  label?: string;
}

export interface Recharge { name: string; en: string; amount: string; who: 'all' | 'main'; note?: string }

export const DAILY: Task[] = [
  { id: 'duty', name: 'Pflicht-Quests', en: 'Duty Quests', who: 'main', count: 5 },
  { id: 'fieldboss', name: 'Feldbosse', en: 'Field Bosses', who: 'main', count: 2 },
];

export const WEEKLY: Task[] = [
  { id: 'daily_dungeon', name: 'Täglicher Dungeon', en: 'Daily Dungeon', who: 'main', count: 14 },
  { id: 'ascension', name: 'Aszensionsritus', en: 'Ascension Trial', who: 'all', count: 3 },
  { id: 'battlefield', name: 'Schlachtfeld', en: 'Battlefield', who: 'all', count: 3 },
  { id: 'sanctuary', name: 'Weihstätte', en: 'Sanctuary', who: 'all', count: 1 },
  { id: 'pve_commands', name: 'PvE-Händler-Aufträge', en: 'Commands (PvE)', who: 'main', count: 12 },
  { id: 'pvp_commands', name: 'PvP-Händler-Aufträge', en: 'Abyss Commands (PvP)', who: 'main', count: 20 },
  { id: 'morph', name: 'Konvertierung Energie', en: 'Substance Morph', who: 'all', label: '20/4' },
  { id: 'pass_energy', name: 'Pass-Energie', en: 'Pass Energy', who: 'all', label: '20/4' },
  { id: 'pass_exp', name: 'Pass-EXP', en: 'Daeva Pass EXP', who: 'all', label: 'max.' },
];

export const RECHARGES: Recharge[] = [
  { name: 'Expedition / Transzendenz', en: 'Expedition / Transcendence', amount: '15 Energie / 3 Std.', who: 'all', note: 'Stapelt 4–7 Tage, dann läuft sie über.' },
  { name: 'Alptraum', en: 'Nightmare', amount: '2 Tickets / Tag', who: 'all' },
  { name: 'Shugofesta', en: 'Shugo Festival', amount: '3 Schlüssel / Tag', who: 'main' },
  { name: 'Beritra', en: 'Beritra', amount: '1 Schlüssel / Tag', who: 'main' },
  { name: 'Abyss-Artefakt', en: 'Abyss Artifact', amount: '0–3 Korridore', who: 'all', note: '1 Korridor je gehaltenem Artefakt.' },
];
