'use client';

import { useState } from 'react';
import {
  postTransaction,
  putSetting,
  type ApiRequestOptions,
  type RequestCreateTransactionJson,
} from '@loto/api';
import { useGetSetting } from '@loto/api/queries';
import { useBranchClock } from '@/client/clock/use-branch-clock';
import { useDeviceDraft } from '@/client/drafts/use-device-draft';
import { guardedWrite, versionOf, type GuardedWriteResult } from '@/client/mutations/guarded-write';
import type { OperationResult } from '@/client/mutations/operation-controller';
import { useIdempotentOperation } from '@/client/mutations/use-idempotent-operation';
import { useScopedApiClient } from '@/client/query/scoped-query-provider';

const sendTransaction = (
  payload: RequestCreateTransactionJson,
  key: string,
  options: ApiRequestOptions,
) => postTransaction(payload, { 'Idempotency-Key': key }, options);

export function WritesFixture() {
  return (
    <div>
      <GuardedSetting />
      <IdempotentCreate />
      <DeviceNotes />
    </div>
  );
}

function GuardedSetting() {
  const client = useScopedApiClient();
  const setting = useGetSetting();
  const [hours, setHours] = useState('8');
  const [result, setResult] = useState<GuardedWriteResult<unknown> | null>(null);
  const loaded = setting.data ? versionOf(setting.data, setting.data.data.version) : null;
  const [saved, setSaved] = useState<number | null>(null);
  const version = saved ?? loaded;

  async function save() {
    const outcome = await guardedWrite(version, (ifMatch) =>
      putSetting({ dailyTargetHours: hours }, { 'If-Match': ifMatch }, { client }),
    );
    if (outcome.kind === 'saved') setSaved(outcome.version);
    setResult(outcome.kind === 'saved' ? { ...outcome, result: null } : outcome);
  }

  return (
    <section>
      <h2>Configuração</h2>
      <p data-testid="setting-version">{version ?? '-'}</p>
      <label>
        Horas{' '}
        <input name="hours" value={hours} onChange={(event) => setHours(event.target.value)} />
      </label>
      <button type="button" onClick={() => void save()}>
        Salvar configuração
      </button>
      <button
        type="button"
        onClick={async () => {
          setSaved(null);
          await setting.refetch();
        }}
      >
        Recarregar e conferir
      </button>
      <pre data-testid="setting-result">{JSON.stringify(result)}</pre>
    </section>
  );
}

function IdempotentCreate() {
  const operation = useIdempotentOperation({
    context: 'e2e-transaction-create',
    operation: 'POST /transaction',
    send: sendTransaction,
  });
  const [result, setResult] = useState<unknown>(null);
  const [form, setForm] = useState({
    accountId: '',
    transactionTypeId: '',
    operatorId: '',
    value: '12.34',
    date: '',
  });
  const field = (name: keyof typeof form) => (
    <label>
      {name}{' '}
      <input
        name={name}
        value={form[name]}
        onChange={(event) => setForm({ ...form, [name]: event.target.value })}
      />
    </label>
  );
  const payload: RequestCreateTransactionJson = {
    date: form.date,
    value: form.value,
    transactionTypeId: form.transactionTypeId,
    accountId: form.accountId,
    recordedByOperatorId: form.operatorId || null,
    description: 'E2E idempotência',
  };
  const summarize = (value: OperationResult<unknown>) =>
    value.kind === 'confirmed'
      ? { kind: value.kind, id: (value.result as { data?: { id?: string } }).data?.id ?? null }
      : value;

  return (
    <section>
      <h2>Lançamento idempotente</h2>
      {field('accountId')}
      {field('transactionTypeId')}
      {field('operatorId')}
      {field('value')}
      {field('date')}
      <button
        type="button"
        disabled={!operation.ready}
        onClick={async () => setResult(summarize(await operation.submit(payload)))}
      >
        Registrar
      </button>
      <button
        type="button"
        onClick={async () => setResult(summarize(await operation.verifyAndRetry()))}
      >
        Verificar e tentar novamente
      </button>
      <p data-testid="operation-ready">{operation.ready ? 'pronto' : 'aguardando'}</p>
      <pre data-testid="operation-result">{JSON.stringify(result)}</pre>
      <pre data-testid="operation-record">
        {JSON.stringify(
          operation.record
            ? {
                status: operation.record.status,
                key: operation.record.key,
                dispatches: operation.record.dispatches,
              }
            : null,
        )}
      </pre>
    </section>
  );
}

function DeviceNotes() {
  const { state: clock } = useBranchClock();
  // Authority is rechecked by a fresh branch read in this session before input is restored.
  const draft = useDeviceDraft<{ notes: string }>('e2e-notes', {
    authorityVerified: clock.status === 'ready',
  });
  // Local edits win; until the first edit the restored device input is shown.
  const [edited, setEdited] = useState<string | null>(null);
  const restored =
    draft.state.status === 'loaded' && draft.state.result.status === 'found'
      ? draft.state.result.data.notes
      : '';
  const notes = edited ?? restored;
  return (
    <section>
      <h2>Observações</h2>
      <textarea
        name="notes"
        value={notes}
        onChange={(event) => {
          setEdited(event.target.value);
          draft.save({ notes: event.target.value });
        }}
      />
      <p data-testid="draft-state">
        {draft.state.status === 'loaded' ? draft.state.result.status : draft.state.status}
      </p>
      <button type="button" onClick={() => draft.clear()}>
        Descartar rascunho
      </button>
    </section>
  );
}
