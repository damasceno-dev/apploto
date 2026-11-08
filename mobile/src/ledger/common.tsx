import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  ApiError,
  getAccount,
  getCategory,
  getClient,
  getOperator,
  getTransactionType,
  type ResponseClientJson,
  type ResponseOperatorJson,
} from '@loto/api';
import { useSession, useScopedApiClient } from '@/session/provider';
import { statementAllowed } from '@/admin/clients';
import { secureUuid } from '@/storage/native';
import { FinancialOperation, type FinancialIntent, type OperationRecord } from '@/data/operations';
import type { ActionFailure } from '@/data/failures';
import { Button, Checkbox, Notice, RadioGroup, Radio, Skeleton, UncertainNotice } from '@/ui';
import { errorMessages, useTimeReadsAllowed } from '@/time/common';
import {
  branchAccounts,
  memberAccounts,
  withDirection,
  type AccountOption,
  type TypeWithDirection,
} from './contracts';

export { errorMessages };
/** Branch reads wait for verified authority, connection and foreground (same gate as M5/M9). */
export const useLedgerReads = useTimeReadsAllowed;
export const elevated = (role: string | undefined) => role === 'Manager' || role === 'Admin';

/* Lookups -------------------------------------------------------------------------------------- */

/** Types with their category direction (any branch role). */
export function useTypes() {
  const { snapshot } = useSession();
  const client = useScopedApiClient();
  const reads = useLedgerReads();
  return useQuery({
    queryKey: ['ledger', snapshot.scope, 'types'],
    queryFn: async ({ signal }) => {
      const [types, categories] = await Promise.all([
        getTransactionType({ client, signal }),
        getCategory({ client, signal }),
      ]);
      return withDirection(types.data.items, categories.data.items) as TypeWithDirection[];
    },
    enabled: reads,
    retry: false,
    staleTime: 0,
  });
}
/**
 * Accounts the caller may name: a Member's explicit grants (self-context, re-read by the shell on
 * entry), Manager/Admin the branch accounts (`GET /account` is Manager/Admin only).
 */
export interface Lookup<T> {
  readonly status: 'pending' | 'error' | 'success';
  readonly data: T | undefined;
  readonly error: unknown;
  readonly refetch: () => unknown;
}
export function useAccounts(): Lookup<AccountOption[]> {
  const { snapshot, engine } = useSession();
  const client = useScopedApiClient();
  const reads = useLedgerReads();
  const manage = elevated(snapshot.branch?.role);
  const branch = useQuery({
    queryKey: ['ledger', snapshot.scope, 'accounts'],
    queryFn: async ({ signal }) =>
      branchAccounts((await getAccount({ client, signal })).data.accounts),
    enabled: reads && manage,
    retry: false,
    staleTime: 0,
  });
  if (manage)
    return {
      status: branch.status,
      data: branch.data,
      error: branch.error,
      refetch: branch.refetch,
    };
  return {
    status:
      snapshot.selfStatus === 'ready'
        ? 'success'
        : snapshot.selfStatus === 'failed'
          ? 'error'
          : 'pending',
    data: snapshot.selfStatus === 'ready' ? memberAccounts(snapshot.selfContext) : undefined,
    error: snapshot.selfStatus === 'failed' ? new Error(snapshot.selfMessages.join(' ')) : null,
    refetch: () => engine.recheckSelfContext(),
  };
}
export function useClients(enabled = true) {
  const { snapshot } = useSession();
  const client = useScopedApiClient();
  const reads = useLedgerReads();
  return useQuery({
    queryKey: ['ledger', snapshot.scope, 'clients'],
    queryFn: async ({ signal }) =>
      (await getClient({ client, signal })).data.clients as ResponseClientJson[],
    enabled: reads && enabled,
    retry: false,
    staleTime: 0,
  });
}
/** `GET /operator` is Manager/Admin only: Members never ask for it. */
export function useOperators() {
  const { snapshot } = useSession();
  const client = useScopedApiClient();
  const reads = useLedgerReads();
  return useQuery({
    queryKey: ['ledger', snapshot.scope, 'operators'],
    queryFn: async ({ signal }) =>
      (await getOperator({ client, signal })).data.operators as ResponseOperatorJson[],
    enabled: reads && elevated(snapshot.branch?.role),
    retry: false,
    staleTime: 0,
  });
}

/* Durable financial intent ---------------------------------------------------------------------- */

