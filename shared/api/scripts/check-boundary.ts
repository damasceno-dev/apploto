// Runs inside `@loto/api`'s `lint` script (Shared M2 4.3, 4.4), so the standing `pnpm -r lint` gate
// enforces both rules:
//
// 1. Import boundary. Walks the transitive import graph of `src/index.ts` — static imports, type-only
//    imports, re-exports and dynamic `import()` — resolving each specifier with the package's own
//    TypeScript settings. It fails if the root entry reaches React, any `@tanstack/*` package, or
//    a file under `src/queries/` or `src/generated/queries/`. A text search could not see a
//    violation hidden behind an innocent intermediate module; this walk does.
// 2. No generated retry policy. Fails if any generated file mentions `retry`: retries are app-owned,
//    and writes are never retried automatically.
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import ts from 'typescript';
import { generatedRelativeDir, packageDir } from './codegen.ts';

const entry = join(packageDir, 'src/index.ts');
const forbiddenDirectories = ['src/queries', 'src/generated/queries'].map(
  (dir) => join(packageDir, dir) + sep,
);
const isForbiddenPackage = (specifier: string) =>
  specifier === 'react' ||
  specifier.startsWith('react/') ||
  specifier === 'react-dom' ||
  specifier.startsWith('@tanstack/');

function compilerOptions(): ts.CompilerOptions {
  const configPath = join(packageDir, 'tsconfig.json');
  const config = ts.readConfigFile(configPath, ts.sys.readFile);
  if (config.error)
    throw new Error(ts.flattenDiagnosticMessageText(config.error.messageText, '\n'));
  return ts.parseJsonConfigFileContent(config.config, ts.sys, packageDir).options;
}

/**
 * Returns one line per violation (the forbidden target and the import chain that reached it) and
 * the number of package modules walked, so a run can show the walk was not vacuous.
 */
export function findBoundaryViolations(start: string = entry): {
  violations: string[];
  walked: number;
} {
  const options = compilerOptions();
  const violations: string[] = [];
  const parents = new Map<string, string | undefined>([[start, undefined]]);
  const pending = [start];
  const chain = (file: string): string => {
    const steps: string[] = [];
    for (let at: string | undefined = file; at !== undefined; at = parents.get(at)) {
      steps.unshift(relative(packageDir, at));
    }
    return steps.join(' → ');
  };

  while (pending.length > 0) {
    const file = pending.pop() as string;
    const { importedFiles } = ts.preProcessFile(readFileSync(file, 'utf8'), true, true);
    for (const { fileName: specifier } of importedFiles) {
      if (isForbiddenPackage(specifier)) {
        violations.push(`${specifier} via ${chain(file)}`);
        continue;
      }
      const resolved = ts.resolveModuleName(specifier, file, options, ts.sys).resolvedModule;
      if (resolved === undefined) {
        if (specifier.startsWith('.'))
          violations.push(`unresolvable ${specifier} in ${chain(file)}`);
        continue;
      }
      const target = resolved.resolvedFileName;
      if (resolved.isExternalLibraryImport || !target.startsWith(packageDir + sep)) continue;
      if (forbiddenDirectories.some((dir) => target.startsWith(dir))) {
        violations.push(`${relative(packageDir, target)} via ${chain(file)}`);
        continue;
      }
      if (!parents.has(target)) {
        parents.set(target, file);
        pending.push(target);
      }
    }
  }
  return { violations: violations.sort(), walked: parents.size };
}

export function findGeneratedRetries(
  root: string = join(packageDir, generatedRelativeDir),
): string[] {
  if (!existsSync(root)) return [];
  return readdirSync(root, { recursive: true, withFileTypes: true })
    .filter((item) => item.isFile())
    .map((item) => join(item.parentPath, item.name))
    .filter((file) => /\bretry\b/i.test(readFileSync(file, 'utf8')))
    .map((file) => relative(packageDir, file))
    .sort();
}

if (import.meta.main) {
  const { violations, walked } = findBoundaryViolations();
  const retries = findGeneratedRetries();
  if (violations.length > 0) {
    console.error(
      `@loto/api root entry must stay hook-free; it reaches:\n  ${violations.join('\n  ')}`,
    );
  }
  if (retries.length > 0) {
    console.error(
      `Generated code must not configure retries; found "retry" in:\n  ${retries.join('\n  ')}`,
    );
  }
  if (violations.length > 0 || retries.length > 0) process.exitCode = 1;
  else {
    console.log(
      `check-boundary passed — ${walked} modules reachable from src/index.ts are hook-free, and generated code sets no retry.`,
    );
  }
}
