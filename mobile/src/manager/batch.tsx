import { useMemo, useState } from 'react';
import { View } from 'react-native';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getSessionSessionIdReview,
  postSessionApproveDay,
  type ResponseManagerQueueCloseJson,
} from '@loto/api';
import { useSession, useScopedApiClient } from '@/session/provider';
import { spacing } from '@/theme/style';
import { AppText, Button, ConfirmDialog, FormField, Notice, Skeleton, TextField } from '@/ui';
import {
  CommandState,
  day,
  errorMessages,
  managerKey,
  plural,
  useDecisionCommand,
  useManagerReads,
} from './common';
import {
  batchBody,
  batchGroups,
  batchMode,
  sameBatchSet,
  toBatchCandidate,
  type BatchCandidate,
  type BatchGroup,
} from './contracts';

/**
 * One approve-day action per current cashbox/business date with several submitted sessions. The
 * selection is the exact fetched SessionId+Version list; the server approves all or nothing, and
 * the device never loops single approvals or fills in rows from an account/date lookup.
 */
export function BatchApproval({
  pending,
  date,
  onDone,
  onReceipt,
  open,
}: {
  pending: readonly ResponseManagerQueueCloseJson[];
  date: string;
  onDone: () => void;
  onReceipt: (message: string) => void;
  open: (path: string, row?: string) => void;
}) {
  const boxes = useMemo(() => {
    const map = new Map<string, ResponseManagerQueueCloseJson[]>();
    for (const row of pending) map.set(row.accountId, [...(map.get(row.accountId) ?? []), row]);
    return [...map.entries()].filter(([, rows]) => rows.length >= 2);
  }, [pending]);
  const [active, setActive] = useState<string | null>(null);
  if (boxes.length === 0) return null;
  return (
    <View style={{ gap: spacing.stack.sm }} testID="queue-batch">
      {boxes.map(([accountId, rows]) => (
        <View key={accountId} style={{ gap: spacing.stack.xs }}>
          <Button
            label={`Aprovar o dia — ${rows[0]!.accountName}`}
            variant="secondary"
            testID={`batch-open-${accountId}`}
            onPress={() => setActive(accountId)}
          />
          {active === accountId ? (
            <BatchDialog
              accountId={accountId}
              rows={rows}
              date={date}
              onClose={() => setActive(null)}
              onDone={onDone}
              onReceipt={onReceipt}
              open={open}
            />
          ) : null}
        </View>
      ))}
    </View>
  );
}

