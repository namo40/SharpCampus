// Verifies every <SourceLink> reference against the local repository at SOURCE_COMMIT,
// so excerpts and links can be validated without the GitHub repository being reachable.
//
// Checks per link: the referenced path exists at SOURCE_COMMIT, any from/to line range is
// inside the file, and the code fence immediately above the link (if any) appears verbatim
// in the referenced file. Verbatim allows exactly three editorial liberties: a uniform
// dedent, silently dropping source comment lines (// and ///) and blank lines, and eliding
// code with a line containing only "// ..." — the chunks around an elision must each match
// contiguously and appear in source order. Every code line must match exactly.

import { execFileSync } from 'node:child_process';
import { readFileSync, readdirSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const websiteDir = dirname(dirname(fileURLToPath(import.meta.url)));
const repoRoot = dirname(websiteDir);
const docsDir = join(websiteDir, 'src', 'content', 'docs');

const sourceRef = readFileSync(join(websiteDir, 'src', 'source-ref.ts'), 'utf8');
const commit = sourceRef.match(/SOURCE_COMMIT = '([0-9a-f]{40})'/)?.[1];
if (!commit) {
  console.error('Could not read SOURCE_COMMIT from src/source-ref.ts');
  process.exit(1);
}

function git(...args) {
  return execFileSync('git', args, { cwd: repoRoot, encoding: 'utf8', maxBuffer: 16 * 1024 * 1024 });
}

const errors = [];

try {
  git('cat-file', '-e', `${commit}^{commit}`);
} catch {
  console.error(`SOURCE_COMMIT ${commit} does not exist in the local repository`);
  process.exit(1);
}

// main may move past SOURCE_COMMIT (site-only commits do), but a referenced source file
// changing after the pin means stale excerpts, so those paths are checked against main.
// Skipped when main is not resolvable (e.g. a detached CI checkout).
let drifted = new Set();
let mainNote = '';
try {
  const mainHead = git('rev-parse', '--verify', '--quiet', 'main').trim();
  if (mainHead === commit) {
    mainNote = ' (== main HEAD)';
  } else {
    drifted = new Set(git('diff', '--name-only', commit, 'main').split('\n').filter(Boolean));
    mainNote = ' (behind main, checking referenced paths for drift)';
  }
} catch {
  console.warn('warn: branch "main" not resolvable, skipping drift check against main');
}

const fileCache = new Map();
function sourceAt(path) {
  if (!fileCache.has(path)) {
    try {
      fileCache.set(path, git('show', `${commit}:${path}`).replaceAll('\r\n', '\n'));
    } catch {
      fileCache.set(path, null);
    }
  }
  return fileCache.get(path);
}

function* mdxFiles(dir) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) yield* mdxFiles(full);
    else if (/\.mdx?$/.test(entry.name)) yield full;
  }
}

function isSkippable(sourceLine) {
  const t = sourceLine.trim();
  return t === '' || t.startsWith('//');
}

// The whitespace prefix removed by the page's dedent, or null when the line differs.
function dedentOf(sourceLine, chunkLine) {
  if (!sourceLine.endsWith(chunkLine)) return null;
  const prefix = sourceLine.slice(0, sourceLine.length - chunkLine.length);
  return prefix.trim() === '' ? prefix : null;
}

// A chunk matches a window of source lines when every chunk line agrees after removing one
// uniform whitespace prefix. Comment and blank source lines may sit between matched lines
// unquoted; a matching line is always preferred over skipping it.
function matchChunkAt(sourceLines, start, chunkLines) {
  let i = start;
  let indent = null;
  for (const line of chunkLines) {
    if (line.trim() === '') {
      while (i < sourceLines.length && sourceLines[i].trim() !== '' && isSkippable(sourceLines[i])) i++;
      if (i >= sourceLines.length || sourceLines[i].trim() !== '') return -1;
      i++;
      continue;
    }
    while (i < sourceLines.length && dedentOf(sourceLines[i], line) === null && isSkippable(sourceLines[i])) i++;
    if (i >= sourceLines.length) return -1;
    const prefix = dedentOf(sourceLines[i], line);
    if (prefix === null) return -1;
    if (indent === null) indent = prefix;
    else if (prefix !== indent) return -1;
    i++;
  }
  return i;
}

