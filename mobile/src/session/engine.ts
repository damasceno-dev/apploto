import {
  createApiClient,
  getUserProfile,
  postUserLogin,
  postUserLogout,
  postUserRenewToken,
  postBranchSession,
  getBranchCurrent,
  isApiError,
  ApiError,
  type ApiClient,
  type AuthTier,
  type RequestUserLoginJson,
  type ResponseUserProfileJson,
  type ResponseGetCurrentBranchSummaryJson,
} from '@loto/api';
import { parseCalendarDate, formatWallDateTime } from '@loto/core';
import { authPolicy } from '../data/auth-policy.generated';
import { isServerId } from './credentials';
import {
  SessionGateError,
  SessionSupersededError,
  type CredentialEnvelope,
  type CredentialStore,
  type SessionSnapshot,
} from './types';

export interface SessionEngineOptions {
  readonly baseUrl: string;
  readonly credentials: CredentialStore;
  readonly fetch: typeof fetch;
  readonly monotonicNow: () => number;
  readonly clearSelections: (userId: string, branchId: string) => Promise<void>;
}
const renewalCopy = 'Não foi possível renovar sua sessão. Entre novamente.';
const storageCopy =
  'Não foi possível guardar as alterações neste dispositivo. Mantenha esta tela aberta.';
const messagesOf = (error: unknown) => (isApiError(error) ? error.errorMessages : []);

