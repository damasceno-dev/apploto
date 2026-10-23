// The branch namespace of web routes (owner decision D-M2-1, 2026-10-22): every branch-scoped
// destination lives under `/f/{branchId}/…`, so each browser tab carries its own branch context in
// its address. Isomorphic: no secrets. The id in a path is **untrusted navigation context** — never
// authority or a credential. The BFF selects the authenticated user's own credential for it and the
// API verifies membership and role on every call.

export const branchNamespace = '/f';

/** The API's branch ids are GUIDs; anything else is not a branch route. */
const branchIdPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;

export function isBranchId(value: unknown): value is string {
  return typeof value === 'string' && branchIdPattern.test(value);
}

export interface BranchRoute {
  readonly branchId: string;
  /** The destination inside the branch, starting with `/` (`/` for the namespace root). */
  readonly rest: string;
}

/** `/f/{branchId}/gestao/fila` → `{ branchId, rest: '/gestao/fila' }`; null outside the namespace. */
export function parseBranchPath(pathname: string): BranchRoute | null {
  if (!pathname.startsWith(`${branchNamespace}/`)) return null;
  const after = pathname.slice(branchNamespace.length + 1);
  const slash = after.indexOf('/');
  const branchId = slash === -1 ? after : after.slice(0, slash);
  if (!isBranchId(branchId)) return null;
  return { branchId, rest: slash === -1 ? '/' : after.slice(slash) };
}

/** The address of `rest` (a path, optionally with a query) inside `branchId`'s namespace. */
export function branchPath(branchId: string, rest: string): string {
  if (!isBranchId(branchId)) throw new RangeError('Not a branch id.');
  if (!rest.startsWith('/')) throw new RangeError('A branch destination starts with "/".');
  return `${branchNamespace}/${branchId}${rest === '/' ? '' : rest}`;
}
