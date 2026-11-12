import { useMemo, useState } from 'react';
import { View } from 'react-native';
import { useLocalSearchParams } from 'expo-router';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getSessionSessionIdHistory,
  getTransaction,
  getSessionSessionIdReview,
  postSessionSessionIdApprove,
  postSessionSessionIdReject,
  postSessionSessionIdReopen,
  toIfMatch,
  type ResponseDailyCloseReviewJson,
  type ResponseSessionCountAttemptJson,
  type ResponseListTransactionItemJson,
} from '@loto/api';
import {
  dailyCloseReviewEventTypeLabels,
  directionLabels,
  formatSignedBrlWire,
  sessionCloseFieldLabels,
  sessionCountAttemptKindLabels,
} from '@loto/core';
import { useSession, useScopedApiClient } from '@/session/provider';
import { useShell } from '@/shell/provider';
import { spacing } from '@/theme/style';
import {
  AppText,
  Badge,
  Button,
  Card,
  ConfirmDialog,
  Disclosure,
  FormField,
  Notice,
  Skeleton,
  TextField,
} from '@/ui';
import {
  CommandState,
  OnceState,
  day,
  errorMessages,
  instant,
  managerKey,
  money,
  useDecisionCommand,
  useManagerAccess,
  useManagerReads,
  useOnceCommand,
} from './common';
import {
  approvalChoice,
  approveBody,
  canReject,
  calendarPart,
  canReopen,
  explanationCheck,
  isId,
  offersSingleManager,
} from './contracts';
import { DiscrepancyPanel } from './custody';

const person = (user?: string | null, operator?: string | null) => operator ?? user ?? '—';

function Attempt({ a, submitted }: { a: ResponseSessionCountAttemptJson; submitted: boolean }) {
  return (
    <View testID={`attempt-${a.id}`} style={{ gap: spacing.stack.xs }}>
      <AppText variant="body-strong">
        {`Contagem ${a.attemptNumber} · ${sessionCountAttemptKindLabels[a.kind]}${submitted ? ' · enviada' : ''}`}
      </AppText>
      <AppText variant="helper">{`Conferida por ${a.conferredByName} em ${instant(a.conferredAt) ?? '—'}`}</AppText>
      <AppText variant="helper">
        {`Abertura ${money(a.totalOpening)} · Entradas ${money(a.totalIn)} · Saídas ${money(a.totalOut)} · Esperado ${money(a.expectedClosing)} · Contado ${money(a.totalClosing)} · Diferença ${formatSignedBrlWire(a.difference)}`}
      </AppText>
    </View>
  );
}

