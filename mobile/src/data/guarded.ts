import { parseETag, toIfMatch, type ApiRequestOptions } from '@loto/api';
import type { SessionEngine } from '../session/engine';
import { SessionGateError, SessionSupersededError } from '../session/types';
import { actionFailure } from './failures';
/** Session-lived concurrency token. Never persisted with a draft or upgraded after a conflict. */
export class GuardedResource {
  private version: number | null = null;
  private blocked = false;
  private active = false;
  private readonly scope;
  private readonly client;
  constructor(private readonly engine: SessionEngine) {
    this.scope = engine.getSnapshot().scope;
    this.client = engine.client();
  }
  acceptFresh(headers: Headers, version: number) {
    if (this.engine.getSnapshot().scope !== this.scope) throw new SessionSupersededError();
    if (parseETag(headers.get('etag')) !== version)
      throw new SessionGateError('A versão do registro não pôde ser verificada.');
    toIfMatch(version);
    this.version = version;
    this.blocked = false;
  }
  async execute<T>(send: (etag: string, options: ApiRequestOptions) => Promise<T>) {
    if (
      this.active ||
      this.blocked ||
      this.version === null ||
      this.engine.getSnapshot().writesPaused
    )
      return { kind: 'paused' } as const;
    if (this.engine.getSnapshot().scope !== this.scope) throw new SessionSupersededError();
    this.active = true;
    try {
      const data = await send(toIfMatch(this.version), { client: this.client });
      this.blocked = true; // A subsequent edit requires the acknowledged new version or a fresh read.
      return { kind: 'confirmed', data } as const;
    } catch (error) {
      const failure = actionFailure(error);
      // No deduplication is invented for guarded/non-idempotent commands.
      if (failure.kind === 'uncertain' || failure.kind === 'conflict') this.blocked = true;
      return { kind: 'failed', failure } as const;
    } finally {
      this.active = false;
    }
  }
}
