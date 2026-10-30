import { SessionGateError } from '../session/types';
import type { Role } from '@loto/api';
import { safeDestination } from '../data/navigation';
import type { DeviceStore } from '../storage/ports';
import { canEnter, matchDestination, type WorkMode } from './policy';

export interface ShellNavigation {
  readonly v: 1;
  readonly path: string;
  readonly mode: WorkMode;
  readonly origins: readonly string[];
}
const fields = ['v', 'path', 'mode', 'origins'];
export function permittedPath(value: unknown, role: Role): value is string {
  if (typeof value !== 'string' || safeDestination(value) !== value) return false;
  const destination = matchDestination(value.split('?')[0]!);
  return destination !== null && canEnter(destination, role);
}
export async function validateShellNavigation(
  value: unknown,
  role: Role,
  linked: boolean,
  resourceReady: (path: string) => Promise<boolean> = async () => false,
): Promise<ShellNavigation | null> {
  if (!value || typeof value !== 'object' || Object.keys(value).some((k) => !fields.includes(k)))
    return null;
  const entry = value as ShellNavigation;
  if (
    entry.v !== 1 ||
    !['management', 'shift'].includes(entry.mode) ||
    (entry.mode === 'management' && role === 'Member') ||
    (entry.mode === 'shift' && role !== 'Member' && !linked) ||
    !Array.isArray(entry.origins) ||
    entry.origins.length > 8
  )
    return null;
  for (const path of [entry.path, ...entry.origins]) {
    if (!permittedPath(path, role)) return null;
    const route = matchDestination(path.split('?')[0]!)!;
    if (
      (route.owner !== 'shared' && route.owner !== entry.mode) ||
      (route.path.includes(':id') && !(await resourceReady(path)))
    )
      return null;
  }
  return { v: 1, path: entry.path, mode: entry.mode, origins: [...entry.origins] };
}
/** Non-secret presentation candidates, isolated by verified identity, canonical-email hash and branch. */
export class ShellNavigationStore {
  constructor(
    private readonly storage: DeviceStore,
    private readonly hash: (email: string) => Promise<string>,
  ) {}
  private async key(user: { userId: string; email: string }, branchId: string) {
    return `loto.shell.navigation.v1:${user.userId}:${await this.hash(user.email)}:${branchId}`;
  }
  async read(user: { userId: string; email: string }, branchId: string): Promise<unknown> {
    const raw = await this.storage.getItem(await this.key(user, branchId));
    try {
      return raw === null ? null : JSON.parse(raw);
    } catch {
      return null;
    }
  }
  async write(user: { userId: string; email: string }, branchId: string, entry: ShellNavigation) {
    if (
      safeDestination(entry.path) !== entry.path ||
      !matchDestination(entry.path.split('?')[0]!) ||
      !['management', 'shift'].includes(entry.mode) ||
      entry.origins.length > 8 ||
      entry.origins.some(
        (origin) => safeDestination(origin) !== origin || !matchDestination(origin.split('?')[0]!),
      )
    )
      throw new SessionGateError('A navegação não pôde ser guardada com segurança.');
    // Rebuild the envelope; never serialize an arbitrary form object or credentials.
    await this.storage.setItem(
      await this.key(user, branchId),
      JSON.stringify({
        v: 1,
        path: entry.path,
        mode: entry.mode,
        origins: entry.origins,
      }),
    );
  }
  async clear(user: { userId: string; email: string }, branchId: string) {
    await this.storage.removeItem(await this.key(user, branchId));
  }
}

/** Allowlisted non-secret filter/position state; incoming mode and origin claims are never authority. */
export function navigationAddress(
  pathname: string,
  params: Readonly<Record<string, unknown>>,
): string {
  const allowed = new Set([
    'date',
    'from',
    'to',
    'page',
    'pageSize',
    'status',
    'accountId',
    'operatorId',
    'clientId',
    'tab',
    'section',
    'row',
    'focus',
  ]);
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params))
    if (allowed.has(key) && typeof value === 'string') query.set(key, value);
  const path = pathname + (query.size ? '?' + query.toString() : '');
  return safeDestination(path) === path ? path : pathname;
}
