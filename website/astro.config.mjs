// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';
import mermaid from './src/integrations/mermaid.mjs';

export default defineConfig({
  site: 'https://namo40.github.io',
  base: '/SharpCampus',
  markdown: {
    // Mermaid fences are rendered by the mermaid integration, not by a highlighter.
    syntaxHighlight: { type: 'shiki', excludeLangs: ['mermaid'] },
  },
  integrations: [
    // Must precede starlight so its rehype plugin claims mermaid fences
    // before Expressive Code turns them into styled code blocks.
    mermaid(),
    starlight({
      title: 'SharpCampus',
      social: [
        {
          icon: 'github',
          label: 'GitHub',
          href: 'https://github.com/namo40/SharpCampus',
        },
      ],
      defaultLocale: 'root',
      locales: {
        root: { label: 'English', lang: 'en' },
        ko: { label: '한국어', lang: 'ko' },
        ja: { label: '日本語', lang: 'ja' },
      },
      customCss: ['./src/styles/custom.css'],
      routeMiddleware: './src/starlightRouteData.ts',
      components: { Footer: './src/components/Footer.astro' },
      sidebar: [
        {
          label: 'Getting Started',
          translations: { ko: '시작 가이드', ja: 'はじめに' },
          autogenerate: { directory: 'getting-started' },
        },
        {
          label: 'Architecture',
          translations: { ko: '아키텍처', ja: 'アーキテクチャ' },
          autogenerate: { directory: 'architecture' },
        },
        {
          label: 'Libraries',
          translations: { ko: '라이브러리', ja: 'ライブラリ' },
          autogenerate: { directory: 'libraries' },
        },
        {
          label: 'Topics',
          translations: { ko: '주제 해설', ja: 'トピック解説' },
          autogenerate: { directory: 'topics' },
        },
      ],
    }),
  ],
});
