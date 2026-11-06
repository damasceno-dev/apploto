import { useEffect, useMemo, useState } from 'react';
import { View } from 'react-native';
import { useLocalSearchParams } from 'expo-router';
import { useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query';
import {
  getOperator,
  getProduct,
  getSessionSessionIdHistory,
  getTransaction,
  postSessionHandoverHandoverIdWithdraw,
  postSessionSessionIdConferir,
  postSessionSessionIdFinish,
  postSessionSessionIdHandover,
  postSessionSessionIdRecall,
  postSessionSessionIdRecount,
  postSessionSessionIdSubmit,
  toIfMatch,
  type ResponseDailyCloseHistoryJson,
  type ResponseDailyCloseReviewJson,
  type ResponseSessionCountAttemptJson,
  type ResponseSessionHandoverJson,
  type SessionCloseField,
} from '@loto/api';
import {
  dailyCloseReviewEventTypeLabels,
  directionLabels,
  formatBrlWire,
  formatSignedBrlWire,
  openingDiscrepancyAttributionLabels,
  sessionCloseFieldLabels,
  sessionCountAttemptKindLabels,
  sessionCustodyStateLabels,
  sessionHandoverStateLabels,
} from '@loto/core';
import { useSession, useScopedApiClient } from '@/session/provider';
import { useShell } from '@/shell/provider';
import { spacing } from '@/theme/style';
import {
  AppText,
  Badge,
  Button,
  Card,
  Checkbox,
  DetailList,
  Disclosure,
  FormField,
  Notice,
  Select,
  Skeleton,
  SummaryStrip,
  TextField,
  UncertainNotice,
  announce,
  saveStatusText,
  useToast,
} from '@/ui';
import { RetainedNotice } from '@/admin/common';
import { CountMoneyField } from './arithmetic';
import {
  CLOSE_FIELDS,
  calendarPart,
  sameBusinessDay,
  correctionContext,
  digitsFromWire,
  endUndo,
  omittedOpeningProducts,
  pluralDrafts,
  repeatOpening,
  signedField,
  undoRepeat,
  wireFromDigits,
  type CountInput,
  type RepeatUndo,
} from './contracts';
import { readReview, useCountForm, type CountForm } from './count';
import {
  CommandState,
  auditInstant,
  businessDate,
  errorMessages,
  useDayCommand,
  useDayReads,
} from './common';

/** Server figures (totals, expected closing) may be negative: never feed them to the unsigned formatter. */
const serverMoney = (value: number) =>
  value < 0 ? formatSignedBrlWire(value) : formatBrlWire(value);

const fieldHelp: Record<SessionCloseField, string> = {
  TerminalReport: 'Valor do relatório do terminal CAIXA; pode ser negativo.',
  BolaoFee: 'Nunca negativo. Zero cancela o lançamento automático.',
  SpecialFee: 'Nunca negativo. Zero cancela o lançamento automático.',
};

/**
 * Close day and Fix & resubmit (`/fechamentos/:id`, `/fechamentos/:id/correcao`): the exact session
 * is the resource — never account + date. A failed read is a read failure, never "no session".
 */
export function CloseScreen({ correction }: { correction: boolean }) {
  const params = useLocalSearchParams<{ id?: string }>();
  const sessionId = typeof params.id === 'string' ? params.id : '';
  const { engine, snapshot } = useSession();
  const reads = useDayReads();
  const review = useQuery({
    queryKey: ['day', snapshot.scope, 'close-review', sessionId],
    queryFn: () => readReview(engine, sessionId),
    enabled: reads && !!sessionId,
    retry: false,
    staleTime: Infinity,
    gcTime: 0,
  });
  if (review.status === 'pending') return <Skeleton label="Carregando o fechamento…" />;
  if (review.status === 'error')
    return (
      <Notice
        kind="danger"
        testID="close-read-error"
        title="Não foi possível carregar este fechamento."
        messages={errorMessages(review.error)}
        actions={<Button label="Tentar novamente" onPress={() => void review.refetch()} />}
      />
    );
  return <CloseForm key={review.data.id} review={review.data} correction={correction} />;
}

function CloseForm({
  review,
  correction,
}: {
  review: ResponseDailyCloseReviewJson;
  correction: boolean;
}) {
  const { snapshot, engine } = useSession();
  const shell = useShell();
  const client = useScopedApiClient();
  const reads = useDayReads();
  const toast = useToast();
  const queries = useQueryClient();
  // Gap AA: a stored row whose product the active list no longer has locks the whole count.
  const products = useQuery({
    queryKey: ['day', snapshot.scope, 'close-products'],
    queryFn: async () => (await getProduct({ client })).data.items.map((p) => p.id),
    enabled: reads,
    retry: false,
    staleTime: 0,
  });
  const form = useCountForm(
    review.id,
    review,
    products.status === 'success' ? products.data : null,
  );
  const session = form.current;
  const [undo, setUndo] = useState<RepeatUndo>({});
  /** The exact revisions the operator confirmed; a new revision is never pre-confirmed. */
  const [stillObservedFor, setStillObservedFor] = useState<string | null>(null);
  const conferir = useDayCommand();
  const recount = useDayCommand();
  const submit = useDayCommand();
  const recall = useDayCommand();
  const history = useQuery({
    queryKey: ['day', snapshot.scope, 'close-history', session.id, session.version],
    queryFn: async () => (await getSessionSessionIdHistory(session.id, { client })).data,
    enabled: reads,
    retry: false,
    staleTime: 0,
  });
  const ledgerFilter = {
    AccountId: session.accountId,
    DateFrom: calendarPart(session.date),
    DateTo: calendarPart(session.date),
  };
  const ledger = useQuery({
    queryKey: ['day', snapshot.scope, 'close-ledger', session.id, session.custody.ledgerRevision],
    queryFn: async () =>
      (await getTransaction({ ...ledgerFilter, Page: 1, PageSize: 20 }, { client })).data,
    enabled: reads,
    retry: false,
    staleTime: 0,
  });
  const drafts = useQuery({
    queryKey: ['day', snapshot.scope, 'close-drafts', session.id, session.custody.ledgerRevision],
    queryFn: async () =>
      (await getTransaction({ ...ledgerFilter, Status: 'Draft', Page: 1, PageSize: 1 }, { client }))
        .data.totalCount,
    enabled: reads,
    retry: false,
    staleTime: 0,
  });
  const count = session.count;
  const presented = JSON.stringify([
    session.id,
    count.confirmationToken,
    count.countRevision,
    count.ledgerRevision,
    count.openingRevision,
  ]);
  const stillObserved = stillObservedFor === presented;
  const setStillObserved = (checked: boolean) => setStillObservedFor(checked ? presented : null);
  const allowed = session.allowedActions;
  const isCorrection = correctionContext(session);
  const readOnly = !allowed.canSaveCount || form.lock !== null;
  const changeItem = (productId: string, digits: string) => {
    setUndo((u) => endUndo(u, productId));
    form.setInput((prev) => ({ ...prev, items: { ...prev.items, [productId]: digits } }));
  };
  const setBuffer = (key: string, raw: string | undefined) => {
    form.setInput((prev) => {
      const buffers = { ...prev.buffers };
      if (raw === undefined) delete buffers[key];
      else buffers[key] = raw;
      return { ...prev, buffers };
    }, false);
  };
  const refreshAll = async () => {
    void products.refetch();
    const fresh = await form.reload();
    void history.refetch();
    void ledger.refetch();
    void drafts.refetch();
    return fresh;
  };
  const busy =
    form.saving || conferir.pending || recount.pending || submit.pending || recall.pending;
  const omitted = omittedOpeningProducts(count.confirmedAttempt, session.items);
  const today = snapshot.clock!.branchLocalDate;
  const mineCustody = session.custody.custodianOperatorId === snapshot.selfContext?.operatorId;
  const goBack = () => void shell.back();
  useEffect(() => {
    if (count.candidateState === 'Current') announce('Resultado da conferência disponível.');
  }, [count.candidateState]);

  const conferirBody = {
    confirmationToken: count.confirmationToken,
    countRevision: count.countRevision,
    ledgerRevision: count.ledgerRevision,
    openingRevision: count.openingRevision,
    confirmCountStillObserved: count.candidateState === 'Unconfirmed' && stillObserved,
  };
  const conferirFingerprint = JSON.stringify([session.id, form.version(), conferirBody]);
  const canConferir =
    allowed.canConferir &&
    !form.dirty &&
    !form.openCalculations &&
    !form.saving &&
    !busy &&
    !(
      count.candidateState === 'Unconfirmed' &&
      count.requiresStillObservedConfirmation &&
      !stillObserved
    );
  const doConferir = () => {
    // The intent is immutable: a retry resends this exact body, key and If-Match.
    const ifMatch = toIfMatch(form.version());
    void conferir.run(
      conferirFingerprint,
      (key) =>
        postSessionSessionIdConferir(
          session.id,
          conferirBody,
          { 'Idempotency-Key': key, 'If-Match': ifMatch },
          { client: engine.client() },
        ),
      (result) => {
        form.adopt(result.data.session);
        setStillObservedFor(null);
        void history.refetch();
      },
    );
  };
  const doRecount = () => {
    const previousAttemptId = count.latestAttemptId;
    if (!previousAttemptId) return;
    const ifMatch = toIfMatch(form.version());
    void recount.run(
      JSON.stringify(['recount', session.id, form.version(), previousAttemptId]),
      (key) =>
        postSessionSessionIdRecount(
          session.id,
          { previousAttemptId },
          { 'Idempotency-Key': key, 'If-Match': ifMatch },
          { client: engine.client() },
        ),
      (result) => {
        form.adopt(result.data);
        void history.refetch();
      },
    );
  };
  const attempt = count.candidateState === 'Current' ? count.confirmedAttempt : null;
  const doSubmit = () => {
    if (!attempt || form.dirty || form.saving || form.openCalculations) return;
    const ifMatch = toIfMatch(form.version());
    const body = {
      attemptId: attempt.id,
      countRevision: count.countRevision,
      ledgerRevision: count.ledgerRevision,
      openingRevision: count.openingRevision,
    };
    void submit.run(
      JSON.stringify(['submit', session.id, form.version(), body]),
      () =>
        postSessionSessionIdSubmit(
          session.id,
          body,
          { 'If-Match': ifMatch },
          { client: engine.client() },
        ),
      async (result) => {
        form.adopt(result.data);
        setUndo({});
        toast.show('Fechamento enviado.');
        await form.clearDraft();
        await queries.invalidateQueries({ queryKey: ['day', snapshot.scope] });
        await refreshAll();
      },
    );
  };
  return (
    <View style={{ gap: spacing.stack.md }} testID="close-day">
      <AppText testID="close-context">{`${session.accountName} · ${businessDate(session.date)} · ${shell.mode === 'management' ? 'Gestão' : 'Meu turno'}`}</AppText>
      <Badge kind="status" domain="daily-close-status" value={session.status} context="Situação" />
      {!sameBusinessDay(session.date, today) && session.status === 'Draft' && !isCorrection ? (
        <Notice
          kind="info"
          title={`Fechamento de ${businessDate(session.date)}.`}
          body="É um dia anterior: as alterações só vão ao servidor quando você tocar em Salvar."
        />
      ) : null}
      {isCorrection && !correction ? (
        <Notice
          kind="warning"
          testID="close-correction-link"
          title="Este fechamento precisa de correção."
          actions={
            <Button
              label="Ver por que precisa de revisão"
              variant="secondary"
              onPress={() =>
                void shell.replacePreservingOrigin(`/fechamentos/${session.id}/correcao`)
              }
            />
          }
        />
      ) : null}
      {correction ? <ReviewCauses session={session} history={history} /> : null}
      {form.successor ? (
        <Notice
          kind="info"
          testID="close-successor"
          title={`O fechamento de ${businessDate(form.successor.date)} voltou para rascunho porque o fechamento anterior foi corrigido.`}
          onDismiss={form.dismissSuccessor}
        />
      ) : null}
      <RetainedNotice
        visible={!!form.draft.retained}
        onRestore={() => {
          const retained = form.draft.retained!.input;
          form.draft.dismissRetained();
          if (!form.restore(retained))
            toast.show('Valores do aparelho restaurados para revisão. Nada foi salvo no servidor.');
        }}
      />
      {form.draft.problem ? <Notice kind="warning" title={form.draft.problem} /> : null}
      {form.lock === 'refused' || form.lock === 'retired' ? (
        <Notice
          kind="warning"
          testID="close-unsupported"
          title={
            form.lock === 'refused'
              ? 'O servidor recusou salvar esta contagem.'
              : 'Esta contagem tem um valor de um produto desativado.'
          }
          body="Enquanto isso não for decidido, a contagem fica bloqueada para edição e salvamento: o aplicativo não remove nem reenvia esse valor. Seus valores continuam neste aparelho; peça à gerência para revisar o fechamento."
          messages={form.lock === 'refused' ? form.failure?.messages : undefined}
        />
      ) : form.lock === 'unverified' && products.status === 'error' ? (
        <Notice
          kind="danger"
          testID="close-products-unknown"
          title="Não foi possível verificar os produtos desta contagem."
          body="A contagem fica bloqueada para edição e salvamento até essa leitura funcionar."
          messages={errorMessages(products.error)}
          actions={<Button label="Tentar novamente" onPress={() => void products.refetch()} />}
        />
      ) : null}
      {form.reconciliation ? <Reconcile form={form} /> : null}
      <Identities session={session} />
      {session.openingDiscrepancy ? (
        <Notice
          kind="info"
          testID="close-opening-discrepancy"
          title={`Diferença na abertura: ${formatSignedBrlWire(session.openingDiscrepancy.currentTotalDelta)}`}
          body={`Atribuição: ${openingDiscrepancyAttributionLabels[session.openingDiscrepancy.attribution]}. Decidida pela gerência.`}
        />
      ) : null}
      <Conference
        count={count}
        stillObserved={stillObserved}
        setStillObserved={setStillObserved}
        omitted={omitted}
      />
      <Card title="Contagem por produto" testID="close-count">
        {session.items.map((row) => {
          if (row.isCashVarianceProduct)
            return (
              <View
                key={row.productId}
                testID={`close-variance-${row.productId}`}
                style={{ gap: spacing.stack.xs }}
              >
                <AppText variant="body-strong">{row.productName}</AppText>
                <AppText>
                  {session.status === 'Draft' ||
                  session.status === 'Rejected' ||
                  row.closingValue == null
                    ? 'Em branco até o envio.'
                    : `Diferença oficial: ${formatSignedBrlWire(row.closingValue)}`}
                </AppText>
              </View>
            );
          const opening = digitsFromWire(row.openingValue ?? 0);
          const value = form.input.items[row.productId] ?? '';
          return (
            <View
              key={row.productId}
              testID={`close-row-${row.productId}`}
              style={{ gap: spacing.stack.xs }}
            >
              <AppText variant="body-strong">{row.productName}</AppText>
              <AppText>{`Abertura ${serverMoney(row.openingValue ?? 0)} · calculado pelo sistema`}</AppText>
              <FormField label={`Contagem de ${row.productName}`} readOnly={readOnly}>
                <CountMoneyField
                  label={`Contagem de ${row.productName}`}
                  testID={`close-input-${row.productId}`}
                  value={value}
                  readOnly={readOnly}
                  disabled={busy && !form.saving}
                  onValueChange={(digits) => changeItem(row.productId, digits)}
                  buffer={form.input.buffers[`item:${row.productId}`]}
                  onBufferChange={(raw) => setBuffer(`item:${row.productId}`, raw)}
                />
              </FormField>
              {!readOnly ? (
                row.productId in undo ? (
                  <Button
                    label="Desfazer repetição"
                    variant="link"
                    testID={`close-undo-${row.productId}`}
                    accessibilityLabel={`Desfazer repetição de ${row.productName}`}
                    onPress={() => {
                      const next = undoRepeat(form.input.items, undo, row.productId);
                      setUndo(next.undo);
                      form.setInput({ ...form.input, items: next.items });
                    }}
                  />
                ) : (
                  <Button
                    label="Repetir abertura"
                    variant="link"
                    testID={`close-repeat-${row.productId}`}
                    accessibilityLabel={`Repetir abertura de ${row.productName}`}
                    onPress={() => {
                      const next = repeatOpening(form.input.items, undo, row.productId, opening);
                      setUndo(next.undo);
                      if (next.items !== form.input.items)
                        form.setInput({ ...form.input, items: next.items });
                    }}
                  />
                )
              ) : null}
            </View>
          );
        })}
      </Card>
      <Card title="Campos do fechamento" testID="close-fields">
        {CLOSE_FIELDS.map((field) => {
          const entry = form.input.fields[field];
          const server = session.closeFields.find((f) => f.field === field);
          const update = (patch: Partial<CountInput['fields'][typeof field]>) =>
            form.setInput((prev) => ({
              ...prev,
              fields: { ...prev.fields, [field]: { ...prev.fields[field], ...patch } },
            }));
          return (
            <FormField
              key={field}
              label={sessionCloseFieldLabels[field]}
              helper={fieldHelp[field]}
              readOnly={readOnly}
            >
              <CountMoneyField
                label={sessionCloseFieldLabels[field]}
                testID={`close-field-${field}`}
                value={entry.digits}
                placeholderZero
                readOnly={readOnly}
                onValueChange={(digits) => update({ digits })}
                sign={
                  signedField(field)
                    ? {
                        negative: entry.negative,
                        onNegativeChange: (negative) => update({ negative }),
                      }
                    : undefined
                }
                buffer={form.input.buffers[`field:${field}`]}
                onBufferChange={(raw) => setBuffer(`field:${field}`, raw)}
              />
              {server?.transactionId && server.value !== 0 ? <Badge kind="automatic" /> : null}
            </FormField>
          );
        })}
      </Card>
      <FormField label="Observações" optional readOnly={readOnly}>
        <TextField
          kind="textarea"
          testID="close-notes"
          value={form.input.notes}
          maxLength={1000}
          readOnly={readOnly}
          onChangeText={(notes) => form.setInput((prev) => ({ ...prev, notes }))}
        />
      </FormField>
      <SaveBar form={form} />
      {Object.keys(form.input.buffers).length ? (
        <AppText variant="helper" testID="close-open-calculations">
          Use ou descarte as contas abertas: só o resultado aceito é salvo.
        </AppText>
      ) : null}
      <CommandState
        command={conferir}
        receipt="Conferência registrada no servidor."
        testID="close-conferir"
        reread={() =>
          void refreshAll()
            .then(() => conferir.clear())
            .catch(() => undefined)
        }
      />
      <CommandState
        command={recount}
        receipt="Recontagem iniciada no servidor."
        testID="close-recount"
        reread={() =>
          void refreshAll()
            .then(() => recount.clear())
            .catch(() => undefined)
        }
      />
      <CommandState
        command={submit}
        receipt="Fechamento enviado no servidor."
        testID="close-submit"
        reread={() =>
          void refreshAll()
            .then(() => submit.clear())
            .catch(() => undefined)
        }
      />
      {allowed.canConferir || allowed.canRecount || allowed.canSubmit ? (
        <View style={{ gap: spacing.stack.sm }}>
          {attempt && allowed.canSubmit ? (
            <Button
              label={isCorrection ? 'Reenviar para aprovação' : 'Enviar para aprovação'}
              variant={shell.mode === 'shift' ? 'cta' : 'primary'}
              testID="close-submit-button"
              loading={submit.pending}
              loadingLabel="Enviando…"
              disabled={submit.blocked || busy || form.dirty || form.openCalculations}
              disabledReason={
                form.openCalculations
                  ? 'Use ou descarte o cálculo aberto antes de enviar.'
                  : form.dirty
                    ? 'Salve as alterações e confira antes de enviar.'
                    : undefined
              }
              onPress={doSubmit}
              fullWidth
            />
          ) : allowed.canConferir ? (
            <Button
              label="Conferir"
              variant={shell.mode === 'shift' ? 'cta' : 'primary'}
              testID="close-conferir-button"
              loading={conferir.pending}
              loadingLabel="Conferindo…"
              disabled={!canConferir || conferir.blocked}
              disabledReason={
                form.openCalculations
                  ? 'Use ou descarte o cálculo aberto antes de conferir.'
                  : form.dirty
                    ? 'Salve a contagem antes de conferir.'
                    : count.candidateState === 'Unconfirmed' &&
                        count.requiresStillObservedConfirmation &&
                        !stillObserved
                      ? 'Confirme que a contagem ainda representa o caixa, ou reconte.'
                      : undefined
              }
              onPress={doConferir}
              fullWidth
            />
          ) : null}
          {allowed.canRecount && count.latestAttemptId ? (
            <Button
              label="Recontar"
              variant="secondary"
              testID="close-recount-button"
              loading={recount.pending}
              loadingLabel="Iniciando recontagem…"
              disabled={recount.blocked || busy}
              onPress={doRecount}
              fullWidth
            />
          ) : null}
        </View>
      ) : null}
      <Blockers
        session={session}
        drafts={drafts}
        goDrafts={(path) => void shell.navigate(path, true)}
      />
      {allowed.canRecall ? (
        <>
          <CommandState
            command={recall}
            receipt="Envio desfeito no servidor."
            testID="close-recall"
            reread={() =>
              void refreshAll()
                .then(() => recall.clear())
                .catch(() => undefined)
            }
          />
          <Button
            label="Desfazer envio"
            variant="secondary"
            testID="close-recall-button"
            loading={recall.pending}
            loadingLabel="Desfazendo…"
            disabled={recall.blocked || busy}
            onPress={() =>
              void recall.run(
                JSON.stringify(['recall', session.id, form.version()]),
                () => postSessionSessionIdRecall(session.id, { client: engine.client() }),
                async () => {
                  toast.show('Envio desfeito.');
                  await refreshAll();
                  await queries.invalidateQueries({ queryKey: ['day', snapshot.scope] });
                },
              )
            }
          />
        </>
      ) : null}
      {session.custody.custodyState === 'Active' &&
      session.status !== 'Draft' &&
      session.status !== 'Rejected' ? (
        <Custody
          session={session}
          version={form.version}
          mine={mineCustody}
          history={history}
          refresh={refreshAll}
        />
      ) : null}
      <Ledger ledger={ledger} />
      <Button label="Voltar" variant="secondary" testID="close-back" onPress={goBack} />
    </View>
  );
}

function SaveBar({ form }: { form: CountForm }) {
  const saved = form.savedAt ? auditInstant(form.savedAt) : null;
  const text = saveStatusText(form.saveState, saved ? { time: saved } : undefined);
  return (
    <View style={{ gap: spacing.stack.xs }} testID="close-save-bar">
      <AppText testID="close-save-status" accessibilityLiveRegion="polite">
        {text}
      </AppText>
      {form.failure && form.saveState === 'failed' && !form.unsupported ? (
        <Notice
          kind="danger"
          title="O servidor não salvou a contagem."
          body="Seus valores continuam na tela."
          messages={form.failure.messages}
        />
      ) : null}
      {form.saveState === 'uncertain' ? (
        <UncertainNotice
          messages={form.failure?.messages}
          recovery={
            <Button
              label="Recarregar e conferir"
              testID="close-reload"
              onPress={() => void form.reload()}
            />
          }
        />
      ) : null}
      {form.saveState === 'stale' ? (
        <Notice
          kind="warning"
          testID="close-stale"
          title="Este fechamento mudou em outro lugar."
          body="Seus valores continuam na tela. Recarregue para comparar antes de salvar de novo."
          messages={form.failure?.messages}
          actions={
            <Button
              label="Recarregar e conferir"
              testID="close-reload"
              onPress={() => void form.reload()}
            />
          }
        />
      ) : null}
      <Button
        label="Salvar"
        variant="secondary"
        testID="close-save"
        loading={form.saving}
        loadingLabel="Salvando…"
        disabled={!form.canSave}
        disabledReason={
          !form.current.allowedActions.canSaveCount
            ? 'Este fechamento não aceita alterações agora.'
            : !form.dirty && !form.neverRecorded
              ? 'Nenhuma alteração desde o último salvamento'
              : undefined
        }
        onPress={() => void form.save(true)}
      />
    </View>
  );
}

function Reconcile({ form }: { form: CountForm }) {
  const { server, mine } = form.reconciliation!;
  const rows = form.current.items.filter(
    (row) =>
      !row.isCashVarianceProduct &&
      (server.items[row.productId] ?? '') !== (mine.items[row.productId] ?? ''),
  );
  const show = (d: string | undefined) => (d ? formatBrlWire(wireFromDigits(d)) : 'não informado');
  return (
    <Card title="Compare antes de salvar" testID="close-reconcile">
      <AppText>
        Outra pessoa salvou este fechamento depois de você. Escolha o que fica no formulário; nada é
        enviado sem o seu Salvar.
      </AppText>
      {rows.map((row) => (
        <AppText
          key={row.productId}
        >{`${row.productName}: servidor ${show(server.items[row.productId])} · seu valor ${show(mine.items[row.productId])}`}</AppText>
      ))}
      {server.notes !== mine.notes ? <AppText>Observações diferentes.</AppText> : null}
      <Button
        label="Usar os valores do servidor"
        testID="close-use-server"
        onPress={form.useServer}
      />
      <Button
        label="Manter meus valores para salvar"
        variant="secondary"
        testID="close-keep-mine"
        onPress={form.keepMine}
      />
    </Card>
  );
}

function Identities({ session }: { session: ResponseDailyCloseReviewJson }) {
  const who = (user?: string | null, operator?: string | null) =>
    user
      ? operator
        ? `${user} · ${operator}`
        : `${user} · Sem operador associado ao registro`
      : 'ainda não';
  return (
    <DetailList
      testID="close-identities"
      rows={[
        {
          label: 'Responsável pelo turno',
          value: session.custody.custodianOperatorName ?? null,
        },
        {
          label: 'Turno',
          value: session.custody.custodyState
            ? `${sessionCustodyStateLabels[session.custody.custodyState]}${session.custody.startedAt ? ` · desde ${auditInstant(session.custody.startedAt)}` : ''}`
            : null,
        },
        { label: 'Aberto por', value: session.openedByUserName },
        {
          label: 'Contado por',
          value: who(session.recordedByUserName, session.recordedByOperatorName),
        },
        {
          label: 'Enviado por',
          value: who(session.submittedByUserName, session.submittedByOperatorName),
        },
      ]}
    />
  );
}

function AttemptFigures({ attempt }: { attempt: ResponseSessionCountAttemptJson }) {
  return (
    <>
      <SummaryStrip
        items={[
          { label: 'Total do fechamento', value: serverMoney(attempt.totalClosing) },
          { label: 'Total da abertura', value: serverMoney(attempt.totalOpening) },
          { label: 'Entradas', value: serverMoney(attempt.totalIn) },
          { label: 'Saídas', value: serverMoney(attempt.totalOut) },
        ]}
      />
      <AppText variant="money">
        {`Abertura ${serverMoney(attempt.totalOpening)} + entradas ${serverMoney(attempt.totalIn)} − saídas ${serverMoney(attempt.totalOut)} = esperado ${serverMoney(attempt.expectedClosing)}`}
      </AppText>
      <AppText testID="close-server-difference">{`Diferença calculada pelo servidor: ${formatSignedBrlWire(attempt.difference)}`}</AppText>
      <AppText variant="helper">{`Conferência ${attempt.attemptNumber} · ${sessionCountAttemptKindLabels[attempt.kind]} · ${attempt.conferredByName} · ${auditInstant(attempt.conferredAt)}`}</AppText>
    </>
  );
}

/** Driven only by the server's CandidateState: nothing is revealed or computed before Conferir. */
function Conference({
  count,
  stillObserved,
  setStillObserved,
  omitted,
}: {
  count: ResponseDailyCloseReviewJson['count'];
  stillObserved: boolean;
  setStillObserved: (v: boolean) => void;
  omitted: ReturnType<typeof omittedOpeningProducts>;
}) {
  const state = count.candidateState;
  return (
    <Card title="Conferência" testID={`close-conference-${state}`}>
      {state === 'NotConferred' ? <AppText>Conte o caixa, salve e confira.</AppText> : null}
      {state === 'Recounting' ? (
        <AppText>Recontagem em andamento — o resultado aparece depois de conferir.</AppText>
      ) : null}
      {state === 'Outdated' ? (
        <Notice kind="warning" title="A contagem mudou depois da conferência. Confira novamente." />
      ) : null}
      {state === 'Current' && count.confirmedAttempt ? (
        <>
          <AppText variant="section-title">Resultado da conferência</AppText>
          <AttemptFigures attempt={count.confirmedAttempt} />
        </>
      ) : null}
      {state === 'Unconfirmed' ? (
        <>
          <Notice
            kind="warning"
            title="O movimento ou a abertura mudou depois da conferência."
            body="O resultado anterior fica como registro. Confirme que a contagem ainda representa o caixa ou reconte."
          />
          {count.confirmedAttempt ? <AttemptFigures attempt={count.confirmedAttempt} /> : null}
          {count.unconfirmedRecalculation ? (
            <View testID="close-unconfirmed" style={{ gap: spacing.stack.xs }}>
              <AppText variant="body-strong">Recalculado — não confirmado</AppText>
              <AppText>{`Esperado ${serverMoney(count.unconfirmedRecalculation.expectedClosing)} · fechamento ${serverMoney(count.unconfirmedRecalculation.totalClosing)} · diferença ${formatSignedBrlWire(count.unconfirmedRecalculation.difference)}`}</AppText>
            </View>
          ) : null}
          {count.requiresStillObservedConfirmation ? (
            <Checkbox
              testID="close-still-observed"
              label="Confirmo que esta contagem ainda representa o caixa"
              checked={stillObserved}
              onCheckedChange={setStillObserved}
            />
          ) : null}
        </>
      ) : null}
      {omitted.length ? (
        <Notice
          kind="warning"
          testID="close-omitted-opening"
          title="A conta exibida não confere com a diferença do servidor. Confira a abertura e o fechamento anterior com a gerência antes de enviar ou aprovar."
          messages={omitted.map(
            (item) =>
              `${item.productName}: abertura ${serverMoney(item.openingValue!)} no servidor, fora da tabela exibida.`,
          )}
        />
      ) : null}
    </Card>
  );
}

function Blockers({
  session,
  drafts,
  goDrafts,
}: {
  session: ResponseDailyCloseReviewJson;
  drafts: { status: string; data?: number; error: unknown; refetch: () => unknown };
  goDrafts: (path: string) => void;
}) {
  if (session.status !== 'Draft' && session.status !== 'Rejected') return null;
  return (
    <Card title="Pendências para enviar" testID="close-blockers">
      {!session.itemsFirstRecordedAt ? <AppText>A contagem ainda não foi salva.</AppText> : null}
      {drafts.status === 'pending' ? (
        <Skeleton label="Conferindo rascunhos…" />
      ) : drafts.status === 'error' ? (
        <Notice
          kind="warning"
          title="Não foi possível verificar os rascunhos deste caixa."
          messages={errorMessages(drafts.error)}
          actions={<Button label="Tentar novamente" onPress={() => void drafts.refetch()} />}
        />
      ) : drafts.data! > 0 ? (
        <>
          <AppText testID="close-draft-count">{pluralDrafts(drafts.data!)}</AppText>
          <Button
            label="Finalizar movimentações"
            variant="link"
            role="link"
            onPress={() =>
              goDrafts(
                `/transacoes?accountId=${session.accountId}&from=${calendarPart(session.date)}&to=${calendarPart(session.date)}&status=Draft`,
              )
            }
          />
        </>
      ) : (
        <AppText>Nenhuma movimentação em rascunho neste caixa e dia.</AppText>
      )}
    </Card>
  );
}

function Ledger({
  ledger,
}: {
  ledger: {
    status: string;
    data?: import('@loto/api').ResponseListTransactionsJson;
    error: unknown;
    refetch: () => unknown;
  };
}) {
  return (
    <Card title="Transações do dia" testID="close-ledger">
      {ledger.status === 'pending' ? (
        <Skeleton label="Carregando transações…" />
      ) : ledger.status === 'error' ? (
        <Notice
          kind="warning"
          title="Não foi possível carregar as transações do dia."
          messages={errorMessages(ledger.error)}
          actions={<Button label="Tentar novamente" onPress={() => void ledger.refetch()} />}
        />
      ) : ledger.data!.items.length === 0 ? (
        <AppText>Nenhuma transação neste caixa e dia.</AppText>
      ) : (
        <>
          {ledger.data!.items.map((row) => (
            <View key={row.id} style={{ gap: spacing.stack.xs }} testID={`close-ledger-${row.id}`}>
              <AppText variant="body-strong">{row.transactionTypeName}</AppText>
              <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.inline.sm }}>
                <AppText>{`${directionLabels[row.direction]} · ${formatBrlWire(row.value)}`}</AppText>
                <Badge
                  kind="status"
                  domain="transaction-status"
                  value={row.status}
                  context="Situação"
                />
                {row.generationKind ? <Badge kind="automatic" /> : null}
              </View>
            </View>
          ))}
          {ledger.data!.totalCount > ledger.data!.items.length ? (
            <AppText variant="helper">{`Mostrando ${ledger.data!.items.length} de ${ledger.data!.totalCount}. A lista completa fica em Transações.`}</AppText>
          ) : null}
        </>
      )}
    </Card>
  );
}

