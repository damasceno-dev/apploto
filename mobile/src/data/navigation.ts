import type { SessionEngine } from '../session/engine';
import { isServerId } from '../session/credentials';
import { SessionGateError } from '../session/types';
import type { DeviceStore } from '../storage/ports';

const families = [
  '/conta',
  '/filiais',
  '/plataforma',
  '/gestao',
  '/turno',
  '/fechamentos',
  '/transacoes',
  '/clientes',
  '/relatorios',
];
const parameters = new Set([
  'date',
  'from',
  'to',
  'page',
  'pageSize',
  'status',
  'accountId',
  'operatorId',
  'clientId',
  'mode',
  'tab',
  'section',
  'row',
  'focus',
]);
const hasControl = (value: string) =>
  Array.from(value).some((character) => {
    const point = character.codePointAt(0)!;
    return point < 32 || point === 127;
  });
export function safeDestination(value: unknown): string | null {
  if (
    typeof value !== 'string' ||
    value.length > 1024 ||
    !value.startsWith('/') ||
    value.startsWith('//') ||
    value.includes('\\') ||
    hasControl(value)
  )
    return null;
  try {
    const url = new URL(value, 'https://navigation.invalid');
    let decoded = url.pathname;
    for (let i = 0; i < 3; i++) decoded = decodeURIComponent(decoded);
    if (
      url.origin !== 'https://navigation.invalid' ||
      url.hash ||
      decoded.includes('\\') ||
      decoded.startsWith('//') ||
      /eyJ[\w-]+\.[\w-]+\.|(?:^|\/)(?:password|senha|recovery|reset|convite|invitation|invite|assertion|challenge|token|oauth|auth)(?:\/|$)/i.test(
        decoded,
      ) ||
      hasControl(decoded) ||
      !families.some((path) => decoded === path || decoded.startsWith(path + '/'))
    )
      return null;
    for (const [key, content] of url.searchParams)
      if (
        !parameters.has(key) ||
        hasControl(content) ||
        /eyJ[\w-]+\.[\w-]+\.|[?#&].*(?:token|password|secret|code|auth)=/i.test(content)
      )
        return null;
    return url.pathname + url.search;
  } catch {
    return null;
  }
}
export interface NavigationIntent {
  readonly v: 1;
  readonly userId: string | null;
  readonly branchId: string | null;
  readonly destination: string;
  readonly origin: readonly string[];
  readonly serverCapturedAt: number | null;
}
const key = 'loto.navigation.intent.v1';
/** Non-secret intent only. Capability links are handled in memory by their future M11 journey. */
export class NavigationIntentStore {
  private pending: { record: NavigationIntent; capturedAt: number } | null = null;
  constructor(
    private readonly storage: DeviceStore,
    private readonly engine: SessionEngine,
    private readonly monotonicNow: () => number = () => performance.now(),
  ) {}
  async hold(destination: unknown, branchId: string | null = null, origin: readonly string[] = []) {
    if (!this.engine.canCaptureNavigation() || this.engine.getSnapshot().status === 'closing')
      return;
    const safe = safeDestination(destination);
    if (
      !safe ||
      (branchId !== null && !isServerId(branchId)) ||
      origin.length > 8 ||
      origin.some((path) => safeDestination(path) === null)
    )
      throw new SessionGateError('O destino solicitado não é válido.');
    const record: NavigationIntent = {
      v: 1,
      userId: this.engine.navigationOwner(),
      branchId: branchId ?? this.engine.navigationBranch(),
      destination: safe,
      origin,
      serverCapturedAt: this.engine.serverNow(),
    };
    // Without authoritative time, retain only this process's short-lived incoming journey.
    // A restart must never attach an indefinitely old anonymous link to a later shop user.
    if (record.serverCapturedAt === null) {
      await this.storage.removeItem(key);
      this.pending = { record, capturedAt: this.monotonicNow() };
    } else {
      this.pending = null;
      await this.storage.setItem(key, JSON.stringify(record));
    }
  }
  async resolve(
    recheck: (destination: string) => Promise<boolean>,
  ): Promise<NavigationIntent | null> {
    const snapshot = this.engine.getSnapshot();
    if (!snapshot.user || !snapshot.branch || snapshot.status !== 'branch') return null;
    const pending = this.pending;
    if (
      pending &&
      (this.monotonicNow() < pending.capturedAt ||
        this.monotonicNow() - pending.capturedAt >= 1800000)
    ) {
      this.pending = null;
      return null;
    }
    const raw = pending ? JSON.stringify(pending.record) : await this.storage.getItem(key);
    if (raw === null) return null;
    let intent: NavigationIntent;
    try {
      const candidate = JSON.parse(raw) as NavigationIntent;
      const now = this.engine.serverNow();
      if (
        candidate.v !== 1 ||
        safeDestination(candidate.destination) === null ||
        (candidate.userId !== null && candidate.userId !== snapshot.user.userId) ||
        (candidate.branchId !== null && !isServerId(candidate.branchId)) ||
        !Array.isArray(candidate.origin) ||
        candidate.origin.length > 8 ||
        candidate.origin.some((path) => safeDestination(path) === null) ||
        (candidate.serverCapturedAt === null
          ? !pending
          : typeof candidate.serverCapturedAt !== 'number' ||
            !Number.isFinite(candidate.serverCapturedAt) ||
            now === null ||
            now < candidate.serverCapturedAt ||
            now - candidate.serverCapturedAt >= 1800000)
      )
        throw new Error('Invalid retained intent');
      intent = candidate;
    } catch {
      await this.acknowledge();
      return null;
    }
    if (intent.branchId !== null && intent.branchId !== snapshot.branch.id) return null;
    for (const destination of [...intent.origin, intent.destination]) {
      if (!(await recheck(destination))) {
        await this.acknowledge();
        return null;
      }
      if (this.engine.getSnapshot().scope !== snapshot.scope) return null;
    }
    // Bind an anonymous incoming link to the identity that actually passed its fresh gates.
    const verified = {
      ...intent,
      userId: snapshot.user.userId,
      branchId: snapshot.branch.id,
      serverCapturedAt: intent.serverCapturedAt ?? this.engine.serverNow(),
    };
    if (verified.serverCapturedAt === null) return null;
    if (
      pending &&
      (this.pending !== pending ||
        this.monotonicNow() - pending.capturedAt >= 1800000 ||
        this.monotonicNow() < pending.capturedAt)
    )
      return null;
    await this.storage.setItem(key, JSON.stringify(verified));
    this.pending = null;
    return this.engine.getSnapshot().scope === snapshot.scope ? verified : null;
  }
  async discardForeignBranch(branchId: string) {
    try {
      const raw = this.pending?.record ?? JSON.parse((await this.storage.getItem(key)) ?? 'null');
      if (raw && raw.branchId !== null && raw.branchId !== branchId) await this.acknowledge();
    } catch {
      await this.acknowledge();
    }
  }
  async acknowledge() {
    this.pending = null;
    await this.storage.removeItem(key);
  }
}