export type IntentState =
  | { readonly phase: 'checking' }
  | { readonly phase: 'idle' }
  | { readonly phase: 'sending' }
  | { readonly phase: 'confirmed'; readonly data: unknown; readonly cleared: boolean }
  | { readonly phase: 'refused'; readonly failure: ActionFailure }
  | { readonly phase: 'uncertain'; readonly failure: ActionFailure | null }
  | { readonly phase: 'reconcile' }
  | { readonly phase: 'unresolved' }
  | { readonly phase: 'storage' }
  | { readonly phase: 'retained'; readonly record: OperationRecord };

/**
 * One durable, contract-idempotent financial command per form resource (M2 `FinancialOperation`):
 * the exact request bytes, key and scope are persisted before dispatch; an unknown outcome is
 * verified only by an explicit identical retry inside the server's replay window; a changed
 * request is blocked until the earlier one is reconciled. Nothing here ever retries on its own.
 */
export function useFinancialIntent(
  resource: string,
  /** Runs once per confirmed outcome — from the first send or from an explicit verification. */
  onConfirmed?: (data: unknown) => void | Promise<void>,
  /** A screen that offers further commands (the cheque card) returns to idle after a confirmation. */
  repeatable = false,
) {
  const { engine, store, snapshot } = useSession();
  const confirmedHandler = useRef(onConfirmed);
  useEffect(() => {
    confirmedHandler.current = onConfirmed;
  }, [onConfirmed]);
  const scope = snapshot.scope;
  const owner = `${resource}:${scope}`;
  // State belongs to one resource and scope: anything else reads as still checking.
  const [stored, setStored] = useState<{ owner: string; state: IntentState }>({
    owner,
    state: { phase: 'checking' },
  });
  const state: IntentState = useMemo(
    () => (stored.owner === owner ? stored.state : { phase: 'checking' }),
    [stored, owner],
  );
  const setState = useCallback((next: IntentState) => setStored({ owner, state: next }), [owner]);
  const [paused, setPaused] = useState(false);
  const controller = useRef<FinancialOperation | null>(null);
  useEffect(() => {
    let active = true;
    controller.current = null;
    void (async () => {
      const storage = await store.scope();
      const operation = new FinancialOperation(engine, store, storage, resource, secureUuid);
      const found = await operation.inspect();
      if (!active) return;
      controller.current = operation;
      if (found.status === 'corrupt') setState({ phase: 'storage' });
      else if (found.status === 'found')
        setState(
          found.unknown
            ? { phase: 'uncertain', failure: null }
            : { phase: 'retained', record: found.record },
        );
      else setState({ phase: 'idle' });
    })().catch(() => {
      if (active) setState({ phase: 'storage' });
    });
    return () => {
      active = false;
    };
  }, [engine, store, resource, scope, setState]);
  const apply = useCallback(
    (outcome: Awaited<ReturnType<FinancialOperation['submit']>>, previous: IntentState) => {
      switch (outcome.kind) {
        case 'confirmed':
          return setState({
            phase: 'confirmed',
            data: outcome.data,
            cleared: outcome.deviceRecordCleared,
          });
        case 'failed':
          return setState(
            outcome.failure.kind === 'uncertain'
              ? { phase: 'uncertain', failure: outcome.failure }
              : { phase: 'refused', failure: outcome.failure },
          );
        case 'paused':
          setPaused(true);
          return setState(previous);
        case 'in-flight':
          return setState(previous);
        case 'nothing-to-retry':
          return setState({ phase: 'idle' });
        case 'storage-unreadable':
          return setState({ phase: 'storage' });
        case 'reconcile':
          return setState({ phase: 'reconcile' });
        case 'unresolved':
          return setState({ phase: 'unresolved' });
        case 'cancelled':
          return setState({ phase: 'idle' });
      }
    },
    [setState],
  );
  const run = useCallback(
    async (intent: FinancialIntent | null) => {
      const operation = controller.current;
      if (!operation) return null;
      const previous = state;
      if (previous.phase === 'sending' || previous.phase === 'confirmed') return null;
      setPaused(false);
      setState({ phase: 'sending' });
      try {
        const outcome = intent ? await operation.submit(intent) : await operation.verifyAndRetry();
        if (engine.getSnapshot().scope !== scope) return null;
        apply(outcome, previous);
        if (outcome.kind === 'confirmed') {
          await confirmedHandler.current?.((outcome.data as { data: unknown }).data);
          if (repeatable) setState({ phase: 'idle' });
        }
        return outcome;
      } catch (error) {
        // A request that fails structural/privacy validation is never sent.
        setState({
          phase: 'refused',
          failure: {
            kind: 'not-sent',
            messages: [error instanceof Error ? error.message : 'Pedido inválido.'],
            recovery: 'Nada foi enviado.',
            retryAfter: null,
          },
        });
        return null;
      }
    },
    [apply, engine, scope, state, setState, repeatable],
  );
  return {
    state,
    paused,
    /** True while nothing may be sent: in flight, confirmed, unknown or awaiting reconciliation. */
    blocked:
      snapshot.writesPaused ||
      [
        'checking',
        'sending',
        'confirmed',
        'uncertain',
        'reconcile',
        'unresolved',
        'storage',
      ].includes(state.phase),
    submit: (intent: FinancialIntent) => run(intent),
    /** Explicit verification of an unknown outcome: the identical bytes and key, once. */
    retry: () => run(null),
    /** Start a new operation after a confirmed one (the receipt stays with the caller). */
    reset: () => setState({ phase: 'idle' }),
    /**
     * Explicit discard after the person checked the outcome themselves (supported read or the
     * person's own confirmation). Never dispatches; refused while the record changed or in flight.
     */
    discard: async (outcome: 'confirmed' | 'not-executed') => {
      const operation = controller.current;
      if (!operation) return 'unresolved' as const;
      const result = await operation.discardAfterReconciliation(async () => outcome, true);
      if (result === 'discarded' || result === 'nothing-to-discard') setState({ phase: 'idle' });
      return result;
    },
  };
}
export type Intent = ReturnType<typeof useFinancialIntent>;