/** Fix & resubmit: separate, dated server events; honest fallbacks when history is incomplete. */
function ReviewCauses({
  session,
  history,
}: {
  session: ResponseDailyCloseReviewJson;
  history: {
    status: string;
    data?: ResponseDailyCloseHistoryJson;
    error: unknown;
    refetch: () => unknown;
  };
}) {
  if (history.status === 'pending') return <Skeleton label="Carregando o histórico…" />;
  if (history.status === 'error')
    return (
      <Notice
        kind="warning"
        title="Não foi possível carregar por que este fechamento precisa de revisão."
        messages={errorMessages(history.error)}
        actions={<Button label="Tentar novamente" onPress={() => void history.refetch()} />}
      />
    );
  const data = history.data!;
  const rejected = data.events.filter((e) => e.type === 'Rejected');
  const changed = data.events.filter((e) => e.type === 'OpeningChanged');
  return (
    <Card title="Por que este fechamento precisa de revisão?" testID="close-causes">
      {data.events.map((event) => (
        <View key={event.id} style={{ gap: spacing.stack.xs }} testID={`close-event-${event.id}`}>
          <Badge kind="status" domain="review-event-type" value={event.type} context="Evento" />
          <AppText variant="body-strong">{dailyCloseReviewEventTypeLabels[event.type]}</AppText>
          <AppText>
            {`${event.type === 'Rejected' ? 'Rejeitado em' : 'Alterada em'} ${auditInstant(event.occurredAt)} · ${event.actorUserName}${event.actorOperatorName ? ` (${event.actorOperatorName})` : ''}`}
          </AppText>
          {event.type === 'Rejected' ? (
            <AppText>{`Observação da gerência: ${event.rejectionReason ?? '—'}`}</AppText>
          ) : (
            <>
              <AppText>
                {event.sourceDate
                  ? `Fechamento de ${businessDate(event.sourceDate)} corrigido → abertura de ${businessDate(session.date)} alterada`
                  : 'O fechamento anterior mudou a abertura deste dia.'}
              </AppText>
              <AppText>
                Esta alteração não é uma nova rejeição. O fechamento anterior mudou a abertura deste
                dia.
              </AppText>
              <Disclosure label="Ver o que mudou" testID={`close-event-diff-${event.id}`}>
                {event.items.map((item) => (
                  <AppText key={item.productId}>
                    {`${item.productName}: antes ${item.beforeValue == null ? 'sem valor salvo' : formatBrlWire(item.beforeValue)} · depois ${item.afterValue == null ? 'sem valor salvo' : formatBrlWire(item.afterValue)} · alteração ${formatSignedBrlWire(item.change)}`}
                  </AppText>
                ))}
              </Disclosure>
            </>
          )}
        </View>
      ))}
      {session.rejectionReason && rejected.length === 0 ? (
        <View style={{ gap: spacing.stack.xs }}>
          <AppText variant="body-strong">Rejeitado na conferência</AppText>
          <AppText>Data da rejeição indisponível</AppText>
          <AppText>{`Observação da gerência: ${session.rejectionReason}`}</AppText>
        </View>
      ) : null}
      {session.openingRecheckRequiredAt && changed.length === 0 ? (
        <View style={{ gap: spacing.stack.xs }}>
          <AppText variant="body-strong">Abertura alterada</AppText>
          <AppText>Detalhes da alteração indisponíveis para este fechamento</AppText>
        </View>
      ) : null}
      {data.currentOpeningSource ? (
        <AppText variant="helper">{`Abertura atual vem do fechamento de ${businessDate(data.currentOpeningSource.date)}.`}</AppText>
      ) : null}
      <AppText>
        Revise a observação da gerência, confira a abertura alterada e reenvie a contagem.
      </AppText>
    </Card>
  );
}

