// Shared generation steps for `generate` and `generate:check` (Shared M2 Phase 2.5, 3.3, 4.2).
// `generateInto(root)` regenerates `<root>/src/generated/` from `<root>/openapi/server-api.openapi.json`
// and `<root>/orval.config.ts`; `root` is the real package or an isolated mirror of it. Build-time
// tooling only — nothing here ships in the package. No .NET is needed.
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join, resolve } from 'node:path';
import * as prettier from 'prettier';

export const packageDir = resolve(import.meta.dirname, '..');
export const repoRoot = resolve(packageDir, '../..');
export const generatedRelativeDir = 'src/generated';
const documentRelativePath = 'openapi/server-api.openapi.json';
const operationsRelativePath = 'src/generated/operations/operations.ts';
const orvalProjects = ['raw', 'queries'] as const;

const requireFromPackage = createRequire(join(packageDir, 'package.json'));

function orvalBin(): string {
  const manifestPath = requireFromPackage.resolve('orval/package.json');
  const manifest = JSON.parse(readFileSync(manifestPath, 'utf8')) as {
    bin: Record<string, string>;
  };
  const bin = manifest.bin['orval'];
  if (bin === undefined) throw new Error('The pinned orval package declares no `orval` binary.');
  return join(dirname(manifestPath), bin);
}

/** Regenerates the whole owned `src/generated/` tree under `root`; nothing else is written. */
export async function generateInto(root: string): Promise<void> {
  rmSync(join(root, generatedRelativeDir), { recursive: true, force: true });
  const bin = orvalBin();
  for (const project of orvalProjects) {
    const run = spawnSync(
      process.execPath,
      [bin, '--config', join(root, 'orval.config.ts'), '--project', project],
      { cwd: root, encoding: 'utf8' },
    );
    if (run.status !== 0) {
      throw new Error(
        `orval --project ${project} failed (exit ${run.status}).\n${run.stdout}${run.stderr}`,
      );
    }
  }
  await writeOperations(root);
}

type AuthTier = 'anonymous' | 'identity' | 'branch';

interface OpenApiParameter {
  $ref?: string;
  name?: string;
  in?: string;
}

interface OpenApiDocument {
  security?: unknown[];
  paths: Record<string, Record<string, { security?: unknown; parameters?: OpenApiParameter[] }>>;
  components?: {
    securitySchemes?: Record<string, { type?: string; scheme?: string }>;
    parameters?: Record<string, OpenApiParameter>;
  };
}

export interface OperationContract {
  method: string;
  path: string;
  authTier: AuthTier;
  /** The header parameters the operation declares (for example `Idempotency-Key`), sorted. */
  headers: string[];
}

const httpMethods = new Set(['get', 'put', 'post', 'delete', 'options', 'head', 'patch', 'trace']);
const schemeTiers: Record<string, AuthTier> = {
  IdentityBearer: 'identity',
  BranchBearer: 'branch',
};

/**
 * Derives each operation's tier from its own `security` (server H4) and lists the header parameters
 * it declares. Only the security shapes the server publishes are accepted — `[]`,
 * `[{ IdentityBearer: [] }]` and `[{ BranchBearer: [roles…] }]` over the two `http`/`bearer` schemes,
 * with no document-wide requirement. Anything else fails generation instead of being guessed.
 */
