import type { Role } from '@loto/api';
import { canEnter, scopedDestination, type WorkMode } from './destinations';
import { validateIntentPath } from './route-policy';

export interface NavigationEntry {
  readonly href: string;
  readonly mode: WorkMode;
  readonly origins: readonly {
    readonly href: string;
    readonly label: string;
    readonly mode?: WorkMode;
  }[];
  readonly scroll: number;
  readonly focus: string | null;
  readonly section?: string | null;
  readonly row?: string | null;
}
export const navigationKey = 'loto-navigation-v2';
/** No credentials, versions or financial input: only app-owned, tab-local navigation candidates. */
export function validateNavigation(
  value: unknown,
  branchId: string,
  role: Role,
  linked: boolean,
): NavigationEntry | null {
  if (!value || typeof value !== 'object') return null;
  if (
    Object.keys(value).some(
      (key) => !['href', 'mode', 'origins', 'scroll', 'focus', 'section', 'row'].includes(key),
    )
  )
    return null;
  const entry = value as Partial<NavigationEntry>;
  if (entry.mode !== 'management' && entry.mode !== 'shift') return null;
  if (
    (entry.mode === 'management' && role === 'Member') ||
    (entry.mode === 'shift' && role !== 'Member' && !linked)
  )
    return null;
  const permitted = (href: unknown): href is string => {
    if (typeof href !== 'string' || validateIntentPath(href) !== href) return false;
    const parameters = new URL(href, 'https://navigation.invalid').searchParams;
    const allowed = new Set([
      'data',
      'de',
      'ate',
      'conta',
      'ano',
      'mes',
      'instrumento',
      'pagina',
      'tamanho',
      'tipo',
      'status',
      'operador',
      'mine',
      'cliente',
      'categoria',
    ]);
    for (const name of parameters.keys()) if (!allowed.has(name)) return false;
    const destination = scopedDestination(href, branchId);
    // Resource origins wait for a feature-owned resource validation, never restored from storage alone.
    return destination !== null && !destination.path.includes(':id') && canEnter(destination, role);
  };
  if (!permitted(entry.href) || !Array.isArray(entry.origins) || entry.origins.length > 8)
    return null;
  if (
    !entry.origins.every(
      (origin) =>
        origin &&
        permitted(origin.href) &&
        typeof origin.label === 'string' &&
        origin.label.length <= 100 &&
        origin.label === scopedDestination(origin.href, branchId)?.title &&
        (origin.mode === undefined ||
          (origin.mode === 'management'
            ? role !== 'Member'
            : origin.mode === 'shift' && (role === 'Member' || linked))),
    )
  )
    return null;
  if (
    typeof entry.scroll !== 'number' ||
    !Number.isFinite(entry.scroll) ||
    entry.scroll < 0 ||
    entry.scroll > 1_000_000
  )
    return null;
  if (
    entry.focus !== null &&
    (typeof entry.focus !== 'string' || !/^[a-zA-Z][\w-]{0,100}$/.test(entry.focus))
  )
    return null;
  for (const position of [entry.section, entry.row])
    if (
      position !== undefined &&
      position !== null &&
      (typeof position !== 'string' || !/^[a-zA-Z][\w-]{0,100}$/.test(position))
    )
      return null;
  return entry as NavigationEntry;
}

/** A navigation candidate is isolated to one identity, branch AND address, so history entries do not overwrite each other. */
export function navigationAddress(identity: string, branch: string, href: string): string {
  return `${identity}|${branch}|${href}`;
}
