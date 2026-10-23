// Semantic route ownership for the session gate (Web M2). Paths come from the shell blueprint's web
// route map (`design/specs/screens/shell-and-identity.md`) inside the branch namespace decided for
// D-M2-1 (`/f/{branchId}/…`, see `branch-route.ts`); M4 builds the screens behind them.
import type { Role } from '@loto/api';
import type { SessionSnapshot } from './bff-protocol';
import { branchPath, parseBranchPath } from './branch-route';

/** `public`: no gate (technical placeholder, assets). The others name the authority a route needs. */
export type RouteTier = 'public' | 'anonymous' | 'identity' | 'branch';

export const gateDestinations = Object.freeze({
  login: '/entrar',
  branchPicker: '/filiais',
});

/** Role home inside a branch (catalog: Member → Meu turno cockpit; Manager/Admin → Gestão queue). */
export const roleHomes: Readonly<Record<Role, string>> = Object.freeze({
  Member: '/turno/hoje',
  Manager: '/gestao/fila',
  Admin: '/gestao/fila',
});

type Family = readonly [prefix: string, tier: RouteTier];

const productFamilies: readonly Family[] = [
  ['/entrar', 'anonymous'],
  ['/reset-password', 'anonymous'],
  ['/invitation', 'anonymous'],
  ['/filiais', 'identity'],
  ['/plataforma', 'identity'],
  ['/conta', 'identity'],
  // Branch destinations addressed without a branch (an old bookmark, a link written from the route
  // map): they carry no tenant, so the gate holds them until a branch is chosen.
  ['/gestao', 'branch'],
  ['/turno', 'branch'],
  ['/fechamentos', 'branch'],
  ['/transacoes', 'branch'],
  ['/clientes', 'branch'],
  ['/relatorios', 'branch'],
];

/** Technical fixtures that exist only in the end-to-end build (`page.e2e.tsx`). */
const fixtureFamilies: readonly Family[] = [['/e2e/identity', 'identity']];

export interface RoutePolicyOptions {
  readonly fixtures?: boolean;
}

/** Everything under `/f/{branchId}` is branch work for that branch, whatever follows. */
export function routeTier(pathname: string, options: RoutePolicyOptions = {}): RouteTier {
  if (parseBranchPath(pathname) !== null) return 'branch';
  const families = options.fixtures ? [...fixtureFamilies, ...productFamilies] : productFamilies;
  for (const [prefix, tier] of families) {
    if (pathname === prefix || pathname.startsWith(`${prefix}/`)) return tier;
  }
  return 'public';
}

/** The branch a route names (null for unscoped routes). Navigation context, not authority. */
export function routeBranch(pathname: string): string | null {
  return parseBranchPath(pathname)?.branchId ?? null;
}

const maxIntentLength = 512;

/**
 * Accepts a same-origin application path for a navigation intent and returns its normalized
 * `pathname + search`, or null. Only identity/branch routes qualify: an intent is where to go once
 * the gate passes, never an API request, a capability link or another origin.
 */
export function validateIntentPath(
  value: unknown,
  options: RoutePolicyOptions = {},
): string | null {
  if (typeof value !== 'string' || value.length === 0 || value.length > maxIntentLength)
    return null;
  if (
    !value.startsWith('/') ||
    value.startsWith('//') ||
    value.includes('\\') ||
    hasControl(value)
  ) {
    return null;
  }
  let url: URL;
  try {
    url = new URL(value, 'http://intent.invalid');
  } catch {
    return null;
  }
  if (url.origin !== 'http://intent.invalid' || url.hash) return null;
  const tier = routeTier(url.pathname, options);
  if (tier !== 'branch' && tier !== 'identity') return null;
  // Route paths carry filters only (blueprint route map); anything that could be a credential makes
  // the whole intent unusable rather than being stored in a cookie or echoed back as `resume`.
  for (const [name, parameter] of url.searchParams) {
    if (credentialParameter.test(name) || credentialValue.test(parameter)) return null;
  }
  return url.pathname + url.search;
}

/** Names that carry capabilities, tokens, proofs or secrets in this app or common auth flows. */
const credentialParameter =
  /^(?:.*(?:token|secret|password|senha|passwd|credential|signature|assertion|apikey|api[_-]key|auth).*|code|state|nonce|key|sig|jwt|capability|challenge(?:id)?)$/i;
/** JWT-shaped values, whatever the parameter is called. */
const credentialValue = /^eyJ[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*$/;

export type GateDecision =
  | { readonly kind: 'allow' }
  | { readonly kind: 'redirect'; readonly to: string; readonly holdIntent: boolean }
  /** The route's branch is not open to this identity: 403, verbatim messages, back to the picker. */
  | { readonly kind: 'forbidden'; readonly messages: readonly string[] };

/**
 * Where a route of `tier` sends a caller in `snapshot`'s state, before anything is rendered or
 * fetched. For a branch route that names a branch, `snapshot` is that branch's resolution.
 */
export function decideGate(
  tier: RouteTier,
  snapshot: Pick<SessionSnapshot, 'state' | 'messages'>,
  options: { readonly branchId?: string | null } = {},
): GateDecision {
  if (tier === 'public' || tier === 'anonymous') return { kind: 'allow' };
  const holdIntent = tier === 'branch';
  if (snapshot.state === 'anonymous' || snapshot.state === 'renewal-unconfirmed') {
    return { kind: 'redirect', to: gateDestinations.login, holdIntent };
  }
  if (tier === 'identity') return { kind: 'allow' };
  const named = (options.branchId ?? null) !== null;
  switch (snapshot.state) {
    case 'branch':
      return named
        ? { kind: 'allow' }
        : { kind: 'redirect', to: gateDestinations.branchPicker, holdIntent };
    case 'branch-unavailable':
      return named
        ? { kind: 'forbidden', messages: snapshot.messages }
        : { kind: 'redirect', to: gateDestinations.branchPicker, holdIntent };
    case 'no-memberships':
    case 'choose-branch':
      return { kind: 'redirect', to: gateDestinations.branchPicker, holdIntent };
  }
}

/**
 * Where an authorized state lands: the resumable intent (an unscoped one inside the opened
 * branch), else the role home of the opened branch, inside its namespace.
 */
export function landingFor(snapshot: SessionSnapshot): string {
  if (snapshot.state === 'anonymous' || snapshot.state === 'renewal-unconfirmed') {
    return gateDestinations.login;
  }
  const resume = snapshot.resume;
  const resumePath = resume?.split('?', 1)[0] ?? '';
  if (
    resume !== null &&
    (parseBranchPath(resumePath) !== null || routeTier(resumePath) === 'identity')
  ) {
    return resume;
  }
  if (snapshot.state !== 'branch' || snapshot.branch === null) return gateDestinations.branchPicker;
  return branchPath(snapshot.branch.id, resume ?? roleHomes[snapshot.branch.role]);
}

function hasControl(value: string): boolean {
  for (let index = 0; index < value.length; index += 1) {
    const code = value.charCodeAt(index);
    if (code < 0x20 || code === 0x7f) return true;
  }
  return false;
}