/**
 * Finish and Passar o caixa after a submitted count. Custody stays with the outgoing custodian until
 * the server reports an accepted handover; Gestão acts for an absent custodian only with a reason.
 */
function Custody({
  session,
  version,
  mine,
  history,
  refresh,
}: {
  session: ResponseDailyCloseReviewJson;
  version: () => number;
  mine: boolean;
  history: UseQueryResult<ResponseDailyCloseHistoryJson>;
  refresh: () => Promise<unknown>;
}) {
  const { engine, snapshot } = useSession();
  const client = useScopedApiClient();
  const reads = useDayReads();
  const toast = useToast();
  const queries = useQueryClient();
  const role = snapshot.branch!.role;
  const elevated = role !== 'Member';
  const finish = useDayCommand();
  const offer = useDayCommand();
  const withdraw = useDayCommand();
  const [reason, setReason] = useState('');
  const [incoming, setIncoming] = useState<string | null>(null);
  const [offering, setOffering] = useState(false);
  const pending: ResponseSessionHandoverJson | null = useMemo(
    () =>
      history.data?.handovers.find(
        (h) =>
          h.outgoingSessionId === session.id && (h.state === 'Offered' || h.state === 'Disputed'),
      ) ?? null,
    [history.data, session.id],
  );
  const operators = useQuery({
    queryKey: ['day', snapshot.scope, 'operators'],
    queryFn: async () => (await getOperator({ client })).data.operators,
    enabled: reads && elevated && offering,
    retry: false,
  });
  if (!mine && !elevated) return null;
  const exception = !mine;
  const needsReason = exception;
  const reasonText = reason.trim();
  const after = async (message: string) => {
    toast.show(message);
    await queries.invalidateQueries({ queryKey: ['day', snapshot.scope] });
    await refresh();
  };
  // Custody actions depend on whether a passagem is pending: without a current history read that is
  // unknown, so no action is offered (never "no passagem").
  if (history.status !== 'success')
    return (
      <Card title="Turno" testID="close-custody">
        {history.status === 'error' ? (
          <Notice
            kind="danger"
            testID="close-custody-unknown"
            title="Não foi possível verificar se há uma passagem do caixa pendente."
            body="Encerrar o turno e passar o caixa ficam indisponíveis até essa leitura funcionar."
            messages={errorMessages(history.error)}
            actions={<Button label="Tentar novamente" onPress={() => void history.refetch()} />}
          />
        ) : (
          <Skeleton label="Verificando a passagem do caixa…" />
        )}
      </Card>
    );
  return (
    <Card title="Turno" testID="close-custody">
      <AppText>
        {exception
          ? `O turno é de ${session.custody.custodianOperatorName ?? 'outro operador'}. Agir por ele é uma exceção da gerência, com motivo.`
          : 'O fechamento foi enviado, mas o caixa continua com você até encerrar o turno ou passar o caixa.'}
      </AppText>
      {pending ? (
        <View testID="close-pending-offer" style={{ gap: spacing.stack.xs }}>
          <AppText>{`Passagem para ${pending.intendedIncomingOperatorName} · ${sessionHandoverStateLabels[pending.state]} · ${auditInstant(pending.offeredAt)}`}</AppText>
          {pending.state === 'Disputed' && pending.reason ? (
            <AppText>{`Motivo da contestação: ${pending.reason}`}</AppText>
          ) : null}
          <AppText variant="helper">O caixa continua com você até a pessoa aceitar.</AppText>
        </View>
      ) : null}
      <FormField
        label="Motivo"
        required={needsReason || !!pending}
        optional={!needsReason && !pending}
      >
        <TextField
          kind="textarea"
          testID="close-custody-reason"
          value={reason}
          maxLength={500}
          onChangeText={setReason}
        />
      </FormField>
      <CommandState
        command={finish}
        receipt="Turno encerrado no servidor."
        testID="close-finish"
        reread={() => void refresh().then(() => finish.clear())}
      />
      <CommandState
        command={offer}
        receipt="Passagem oferecida no servidor."
        testID="close-offer"
        reread={() => void refresh().then(() => offer.clear())}
      />
      <CommandState
        command={withdraw}
        receipt="Passagem retirada no servidor."
        testID="close-withdraw"
        reread={() => void refresh().then(() => withdraw.clear())}
      />
      {pending ? (
        <Button
          label="Retirar a passagem"
          variant="secondary"
          testID="close-withdraw-button"
          loading={withdraw.pending}
          loadingLabel="Retirando…"
          disabled={withdraw.blocked || reasonText === ''}
          disabledReason={
            reasonText === '' ? 'Informe o motivo para retirar a passagem.' : undefined
          }
          onPress={() =>
            void withdraw.run(
              JSON.stringify(['withdraw', pending.id, pending.version, reasonText]),
              () =>
                postSessionHandoverHandoverIdWithdraw(
                  pending.id,
                  { reason: reasonText },
                  { 'If-Match': toIfMatch(pending.version) },
                  { client: engine.client() },
                ),
              () => after('Passagem retirada.'),
            )
          }
        />
      ) : (
        <>
          <Button
            label="Encerrar turno"
            testID="close-finish-button"
            loading={finish.pending}
            loadingLabel="Encerrando…"
            disabled={finish.blocked || offer.pending || (needsReason && reasonText === '')}
            disabledReason={
              needsReason && reasonText === '' ? 'Informe o motivo da exceção.' : undefined
            }
            onPress={() => {
              const ifMatch = toIfMatch(version());
              void finish.run(
                JSON.stringify(['finish', session.id, version(), reasonText]),
                (key) =>
                  postSessionSessionIdFinish(
                    session.id,
                    reasonText ? { reason: reasonText } : {},
                    { 'Idempotency-Key': key, 'If-Match': ifMatch },
                    { client: engine.client() },
                  ),
                () => after('Turno encerrado.'),
              );
            }}
          />
          {elevated ? (
            offering ? (
              <>
                <FormField
                  label="Próximo operador"
                  required
                  helper="A pessoa precisa ter este caixa atribuído."
                >
                  {operators.status === 'success' ? (
                    <Select
                      testID="close-offer-incoming"
                      value={incoming}
                      onValueChange={setIncoming}
                      options={operators.data
                        .filter((o) => o.id !== session.custody.custodianOperatorId)
                        .map((o) => ({ value: o.id, label: o.name }))}
                    />
                  ) : operators.status === 'error' ? (
                    <Notice
                      kind="danger"
                      title="Não foi possível carregar os operadores."
                      messages={errorMessages(operators.error)}
                    />
                  ) : (
                    <Skeleton label="Carregando operadores…" />
                  )}
                </FormField>
                <Button
                  label="Passar o caixa"
                  testID="close-offer-button"
                  loading={offer.pending}
                  loadingLabel="Oferecendo…"
                  disabled={
                    offer.blocked ||
                    finish.pending ||
                    !incoming ||
                    (needsReason && reasonText === '')
                  }
                  onPress={() => {
                    const body = {
                      incomingOperatorId: incoming!,
                      countRevision: session.custody.countRevision,
                      ...(reasonText ? { reason: reasonText } : {}),
                    };
                    const ifMatch = toIfMatch(version());
                    void offer.run(
                      JSON.stringify(['offer', session.id, version(), body]),
                      (key) =>
                        postSessionSessionIdHandover(
                          session.id,
                          body,
                          { 'Idempotency-Key': key, 'If-Match': ifMatch },
                          { client: engine.client() },
                        ),
                      () => after('Passagem oferecida.'),
                    );
                  }}
                />
              </>
            ) : (
              <Button
                label="Passar o caixa"
                variant="secondary"
                testID="close-offer-start"
                onPress={() => setOffering(true)}
              />
            )
          ) : (
            <Notice
              kind="info"
              testID="close-offer-unavailable"
              title="Passar o caixa pelo aplicativo"
              body="Seu perfil ainda não tem uma lista de operadores para escolher quem recebe o caixa. Peça à gerência para registrar a passagem, ou encerre o turno."
            />
          )}
        </>
      )}
      {session.custody.custodyState ? (
        <AppText variant="helper">{`Situação do turno: ${sessionCustodyStateLabels[session.custody.custodyState]}`}</AppText>
      ) : null}
    </Card>
  );
}