function findChunk(sourceLines, chunkLines, startFrom) {
  for (let s = startFrom; s < sourceLines.length; s++) {
    const end = matchChunkAt(sourceLines, s, chunkLines);
    if (end !== -1) return end;
  }
  return -1;
}

// Chunks (separated by "// ..." elision lines) must appear contiguously and in order.
function fenceMatches(fenceLines, source) {
  const sourceLines = source.split('\n');
  const chunks = fenceLines
    .join('\n')
    .split(/^\s*\/\/ \.\.\.\s*$/m)
    .map((c) => c.replace(/^\n+|\n+$/g, ''))
    .filter((c) => c.length > 0)
    .map((c) => c.split('\n'));
  let cursor = 0;
  for (const chunk of chunks) {
    cursor = findChunk(sourceLines, chunk, cursor);
    if (cursor === -1) return false;
  }
  return chunks.length > 0;
}

const tagPattern = /<SourceLink\s+path="([^"]+)"(?:\s+from=\{(\d+)\})?(?:\s+to=\{(\d+)\})?(?:\s+label="[^"]*")?\s*\/>/g;

let linkCount = 0;
let excerptCount = 0;
let fileCount = 0;

for (const file of mdxFiles(docsDir)) {
  const rel = relative(websiteDir, file).replaceAll('\\', '/');
  const lines = readFileSync(file, 'utf8').replaceAll('\r\n', '\n').split('\n');

  let fenceStart = -1;
  let lastFence = null; // { endLine, content: string[] }
  let hasLinks = false;

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];

    if (/^\s*```/.test(line)) {
      if (fenceStart === -1) {
        fenceStart = i;
      } else {
        lastFence = { endLine: i, content: lines.slice(fenceStart + 1, i) };
        fenceStart = -1;
      }
      continue;
    }
    if (fenceStart !== -1) continue;

    const tags = [...line.matchAll(tagPattern)];
    if (tags.length === 0) {
      // A fence only backs the link directly below it; any prose in between detaches it.
      if (lastFence && line.trim() !== '') lastFence = null;
      continue;
    }

    hasLinks = true;
    const loc = `${rel}:${i + 1}`;
    for (const [, path, fromRaw, toRaw] of tags) {
      linkCount++;
      if (drifted.has(path)) errors.push(`${loc}: ${path} changed between SOURCE_COMMIT and main`);
      const source = sourceAt(path);
      if (source === null) {
        errors.push(`${loc}: path not found at SOURCE_COMMIT: ${path}`);
        continue;
      }
      if (fromRaw !== undefined) {
        const from = Number(fromRaw);
        const to = toRaw !== undefined ? Number(toRaw) : from;
        const total = source.split('\n').length;
        if (from < 1 || to < from || to > total) {
          errors.push(`${loc}: line range ${from}-${to} outside ${path} (${total} lines)`);
        }
      }
    }

    // Only links standing alone on their line caption the fence above it (several may share
    // the line, joined by "·"); a link mentioned inside prose is a plain reference. A fence
    // with several captions must match one of them in full.
    const isCaption = line.replace(tagPattern, '').trim().replaceAll('·', '').trim() === '';
    if (isCaption && lastFence && lastFence.endLine < i) {
      const sources = tags.map(([, path]) => sourceAt(path)).filter((s) => s !== null);
      if (sources.length > 0) {
        excerptCount++;
        if (!sources.some((source) => fenceMatches(lastFence.content, source))) {
          errors.push(`${loc}: excerpt does not match ${tags.map(([, p]) => p).join(' / ')} verbatim`);
        }
      }
    }
    lastFence = null;
  }

  if (hasLinks) fileCount++;
}

console.log(`SOURCE_COMMIT ${commit}${mainNote}`);
console.log(`${linkCount} links in ${fileCount} files, ${excerptCount} excerpts compared`);

if (errors.length > 0) {
  console.error(`\n${errors.length} error(s):`);
  for (const e of errors) console.error(`  ${e}`);
  process.exit(1);
}
console.log('OK');
