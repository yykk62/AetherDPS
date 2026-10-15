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
- **Texte:** `src/i18n/de.json`. Guides als Markdown in `src/content/guides/`.
- **Wakayashi:** Cheat Sheet, Checkliste und Event-Zeiten mit Erlaubnis, immer mit Nennung (`src/components/Credit.astro`).
