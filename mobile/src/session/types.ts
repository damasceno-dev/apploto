import type {
  ResponseTokenJson,
  ResponseSelfContextJson,
  ResponseUserProfileJson,
  ResponseBranchSummaryJson,
  ResponseGetCurrentBranchSummaryJson,
} from '@loto/api';

export interface CredentialEnvelope {
  readonly v: 1;
  readonly user: Pick<ResponseUserProfileJson, 'userId' | 'email' | 'name'>;
  readonly tokens: ResponseTokenJson;
  readonly branch: { readonly id: string; readonly token: string } | null;
}
export interface CredentialStore {
  read(): Promise<CredentialEnvelope | null>;
  write(value: CredentialEnvelope): Promise<void>;
  clear(): Promise<void>;
}
export type SessionStatus =
  | 'starting'
  | 'anonymous'
  | 'identity'
  | 'branch'
  | 'recovering'
  | 'renewal-unconfirmed'
  | 'storage-error'
  | 'offline'
  | 'closing';
export interface SessionSnapshot {
  readonly status: SessionStatus;
  readonly scope: number;
  readonly user: ResponseUserProfileJson | null;
  readonly branch: ResponseBranchSummaryJson | null;
  readonly clock: ResponseGetCurrentBranchSummaryJson | null;
  readonly accessChanged: boolean;
  readonly selfContext: ResponseSelfContextJson | null;
  readonly selfStatus: 'idle' | 'checking' | 'ready' | 'failed';
  readonly selfMessages: readonly string[];
  readonly dayChanged: boolean;
  readonly online: boolean;
  readonly foreground: boolean;
  readonly writesPaused: boolean;
  readonly messages: readonly string[];
  readonly recovery: string | null;
}
export class SessionGateError extends Error {
  constructor(message = 'A sessão precisa ser verificada antes de continuar.') {
    super(message);
    this.name = 'SessionGateError';
  }
}
export class SessionSupersededError extends Error {
  constructor() {
    super('O contexto da sessão mudou.');
    this.name = 'AbortError';
  }
}