export function deriveOperations(document: OpenApiDocument): OperationContract[] {
  if (document.security !== undefined && document.security.length > 0) {
    throw new Error(
      'The document declares a document-wide security requirement; tiers must be per operation.',
    );
  }
  for (const name of Object.keys(schemeTiers)) {
    const scheme = document.components?.securitySchemes?.[name];
    if (scheme?.type !== 'http' || scheme.scheme?.toLowerCase() !== 'bearer') {
      throw new Error(`Security scheme ${name} must be an http bearer scheme.`);
    }
  }

  const operations: OperationContract[] = [];
  for (const [path, item] of Object.entries(document.paths)) {
    for (const [method, operation] of Object.entries(item)) {
      if (!httpMethods.has(method)) continue;
      const where = `${method.toUpperCase()} ${path}`;
      const security = operation.security;
      if (!Array.isArray(security)) throw new Error(`${where} publishes no security requirement.`);
      let authTier: AuthTier;
      if (security.length === 0) {
        authTier = 'anonymous';
      } else {
        const requirement = security[0] as Record<string, unknown>;
        const schemes = Object.keys(requirement);
        const scheme = schemes[0];
        const tier = scheme === undefined ? undefined : schemeTiers[scheme];
        if (security.length !== 1 || schemes.length !== 1 || tier === undefined) {
          throw new Error(
            `${where} publishes an unsupported security requirement: ${JSON.stringify(security)}.`,
          );
        }
        const scopes = requirement[scheme as string];
        if (!Array.isArray(scopes) || (tier === 'identity' && scopes.length > 0)) {
          throw new Error(
            `${where} publishes unsupported ${scheme} scopes: ${JSON.stringify(scopes)}.`,
          );
        }
        authTier = tier;
      }
      const headers = (operation.parameters ?? [])
        .map((parameter) => resolveParameter(document, parameter, where))
        .filter((parameter) => parameter.in === 'header')
        .map((parameter) => parameter.name as string)
        .sort(compare);
      operations.push({ method: method.toUpperCase(), path, authTier, headers });
    }
  }

  operations.sort((a, b) =>
    a.path === b.path ? compare(a.method, b.method) : compare(a.path, b.path),
  );
  assertUnambiguous(operations);
  return operations;
}

function resolveParameter(
  document: OpenApiDocument,
  parameter: OpenApiParameter,
  where: string,
): OpenApiParameter {
  if (parameter.$ref === undefined) {
    if (typeof parameter.name !== 'string' || typeof parameter.in !== 'string') {
      throw new Error(`${where} declares a parameter without a name or location.`);
    }
    return parameter;
  }
  const id = parameter.$ref.replace('#/components/parameters/', '');
  const target = document.components?.parameters?.[id];
  if (target === undefined || target.$ref !== undefined) {
    throw new Error(`${where} references unsupported parameter ${parameter.$ref}.`);
  }
  return resolveParameter(document, target, where);
}

function compare(a: string, b: string): number {
  return a < b ? -1 : a > b ? 1 : 0;
}

/** Runtime matching prefers the template with more literal segments; a tie would be a guess. */
function assertUnambiguous(operations: { method: string; path: string }[]): void {
  const parse = (path: string) =>
    path
      .split('/')
      .slice(1)
      .map((s) => (s.startsWith('{') ? null : s));
  for (const [index, a] of operations.entries()) {
    for (const b of operations.slice(index + 1)) {
      const left = parse(a.path);
      const right = parse(b.path);
      if (a.method !== b.method || left.length !== right.length) continue;
      const overlap = left.every((s, i) => s === null || right[i] === null || s === right[i]);
      const literals = (segments: (string | null)[]) => segments.filter((s) => s !== null).length;
      if (overlap && literals(left) === literals(right)) {
        throw new Error(
          `${a.method} ${a.path} and ${b.path} can match the same path with equal precedence.`,
        );
      }
    }
  }
}

async function writeOperations(root: string): Promise<void> {
  const document = JSON.parse(
    readFileSync(join(root, documentRelativePath), 'utf8'),
  ) as OpenApiDocument;
  const entries = deriveOperations(document)
    .map(
      (op) =>
        `  { method: '${op.method}', path: '${op.path}', authTier: '${op.authTier}', headers: [${op.headers
          .map((name) => `'${name}'`)
          .join(', ')}] },`,
    )
    .join('\n');
  const source = `/**
 * Generated by shared/api/scripts/codegen.ts from openapi/server-api.openapi.json. Do not edit.
 * Each operation's token tier, from its published \`security\` (server H4), and the header
 * parameters it declares — protected headers a caller's options may not replace.
 */
export const operations = [
${entries}
] as const satisfies readonly {
  readonly method: string;
  readonly path: string;
  readonly authTier: 'anonymous' | 'identity' | 'branch';
  readonly headers: readonly string[];
}[];
`;
  const destination = join(root, operationsRelativePath);
  mkdirSync(dirname(destination), { recursive: true });
  const options = await prettier.resolveConfig(destination);
  writeFileSync(destination, await prettier.format(source, { ...options, filepath: destination }));
}