/**
 * Verbatim refusal, unknown outcome, replay-window expiry and changed-input blocks for one durable
 * intent. A definite refusal keeps the typed input; an unknown one keeps the exact request.
 */
export function IntentNotices({
  intent,
  testID,
  verify,
}: {
  intent: Intent;
  testID: string;
  /** Where the person can check the supported resource (list, statement, plan) themselves. */
  verify?: ReactNode;
}) {
  const [reconciled, setReconciled] = useState<'confirmed' | 'not-executed' | null>(null);
  const [checked, setChecked] = useState(false);
  const [discardState, setDiscardState] = useState<string | null>(null);
  const { state } = intent;
  const paused = intent.paused ? (
    <Notice kind="warning" title="Envios pausados até a sessão ser verificada. Nada foi enviado." />
  ) : null;
  if (state.phase === 'checking')
    return <Skeleton label="Conferindo operações guardadas neste dispositivo…" />;
  if (state.phase === 'storage')
    return (
      <Notice
        kind="danger"
        testID={`${testID}-storage`}
        title="Não foi possível ler a operação guardada neste dispositivo."
        body="Nada será enviado até a leitura funcionar. Tente novamente mais tarde."
      />
    );
  if (state.phase === 'refused')
    return (
      <>
        {paused}
        <Notice
          kind="danger"
          testID={`${testID}-refused`}
          title={
            state.failure.kind === 'not-sent'
              ? 'Não foi enviado.'
              : state.failure.kind === 'conflict'
                ? 'O servidor recusou: os dados mudaram.'
                : 'O servidor não aceitou a operação.'
          }
          body={`${state.failure.recovery || 'O que você digitou continua na tela.'}${state.failure.retryAfter ? ` Tente de novo depois de ${state.failure.retryAfter} s.` : ''}`}
          messages={state.failure.messages}
        />
      </>
    );
  if (state.phase === 'uncertain')
    return (
      <>
        {paused}
        <UncertainNotice
          testID={`${testID}-uncertain`}
          messages={state.failure?.messages ?? []}
          recovery={
            <>
              <Button
                label="Verificar e tentar novamente"
                testID={`${testID}-retry`}
                onPress={() => void intent.retry()}
              />
              {verify}
            </>
          }
        />
      </>
    );
  if (state.phase === 'unresolved' || state.phase === 'retained')
    return (
      <Notice
        kind="warning"
        testID={`${testID}-unresolved`}
        title={
          state.phase === 'unresolved'
            ? 'Há uma operação anterior sem confirmação para este formulário.'
            : 'Há uma operação recusada guardada para este formulário.'
        }
        body={
          state.phase === 'unresolved'
            ? 'Os dados mudaram desde o envio sem resposta. Verifique a operação anterior antes de enviar outra; nada novo foi enviado.'
            : 'Envie os dados corrigidos para substituí-la, ou verifique a anterior.'
        }
        actions={
          state.phase === 'unresolved' ? (
            <Button
              label="Verificar a operação anterior"
              testID={`${testID}-verify-previous`}
              onPress={() => void intent.retry()}
            />
          ) : undefined
        }
      />
    );
  if (state.phase === 'reconcile')
    return (
      <Notice
        kind="warning"
        testID={`${testID}-reconcile`}
        title="A operação anterior não pode mais ser verificada automaticamente."
        body="O prazo de repetição segura passou ou o horário do servidor não pôde ser confirmado. Confira no servidor se ela foi registrada; nada será reenviado."
        actions={
          <>
            {verify}
            <RadioGroup
              accessibilityLabel="O que você conferiu?"
              value={reconciled}
              onValueChange={(value) => setReconciled(value as 'confirmed' | 'not-executed')}
            >
              <Radio value="confirmed" label="Ela foi registrada" />
              <Radio value="not-executed" label="Ela não foi registrada" />
            </RadioGroup>
            <Checkbox
              label="Conferi no servidor e quero descartar a operação guardada"
              checked={checked}
              onCheckedChange={setChecked}
            />
            <Button
              label="Descartar operação guardada"
              variant="secondary"
              testID={`${testID}-discard`}
              disabled={!checked || !reconciled}
              onPress={() =>
                void intent.discard(reconciled!).then((result) => setDiscardState(result))
              }
            />
            {discardState && discardState !== 'discarded' ? (
              <Notice kind="warning" title="A operação guardada não pôde ser descartada agora." />
            ) : null}
          </>
        }
      />
    );
  return paused;
}

