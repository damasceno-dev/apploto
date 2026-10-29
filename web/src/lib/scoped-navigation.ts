import type { Role } from '@loto/api';
import { matchDestination, scopedDestination, type WorkMode } from './destinations';
import { parseBranchPath } from './branch-route';
import {
  navigationKey,
  navigationAddress,
  validateNavigation,
  type NavigationEntry,
} from './navigation-state';

export interface NavigationScope {
  /** Opaque authenticated UserId for navigation only; durable input keeps M2 email hashes. */
  readonly identity: string;
  readonly branchId: string;
  readonly role: Role;
  readonly linked: boolean;
}
/** Prepare only navigation. The owning screen retains input separately; this function can never save or replay it. */
export function prepareOriginNavigation(
  scope: NavigationScope,
  source: NavigationEntry,
  target: string,
): NavigationEntry | null {
  const validated = validateNavigation(source, scope.branchId, scope.role, scope.linked);
  if (!validated || !scopedDestination(target, scope.branchId)) return null;
  const route = parseBranchPath(source.href.split('?')[0] ?? '');
  const destination = route ? matchDestination(route.rest) : null;
  if (!destination) return null;
  return validateNavigation(
    {
      href: target,
      mode: source.mode,
      origins: [
        ...validated.origins,
        { href: validated.href, label: destination.title, mode: validated.mode },
      ],
      scroll: 0,
      focus: null,
    },
    scope.branchId,
    scope.role,
    scope.linked,
  );
}
export function rememberNavigation(
  storage: Storage | null,
  scope: NavigationScope,
  entry: NavigationEntry,
): boolean {
  const validated = validateNavigation(entry, scope.branchId, scope.role, scope.linked);
  if (!storage || !validated) return false;
  try {
    const existing: unknown = JSON.parse(storage.getItem(navigationKey) ?? '{}');
    const saved =
      existing && typeof existing === 'object' && !Array.isArray(existing)
        ? (existing as Record<string, unknown>)
        : {};
    saved[navigationAddress(scope.identity, scope.branchId, entry.href)] = validated;
    storage.setItem(navigationKey, JSON.stringify(saved));
    return true;
  } catch {
    return false;
  }
}
export function homeMode(role: Role, requested: WorkMode): WorkMode {
  return role === 'Member' ? 'shift' : requested;
}
