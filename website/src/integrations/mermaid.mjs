import { fileURLToPath } from 'node:url';
import rehypeMermaid from 'rehype-mermaid';

const MERMAID_ID_PATTERN = /^mermaid(-dark)?-/;

/**
 * rehype-mermaid emits its `dark: true` output as a `<picture>` driven by
 * `prefers-color-scheme`, which ignores Starlight's manual theme toggle.
 * Starlight always resolves the toggle to `data-theme` on `<html>`, so the two
 * variants are split into plain `<img>` elements that CSS can switch instead.
 */
function rehypeMermaidThemeSplit() {
  return (tree) => walk(tree);
}

function walk(node) {
  if (!Array.isArray(node.children)) return;
  for (let i = 0; i < node.children.length; i += 1) {
    const child = node.children[i];
    const split = splitMermaidPicture(child);
    if (split) {
      node.children[i] = split;
    } else {
      walk(child);
    }
  }
}

function splitMermaidPicture(node) {
  if (node.type !== 'element' || node.tagName !== 'picture') return null;

  const [source, img] = node.children ?? [];
  if (source?.tagName !== 'source' || img?.tagName !== 'img') return null;
  if (typeof img.properties?.id !== 'string') return null;
  if (!MERMAID_ID_PATTERN.test(img.properties.id)) return null;

  const sourceIsDark = String(source.properties?.media ?? '').includes('dark');
  const fromSource = toImg(source.properties, img.properties.alt, sourceIsDark);
  const fromImg = toImg(img.properties, img.properties.alt, !sourceIsDark);

  return {
    type: 'element',
    tagName: 'div',
    properties: { className: ['mermaid-diagram'] },
    children: sourceIsDark ? [fromImg, fromSource] : [fromSource, fromImg],
  };
}

function toImg(properties, alt, isDark) {
  return {
    type: 'element',
    tagName: 'img',
    properties: {
      alt: alt ?? '',
      id: properties.id,
      src: properties.src ?? properties.srcset,
      width: properties.width,
      height: properties.height,
      className: [isDark ? 'mermaid-diagram-dark' : 'mermaid-diagram-light'],
    },
    children: [],
  };
}

export default function mermaid(options = {}) {
  return {
    name: 'sharpcampus-mermaid',
    hooks: {
      'astro:config:setup'({ updateConfig, injectScript }) {
        updateConfig({
          markdown: {
            rehypePlugins: [
              [
                rehypeMermaid,
                {
                  strategy: 'img-svg',
                  dark: true,
                  colorScheme: 'light',
                  // Mermaid sizes boxes by measuring text in the rendering
                  // browser; without explicit CJK fonts its fallback there is
                  // narrower than what the page uses, and ko/ja labels get
                  // clipped. Pin the same chain for measuring and viewing.
                  mermaidConfig: {
                    fontFamily:
                      '"trebuchet ms", verdana, arial, "Malgun Gothic", "Yu Gothic UI", "Meiryo", sans-serif',
                  },
                  ...options,
                },
              ],
              rehypeMermaidThemeSplit,
            ],
          },
        });

        const lightbox = fileURLToPath(new URL('../scripts/mermaid-lightbox.js', import.meta.url));
        injectScript('page', `import ${JSON.stringify(lightbox.replaceAll('\\', '/'))};`);
      },
    },
  };
}
