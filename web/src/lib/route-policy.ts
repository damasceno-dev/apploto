// Semantic route ownership for the session gate (Web M2). Paths come from the shell blueprint's web
// route map (`design/specs/screens/shell-and-identity.md`); M4 builds the screens behind them.
import type { Role } from '@loto/api';
import type { SessionSnapshot } from './bff-protocol';

/** `public`: no gate (technical placeholder, assets). The others name the authority a route needs. */
export type RouteTier = 'public' | 'anonymous' | 'identity' | 'branch';

export const gateDestinations = Object.freeze({
  login: '/entrar',
  branchPicker: '/filiais',
});

/** Role home (catalog: Member → Meu turno cockpit; Manager/Admin → Gestão work queue). */
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
  ['/gestao', 'branch'],
  ['/turno', 'branch'],
  ['/fechamentos', 'branch'],
  ['/transacoes', 'branch'],
  ['/clientes', 'branch'],
  ['/relatorios', 'branch'],
];

/** Technical fixtures that exist only in the end-to-end build (`page.e2e.tsx`). */
const fixtureFamilies: readonly Family[] = [
  ['/e2e/branch', 'branch'],
  ['/e2e/identity', 'identity'],
];

export interface RoutePolicyOptions {
  readonly fixtures?: boolean;
}

export function routeTier(pathname: string, options: RoutePolicyOptions = {}): RouteTier {
  const families = options.fixtures ? [...fixtureFamilies, ...productFamilies] : productFamilies;
  for (const [prefix, tier] of families) {
    if (pathname === prefix || pathname.startsWith(`${prefix}/`)) return tier;
  }
  return 'public';
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
  | { readonly kind: 'redirect'; readonly to: string; readonly holdIntent: boolean };

/** Where a route of `tier` sends a caller in `snapshot`'s state, before anything is rendered or fetched. */
export function decideGate(
  tier: RouteTier,
  snapshot: Pick<SessionSnapshot, 'state'>,
): GateDecision {
  if (tier === 'public' || tier === 'anonymous') return { kind: 'allow' };
  const holdIntent = tier === 'branch';
  switch (snapshot.state) {
    case 'anonymous':
    case 'renewal-unconfirmed':
      return { kind: 'redirect', to: gateDestinations.login, holdIntent };
    case 'no-memberships':
    case 'choose-branch':
    case 'branch-unavailable':
      return tier === 'identity'
        ? { kind: 'allow' }
        : { kind: 'redirect', to: gateDestinations.branchPicker, holdIntent };
    case 'branch':
      return { kind: 'allow' };
  }
}

/** Where an authorized state lands: the resumable intent, else the live role's home. */
export function landingFor(snapshot: SessionSnapshot): string {
  if (snapshot.state === 'anonymous' || snapshot.state === 'renewal-unconfirmed') {
    return gateDestinations.login;
  }
  if (snapshot.state !== 'branch' || snapshot.branch === null) return gateDestinations.branchPicker;
  return snapshot.resume ?? roleHomes[snapshot.branch.role];
}

function hasControl(value: string): boolean {
  for (let index = 0; index < value.length; index += 1) {
    const code = value.charCodeAt(index);
    if (code < 0x20 || code === 0x7f) return true;
  }
  return false;
}
