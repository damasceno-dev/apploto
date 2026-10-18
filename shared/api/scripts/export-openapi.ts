// Shared M2 Phase 1 (1.3, 1.5): export the server's OpenAPI document through the opt-in build-time
// generator (server H2) and keep the committed generation input byte-identical to it.
//
//   node scripts/export-openapi.ts           write shared/api/openapi/server-api.openapi.json
//   node scripts/export-openapi.ts --check   re-export to a temporary file and fail on any byte drift
//
// Build-time tooling only: it runs the .NET 10 SDK, never ships in the package, and is the one place
// in `shared/api` that touches the process environment — to give the child build inert placeholders.
// Every path is resolved from this file, so the caller's working directory never matters.
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { copyFileSync, existsSync, mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const packageDir = resolve(import.meta.dirname, '..');
const repoRoot = resolve(packageDir, '../..');
const serverProject = join(repoRoot, 'server/server.API/server.API.csproj');
export const committedDocument = join(packageDir, 'openapi/server-api.openapi.json');

// The generator builds the host (without starting it), and host construction requires these
// settings. Inert values only: an unresolvable `.invalid` host and a non-secret key. Real
// connection strings, signing keys and environment names are never inherited.
const inertConfiguration: Record<string, string> = {
  ConnectionStrings__DefaultConnection:
    'Host=openapi-export.invalid;Database=openapi_export;Username=openapi_export;Password=openapi_export',
  Token__SigningKey: 'openapi-export-placeholder-signing-key-not-a-secret-0123456789',
  Token__ExpirationTimeInMinutes: '60',
};
/**
 * True for any variable .NET could read as database, token or environment configuration. Environment
 * variable names are matched case-insensitively (the configuration provider ignores case), with `:`
 * treated like `__`, including the `DOTNET_`/`ASPNETCORE_` host prefixes (which also feed application
 * configuration) and the legacy `*CONNSTR_` connection-string prefixes the provider maps to
 * `ConnectionStrings:*`.
 */
export function isServerConfiguration(name: string): boolean {
  const key = name.toUpperCase().replaceAll(':', '__');
  const unprefixed = key.replace(/^(DOTNET_|ASPNETCORE_)/, '');
  return (
    key.startsWith('ASPNETCORE_') ||
    key === 'DOTNET_ENVIRONMENT' ||
    /^(CONNECTIONSTRINGS__|TOKEN__)/.test(unprefixed) ||
    /^(SQLCONNSTR|SQLAZURECONNSTR|MYSQLCONNSTR|POSTGRESQLCONNSTR|CUSTOMCONNSTR)_/.test(unprefixed)
  );
}

/** The parent environment minus every server-configuration alias, plus the canonical inert values. */
export function childEnvironment(parent: NodeJS.ProcessEnv = process.env): NodeJS.ProcessEnv {
  const environment: NodeJS.ProcessEnv = {};
  for (const [name, value] of Object.entries(parent)) {
    if (!isServerConfiguration(name)) environment[name] = value;
  }
  return { ...environment, ...inertConfiguration };
}

export function sha256(path: string): string {
  return createHash('sha256').update(readFileSync(path)).digest('hex');
}

/**
 * Exports the document into a fresh temporary directory and copies its exact bytes to `destination`.
 * The build always runs with `--no-incremental`: a cached Release build can exit 0 without running
 * the generator for a new output directory (SV22 review), so a missing document is a failure even
 * after a successful build, and nothing ever falls back to an older file.
 */
export function exportDocument(destination: string): void {
  const outputDir = mkdtempSync(join(tmpdir(), 'loto-openapi-export-'));
  try {
    const build = spawnSync(
      'dotnet',
      [
        'build',
        serverProject,
        '-c',
        'Release',
        '--no-incremental',
        '-nologo',
        '-v:quiet',
        '-p:OpenApiGenerateDocuments=true',
        `-p:OpenApiDocumentsDirectory=${outputDir}`,
      ],
      { cwd: repoRoot, env: childEnvironment(), encoding: 'utf8' },
    );
    if (build.error) {
      throw new Error(
        `Could not run the .NET SDK (${build.error.message}); export needs the .NET 10 SDK on PATH.`,
      );
    }
    if (build.status !== 0) {
      throw new Error(
        `The server export build failed (exit ${build.status}).\n${build.stdout}${build.stderr}`,
      );
    }

    const exported = join(outputDir, 'server.API.json');
    if (!existsSync(exported)) {
      throw new Error(
        `The server build exited 0 but wrote no document to ${exported}; refusing to use any older file.`,
      );
    }
    copyFileSync(exported, destination);
  } finally {
    rmSync(outputDir, { recursive: true, force: true });
  }
}

function describe(path: string): string {
  const document = JSON.parse(readFileSync(path, 'utf8')) as {
    paths: Record<string, Record<string, unknown>>;
    components?: { schemas?: Record<string, unknown> };
  };
  const operations = Object.values(document.paths).reduce(
    (count, item) => count + Object.keys(item).length,
    0,
  );
  return `sha256 ${sha256(path)}, ${Object.keys(document.paths).length} paths, ${operations} operations, ${
    Object.keys(document.components?.schemas ?? {}).length
  } schemas`;
}

function main(): void {
  if (!process.argv.includes('--check')) {
    exportDocument(committedDocument);
    console.log(`Exported ${committedDocument} (${describe(committedDocument)})`);
    return;
  }

  if (!existsSync(committedDocument)) {
    throw new Error(`No committed document at ${committedDocument}; run export:openapi first.`);
  }
  const scratch = mkdtempSync(join(tmpdir(), 'loto-openapi-check-'));
  try {
    const fresh = join(scratch, 'server-api.openapi.json');
    exportDocument(fresh);
    if (!readFileSync(fresh).equals(readFileSync(committedDocument))) {
      throw new Error(
        `OpenAPI drift: the committed document (${describe(committedDocument)}) differs from a fresh ` +
          `export (${describe(fresh)}). Run export:openapi, regenerate and review the diff.`,
      );
    }
    console.log(
      `export:check passed — committed document matches a fresh export (${describe(fresh)})`,
    );
  } finally {
    rmSync(scratch, { recursive: true, force: true });
  }
}

if (import.meta.main) {
  try {
    main();
  } catch (error) {
    console.error(error instanceof Error ? error.message : error);
    process.exitCode = 1;
  }
}
