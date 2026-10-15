// Kopiert Bilder aus dem App-Projekt und docs/media nach public/gen/, damit es nur eine Quelle gibt.
// Läuft automatisch vor "dev" und "build".
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const web = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const repo = path.resolve(web, '..');
const out = path.join(web, 'public', 'gen');
const copies = [
  ['AetherDPS.App/Assets/pets', 'pets'],
  ['AetherDPS.App/Assets/Classes', 'classes'],
  ['AetherDPS.App/Assets/icon.png', 'icon.png'],
  ['docs/media', 'media'],
];

fs.rmSync(out, { recursive: true, force: true });
for (const [from, to] of copies) fs.cpSync(path.join(repo, from), path.join(out, to), { recursive: true });
fs.cpSync(path.join(repo, 'AetherDPS.App/Assets/icon.png'), path.join(web, 'public', 'favicon.png'));
console.log('Bilder nach public/gen kopiert');
