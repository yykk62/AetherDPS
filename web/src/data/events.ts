import type { Schedule } from '../lib/schedule';

export interface GameEvent {
  id: string;
  type: 'pvp' | 'boss' | 'event';
  name: string;
  en: string;
  note: string;
  schedule: Schedule;
  /** false = für EU noch nicht bestätigt (siehe docs/PLAN.md). */
  confirmed: boolean;
}

// Riss und Artefakt-Belagerung wie in der App (ScheduleEntry.Builtins), der Rest nach Wakayashi.
export const EVENTS: GameEvent[] = [
  {
    id: 'rift', type: 'pvp', name: 'Raum-Zeit-Riss', en: 'Spacetime Rift',
    note: 'Portal ca. 10 Min. offen, Event ca. 1 Std. Über den Riss kommst du auf die andere Fraktionsseite.',
    schedule: { kind: 'cycle', base: '02:00', everyMinutes: 180, openMinutes: 10 }, confirmed: true,
  },
  {
    id: 'beritra', type: 'boss', name: 'Beritra-Luftangriff', en: 'Beritra Air Raid',
    note: 'Stündlich zur halben Stunde.',
    schedule: { kind: 'cycle', base: '00:30', everyMinutes: 60, openMinutes: 5 }, confirmed: false,
  },
  {
    id: 'shugo', type: 'event', name: 'Shugofesta', en: 'Shugo Festival',
    note: '9 Minispiele, kurzes Eintrittsfenster. Lohnt sich erst mit Level 45.',
    schedule: { kind: 'cycle', base: '00:00', everyMinutes: 60, openMinutes: 5 }, confirmed: false,
  },
  {
    id: 'artifact_siege', type: 'pvp', name: 'Abyss-Eroberung', en: 'Artifact Siege',
    note: 'Chaotische Untere Ebene von Reshanta, ab Level 45.',
    schedule: { kind: 'weekly', days: [1, 4, 6], at: '21:00', openMinutes: 30 }, confirmed: true,
  },
  {
    id: 'reshanta_bosses', type: 'boss', name: 'Reshanta-Weltbosse', en: 'Reshanta World Bosses',
    note: 'Argo, Kaira, Tamasa, Dramos, Marakha, Ducal.',
    schedule: { kind: 'weekly', days: [1, 4, 6], at: '21:00', openMinutes: 30 }, confirmed: false,
  },
  {
    id: 'siege_boss', type: 'boss', name: 'Abyss-Belagerungsboss & Nahma', en: 'Siege Boss & Nahma',
    note: 'Chaotische Untere Ebene von Reshanta.',
    schedule: { kind: 'weekly', days: [5, 0], at: '21:00', openMinutes: 20 }, confirmed: true,
  },
];

export const EVENT_TYPES: Record<GameEvent['type'], string> = { pvp: 'PvP', boss: 'Boss', event: 'Event' };
