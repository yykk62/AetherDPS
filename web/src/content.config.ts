import { defineCollection, z } from 'astro:content';
import { glob } from 'astro/loaders';

// Guides als Markdown in src/content/guides/. Dateiname = Adresse (/guides/<name>).
const guides = defineCollection({
  loader: glob({ pattern: '**/*.md', base: './src/content/guides' }),
  schema: z.object({
    title: z.string(),
    description: z.string(),
    updated: z.coerce.date(),
    tags: z.array(z.string()).default([]),
    draft: z.boolean().default(false),
  }),
});

export const collections = { guides };
