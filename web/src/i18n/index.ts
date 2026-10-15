import de from './de.json';

// Deutsch ist die Quelle. Weitere Sprachen kommen als eigene Dateien dazu (gleiche Schlüssel).
const dicts: Record<string, Record<string, string>> = { de };
export type Key = keyof typeof de;

export function t(key: Key, ...args: (string | number)[]): string {
  const text = dicts.de[key] ?? key;
  return args.reduce<string>((s, a, i) => s.replaceAll(`{${i}}`, String(a)), text);
}
