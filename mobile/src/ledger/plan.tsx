import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { View } from 'react-native';
import { useFocusEffect, useLocalSearchParams } from 'expo-router';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getTransactionInstallmentOriginTransactionId,
  postTransactionInstallmentOriginTransactionIdFinalize,
  postTransactionInstallmentPreview,
  postTransactionTransactionIdCancel,
  toIfMatch,
  type ResponseCreateTransactionInstallmentJson,
  type ResponseInstallmentPlanJson,
  type ResponseInstallmentPreviewJson,
} from '@loto/api';
import {
  agingBucketLabels,
  asCents,
  centsFromWire,
  dailyCloseStatusLabels,
  formatBrl,
  formatBrlWire,
  formatCount,
  formatDate,
  formatSignedBrl,
  installmentPlanStatusLabels,
  parseCalendarDate,
  type CalendarDate,
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
  ConfirmDialog,
  DateField,
  FormField,
  Notice,
  SegmentedControl,
  Skeleton,
  TextField,
  moveFocusTo,
  useToast,
} from '@/ui';
import { RetainedNotice, useAdminDraft } from '@/admin/common';
import { useTimeCommand } from '@/time/common';
import {
  IntentNotices,
  elevated,
  errorMessages,
  useAccounts,
  useClients,
  useFinancialIntent,
  useLedgerReads,
  useOperators,
  usePreview,
  useTypes,
} from './common';
import {
  cashboxOf,
  day,
  emptyPlan,
  impactLines,
  isPlanInput,
  planMissing,
  planRequest,
  planSumDifference,
  planTypes,
  reconcileChoices,
  type PlanInput,
} from './contracts';
import {
  AccountField,
  AmountField,
  ClientField,
  DateTimeFields,
  DescriptionField,
  OperatorField,
  TypeField,
} from './fields';
import { useSessionBinding } from './opening';
import { nowTime } from './create';
import { ChequeBatch } from './cheque';

const businessDay = (value: string | null | undefined) => {
  const date = value ? parseCalendarDate(day(value)!) : null;
  return date ? formatDate(date) : '—';
};

/**
 * Installment (pre-dated cheque) plan — new: the server previews the exact schedule (dates,
 * rounding, descriptions) and impact; the person accepts that schedule explicitly, then one
 * durable idempotent create records the whole plan (or a Draft plan).
 */