/** One mobile identity and at most one branch. No credential is exposed in snapshots/cache. */
export class SessionEngine {
  private envelope: CredentialEnvelope | null = null;
  private profile: ResponseUserProfileJson | null = null;
  private identityGeneration = 0;
  private scopeGeneration = 0;
  private scopeAbort = new AbortController();
  private listeners = new Set<() => void>();
  private storageTail: Promise<unknown> = Promise.resolve();
  private renewal: Promise<boolean> | null = null;
  private verificationAttempts = 0;
  private commandActive = false;
  private closing = false;
  private lastClockAt: number | null = null;
  private baselineDate: string | null = null;
  private clockReadSequence = 0;
  private observedServerMs: number | null = null;
  private snapshot: SessionSnapshot = {
    status: 'starting',
    scope: 0,
    user: null,
    branch: null,
    clock: null,
    dayChanged: false,
    online: true,
    foreground: true,
    writesPaused: true,
    messages: [],
    recovery: null,
  };
  constructor(private readonly options: SessionEngineOptions) {}
  subscribe = (listener: () => void) => {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  };
  getSnapshot = () => this.snapshot;
  get baseUrl() {
    return this.options.baseUrl;
  }
  serverNow = () => this.observedServerMs;
  private publish(update: Partial<SessionSnapshot>) {
    const next = { ...this.snapshot, ...update, scope: this.scopeGeneration };
    next.writesPaused =
      next.status !== 'branch' ||
      next.dayChanged ||
      !next.online ||
      !next.foreground ||
      !next.clock ||
      this.lastClockAt === null ||
      this.options.monotonicNow() - this.lastClockAt > 60000 ||
      this.closing ||
      !!this.renewal;
    this.snapshot = next;
    for (const listener of this.listeners) listener();
  }
  private boundary() {
    this.scopeAbort.abort();
    this.scopeAbort = new AbortController();
    this.scopeGeneration++;
    this.lastClockAt = null;
    this.publish({ clock: null, branch: null, dayChanged: false });
  }
  private assertIdentity(generation: number) {
    if (generation !== this.identityGeneration) throw new SessionSupersededError();
  }
  private async durable(value: CredentialEnvelope | null, generation: number) {
    const job = this.storageTail
      .catch(() => undefined)
      .then(async () => {
        this.assertIdentity(generation);
        if (value) await this.options.credentials.write(value);
        else await this.options.credentials.clear();
        this.assertIdentity(generation);
      });
    this.storageTail = job;
    try {
      await job;
    } catch (error) {
      if (generation === this.identityGeneration)
        this.publish({ status: 'storage-error', recovery: storageCopy });
      throw error;
    }
  }
  private direct(token: string | null): ApiClient {
    return createApiClient({
      baseUrl: this.options.baseUrl,
      getAccessToken: () => token,
      fetch: async (input, init) => {
        const response = await this.options.fetch(input, init);
        this.observe(response);
        return response;
      },
    });
  }
  private observe(response: Response) {
    const date = response.headers.get('date');
    const value = date ? Date.parse(date) : NaN;
    if (Number.isFinite(value)) this.observedServerMs = value;
  }
  private async command<T>(action: () => Promise<T>): Promise<T> {
    if (this.commandActive || this.closing)
      throw new SessionGateError('Aguarde a verificação da sessão.');
    this.commandActive = true;
    try {
      return await action();
    } finally {
      this.commandActive = false;
    }
  }
  async start(): Promise<void> {
    return this.command(async () => {
      const generation = ++this.identityGeneration;
      this.boundary();
      this.publish({ status: 'starting', user: null, recovery: null, messages: [] });
      try {
        let value: CredentialEnvelope | null;
        try {
          value = await this.options.credentials.read();
        } catch {
          this.publish({ status: 'storage-error', user: null, recovery: storageCopy });
          return;
        }
        this.assertIdentity(generation);
        this.envelope = value;
        if (!value) {
          this.publish({ status: 'anonymous' });
          return;
        }
        if (!(await this.verifyIdentity(generation))) return;
        if (this.envelope?.branch && this.snapshot.status !== 'branch')
          await this.openBranch(this.envelope.branch.id, generation);
      } catch (error) {
        await this.handleVerificationFailure(error, generation);
      }
    });
  }
  async login(request: RequestUserLoginJson): Promise<void> {
    return this.command(async () => {
      const previousSelection = this.envelope?.branch
        ? { userId: this.envelope.user.userId, branchId: this.envelope.branch.id }
        : null;
      const generation = ++this.identityGeneration;
      this.boundary();
      this.envelope = null;
      this.profile = null;
      this.baselineDate = null;
      this.publish({ status: 'starting', user: null, messages: [], recovery: null });
      await this.durable(null, generation);
      if (previousSelection) await this.cleanupSelections(previousSelection, generation);
      try {
        const { data } = await postUserLogin(request, { client: this.direct(null) });
        this.assertIdentity(generation);
        if (
          !isServerId(data.userId) ||
          !data.email ||
          !data.responseToken.token ||
          !data.responseToken.refreshToken
        )
          throw new SessionGateError('A resposta de autenticação é inválida.');
        this.envelope = {
          v: 1,
          user: { userId: data.userId, name: data.name, email: data.email },
          tokens: data.responseToken,
          branch: null,
        };
        await this.durable(this.envelope, generation);
        await this.verifyIdentity(generation);
      } catch (error) {
        if (generation === this.identityGeneration && this.snapshot.status !== 'storage-error') {
          if (this.envelope) await this.handleVerificationFailure(error, generation);
          else
            this.publish({
              status: 'anonymous',
              messages: messagesOf(error),
              recovery: isApiError(error)
                ? null
                : 'Não foi possível concluir a ação. Verifique sua conexão e tente novamente.',
            });
        }
        throw error;
      }
    });
  }
  private async verifyIdentity(generation: number) {
    if (!this.envelope) throw new SessionGateError();
    let profile;
    try {
      profile = (await getUserProfile({ client: this.direct(this.envelope.tokens.token) })).data;
    } catch (error) {
      if (!isApiError(error) || error.status !== 401) throw error;
      if (!(await this.renewIdentity())) return false;
      if (!this.envelope) throw new SessionGateError();
      profile = (await getUserProfile({ client: this.direct(this.envelope.tokens.token) })).data;
    }
    this.assertIdentity(generation);
    if (profile.userId !== this.envelope?.user.userId || profile.email !== this.envelope.user.email)
      throw new SessionGateError('A identidade guardada não corresponde à sessão verificada.');
    this.profile = profile;
    const branchReady =
      this.envelope?.branch &&
      this.snapshot.branch &&
      this.snapshot.clock &&
      this.envelope.branch.id === this.snapshot.branch.id &&
      this.snapshot.clock.branch.id === this.snapshot.branch.id;
    if (!branchReady && (this.snapshot.branch || this.snapshot.clock)) this.boundary();
    this.publish({
      status: branchReady ? 'branch' : 'identity',
      user: profile,
      messages: [],
      recovery: null,
    });
    return true;
  }
  async selectBranch(id: string | null): Promise<void> {
    return this.command(async () => {
      if (!this.envelope || !this.profile) throw new SessionGateError();
      if (id !== null && !isServerId(id)) throw new SessionGateError('A filial é inválida.');
      const generation = this.identityGeneration;
      const previous = this.envelope.branch;
      this.boundary();
      this.baselineDate = null;
      this.publish({ status: 'recovering' });
      this.envelope = { ...this.envelope, branch: null };
      await this.durable(this.envelope, generation);
      if (previous) {
        try {
          await this.options.clearSelections(this.envelope.user.userId, previous.id);
        } catch (error) {
          this.publish({ status: 'storage-error', recovery: storageCopy });
          throw error;
        }
      }
      if (id && id !== previous?.id)
        await this.cleanupSelections(
          { userId: this.envelope.user.userId, branchId: id },
          generation,
        );
      this.publish({ status: 'identity', user: this.profile });
      if (id) {
        try {
          await this.openBranch(id, generation);
        } catch (error) {
          await this.branchFailure(error, generation);
          throw error;
        }
      }
    });
  }
  private async openBranch(id: string, generation: number, recover = true) {
    if (!this.envelope) throw new SessionGateError();
    let session;
    try {
      session = (
        await postBranchSession(
          { branchId: id },
          { client: this.direct(this.envelope.tokens.token) },
        )
      ).data;
    } catch (error) {
      if (!recover || !isApiError(error) || error.status !== 401 || !(await this.renewIdentity()))
        throw error;
      if (!this.envelope) throw new SessionGateError();
      session = (
        await postBranchSession(
          { branchId: id },
          { client: this.direct(this.envelope.tokens.token) },
        )
      ).data;
    }
    this.assertIdentity(generation);
    if (!this.envelope || session.branch.id !== id || !session.token)
      throw new SessionGateError('O contexto da filial é inválido.');
    this.envelope = { ...this.envelope, branch: { id, token: session.token } };
    await this.durable(this.envelope, generation);
    await this.readClock(generation, recover);
  }
  private async readClock(generation: number, recover = true) {
    const branch = this.envelope?.branch;
    const scope = this.scopeGeneration;
    const sequence = ++this.clockReadSequence;
    if (!branch) throw new SessionGateError();
    let data: ResponseGetCurrentBranchSummaryJson;
    try {
      data = (await getBranchCurrent({ client: this.direct(branch.token) })).data;
    } catch (error) {
      this.assertIdentity(generation);
      if (
        scope !== this.scopeGeneration ||
        sequence !== this.clockReadSequence ||
        this.envelope?.branch?.id !== branch.id
      )
        throw new SessionSupersededError();
      if (recover && isApiError(error) && error.status === 401 && (await this.renewIdentity()))
        return;
      throw error;
    }
    this.assertIdentity(generation);
    if (
      sequence !== this.clockReadSequence ||
      scope !== this.scopeGeneration ||
      this.envelope?.branch?.id !== branch.id
    )
      throw new SessionSupersededError();
    this.validateClock(data, branch.id);
    const roleChanged = this.snapshot.branch && this.snapshot.branch.role !== data.branch.role;
    const dayChanged =
      this.snapshot.clock && this.snapshot.clock.branchLocalDate !== data.branchLocalDate;
    if ((roleChanged || dayChanged) && this.envelope) {
      try {
        await this.options.clearSelections(this.envelope.user.userId, branch.id);
        this.assertIdentity(generation);
        if (scope !== this.scopeGeneration) throw new SessionSupersededError();
      } catch (error) {
        if (generation === this.identityGeneration && !(error instanceof SessionSupersededError)) {
          this.lastClockAt = null;
          this.publish({ status: 'storage-error', recovery: storageCopy });
        }
        throw error;
      }
    }
    if (roleChanged) this.boundary();
    if (this.baselineDate === null) this.baselineDate = data.branchLocalDate;
    this.lastClockAt = this.options.monotonicNow();
    this.publish({
      status: 'branch',
      user: this.profile,
      branch: data.branch,
      clock: data,
      dayChanged: this.baselineDate !== data.branchLocalDate,
      messages: [],
      recovery: null,
    });
  }
  private validateClock(data: ResponseGetCurrentBranchSummaryJson, id: string) {
    if (
      data.branch.id !== id ||
      !['Admin', 'Manager', 'Member'].includes(data.branch.role) ||
      !parseCalendarDate(data.branchLocalDate) ||
      data.branchLocalDateTime.slice(0, 10) !== data.branchLocalDate
    )
      throw new SessionGateError('Não foi possível verificar o horário da filial.');
    formatWallDateTime(data.branchLocalDateTime);
  }
  acknowledgeDayChange() {
    if (this.snapshot.clock) this.baselineDate = this.snapshot.clock.branchLocalDate;
    this.publish({ dayChanged: false });
  }
  /** Timers/events request fresh data; they never advance business time or submit a write. */
  async refresh(): Promise<void> {
    if (
      this.closing ||
      !this.envelope ||
      !this.snapshot.online ||
      !this.snapshot.foreground ||
      ['storage-error', 'renewal-unconfirmed'].includes(this.snapshot.status)
    )
      return;
    const generation = this.identityGeneration;
    const scope = this.scopeGeneration;
    try {
      if (!(await this.verifyIdentity(generation))) return;
      if (scope !== this.scopeGeneration) return;
      if (this.envelope?.branch) await this.readClock(generation);
    } catch (error) {
      await this.handleVerificationFailure(error, generation);
    }
  }
  async connectivity(online: boolean) {
    if (!online) this.lastClockAt = null;
    this.publish({ online });
    if (online) await this.refresh();
  }
  async foreground(active: boolean) {
    if (!active) this.lastClockAt = null;
    this.publish({ foreground: active });
    if (active) await this.refresh();
  }
  async recover() {
    if (this.snapshot.status === 'storage-error' && !this.envelope) {
      await this.start();
      return;
    }
    if (this.snapshot.status === 'storage-error' && this.envelope) {
      this.lastClockAt = null;
      await this.durable(this.envelope, this.identityGeneration);
      this.publish({ status: 'recovering' });
      await this.refresh();
      return;
    }
    if (this.snapshot.status === 'renewal-unconfirmed') {
      await this.renewIdentity(true);
      return;
    }
    await this.refresh();
  }
  private renewIdentity(verification = false, forLogout = false): Promise<boolean> {
    if (this.renewal) return this.renewal;
    if (
      !this.envelope ||
      (this.closing && !forLogout) ||
      (this.snapshot.status === 'renewal-unconfirmed' &&
        (!verification || this.verificationAttempts >= 2))
    )
      return Promise.resolve(false);
    const generation = this.identityGeneration;
    const old = this.envelope;
    this.verificationAttempts++;
    this.publish({ status: 'recovering', recovery: null });
    const job = (async () => {
      let rotated = false;
      let identityVerified = false;
      try {
        const { data } = await postUserRenewToken(
          { value: old.tokens.refreshToken },
          { client: this.direct(null) },
        );
        this.assertIdentity(generation);
        if (!data.token || !data.refreshToken)
          throw new SessionGateError('A resposta de renovação é inválida.');
        this.envelope = { ...old, tokens: data, branch: null };
        await this.durable(this.envelope, generation);
        this.verificationAttempts = 0;
        rotated = true;
        if (!this.closing) {
          this.profile = (await getUserProfile({ client: this.direct(data.token) })).data;
          this.assertIdentity(generation);
          if (this.profile.userId !== old.user.userId || this.profile.email !== old.user.email)
            throw new SessionGateError();
          identityVerified = true;
          if (old.branch) await this.openBranch(old.branch.id, generation, false);
          else
            this.publish({ status: 'identity', user: this.profile, messages: [], recovery: null });
        }
        return true;
      } catch (error) {
        if (generation !== this.identityGeneration) return false;
        if (
          rotated &&
          identityVerified &&
          old.branch &&
          isApiError(error) &&
          this.snapshot.status !== 'storage-error'
        ) {
          await this.branchFailure(error, generation);
          // Rotation/profile are confirmed. Only branch authority was refused/unavailable.
          return true;
        } else if (error instanceof SessionGateError && rotated) {
          await this.clearIdentity([], error.message);
        } else if (isApiError(error) && error.status === 401) {
          await this.clearIdentity(messagesOf(error), renewalCopy);
        } else if (rotated && this.snapshot.status !== 'storage-error') {
          this.lastClockAt = null;
          this.publish({
            status: 'offline',
            messages: messagesOf(error),
            recovery: 'Não foi possível concluir a ação. Verifique sua conexão e tente novamente.',
          });
        } else if (this.snapshot.status !== 'storage-error')
          this.publish({
            status: 'renewal-unconfirmed',
            messages: messagesOf(error),
            recovery:
              'Não foi possível confirmar a renovação da sessão. Entre novamente ou verifique sua conexão.',
          });
        return false;
      }
    })();
    this.renewal = job;
    void job
      .finally(() => {
        if (this.renewal === job) {
          this.renewal = null;
          this.publish({});
        }
      })
      .catch(() => undefined);
    return job;
  }
  private async cleanupSelections(
    selection: { userId: string; branchId: string },
    generation: number,
  ) {
    this.assertIdentity(generation);
    try {
      await this.options.clearSelections(selection.userId, selection.branchId);
      this.assertIdentity(generation);
    } catch (error) {
      if (generation === this.identityGeneration)
        this.publish({ status: 'storage-error', recovery: storageCopy });
      throw error;
    }
  }
  private async clearIdentity(
    messages: readonly string[] = [],
    recovery: string | null = null,
    selection: { userId: string; branchId: string } | null = this.envelope?.branch
      ? { userId: this.envelope.user.userId, branchId: this.envelope.branch.id }
      : null,
  ) {
    const generation = ++this.identityGeneration;
    this.boundary();
    this.envelope = null;
    this.profile = null;
    this.baselineDate = null;
    this.observedServerMs = null;
    this.publish({ status: 'anonymous', user: null, messages, recovery });
    await this.durable(null, generation);
    if (selection) await this.cleanupSelections(selection, generation);
  }
  private async branchFailure(error: unknown, generation: number) {
    this.assertIdentity(generation);
    if (['storage-error', 'renewal-unconfirmed', 'offline'].includes(this.snapshot.status)) return;
    const previous = this.envelope?.branch;
    this.boundary();
    if (previous && this.envelope) {
      try {
        await this.options.clearSelections(this.envelope.user.userId, previous.id);
      } catch (storageError) {
        this.publish({ status: 'storage-error', recovery: storageCopy });
        throw storageError;
      }
      this.assertIdentity(generation);
    }
    if (this.envelope) {
      this.envelope = { ...this.envelope, branch: null };
      await this.durable(this.envelope, generation);
    }
    this.publish({
      status: 'identity',
      user: this.profile,
      messages: messagesOf(error),
      recovery: 'Escolha novamente uma filial disponível.',
    });
  }
  private async handleVerificationFailure(error: unknown, generation: number) {
    if (generation !== this.identityGeneration || error instanceof SessionSupersededError) return;
    if (this.snapshot.status === 'storage-error' || this.snapshot.status === 'renewal-unconfirmed')
      return;
    if (isApiError(error) && error.status === 401) {
      await this.clearIdentity(messagesOf(error), renewalCopy);
      return;
    }
    if (isApiError(error) && [403, 404].includes(error.status ?? 0) && this.envelope?.branch) {
      await this.branchFailure(error, generation);
      return;
    }
    if (error instanceof SessionGateError) {
      await this.clearIdentity([], error.message);
      return;
    }
    this.lastClockAt = null;
    this.publish({
      status: 'offline',
      messages: messagesOf(error),
      recovery: 'Não foi possível concluir a ação. Verifique sua conexão e tente novamente.',
    });
  }
  async logout(): Promise<{ serverConfirmed: boolean; localCleared: boolean }> {
    if (this.closing) throw new SessionGateError('Aguarde o encerramento da sessão.');
    const previousSelection = this.envelope?.branch
      ? { userId: this.envelope.user.userId, branchId: this.envelope.branch.id }
      : null;
    this.closing = true;
    this.boundary();
    this.publish({ status: 'closing', user: null });
    let serverConfirmed = false;
    let localCleared = false;
    try {
      // A completed in-flight rotation must be revoked with its NEW refresh token.
      await this.renewal?.catch(() => false);
      const current = this.envelope;
      if (current) {
        try {
          await postUserLogout(
            { refreshToken: current.tokens.refreshToken },
            { client: this.direct(current.tokens.token) },
          );
          serverConfirmed = true;
        } catch (error) {
          if (
            isApiError(error) &&
            error.status === 401 &&
            (await this.renewIdentity(false, true)) &&
            this.envelope
          ) {
            try {
              await postUserLogout(
                { refreshToken: this.envelope.tokens.refreshToken },
                { client: this.direct(this.envelope.tokens.token) },
              );
              serverConfirmed = true;
            } catch {
              serverConfirmed = false;
            }
          }
        }
      }
    } finally {
      try {
        await this.clearIdentity(
          [],
          serverConfirmed
            ? null
            : 'Você saiu deste dispositivo. Não foi possível confirmar o encerramento da sessão no servidor.',
          previousSelection,
        );
        localCleared = true;
      } finally {
        this.closing = false;
        this.publish({});
      }
    }
    return { serverConfirmed, localCleared };
  }
  private tier(method: string, input: string): AuthTier | null {
    const url = new URL(input);
    const base = new URL(this.options.baseUrl);
    if (url.origin !== base.origin) return null;
    const prefix = base.pathname.replace(/\/$/, '');
    const pathname = url.pathname.slice(prefix.length);
    const segments = pathname.split('/');
    return (
      authPolicy.find(
        ([m, template]) =>
          m === method &&
          template.split('/').length === segments.length &&
          template
            .split('/')
            .every(
              (part, index) =>
                part.startsWith('{') || part.toLowerCase() === segments[index]?.toLowerCase(),
            ),
      )?.[2] ?? null
    );
  }
  /** Immutable scope + cancellation fences cover token wait, response headers AND response body. */
  client(): ApiClient {
    const scope = this.scopeGeneration;
    const signal = this.scopeAbort.signal;
    const guard = () => {
      if (scope !== this.scopeGeneration || signal.aborted || this.closing)
        throw new SessionSupersededError();
    };
    const token = async (tier: AuthTier, method: string) => {
      guard();
      if (tier === 'anonymous') return null;
      if (this.renewal) await this.renewal;
      guard();
      if (
        !this.envelope ||
        !this.profile ||
        !['identity', 'branch'].includes(this.snapshot.status) ||
        !this.snapshot.online ||
        !this.snapshot.foreground ||
        (tier === 'branch' &&
          method !== 'GET' &&
          (this.snapshot.writesPaused ||
            this.lastClockAt === null ||
            this.options.monotonicNow() - this.lastClockAt > 60000))
      )
        throw new SessionGateError();
      if (tier === 'identity') return this.envelope.tokens.token;
      if (this.snapshot.status !== 'branch' || !this.envelope.branch || !this.snapshot.clock)
        throw new SessionGateError();
      return this.envelope.branch.token;
    };
    return createApiClient({
      baseUrl: this.options.baseUrl,
      getAccessToken: ({ authTier, method }) => token(authTier, method),
      fetch: async (input, init) => {
        guard();
        const method = init?.method?.toUpperCase() ?? 'GET';
        const url = String(input);
        const tier = this.tier(method, url);
        const abort = new AbortController();
        const cancel = () => abort.abort();
        signal.addEventListener('abort', cancel, { once: true });
        init?.signal?.addEventListener('abort', cancel, { once: true });
        const cleanup = () => {
          signal.removeEventListener('abort', cancel);
          init?.signal?.removeEventListener('abort', cancel);
        };
        try {
          if (init?.signal?.aborted) abort.abort();
          let response = await this.options.fetch(input, { ...init, signal: abort.signal });
          guard();
          this.observe(response);
          if (response.status === 401 && tier && tier !== 'anonymous') {
            const recovered = await this.renewIdentity();
            guard();
            if (recovered && method === 'GET') {
              const headers = new Headers(init?.headers);
              const access = await token(tier, method);
              guard();
              if (access) headers.set('Authorization', `Bearer ${access}`);
              else headers.delete('Authorization');
              response = await this.options.fetch(input, {
                ...init,
                headers,
                signal: abort.signal,
              });
              guard();
              this.observe(response);
              if (response.status === 401) {
                const refusalBody = await response.text();
                guard();
                let body: unknown = null;
                try {
                  body = JSON.parse(refusalBody);
                } catch {
                  /* An unrecognized error body supplies no messages. */
                }
                const messages =
                  typeof body === 'object' &&
                  body !== null &&
                  'errorMessages' in body &&
                  Array.isArray(body.errorMessages) &&
                  body.errorMessages.every((value: unknown) => typeof value === 'string')
                    ? (body.errorMessages as string[])
                    : [];
                const refusal = new ApiError({
                  kind: 'http',
                  method,
                  path: new URL(url).pathname,
                  dispatched: true,
                  status: 401,
                  headers: response.headers,
                  errorMessages: messages,
                });
                if (tier === 'branch') await this.branchFailure(refusal, this.identityGeneration);
                else await this.clearIdentity(messages, renewalCopy);
                cleanup();
                // Preserve only the definitive rejected response across the deliberate auth boundary.
                return new Response(refusalBody, {
                  status: response.status,
                  headers: response.headers,
                });
              }
            }
          } else if (response.status === 403 && tier === 'branch') {
            this.lastClockAt = null;
            this.publish({ recovery: 'Seu acesso precisa ser verificado novamente.' });
            // No renewal on 403. The next explicit refresh obtains live role/grants.
          }
          return new Proxy(response, {
            get: (target, key) => {
              if (key === 'text')
                return async () => {
                  try {
                    guard();
                    const body = await target.text();
                    guard();
                    return body;
                  } finally {
                    cleanup();
                  }
                };
              const value = Reflect.get(target, key, target);
              return typeof value === 'function' ? value.bind(target) : value;
            },
          });
        } catch (error) {
          cleanup();
          throw error;
        }
      },
    });
  }
}
