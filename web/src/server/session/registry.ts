import 'server-only';
import { createHash } from 'node:crypto';

/**
 * In-process session coordination: renewal single flights, peer results, rotation markers and ended
 * generations. Keys are SHA-256 hashes, never raw tokens. It lives on `globalThis` so the proxy and
 * every route-handler bundle in one Node process share it. It does **not** span processes: several
 * Next instances need sticky sessions or a shared store (recorded in Web M2, decided with M12 hosting).
 */
export interface SessionRegistry {
  readonly now: () => number;
  readonly flights: Map<string, Promise<unknown>>;
  /** Settled outcomes reused by late callers holding the same input, until `expiresAt`. */
  readonly settled: Map<string, { readonly value: unknown; readonly expiresAt: number }>;
  /** Refresh-token hashes this process has rotated: a later failure with one is staleness, not loss. */
  readonly rotated: Map<string, number>;
  /** Consecutive unknown renewal outcomes per refresh-token hash. */
  readonly unknown: Map<string, { readonly count: number; readonly until: number }>;
  /** Ended identity (`sid`) and scope generations, with the request that ended them. */
  readonly endedIdentities: Map<string, Ending>;
  readonly endedScopes: Map<string, Ending>;
  /** Per browser (device cookie): the serialized session-command tail and its current scope. */
  readonly devices: Map<string, DeviceState>;
}

export interface DeviceState {
  tail: Promise<void>;
  pending: number;
  /** The scope this browser's last committed session command left (null: signed out). */
  scope: string | null;
  /** When `scope` was last set; 0 while this process has not committed a command for the browser. */
  touched: number;
}

/** One request's identity: its own generation changes never fence its own writes. */
export type RequestOwner = object;

export interface Ending {
  readonly until: number;
  readonly by: RequestOwner;
}

export const peerResultMs = 15_000;
export const rotationMemoryMs = 10 * 60_000;
export const unknownPauseMs = 60_000;
/** Covers the longest JWT that could still be in flight (`Token:ExpirationTimeInMinutes`, 60 by default). */
export const endedGenerationMs = 2 * 60 * 60_000;
const maxEntries = 10_000;

export function createSessionRegistry(now: () => number = Date.now): SessionRegistry {
  return {
    now,
    flights: new Map(),
    settled: new Map(),
    rotated: new Map(),
    unknown: new Map(),
    endedIdentities: new Map(),
    endedScopes: new Map(),
    devices: new Map(),
  };
}

const globalKey = Symbol.for('loto.web.sessionRegistry');

export function sessionRegistry(): SessionRegistry {
  const holder = globalThis as typeof globalThis & { [globalKey]?: SessionRegistry };
  holder[globalKey] ??= createSessionRegistry();
  return holder[globalKey];
}

export function hashKey(...parts: string[]): string {
  return createHash('sha256').update(parts.join('\u0000')).digest('hex');
}

/**
 * Runs `task` once per `key` at a time and shares its settled value with late callers for `keepMs`
 * when `keep(value)` allows it. Concurrent callers with different keys never share a result.
 */
export async function singleFlight<T>(
  registry: SessionRegistry,
  key: string,
  task: () => Promise<T>,
  keep: (value: T) => number,
): Promise<T> {
  prune(registry);
  const settled = registry.settled.get(key);
  if (settled && settled.expiresAt > registry.now()) return settled.value as T;
  const existing = registry.flights.get(key);
  if (existing) return existing as Promise<T>;
  const flight = (async () => {
    try {
      const value = await task();
      const keepMs = keep(value);
      if (keepMs > 0) registry.settled.set(key, { value, expiresAt: registry.now() + keepMs });
      return value;
    } finally {
      registry.flights.delete(key);
    }
  })();
  registry.flights.set(key, flight);
  return flight;
}

export function endIdentity(registry: SessionRegistry, sid: string, by: RequestOwner): void {
  if (!registry.endedIdentities.has(sid)) {
    registry.endedIdentities.set(sid, { until: registry.now() + endedGenerationMs, by });
  }
}

export function endScope(registry: SessionRegistry, scope: string, by: RequestOwner): void {
  if (!registry.endedScopes.has(scope)) {
    registry.endedScopes.set(scope, { until: registry.now() + endedGenerationMs, by });
  }
}

/** Ended by another request (an ending by `owner` itself does not count). */
export function isIdentityEnded(
  registry: SessionRegistry,
  sid: string,
  owner: RequestOwner,
): boolean {
  return isEndedBy(registry, registry.endedIdentities.get(sid), owner);
}

export function isScopeEnded(
  registry: SessionRegistry,
  scope: string,
  owner: RequestOwner,
): boolean {
  return isEndedBy(registry, registry.endedScopes.get(scope), owner);
}

function isEndedBy(registry: SessionRegistry, ending: Ending | undefined, owner: RequestOwner) {
  return ending !== undefined && ending.until > registry.now() && ending.by !== owner;
}

function prune(registry: SessionRegistry): void {
  const now = registry.now();
  for (const [key, entry] of registry.settled)
    if (entry.expiresAt <= now) registry.settled.delete(key);
  for (const [key, until] of registry.rotated) if (until <= now) registry.rotated.delete(key);
  for (const map of [registry.endedIdentities, registry.endedScopes]) {
    for (const [key, ending] of map) if (ending.until <= now) map.delete(key);
  }
  for (const [key, entry] of registry.unknown) if (entry.until <= now) registry.unknown.delete(key);
  for (const map of [registry.settled, registry.rotated, registry.unknown]) {
    // Bounded memory: drop the oldest insertions first.
    while (map.size > maxEntries) map.delete(map.keys().next().value as string);
  }
}

/**
 * Runs session commands of one browser one at a time, in arrival order. `run` receives the scope
 * the browser's last committed command left (undefined when this process has not seen the browser)
 * and returns the scope it leaves, so the next command can compare-and-swap against it.
 */
export async function serializeDevice<T>(
  registry: SessionRegistry,
  deviceId: string,
  run: (
    current: string | null | undefined,
  ) => Promise<{ value: T; scope: string | null | undefined }>,
): Promise<T> {
  const state: DeviceState = registry.devices.get(deviceId) ?? {
    tail: Promise.resolve(),
    pending: 0,
    scope: null,
    touched: 0,
  };
  let release!: () => void;
  const previous = state.tail;
  state.tail = new Promise<void>((done) => (release = done));
  state.pending += 1;
  registry.devices.set(deviceId, state);
  await previous;
  try {
    const { value, scope } = await run(state.touched > 0 ? state.scope : undefined);
    if (scope !== undefined) {
      state.scope = scope;
      state.touched = registry.now();
    }
    return value;
  } finally {
    state.pending -= 1;
    release();
    pruneDevices(registry);
  }
}

const deviceMemoryMs = endedGenerationMs;

function pruneDevices(registry: SessionRegistry): void {
  const now = registry.now();
  for (const [id, state] of registry.devices) {
    if (state.pending === 0 && state.touched + deviceMemoryMs <= now) registry.devices.delete(id);
  }
  for (const [id, state] of registry.devices) {
    if (registry.devices.size <= maxEntries) break;
    if (state.pending === 0) registry.devices.delete(id);
  }
}