function BatchDialog({
  accountId,
  rows,
  date,
  onClose,
  onDone,
  onReceipt,
  open,
}: {
  accountId: string;
  rows: readonly ResponseManagerQueueCloseJson[];
  date: string;
  onClose: () => void;
  onDone: () => void;
  onReceipt: (message: string) => void;
  open: (path: string, row?: string) => void;
}) {
  const { snapshot, engine } = useSession();
  const client = useScopedApiClient();
  const reads = useManagerReads();
  const queries = useQueryClient();
  const command = useDecisionCommand();
  const [reason, setReason] = useState('');
  const [receipt, setReceipt] = useState<string | null>(null);
  const ids = rows.map((r) => r.dailyCloseId);
  // Fresh per-session reads: versions and server-offered actions come from the review, not the row.
  const reviews = useQuery({
    queryKey: managerKey(snapshot.scope, 'batch', accountId, date, ...ids),
    queryFn: async ({ signal }) =>
      Promise.all(ids.map((id) => getSessionSessionIdReview(id, { client, signal }))),
    select: (list) => list.map((r) => toBatchCandidate(r.data)),
    enabled: reads,
    retry: false,
    staleTime: 0,
    gcTime: 0,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
  // The reviewed set is frozen on first read: a later read can only be ACCEPTED, never swapped in.
  const fresh = reviews.data ?? null;
  const [reviewed, setReviewed] = useState<readonly BatchCandidate[] | null>(null);
  if (!reviewed && fresh) setReviewed(fresh);
  const changed = !!fresh && !!reviewed && !sameBatchSet(fresh, reviewed);
  const stale = changed || reviews.isError || reviews.isFetching;
  const candidates: readonly BatchCandidate[] = reviewed ?? [];
  const group: BatchGroup | null = batchGroups(candidates)[0] ?? null;
  const sameBox = batchGroups(candidates).length <= 1;
  const mode = sameBox ? batchMode(candidates) : 'none';
  const body = stale ? null : batchBody(candidates, mode, reason);
  const confirm = () => {
    if (!body) return;
    void command.run(
      JSON.stringify(['approve-day', body]),
      (key) => postSessionApproveDay(body, { 'Idempotency-Key': key }, { client: engine.client() }),
      async (result) => {
        const message = `${plural(result.data.sessions.length, 'sessão aprovada', 'sessões aprovadas')} no servidor.`;
        setReceipt(message);
        onReceipt(message);
        // A refresh failure after a confirmed batch never sends it again.
        await queries.invalidateQueries({ queryKey: ['manager', snapshot.scope] });
        onDone();
      },
    );
  };
  return (
    <ConfirmDialog
      visible
      testID="batch-dialog"
      title={`Aprovar o dia — ${group?.accountName ?? rows[0]!.accountName} · ${day(date)}`}
      consequence="Todas as sessões abaixo serão aprovadas juntas, ou nenhuma será aprovada."
      confirmLabel="Aprovar todas"
      busyLabel="Aprovando…"
      busy={command.pending}
      confirmDisabled={!body || !!receipt || command.blocked}
      confirmDisabledReason={
        receipt
          ? 'Já aprovado.'
          : changed
            ? 'As sessões mudaram. Revise a lista atualizada.'
            : reviews.isError || reviews.isFetching
              ? 'Aguarde a releitura das sessões.'
              : mode === 'none'
                ? 'O servidor não permite aprovar todas as sessões juntas.'
                : mode === 'single-manager' && !reason.trim()
                  ? 'Informe o motivo da aprovação como gestor único.'
                  : undefined
      }
      onConfirm={confirm}
      onDismiss={() => {
        if (!command.pending) onClose();
      }}
    >
      {reviews.isPending ? <Skeleton label="Conferindo as sessões…" /> : null}
      {changed ? (
        <Notice
          kind="warning"
          testID="batch-changed"
          title="As sessões mudaram desde a sua conferência."
          body="Nada foi aprovado. Revise a lista atualizada antes de confirmar."
          actions={
            <Button
              label="Revisar lista atualizada"
              testID="batch-accept-fresh"
              onPress={() => setReviewed(fresh)}
            />
          }
        />
      ) : null}
      {reviews.isError ? (
        <Notice
          kind="danger"
          testID="batch-read-error"
          title="Não foi possível ler as sessões deste caixa."
          messages={errorMessages(reviews.error)}
          actions={<Button label="Tentar novamente" onPress={() => void reviews.refetch()} />}
        />
      ) : null}
      {candidates.map((c) => (
        <View key={c.sessionId} testID={`batch-session-${c.sessionId}`}>
          <AppText variant="body-strong">{`Sessão ${c.sequence ?? '—'} · ${c.custodianName ?? 'sem responsável'}`}</AppText>
          <AppText variant="helper">
            {c.canApprove
              ? 'Pode ser aprovada de forma independente.'
              : c.canApproveAsSingleManager
                ? 'Só pode ser aprovada como gestor único.'
                : (c.blockedReason ?? 'Não pode ser aprovada agora.')}
          </AppText>
          <Button
            label="Abrir conferência"
            role="link"
            variant="link"
            onPress={() => open(`/gestao/conferencia/${c.sessionId}`, c.sessionId)}
          />
        </View>
      ))}
      {!sameBox ? (
        <Notice kind="warning" title="As sessões mudaram de caixa. Releia a fila." />
      ) : null}
      {mode === 'single-manager' ? (
        <FormField label="Motivo (gestor único)" required>
          <TextField
            kind="textarea"
            testID="batch-reason"
            value={reason}
            maxLength={500}
            onChangeText={setReason}
          />
        </FormField>
      ) : null}
      {receipt ? <Notice kind="success" testID="batch-receipt" title={receipt} /> : null}
      <CommandState
        command={command}
        receipt="Aprovação do dia concluída no servidor."
        testID="batch"
        reread={() => {
          void reviews.refetch().then(() => command.clear());
          onDone();
        }}
      />
    </ConfirmDialog>
  );
}
