// Zeitpläne in Serverzeit (Europe/Berlin = Ortszeit der EU-Server), umgerechnet in die Zeit des Browsers.
// Gleiche Bedeutung wie FixedSpawn/ScheduleEntry in AetherDPS.Engine.

export const SERVER_TZ = 'Europe/Berlin';

export type Schedule =
  | { kind: 'cycle'; base: string; everyMinutes: number; openMinutes?: number }
  | { kind: 'weekly'; days: number[]; at: string; openMinutes?: number };

const DAY = 86_400_000;
const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
let fmt: Intl.DateTimeFormat | undefined;

function parts(ms: number) {
  fmt ??= new Intl.DateTimeFormat('en-US', {
    timeZone: SERVER_TZ, hourCycle: 'h23', year: 'numeric', month: 'numeric', day: 'numeric',
    hour: 'numeric', minute: 'numeric', weekday: 'short',
  });
  const o: Record<string, string> = {};
  for (const p of fmt.formatToParts(new Date(ms))) o[p.type] = p.value;
  return { y: +o.year, m: +o.month, d: +o.day, h: +o.hour, mi: +o.minute, wd: WEEKDAYS.indexOf(o.weekday) };
}

/** Wandzeit in Serverzeit → Zeitpunkt (ms). Berücksichtigt Sommer-/Winterzeit. */
export function serverTime(y: number, m: number, d: number, h: number, mi: number): number {
  const target = Date.UTC(y, m - 1, d, h, mi);
  let guess = target;
  for (let i = 0; i < 3; i++) {
    const p = parts(guess);
    const diff = target - Date.UTC(p.y, p.m - 1, p.d, p.h, p.mi);
    if (!diff) break;
    guess += diff;
  }
  return guess;
}

const hm = (s: string) => s.split(':').map(Number) as [number, number];

/** Alle Termine eines Plans im Zeitraum [from, to]. */
export function occurrences(s: Schedule, from: number, to: number): number[] {
  const start = parts(from - DAY);
  const out: number[] = [];
  for (let i = 0; i < 400; i++) {
    const day = new Date(Date.UTC(start.y, start.m - 1, start.d + i));
    const [y, m, d, wd] = [day.getUTCFullYear(), day.getUTCMonth() + 1, day.getUTCDate(), day.getUTCDay()];
    if (serverTime(y, m, d, 0, 0) > to) break;
    if (s.kind === 'weekly') {
      if (s.days.includes(wd)) out.push(serverTime(y, m, d, ...hm(s.at)));
    } else {
      const [bh, bm] = hm(s.base);
      for (let t = (bh * 60 + bm) % s.everyMinutes; t < 1440; t += s.everyMinutes)
        out.push(serverTime(y, m, d, Math.floor(t / 60), t % 60));
    }
  }
  return out.filter((t) => t >= from && t <= to).sort((a, b) => a - b);
}

/** Nächster Termin; läuft er gerade (innerhalb von openMinutes), ist es der aktuelle. */
export function next(s: Schedule, now = Date.now()): { at: number; live: boolean } | null {
  const open = (s.openMinutes ?? 0) * 60_000;
  const at = occurrences(s, now - open, now + 8 * DAY)[0];
  return at === undefined ? null : { at, live: at <= now };
}

/** Letzter Termin vor jetzt (z. B. der letzte Reset). */
export function last(s: Schedule, now = Date.now()): number {
  return occurrences(s, now - 8 * DAY, now).at(-1) ?? 0;
}

export function countdown(ms: number, dayLabel = 'T'): string {
  const s = Math.max(0, Math.floor(ms / 1000));
  const d = Math.floor(s / 86400);
  const h = Math.floor((s % 86400) / 3600);
  const m = Math.floor((s % 3600) / 60);
  const sec = s % 60;
  const pad = (n: number) => String(n).padStart(2, '0');
  const time = h || d ? `${h}:${pad(m)}:${pad(sec)}` : `${pad(m)}:${pad(sec)}`;
  return d ? `${d} ${dayLabel} ${time}` : time;
}

const DE_DAYS = ['So', 'Mo', 'Di', 'Mi', 'Do', 'Fr', 'Sa'];

/** Kurzbeschreibung für die Anzeige, z. B. "Mo + Do + Sa 21:00" oder "alle 3 Std. ab 02:00". */
export function describe(s: Schedule): string {
  if (s.kind === 'weekly') {
    if (s.days.length === 7) return `täglich ${s.at}`;
    const days = [...s.days].sort((a, b) => ((a + 6) % 7) - ((b + 6) % 7));
    return `${days.map((d) => DE_DAYS[d]).join(' + ')} ${s.at}`;
  }
  if (s.everyMinutes === 60) return `stündlich um :${s.base.split(':')[1]}`;
  const h = s.everyMinutes / 60;
  return `alle ${Number.isInteger(h) ? `${h} Std.` : `${s.everyMinutes} Min.`} ab ${s.base}`;
}

/** Resetzeiten laut Wakayashi (09:00). Die App nimmt bisher 06:00 an, im Spiel prüfen (docs/PLAN.md). */
export const DAILY_RESET: Schedule = { kind: 'weekly', days: [0, 1, 2, 3, 4, 5, 6], at: '09:00' };
export const WEEKLY_RESET: Schedule = { kind: 'weekly', days: [3], at: '09:00' };
