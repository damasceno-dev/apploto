// `pnpm generate:check` (Shared M2 Phase 2.5): fail when `src/generated/` differs in any way from a
// clean generation of the committed document. It never uses git (`git diff` ignores untracked
// additions, and a repo-relative pathspec silently matches nothing from `shared/api`). Instead it
// mirrors the package's generation inputs into a fresh temporary directory, generates there with the
// same config and layout, and compares both trees recursively — every path and every byte — so a
// modified, added (including untracked) or deleted file all fail, from any working directory.
import {
  cpSync,
  existsSync,
  mkdtempSync,
  readFileSync,
  readdirSync,
  rmSync,
  symlinkSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import { join, relative } from 'node:path';
import { generateInto, generatedRelativeDir, packageDir, repoRoot } from './codegen.ts';

/** Files outside `src/generated/` that generation reads; the mirror keeps their relative layout. */
const packageInputs = [
  'package.json',
  'tsconfig.json',
  'orval.config.ts',
  'scripts/header-boundary.ts',
  'openapi',
  'src/client',
];
const rootInputs = ['package.json', 'tsconfig.base.json', '.prettierrc', '.prettierignore'];

function listFiles(dir: string): string[] {
  if (!existsSync(dir)) return [];
  return readdirSync(dir, { recursive: true, withFileTypes: true })
    .filter((entry) => !entry.isDirectory())
    .map((entry) => relative(dir, join(entry.parentPath, entry.name)))
    .sort();
}

export function compareTrees(expectedDir: string, actualDir: string): string[] {
  const expected = new Set(listFiles(expectedDir));
  const actual = new Set(listFiles(actualDir));
  const differences: string[] = [];
  for (const file of expected) {
    if (!actual.has(file)) differences.push(`deleted: ${file}`);
    else if (!readFileSync(join(expectedDir, file)).equals(readFileSync(join(actualDir, file)))) {
      differences.push(`modified: ${file}`);
    }
  }
  for (const file of actual) if (!expected.has(file)) differences.push(`added: ${file}`);
  return differences.sort();
}

async function main(): Promise<void> {
  const mirrorRoot = mkdtempSync(join(tmpdir(), 'loto-generate-check-'));
  try {
    const mirrorPackage = join(mirrorRoot, relative(repoRoot, packageDir));
    for (const input of rootInputs) cpSync(join(repoRoot, input), join(mirrorRoot, input));
    for (const input of packageInputs) {
      cpSync(join(packageDir, input), join(mirrorPackage, input), { recursive: true });
    }
    // Same installed tools (Orval, Prettier, TanStack version detection) without copying them.
    symlinkSync(join(repoRoot, 'node_modules'), join(mirrorRoot, 'node_modules'), 'dir');
    symlinkSync(join(packageDir, 'node_modules'), join(mirrorPackage, 'node_modules'), 'dir');

    await generateInto(mirrorPackage);
    const differences = compareTrees(
      join(mirrorPackage, generatedRelativeDir),
      join(packageDir, generatedRelativeDir),
    );
    if (differences.length > 0) {
      throw new Error(
        `src/generated/ is not a clean generation of openapi/server-api.openapi.json ` +
          `(${differences.length} difference(s)):\n  ${differences.join('\n  ')}\nRun pnpm generate and review the diff.`,
      );
    }
    console.log(
      `generate:check passed — ${listFiles(join(packageDir, generatedRelativeDir)).length} generated files match a clean generation.`,
    );
  } finally {
    rmSync(mirrorRoot, { recursive: true, force: true });
  }
}

if (import.meta.main) {
  try {
    await main();
  } catch (error) {
    console.error(error instanceof Error ? error.message : error);
    process.exitCode = 1;
  }
}
