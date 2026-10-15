# aetherdps.com

Die Webseite zu AetherDPS: Astro, rein statisch, ausgeliefert als Cloudflare Worker mit statischen Dateien.

```bash
npm install
npm run dev      # http://localhost:4321
npm run build    # nach dist/
npm run deploy   # bauen und mit wrangler veröffentlichen
```

- **Spieldaten:** `src/lib/gamedata.ts` liest beim Bauen `../AetherDPS.Engine/GameData` (Feldbosse, Pets, NPC-Namen). Ändert sich etwas in der App, ist die Seite nach dem nächsten Build aktuell.
- **Bilder:** `scripts/sync-assets.mjs` kopiert Pet-, Klassen- und App-Bilder nach `public/gen/` (nicht im Repo). Titelbilder liegen in `public/img/` (mit Higgsfield erzeugt).
- **Zeiten:** `src/lib/schedule.ts` rechnet Serverzeit (Europe/Berlin) in die Zeit des Browsers um; Events in `src/data/events.ts`, Checkliste in `src/data/checklist.ts`.
- **Mein Charakter:** `src/pages/charakter.astro` und `src/lib/character.ts`. Name und Server liegen im localStorage, das Profil kommt bei jedem Besuch vom Worker in `../server` (`GET /api/character`, siehe `server/README.md`). Für `npm run dev` zeigt `.env.development` auf `http://127.0.0.1:8787`, also vorher `npx wrangler dev` in `../server` starten. Gebaut wird gegen `PUBLIC_API_BASE` oder sonst die workers.dev-Adresse.
- **Texte:** `src/i18n/de.json`. Guides als Markdown in `src/content/guides/`.
- **Impressum:** Name und Anschrift stehen in `.impressum.json` (nicht im Repo, Felder name, street, city, country, email). Ohne die Datei bricht der Build ab.
- **Wakayashi:** Cheat Sheet, Checkliste und Event-Zeiten mit Erlaubnis, immer mit Nennung (`src/components/Credit.astro`).