export function NewPlanScreen() {
  const { snapshot, engine } = useSession();
  const shell = useShell();
  const client = useScopedApiClient();
  const queries = useQueryClient();
  const toast = useToast();
  const today = snapshot.clock!.branchLocalDate;
  const manage = elevated(snapshot.branch!.role);
  const own = snapshot.selfContext?.operatorId ?? null;
  const types = useTypes();
  const accounts = useAccounts();
  const clients = useClients();
  const operators = useOperators();
  const draft = useAdminDraft('ledger/plan', isPlanInput, async () => {
    await engine.verifyEntry();
    return engine.getSnapshot().status === 'branch';
  });
  const [input, setInputState] = useState<PlanInput>(() =>
    emptyPlan(today, nowTime(snapshot.clock)),
  );
  /** Device retention starts with the first real change; the current input is kept after each. */
  const [touched, setTouched] = useState(false);
  const latest = useRef(input);
  const { keep } = draft;
  useEffect(() => {
    latest.current = input;
    if (touched) void keep(input, null);
  }, [input, touched, keep]);
  const setInput = useCallback((update: (prev: PlanInput) => PlanInput) => {
    setTouched(true);
    setInputState(update);
  }, []);
  // Back from Clientes with a just-created client: select it; when the router opened a fresh plan
  // form, restore this person's validated device copy for review (nothing is sent) under it. The
  // client alone is not an edit, so it never overwrites the copy before it is restored.
  const params = useLocalSearchParams<{ clientId?: string }>();
  const returnedClient = typeof params.clientId === 'string' ? params.clientId : null;
  const [adopted, setAdopted] = useState<{ client: string | null; restored: boolean }>({
    client: null,
    restored: false,
  });
  if (
    returnedClient &&
    (adopted.client !== returnedClient || (!adopted.restored && draft.retained))
  ) {
    const restoring = !adopted.restored && !!draft.retained;
    const base = restoring ? draft.retained!.input : input;
    if (restoring) {
      draft.dismissRetained();
      setTouched(true);
    }
    setAdopted({ client: returnedClient, restored: adopted.restored || restoring });
    setInputState({ ...base, clientId: returnedClient });
  }
  const [cleared, setCleared] = useState<readonly string[]>([]);
  const [acceptedFor, setAcceptedFor] = useState<string | null>(null);
  const [receipt, setReceipt] = useState<ResponseCreateTransactionInstallmentJson | null>(null);
  const intent = useFinancialIntent('ledger/plan', async (data) => {
    const created = data as ResponseCreateTransactionInstallmentJson;
    setReceipt(created);
    toast.show(
      created.installments[0]?.status === 'Draft'
        ? 'Plano registrado em rascunho.'
        : 'Plano registrado.',
    );
    await draft.discard();
    void queries.invalidateQueries({ queryKey: ['ledger', engine.getSnapshot().scope] });
    void queries.invalidateQueries({ queryKey: ['day', engine.getSnapshot().scope] });
  });
  const offeredTypes = useMemo(() => planTypes(types.data ?? []), [types.data]);
  // A plan is a cheque on a cashbox or bank account; a Tab plan is refused (single-create sale).
  const offeredAccounts = (accounts.data ?? []).filter(
    (a) => a.type === 'Terminal' || a.type === 'BankAccount',
  );
  const account = offeredAccounts.find((a) => a.id === input.accountId) ?? null;
  useEffect(() => {
    if (types.status !== 'success' || accounts.status !== 'success') return;
    const result = reconcileChoices(latest.current, {
      types: new Set(offeredTypes.map((t) => t.id)),
      accounts: new Set(offeredAccounts.map((a) => a.id)),
      // A client just created in Clientes is newer than this list; the server validates it.
      clients: clients.data && !returnedClient ? new Set(clients.data.map((c) => c.id)) : null,
    });
    if (result.cleared.length) {
      setCleared(result.cleared);
      setInput(() => result.input);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [types.status, accounts.status, accounts.data, clients.data, offeredTypes]);
  const cashbox = cashboxOf(account);
  const binding = useSessionBinding({
    cashboxId: cashbox.kind === 'cashbox' ? cashbox.id : null,
    cashboxName: account?.name ?? 'o caixa',
    date: input.date,
    testID: 'plan',
  });
  const operatorRequired = manage && !own;
  const missing = planMissing(input, { operatorRequired });
  const bindingBlocks = ['loading', 'error', 'blocked', 'incomplete'].includes(binding.status);
  const previewMissing = bindingBlocks ? [...missing, 'conferir o turno do caixa'] : missing;
  const candidate = previewMissing.length === 0 ? planRequest(input, binding.binding, false) : null;
  const signature = candidate ? JSON.stringify(candidate) : null;
  const preview = usePreview<ResponseInstallmentPreviewJson>('plan', signature, (signal) =>
    postTransactionInstallmentPreview(candidate!, undefined, { client, signal }).then(
      (r) => r.data,
    ),
  );
  const accepted = !!signature && acceptedFor === signature && !!preview.data;
  const difference = planSumDifference(input);
  /** The signature on screen right now, read after an await to detect changes made meanwhile. */
  const shown = useRef(signature);
  useEffect(() => {
    shown.current = signature;
  }, [signature]);
  const [changedWhileChecking, setChangedWhileChecking] = useState(false);
  const send = async (saveAsDraft: boolean) => {
    if (!accepted || bindingBlocks || intent.blocked) return;
    // Bind the send to the exact schedule the person accepted: the input and signature at the tap.
    const acceptedInput = input;
    const acceptedSignature = signature;
    setChangedWhileChecking(false);
    const fresh = await binding.prepare();
    if (!fresh) return;
    if (shown.current !== acceptedSignature) {
      setChangedWhileChecking(true);
      return;
    }
    await intent.submit({
      operation: 'installment',
      payload: planRequest(acceptedInput, fresh, saveAsDraft),
    });
  };
  if (receipt) {
    const origin = receipt.installments[0]?.id;
    return (
      <Card title="Plano registrado no servidor" testID="plan-receipt">
        <AppText>{`${formatCount(receipt.installments.length)} parcelas · ${receipt.installments[0]?.status === 'Draft' ? 'em rascunho' : 'ativas'}`}</AppText>
        {receipt.installments.map((row) => (
          <AppText
            key={row.id}
            variant="helper"
          >{`${businessDay(row.dueDate)} · ${formatBrlWire(row.value)}`}</AppText>
        ))}
        {origin ? (
          <Button
            label="Abrir o plano"
            testID="plan-receipt-open"
            onPress={() => void shell.navigate(`/transacoes/cheque-pre-datado/${origin}`, true)}
          />
        ) : null}
        <Button label="Voltar" variant="link" role="link" onPress={() => void shell.back()} />
      </Card>
    );
  }
  return (
    <>
      <RetainedNotice
        visible={!!draft.retained}
        onRestore={() => {
          const value = draft.retained!.input;
          draft.dismissRetained();
          setInput(() => value);
        }}
      />
      {draft.problem ? <Notice kind="warning" title={draft.problem} /> : null}
      {cleared.length ? (
        <Notice
          kind="warning"
          title={`Escolha de novo: ${cleared.join(', ')}.`}
          onDismiss={() => setCleared([])}
        />
      ) : null}
      <IntentNotices
        intent={intent}
        testID="plan-intent"
        verify={
          input.accountId && input.date ? (
            <Button
              label="Conferir na lista de transações"
              variant="secondary"
              onPress={() =>
                void shell.navigate(
                  `/transacoes?accountId=${input.accountId}&from=${input.date}&to=${input.date}`,
                  true,
                )
              }
            />
          ) : undefined
        }
      />
      <Card title="Plano de cheques">
        <TypeField
          testID="plan-type"
          label="Tipo de cheque"
          types={offeredTypes}
          value={input.typeId}
          status={types.status}
          retry={() => void types.refetch()}
          onChange={(typeId) => setInput((p) => ({ ...p, typeId }))}
        />
        <AccountField
          testID="plan-account"
          accounts={offeredAccounts}
          lookup={accounts}
          value={input.accountId}
          onChange={(accountId) => setInput((p) => ({ ...p, accountId }))}
        />
        <AmountField
          label="Valor total"
          testID="plan-value"
          value={input.value}
          onChange={(value) => setInput((p) => ({ ...p, value }))}
          buffer={input.buffers.value}
          onBuffer={(raw) =>
            setInput((p) => {
              const buffers = { ...p.buffers };
              if (raw === undefined) delete buffers.value;
              else buffers.value = raw;
              return { ...p, buffers };
            })
          }
        />
        <DateTimeFields
          testID="plan"
          date={input.date}
          time={input.time}
          onDate={(date) => setInput((p) => ({ ...p, date }))}
          onTime={(time) => setInput((p) => ({ ...p, time }))}
        />
        <ClientField
          testID="plan-client"
          clients={clients.data}
          status={clients.status}
          retry={() => void clients.refetch()}
          required={false}
          value={input.clientId}
          onChange={(clientId) => setInput((p) => ({ ...p, clientId }))}
        />
        <DescriptionField
          testID="plan-description"
          value={input.description}
          onChange={(description) => setInput((p) => ({ ...p, description }))}
        />
        {manage && (shell.mode === 'management' || operatorRequired) ? (
          <OperatorField
            testID="plan-operator"
            operators={operators.data}
            status={operators.status}
            required={operatorRequired}
            value={input.operatorId}
            onChange={(operatorId) => setInput((p) => ({ ...p, operatorId }))}
          />
        ) : null}
      </Card>
      <Card title="Parcelas">
        <SegmentedControl
          accessibilityLabel="Como informar as parcelas"
          testID="plan-mode"
          value={input.mode}
          onValueChange={(mode) => setInput((p) => ({ ...p, mode: mode as 'manual' | 'auto' }))}
          options={[
            { value: 'manual', label: 'Uma a uma' },
            { value: 'auto', label: 'Gerar mensais' },
          ]}
        />
        {input.mode === 'manual' ? (
          <>
            {input.rows.map((row, index) => (
              <View key={index} style={{ gap: spacing.stack.xs }} testID={`plan-row-${index}`}>
                <AppText variant="body-strong">{`Parcela ${index + 1}`}</AppText>
                <FormField label={`Bom para — parcela ${index + 1}`} required>
                  <DateField
                    testID={`plan-row-${index}-due`}
                    today={parseCalendarDate(today) ?? undefined}
                    value={row.dueDate ? (parseCalendarDate(row.dueDate) as CalendarDate) : null}
                    onValueChange={(dueDate) =>
                      setInput((p) => ({
                        ...p,
                        rows: p.rows.map((r, i) => (i === index ? { ...r, dueDate } : r)),
                      }))
                    }
                  />
                </FormField>
                <AmountField
                  label={`Valor — parcela ${index + 1}`}
                  testID={`plan-row-${index}-value`}
                  value={row.value}
                  onChange={(value) =>
                    setInput((p) => ({
                      ...p,
                      rows: p.rows.map((r, i) => (i === index ? { ...r, value } : r)),
                    }))
                  }
                  buffer={input.buffers[`row-${index}`]}
                  onBuffer={(raw) =>
                    setInput((p) => {
                      const buffers = { ...p.buffers };
                      if (raw === undefined) delete buffers[`row-${index}`];
                      else buffers[`row-${index}`] = raw;
                      return { ...p, buffers };
                    })
                  }
                />
                {input.rows.length > 2 ? (
                  <Button
                    label={`Remover parcela ${index + 1}`}
                    variant="link"
                    onPress={() =>
                      setInput((p) => ({
                        ...p,
                        rows: p.rows.filter((_, i) => i !== index),
                        buffers: {},
                      }))
                    }
                  />
                ) : null}
              </View>
            ))}
            {input.rows.length < 24 ? (
              <Button
                label="Adicionar parcela"
                variant="secondary"
                testID="plan-add-row"
                onPress={() =>
                  setInput((p) => ({ ...p, rows: [...p.rows, { dueDate: null, value: '' }] }))
                }
              />
            ) : null}
            {difference !== null ? (
              <AppText testID="plan-sum" tone={difference === 0 ? undefined : 'secondary'}>
                {difference === 0
                  ? 'A soma das parcelas é igual ao valor total.'
                  : `Soma das parcelas − valor total: ${formatSignedBrl(asCents(difference))}. O servidor recusa se não for igual.`}
              </AppText>
            ) : null}
          </>
        ) : (
          <>
            <FormField label="Primeiro vencimento" required>
              <DateField
                testID="plan-first-due"
                today={parseCalendarDate(today) ?? undefined}
                value={input.firstDue ? (parseCalendarDate(input.firstDue) as CalendarDate) : null}
                onValueChange={(firstDue) => setInput((p) => ({ ...p, firstDue }))}
              />
            </FormField>
            <FormField
              label="Quantidade de parcelas"
              required
              helper="De 2 a 24. O servidor calcula datas e arredondamento."
            >
              <TextField
                kind="integer"
                testID="plan-count"
                value={input.count}
                maxLength={2}
                onChangeText={(count) =>
                  setInput((p) => ({ ...p, count: count.replace(/\D/g, '').slice(0, 2) }))
                }
              />
            </FormField>
          </>
        )}
      </Card>
      {binding.view}
      {changedWhileChecking ? (
        <Notice
          kind="warning"
          testID="plan-changed"
          title="Os dados mudaram enquanto o turno era conferido. Nada foi enviado."
          body="Confira o novo cronograma do servidor e aceite-o de novo."
        />
      ) : null}
      <PlanPreview
        preview={preview}
        complete={!!candidate}
        missing={previewMissing}
        accepted={accepted}
        onAccept={(checked) => setAcceptedFor(checked ? signature : null)}
      />
      <Button
        label="Salvar plano"
        variant="cta"
        testID="plan-save"
        fullWidth
        loading={intent.state.phase === 'sending'}
        disabled={!accepted || bindingBlocks || intent.blocked}
        disabledReason={
          !accepted
            ? 'Confira e aceite o cronograma do servidor.'
            : bindingBlocks
              ? 'Confira o turno do caixa acima.'
              : undefined
        }
        onPress={() => void send(false)}
      />
      <Button
        label="Salvar plano como rascunho"
        variant="secondary"
        testID="plan-save-draft"
        fullWidth
        disabled={!accepted || bindingBlocks || intent.blocked}
        onPress={() => void send(true)}
      />
    </>
  );
}

function PlanPreview({
  preview,
  complete,
  missing,
  accepted,
  onAccept,
}: {
  preview: ReturnType<typeof usePreview<ResponseInstallmentPreviewJson>>;
  complete: boolean;
  missing: readonly string[];
  accepted: boolean;
  onAccept: (checked: boolean) => void;
}) {
  if (!complete)
    return (
      <AppText
        variant="helper"
        testID="plan-preview-idle"
      >{`O cronograma aparece quando completar: ${missing.join(', ')}.`}</AppText>
    );
  if (preview.pending) return <Skeleton label="O servidor está montando o cronograma…" />;
  if (preview.error)
    return (
      <Notice
        kind="danger"
        testID="plan-preview-error"
        title="O servidor não aceitou este plano."
        body="Corrija os dados acima; nada foi salvo."
        messages={errorMessages(preview.error)}
      />
    );
  if (!preview.data) return null;
  const data = preview.data;
  return (
    <Card title="Cronograma do servidor (nada foi salvo)" testID="plan-preview">
      <AppText variant="body-strong">{`${formatCount(data.installmentCount)} parcelas · total ${formatBrlWire(data.totalValue)}`}</AppText>
      {data.rows.map((row) => (
        <AppText key={row.index} testID={`plan-preview-row-${row.index}`}>
          {`${row.description} · bom para ${businessDay(row.dueDate)} · ${formatBrlWire(row.value)}`}
        </AppText>
      ))}
      {impactLines(
        {
          receivableImpact: {
            rowAppearsInOpenReceivables: false,
            rowDisappearsFromOpenReceivables: false,
          },
          fiadoBalanceImpact: data.impact.fiadoBalanceImpact,
          cashVarianceImpact: data.impact.cashVarianceImpact,
        },
        agingBucketLabels,
        dailyCloseStatusLabels,
      ).map((line) => (
        <AppText key={line.key} variant="helper">
          {line.text}
        </AppText>
      ))}
      {data.impact.openChequeAgingImpact.groupAppearsInOpenCheques ? (
        <AppText variant="helper">{`Passa a constar em cheques em aberto: ${formatBrlWire(data.impact.openChequeAgingImpact.outstandingTotal)}.`}</AppText>
      ) : null}
      <Checkbox
        testID="plan-accept"
        label="Conferi as datas e valores calculados pelo servidor"
        checked={accepted}
        onCheckedChange={onAccept}
      />
    </Card>
  );
}

/**
 * Existing-plan mode: every authorized sibling with its own status/version; **Finalizar plano**
 * activates the listed Draft rows atomically (all or none); per-row corrections go through each
 * installment's own editor; cancelling remaining rows is one explicit command per row.
 */
export function PlanScreen() {
  const { id, focus } = useLocalSearchParams<{ id: string; focus?: string }>();
  const { snapshot, engine } = useSession();
  const shell = useShell();
  const client = useScopedApiClient();
  const reads = useLedgerReads();
  const queries = useQueryClient();
  const toast = useToast();
  const manage = elevated(snapshot.branch!.role);
  const plan = useQuery({
    queryKey: ['ledger', snapshot.scope, 'plan', id],
    queryFn: async ({ signal }) =>
      (await getTransactionInstallmentOriginTransactionId(id, { client, signal })).data,
    enabled: reads && typeof id === 'string',
    retry: false,
    staleTime: 0,
  });
  useFocusEffect(
    useCallback(() => {
      if (reads) void plan.refetch();
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [reads]),
  );
  const finalize = useTimeCommand();
  const cancel = useTimeCommand();
  const [confirm, setConfirm] = useState<'finalize' | 'cancel' | null>(null);
  const [selected, setSelected] = useState<readonly string[]>([]);
  const [reason, setReason] = useState('');
  const [cancelProgress, setCancelProgress] = useState<{
    done: string[];
    failedAt: string | null;
  } | null>(null);
  const [batch, setBatch] = useState(false);
  const rows = useRef(new Map<string, View>());
  useEffect(() => {
    if (focus && plan.data) moveFocusTo(rows.current.get(focus));
  }, [focus, plan.data]);
  if (plan.status === 'pending') return <Skeleton label="Carregando o plano…" />;
  if (plan.status === 'error')
    return (
      <Notice
        kind="danger"
        testID="plan-error"
        title="Não foi possível abrir o plano."
        messages={errorMessages(plan.error)}
        actions={<Button label="Tentar novamente" onPress={() => void plan.refetch()} />}
      />
    );
  const data: ResponseInstallmentPlanJson = plan.data;
  const drafts = data.installments.filter((row) => row.status === 'Draft');
  const draftTotal = drafts.reduce((sum, row) => sum + centsFromWire(row.value), 0);
  const mixed = data.status === 'Mixed';
  const reread = async () => {
    const fresh = await plan.refetch();
    void queries.invalidateQueries({ queryKey: ['ledger', snapshot.scope, 'list'] });
    void queries.invalidateQueries({ queryKey: ['day', snapshot.scope] });
    return fresh;
  };
  const sendFinalize = () =>
    void finalize.run(
      () =>
        postTransactionInstallmentOriginTransactionIdFinalize(
          data.originTransactionId,
          {
            installments: drafts.map((row) => ({ transactionId: row.id, version: row.version })),
            ...(mixed ? { confirmMixedPlan: true } : {}),
          },
          { client: engine.client() },
        ),
      async () => {
        setConfirm(null);
        toast.show('Plano finalizado.');
        await reread();
      },
    );
  /** One guarded cancel per selected row, in order; a failure stops the rest and is never undone. */
  const sendCancel = async () => {
    const targets = data.installments.filter(
      (row) => selected.includes(row.id) && row.status !== 'Cancelled',
    );
    const done: string[] = [];
    setCancelProgress({ done, failedAt: null });
    for (const row of targets) {
      let ok = false;
      await cancel.run(
        () =>
          postTransactionTransactionIdCancel(
            row.id,
            { cancellationReason: reason.trim() },
            { 'If-Match': toIfMatch(row.version) },
            { client: engine.client() },
          ),
        () => {
          ok = true;
        },
      );
      if (!ok) {
        setCancelProgress({ done: [...done], failedAt: row.id });
        break;
      }
      done.push(row.id);
      setCancelProgress({ done: [...done], failedAt: null });
    }
    setConfirm(null);
    setSelected([]);
    setReason('');
    await reread().catch(() => undefined);
  };
  const busy = finalize.pending || cancel.pending;
  return (
    <>
      <Card
        title={`Plano de cheques · ${installmentPlanStatusLabels[data.status]}`}
        testID="plan-summary"
      >
        <Badge
          kind="status"
          domain="installment-plan-status"
          value={data.status}
          context="Situação do plano:"
        />
        <AppText testID="plan-total">{`Total sem canceladas: ${formatBrlWire(data.totalValue)} · ${businessDay(data.date)}`}</AppText>
        <AppText variant="helper">{`${formatCount(data.activeCount)} ativas · ${formatCount(data.draftCount)} em rascunho · ${formatCount(data.cancelledCount)} canceladas`}</AppText>
      </Card>
      {finalize.uncertain || finalize.error || finalize.postWriteError ? (
        <Notice
          kind={finalize.error && !finalize.uncertain ? 'danger' : 'warning'}
          testID="plan-finalize-state"
          title={
            finalize.uncertain
              ? 'Não foi possível confirmar a finalização do plano. Nada será reenviado.'
              : finalize.postWriteError
                ? 'Plano finalizado; a releitura falhou.'
                : 'O servidor não finalizou o plano. Nenhuma parcela mudou.'
          }
          messages={errorMessages(finalize.error ?? finalize.postWriteError)}
          actions={
            <Button
              label="Reler o plano"
              testID="plan-reread"
              onPress={() => void reread().then(() => finalize.clear())}
            />
          }
        />
      ) : null}
      {cancelProgress ? (
        <Notice
          kind={cancelProgress.failedAt ? 'warning' : 'success'}
          testID="plan-cancel-progress"
          title={`${formatCount(cancelProgress.done.length)} ${cancelProgress.done.length === 1 ? 'parcela cancelada' : 'parcelas canceladas'} no servidor.`}
          body={
            cancelProgress.failedAt
              ? 'Um cancelamento não foi aceito ou não foi confirmado; os seguintes não foram enviados. As já canceladas continuam canceladas.'
              : undefined
          }
          messages={errorMessages(cancel.error)}
          onDismiss={() => {
            setCancelProgress(null);
            cancel.clear();
          }}
        />
      ) : null}
      {drafts.length > 0 ? (
        <Button
          label={mixed ? 'Finalizar parcelas restantes' : 'Finalizar plano'}
          testID="plan-finalize"
          loading={finalize.pending}
          disabled={finalize.blocked || cancel.blocked}
          onPress={() => setConfirm('finalize')}
        />
      ) : null}
      <View accessibilityRole="list" style={{ gap: spacing.stack.sm }}>
        {data.installments.map((row, index) => (
          <View
            key={row.id}
            ref={(node) => {
              if (node) rows.current.set(row.id, node);
            }}
            testID={`plan-installment-${row.id}`}
            style={{ gap: spacing.stack.xs }}
          >
            <AppText variant="body-strong">{`${index + 1}. ${businessDay(row.dueDate)} · ${formatBrlWire(row.value)}`}</AppText>
            <Badge kind="status" domain="transaction-status" value={row.status} />
            {row.description ? <AppText variant="helper">{row.description}</AppText> : null}
            {row.classification ? null : (
              <AppText variant="helper">Sem classificação até finalizar.</AppText>
            )}
            <Button
              label="Abrir parcela"
              variant="link"
              role="link"
              testID={`plan-open-${row.id}`}
              onPress={() => void shell.navigate(`/transacoes/${row.id}`, true, false, row.id)}
            />
            {row.status !== 'Cancelled' ? (
              <Checkbox
                testID={`plan-select-${row.id}`}
                label={`Selecionar parcela ${index + 1}`}
                checked={selected.includes(row.id)}
                disabled={busy}
                onCheckedChange={(checked) =>
                  setSelected((prev) =>
                    checked ? [...prev, row.id] : prev.filter((x) => x !== row.id),
                  )
                }
              />
            ) : null}
          </View>
        ))}
      </View>
      {selected.length ? (
        <View style={{ gap: spacing.stack.sm }}>
          <Button
            label={`Cancelar parcelas selecionadas (${formatCount(selected.length)})`}
            variant="secondary"
            testID="plan-cancel"
            disabled={busy}
            onPress={() => setConfirm('cancel')}
          />
          {manage ? (
            <Button
              label="Levar ao cofre ou depositar as selecionadas"
              variant="secondary"
              testID="plan-batch"
              disabled={busy}
              onPress={() => setBatch(true)}
            />
          ) : null}
        </View>
      ) : null}
      <ConfirmDialog
        visible={confirm === 'finalize'}
        testID="plan-finalize-dialog"
        title={mixed ? 'Finalizar as parcelas restantes?' : 'Finalizar o plano?'}
        consequence={`${formatCount(drafts.length)} ${drafts.length === 1 ? 'parcela em rascunho passa' : 'parcelas em rascunho passam'} a ativas juntas, num único envio (${formatBrl(asCents(draftTotal))}). Se uma falhar, nenhuma muda.`}
        confirmLabel={mixed ? 'Finalizar parcelas restantes' : 'Finalizar plano'}
        busyLabel="Finalizando…"
        busy={finalize.pending}
        onConfirm={sendFinalize}
        onDismiss={() => setConfirm(null)}
      >
        {drafts.map((row) => (
          <AppText
            key={row.id}
            variant="helper"
          >{`${businessDay(row.dueDate)} · ${formatBrlWire(row.value)}`}</AppText>
        ))}
      </ConfirmDialog>
      <ConfirmDialog
        visible={confirm === 'cancel'}
        testID="plan-cancel-dialog"
        title="Cancelar as parcelas selecionadas?"
        consequence="Cada parcela é cancelada separadamente, nesta ordem. Se uma for recusada, as seguintes não são enviadas; as já canceladas continuam canceladas."
        confirmLabel="Cancelar parcelas"
        busyLabel="Cancelando…"
        destructive
        busy={cancel.pending}
        onConfirm={() => {
          if (reason.trim()) void sendCancel();
        }}
        onDismiss={() => setConfirm(null)}
      >
        {data.installments
          .filter((row) => selected.includes(row.id))
          .map((row) => (
            <AppText
              key={row.id}
              variant="helper"
            >{`${businessDay(row.dueDate)} · ${formatBrlWire(row.value)}`}</AppText>
          ))}
        <FormField label="Motivo do cancelamento" required>
          <TextField
            kind="textarea"
            testID="plan-cancel-reason"
            value={reason}
            maxLength={500}
            onChangeText={setReason}
          />
        </FormField>
      </ConfirmDialog>
      {batch ? (
        <ChequeBatch
          ids={selected}
          onClose={(changed) => {
            setBatch(false);
            if (changed) {
              setSelected([]);
              void reread();
            }
          }}
        />
      ) : null}
    </>
  );
}