export function ReviewScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { snapshot, engine } = useSession();
  const client = useScopedApiClient();
  const shell = useShell();
  const access = useManagerAccess();
  const reads = useManagerReads() && isId(id);
  const queries = useQueryClient();
  const scope = snapshot.scope;
  const review = useQuery({
    queryKey: managerKey(scope, 'review', id),
    queryFn: ({ signal }) => getSessionSessionIdReview(id!, { client, signal }),
    select: (r) => r.data,
    enabled: reads,
    retry: false,
    staleTime: 0,
  });
  const history = useQuery({
    queryKey: managerKey(scope, 'history', id),
    queryFn: ({ signal }) => getSessionSessionIdHistory(id!, { client, signal }),
    select: (r) => r.data,
    enabled: reads,
    retry: false,
    staleTime: 0,
  });
  const r0 = review.data;
  const ledger = useQuery({
    queryKey: managerKey(scope, 'review-ledger', id),
    queryFn: async ({ signal }) =>
      (
        await getTransaction(
          {
            AccountId: r0!.accountId,
            DateFrom: calendarPart(r0!.date),
            DateTo: calendarPart(r0!.date),
            Page: 1,
            PageSize: 100,
          },
          { client, signal },
        )
      ).data,
    enabled: reads && !!r0,
    retry: false,
    staleTime: 0,
  });
  const ledgerAt = ledger.dataUpdatedAt;
  const [ledgerPaged, setLedgerPaged] = useState<{
    at: number;
    list: ResponseListTransactionItemJson[];
    page: number;
    error: unknown;
  } | null>(null);
  const live = ledgerPaged && ledgerPaged.at === ledgerAt ? ledgerPaged : null;
  const extraLedger = live?.list ?? [];
  const approve = useDecisionCommand();
  const reject = useOnceCommand();
  const reopen = useOnceCommand();
  const [receipt, setReceipt] = useState<string | null>(null);
  const [reason, setReason] = useState('');
  const [rejectReason, setRejectReason] = useState('');
  const [dialog, setDialog] = useState<'approve' | 'reject' | 'reopen' | null>(null);
  const r: ResponseDailyCloseReviewJson | undefined = review.data;
  const names = useMemo(
    () => new Map((r?.items ?? []).map((i) => [i.productId, i.productName] as const)),
    [r],
  );
  if (!isId(id)) return <Notice kind="danger" title="Fechamento não encontrado." />;
  if (!access) return <Notice kind="danger" title="Seu perfil não tem acesso a este destino." />;
  const refresh = async () => {
    await queries.invalidateQueries({ queryKey: ['manager', scope] });
    await Promise.all([review.refetch(), history.refetch()]);
  };
  const reread = (command: { clear: () => void }) => () => void refresh().then(command.clear);
  if (review.isPending) return <Skeleton label="Carregando a conferência…" />;
  if (!r)
    return (
      <Notice
        kind="danger"
        testID="review-error"
        title="Não foi possível carregar a conferência."
        messages={errorMessages(review.error)}
        actions={<Button label="Tentar novamente" onPress={() => void review.refetch()} />}
      />
    );
  // An unknown or unreconciled outcome of ANY decision blocks every other decision; so does a failed
  // re-read, because the data shown may no longer be what the server holds.
  const busy = approve.blocked || reject.blocked || reopen.blocked || !!receipt || review.isError;
  const choice = approvalChoice(r, reason);
  const submittedAttempt: ResponseSessionCountAttemptJson | null =
    history.data?.countAttempts?.find((a) => a.id === r.count.submittedAttemptId) ??
    r.count.confirmedAttempt ??
    null;
  // The explanation check needs a successful, current history read; a cached or fallback attempt is
  // never enough to clear it.
  const verified = !!history.data && !history.isError && !history.isFetching;
  const check = verified && submittedAttempt ? explanationCheck(r, submittedAttempt) : null;
  const official = r.items.find((i) => i.isCashVarianceProduct)?.closingValue;
  const loadMoreLedger = async () => {
    const page = (live?.page ?? 1) + 1;
    try {
      const next = (
        await getTransaction(
          {
            AccountId: r.accountId,
            DateFrom: calendarPart(r.date),
            DateTo: calendarPart(r.date),
            Page: page,
            PageSize: 100,
          },
          { client },
        )
      ).data;
      setLedgerPaged({
        at: ledgerAt,
        list: [...extraLedger, ...next.items],
        page,
        error: null,
      });
    } catch (error) {
      setLedgerPaged({ at: ledgerAt, list: extraLedger, page: live?.page ?? 1, error });
    }
  };
  const doApprove = () => {
    const body = approveBody(choice);
    if (!body) return;
    void approve.run(
      JSON.stringify(['approve', r.id, r.version, body]),
      (key) =>
        postSessionSessionIdApprove(
          r.id,
          body,
          { 'Idempotency-Key': key, 'If-Match': toIfMatch(r.version) },
          { client: engine.client() },
        ),
      async () => {
        setDialog(null);
        setReceipt('Fechamento aprovado no servidor.');
        await refresh();
      },
    );
  };
  const doReject = () => {
    if (!rejectReason.trim()) return;
    void reject.run(
      () =>
        postSessionSessionIdReject(
          r.id,
          { rejectionReason: rejectReason.trim() },
          { client: engine.client() },
        ),
      async () => {
        setDialog(null);
        setReceipt('Fechamento reprovado no servidor.');
        await refresh();
      },
    );
  };
  const doReopen = () =>
    void reopen.run(
      () => postSessionSessionIdReopen(r.id, { client: engine.client() }),
      async () => {
        setDialog(null);
        setReceipt('Fechamento reaberto para correção no servidor.');
        await refresh();
      },
    );
  const openDays = () => void shell.navigate(`/fechamentos/${r.id}`, true);
  const decided = !!receipt;
  return (
    <View style={{ gap: spacing.stack.md }} testID="review-screen">
      <AppText variant="body-strong" testID="review-context">
        {`${r.accountName} · ${day(r.date)}`}
      </AppText>
      <Badge kind="status" domain="daily-close-status" value={r.status} context="Situação" />
      {receipt ? <Notice kind="success" testID="review-receipt" title={receipt} /> : null}
      {review.isError ? (
        <Notice
          kind="warning"
          testID="review-error"
          title="Não foi possível reler a conferência."
          body="Os dados abaixo podem estar desatualizados; nenhuma decisão nova será enviada até a releitura."
          messages={errorMessages(review.error)}
          actions={<Button label="Tentar novamente" onPress={() => void review.refetch()} />}
        />
      ) : null}
      <Card title="Quem fez o quê" testID="review-people">
        <AppText testID="review-recorder">{`Contado por: ${person(r.recordedByUserName, r.recordedByOperatorName)}`}</AppText>
        <AppText testID="review-submitter">{`Enviado por: ${person(r.submittedByUserName, r.submittedByOperatorName)}${r.submittedAt ? ` em ${instant(r.submittedAt)}` : ''}`}</AppText>
        <AppText testID="review-custodian">{`Responsável pelo caixa: ${r.custody.custodianOperatorName ?? '—'}${r.custody.sequence ? ` · sessão ${r.custody.sequence}` : ''}`}</AppText>
        {r.approvedAt ? (
          <AppText testID="review-approver">{`Aprovado por ${r.approvedByUserName ?? '—'} em ${instant(r.approvedAt)}`}</AppText>
        ) : null}
      </Card>
      {submittedAttempt ? (
        <Card title="Comparação (valores do servidor)" testID="review-comparison">
          <AppText variant="helper" testID="review-official-difference">
            {`Diferença oficial: ${money(official)}`}
          </AppText>
          <Attempt a={submittedAttempt} submitted />
          {!verified ? (
            <Notice
              kind="warning"
              testID="review-explanation-unchecked"
              title="Não foi possível conferir a diferença."
              messages={history.isError ? errorMessages(history.error) : []}
              actions={<Button label="Tentar novamente" onPress={() => void history.refetch()} />}
            />
          ) : null}
          {check && (check.omitted.length > 0 || check.differs) ? (
            <Notice
              kind="warning"
              testID="review-explanation-differs"
              title="A conta exibida não confere com a diferença do servidor."
              body="Confira a abertura e o fechamento anterior com a gerência antes de enviar ou aprovar."
              messages={check.omitted.map(
                (o) => `Abertura não exibida: ${o.productName} ${money(o.openingValue)}`,
              )}
            />
          ) : null}
        </Card>
      ) : history.isError ? (
        <Notice
          kind="warning"
          testID="review-explanation-unchecked"
          title="Não foi possível conferir a diferença."
          messages={errorMessages(history.error)}
          actions={<Button label="Tentar novamente" onPress={() => void history.refetch()} />}
        />
      ) : (
        <Notice kind="info" title="Ainda não há contagem conferida para comparar." />
      )}
      {/* Decide first: the discrepancy to triage and the decision controls come before the long detail. */}
      {r.openingDiscrepancy ? (
        <DiscrepancyPanel
          discrepancy={r.openingDiscrepancy}
          productNames={names}
          onChanged={() => void refresh()}
        />
      ) : null}
      {r.status === 'Submitted' &&
      r.allowedActions.approvalBlockedReason &&
      !r.allowedActions.canApprove ? (
        <Notice
          kind="warning"
          testID="review-blocked"
          title="A aprovação não está disponível."
          body={r.allowedActions.approvalBlockedReason}
        />
      ) : null}
      {offersSingleManager(r) && r.status === 'Submitted' ? (
        <FormField label="Motivo da aprovação como gestor único" required>
          <TextField
            kind="textarea"
            testID="approve-reason"
            value={reason}
            maxLength={500}
            onChangeText={setReason}
            disabled={busy}
          />
        </FormField>
      ) : null}
      <CommandState
        command={approve}
        receipt="Fechamento aprovado no servidor."
        testID="approve"
        reread={reread(approve)}
      />
      <OnceState
        command={reject}
        receipt="Fechamento reprovado."
        testID="reject"
        reread={reread(reject)}
      />
      <OnceState
        command={reopen}
        receipt="Fechamento reaberto."
        testID="reopen"
        reread={reread(reopen)}
      />
      <View style={{ gap: spacing.stack.sm }}>
        {r.status === 'Submitted' ? (
          <Button
            label={
              choice.kind === 'single-manager' || offersSingleManager(r)
                ? 'Aprovar como gestor único'
                : 'Aprovar'
            }
            testID="review-approve"
            disabled={
              busy || decided || (choice.kind !== 'independent' && choice.kind !== 'single-manager')
            }
            disabledReason={
              decided
                ? 'Decisão já registrada.'
                : choice.kind === 'blocked'
                  ? (choice.reason ?? 'Informe o motivo para aprovar.')
                  : undefined
            }
            onPress={() => setDialog('approve')}
          />
        ) : null}
        {canReject(r) ? (
          <Button
            label="Reprovar"
            variant="secondary"
            testID="review-reject"
            disabled={busy || decided}
            onPress={() => setDialog('reject')}
          />
        ) : null}
        {canReopen(r) ? (
          <Button
            label="Reabrir para correção"
            variant="secondary"
            testID="review-reopen"
            disabled={busy || decided}
            onPress={() => setDialog('reopen')}
          />
        ) : null}
        {r.allowedActions.canRecall || r.allowedActions.canSaveCount ? (
          <Button
            label="Gerenciar fechamento"
            role="link"
            variant="secondary"
            testID="review-manage"
            onPress={openDays}
          />
        ) : null}
        <Button
          label="Voltar para a fila de trabalho"
          role="link"
          variant="link"
          testID="review-back"
          onPress={() => void shell.back()}
        />
        <Button
          label="Reler"
          variant="link"
          testID="review-refresh"
          onPress={() => void refresh()}
        />
      </View>
      <Card title="Produtos" testID="review-items">
        {r.items.map((i) => (
          <View key={i.productId} testID={`review-item-${i.productId}`}>
            <AppText variant="body-strong">{i.productName}</AppText>
            <AppText variant="helper">
              {i.isCashVarianceProduct
                ? `Diferença oficial do servidor (calculada pelo sistema): ${money(i.closingValue)}`
                : `Abertura (calculada pelo sistema) ${money(i.openingValue)} → Fechamento (informado pelo operador) ${money(i.closingValue)}`}
            </AppText>
          </View>
        ))}
        {r.closeFields.map((f) => (
          <AppText
            key={f.field}
            variant="helper"
          >{`${sessionCloseFieldLabels[f.field]}: ${money(f.value)}`}</AppText>
        ))}
      </Card>
      <Card title="Transações do dia" testID="review-ledger">
        {ledger.isPending ? <Skeleton label="Lendo as transações do dia…" /> : null}
        {ledger.isError ? (
          <Notice
            kind="warning"
            testID="review-ledger-error"
            title="Não foi possível ler as transações do dia."
            messages={errorMessages(ledger.error)}
            actions={<Button label="Tentar novamente" onPress={() => void ledger.refetch()} />}
          />
        ) : null}
        {ledger.data && ledger.data.items.length === 0 ? (
          <AppText variant="helper">Nenhuma transação neste caixa e dia.</AppText>
        ) : null}
        {[...(ledger.data?.items ?? []), ...extraLedger].map((t) => (
          <View key={t.id} testID={`review-ledger-${t.id}`} style={{ gap: spacing.stack.xs }}>
            <AppText variant="body-strong">{t.description ?? directionLabels[t.direction]}</AppText>
            <AppText variant="helper">{`${directionLabels[t.direction]} ${money(t.value)}`}</AppText>
            {t.generationKind ? <Badge kind="automatic" /> : null}
          </View>
        ))}
        {ledger.data && ledger.data.totalCount > ledger.data.items.length + extraLedger.length ? (
          <>
            <AppText variant="helper" testID="review-ledger-more">
              {`Mostrando ${ledger.data.items.length + extraLedger.length} de ${ledger.data.totalCount} transações.`}
            </AppText>
            <Button
              label="Carregar mais transações"
              variant="secondary"
              testID="review-ledger-load-more"
              onPress={() => void loadMoreLedger()}
            />
          </>
        ) : null}
        {live?.error ? (
          <Notice
            kind="warning"
            title="Não foi possível ler mais transações."
            messages={errorMessages(live.error)}
          />
        ) : null}
      </Card>
      {r.notes ? (
        <Card title="Observações">
          <AppText testID="review-notes">{r.notes}</AppText>
        </Card>
      ) : null}
      {r.rejectionReason ? (
        <Notice
          kind="warning"
          testID="review-rejection"
          title="Motivo da reprovação"
          body={r.rejectionReason}
        />
      ) : null}
      <Disclosure label="Histórico" testID="review-history" loading={history.isPending}>
        {history.isError ? (
          <Notice
            kind="danger"
            title="Não foi possível carregar o histórico."
            messages={errorMessages(history.error)}
            actions={<Button label="Tentar novamente" onPress={() => void history.refetch()} />}
          />
        ) : null}
        {history.data?.events.map((e) => (
          <AppText key={e.id} variant="helper">
            {`${instant(e.occurredAt)} · ${dailyCloseReviewEventTypeLabels[e.type]} · ${person(e.actorUserName, e.actorOperatorName)}${e.rejectionReason ? ` · ${e.rejectionReason}` : ''}`}
          </AppText>
        ))}
        {(history.data?.countAttempts ?? []).map((a) => (
          <Attempt key={a.id} a={a} submitted={a.id === r.count.submittedAttemptId} />
        ))}
      </Disclosure>
      <ConfirmDialog
        visible={dialog === 'approve'}
        testID="approve-dialog"
        title="Aprovar fechamento"
        consequence="A aprovação não transfere a custódia do caixa nem paga funcionário."
        confirmLabel="Aprovar"
        busyLabel="Aprovando…"
        busy={approve.pending}
        confirmDisabled={busy && !approve.pending}
        onConfirm={doApprove}
        onDismiss={() => setDialog(null)}
      />
      <ConfirmDialog
        visible={dialog === 'reject'}
        testID="reject-dialog"
        title="Reprovar fechamento"
        consequence="O fechamento volta ao operador para correção."
        confirmLabel="Reprovar"
        busyLabel="Reprovando…"
        destructive
        busy={reject.pending}
        confirmDisabled={!rejectReason.trim() || (busy && !reject.pending)}
        confirmDisabledReason="Informe o motivo."
        onConfirm={doReject}
        onDismiss={() => setDialog(null)}
      >
        <FormField label="Motivo da reprovação" required>
          <TextField
            kind="textarea"
            testID="reject-reason"
            value={rejectReason}
            maxLength={500}
            onChangeText={setRejectReason}
          />
        </FormField>
      </ConfirmDialog>
      <ConfirmDialog
        visible={dialog === 'reopen'}
        testID="reopen-dialog"
        title="Reabrir para correção"
        consequence="O fechamento aprovado volta a ser editável."
        confirmLabel="Reabrir"
        busyLabel="Reabrindo…"
        busy={reopen.pending}
        confirmDisabled={busy && !reopen.pending}
        onConfirm={doReopen}
        onDismiss={() => setDialog(null)}
      />
    </View>
  );
}