/** Why a read without server messages failed, in words (the server's own messages win). */
export function failureCause(error: unknown): string | undefined {
  if (!(error instanceof ApiError) || error.errorMessages.length) return undefined;
  switch (error.kind) {
    case 'network':
      return 'Sem conexão com o servidor.';
    case 'timeout':
      return 'O servidor demorou para responder.';
    case 'invalid-response':
      return 'O servidor respondeu de forma inesperada.';
    case 'invalid-request':
      return 'O pedido não pôde ser montado com estes dados.';
    case 'token-provider':
      return 'A sessão está sendo verificada.';
    case 'http':
      return `O servidor recusou (código ${error.status}).`;
    default:
      return undefined;
  }
}

/**
 * Statement/payment access (Manager/Admin or a Member's explicit Tab grant), decided from completed
 * authority reads only: while self-context is being re-checked the last decision stands, so an
 * in-flight operation's screen is not torn down and rebuilt mid-send. A completed check that
 * removes the grant still closes the screen. Null until the first decision.
 */
export function useStatementAccess(): boolean | null {
  const { snapshot } = useSession();
  const settled =
    snapshot.branch?.role !== 'Member' ||
    snapshot.selfStatus === 'ready' ||
    snapshot.selfStatus === 'failed';
  const decision = settled ? statementAllowed(snapshot) : null;
  const [last, setLast] = useState<boolean | null>(decision);
  if (decision !== null && decision !== last) setLast(decision);
  return decision ?? last;
}

/* Preview -------------------------------------------------------------------------------------- */

/**
 * A hypothetical server preview keyed by the immutable request signature: debounced, cancelled
 * when the signature changes, and never shown for another signature. It never blocks a write and
 * never saves anything; a failure shows no blocking state.
 */
export function usePreview<T>(
  kind: string,
  signature: string | null,
  send: (signal: AbortSignal) => Promise<T>,
  delayMs = 450,
) {
  const { snapshot } = useSession();
  const reads = useLedgerReads();
  const [settled, setSettled] = useState<string | null>(null);
  const latest = useRef(send);
  useEffect(() => {
    latest.current = send;
  }, [send]);
  useEffect(() => {
    if (signature === null) return;
    const timer = setTimeout(() => setSettled(signature), delayMs);
    return () => clearTimeout(timer);
  }, [signature, delayMs]);
  const query = useQuery({
    queryKey: ['ledger', snapshot.scope, 'preview', kind, settled],
    queryFn: ({ signal }) => latest.current(signal),
    enabled: reads && settled !== null && settled === signature,
    retry: false,
    staleTime: 0,
    gcTime: 0,
  });
  const current = settled !== null && settled === signature;
  // A cancelled request (scope pause, re-check, focus change) or one that waited on a session
  // re-verification is not a failed preview: ask again for the same signature once reads resume.
  const cancelled =
    current &&
    ((query.error instanceof ApiError &&
      (query.error.kind === 'aborted' || query.error.kind === 'token-provider')) ||
      (query.error instanceof Error && query.error.name === 'AbortError'));
  const { refetch, isFetching } = query;
  useEffect(() => {
    if (cancelled && reads && !isFetching) void refetch();
  }, [cancelled, reads, isFetching, refetch]);
  return {
    pending: signature !== null && (!current || query.isFetching || cancelled),
    data: current ? query.data : undefined,
    error: current && !cancelled ? query.error : null,
  };
}
