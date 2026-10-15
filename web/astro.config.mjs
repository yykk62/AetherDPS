import { defineConfig } from 'astro/config';

// Statische Seite, gebaut nach dist/ und über Cloudflare (wrangler.jsonc) ausgeliefert.
export default defineConfig({
  site: 'https://aetherdps.com',
  build: { format: 'directory' },
  // Die Spieldaten liegen eine Ebene höher in AetherDPS.Engine/GameData.
  vite: { server: { fs: { allow: ['..'] } } },
});
