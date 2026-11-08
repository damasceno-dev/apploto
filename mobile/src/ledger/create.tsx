import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useLocalSearchParams } from 'expo-router';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getTransactionTransactionId,
  postTransactionPreview,
  postTransactionTransactionIdFinalize,
  toIfMatch,
  type ResponseCreateTransactionJson,
} from '@loto/api';
import {
  agingBucketLabels,
  dailyCloseStatusLabels,
  formatBrlWire,
  generatedMovementMarker,
  parseCalendarDate,
} from '@loto/core';
import { useSession, useScopedApiClient } from '@/session/provider';
import { useShell } from '@/shell/provider';
import {
  AppText,
  Badge,
  Button,
  Card,
  DateField,
  FormField,
  Notice,
  Skeleton,
  useToast,
} from '@/ui';
import { RetainedNotice, useAdminDraft } from '@/admin/common';
import { digitsFromWire } from '@/day/contracts';
import { useTimeCommand } from '@/time/common';
import {
  IntentNotices,
  elevated,
  errorMessages,
  failureCause,
  useAccounts,
  useClients,
  useFinancialIntent,
  useOperators,
  usePreview,
  useTypes,
} from './common';
import {
  accountsForType,
  cashboxOf,
  createMissing,
  createRequest,
  creatableTypes,
  day,
  emptyCreate,
  impactLines,
  isChequeRuleType,
  isCreateInput,
  reconcileChoices,
  type CreateInput,
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

export const nowTime = (clock: { branchLocalDateTime: string } | null | undefined) =>
  clock ? clock.branchLocalDateTime.slice(11, 16) : null;

/**
 * Transaction — Create (fast entry): type-driven form, automatic non-blocking preview, the atomic
 * inline opening when the cashbox has no turno, a durable idempotent create and a separate
 * explicit **Finalizar** for a Draft. Returning to this form never sends anything.
 */
export function CreateScreen() {
  // Each confirmed create ends its form: **Nova transação** mounts a fresh one, with its own device
  // retention and intent (the previous draft was acknowledged and closed with the receipt).
  const [form, setForm] = useState<{ generation: number; accountId: string | null }>({
    generation: 0,
    accountId: null,
  });
  return (
    <CreateForm
      key={form.generation}
      nextAccount={form.accountId}
      routeInput={form.generation === 0}
      onNew={(accountId) => setForm((f) => ({ generation: f.generation + 1, accountId }))}
    />
  );
}
function CreateForm({
  nextAccount,
  routeInput,
  onNew,
}: {
  nextAccount: string | null;
  /** Only the first form takes the route's copyOf/returned client; a later one starts clean. */
  routeInput: boolean;
  onNew: (accountId: string) => void;
}) {
  const params = useLocalSearchParams<{ accountId?: string; clientId?: string; copyOf?: string }>();
  const { snapshot, engine } = useSession();
  const shell = useShell();
  const client = useScopedApiClient();
  const queries = useQueryClient();
  const toast = useToast();
  const today = snapshot.clock!.branchLocalDate;
  const role = snapshot.branch!.role;
  const manage = elevated(role);
  const own = snapshot.selfContext?.operatorId ?? null;
  const types = useTypes();
  const accounts = useAccounts();
  const clients = useClients();
  const operators = useOperators();
  const draft = useAdminDraft('ledger/create', isCreateInput, async () => {
    await engine.verifyEntry();
    return engine.getSnapshot().status === 'branch';
  });
  const [input, setInputState] = useState<CreateInput>(() => ({
    ...emptyCreate(today, nowTime(snapshot.clock)),
    accountId: nextAccount ?? (typeof params.accountId === 'string' ? params.accountId : null),
  }));
  /** Device retention starts with the first real change; the current input is kept after each. */
  const [touched, setTouched] = useState(false);
  const latest = useRef(input);
  const { keep } = draft;
  useEffect(() => {
    latest.current = input;
    if (touched) void keep(input, null);
  }, [input, touched, keep]);
  const setInput = useCallback((update: CreateInput | ((previous: CreateInput) => CreateInput)) => {
    setTouched(true);
    setInputState(update);
  }, []);
  const [cleared, setCleared] = useState<readonly string[]>([]);
  const [receipt, setReceipt] = useState<ResponseCreateTransactionJson | null>(null);
  const intent = useFinancialIntent('ledger/create', async (data) => {
    const created = data as ResponseCreateTransactionJson;
    setReceipt(created);
    toast.show(created.status === 'Draft' ? 'Rascunho registrado.' : 'Transação registrada.');
    await draft.discard();
    // The confirmed receipt stays even when the follow-up reads fail.
    void queries.invalidateQueries({ queryKey: ['ledger', engine.getSnapshot().scope] });
    void queries.invalidateQueries({ queryKey: ['day', engine.getSnapshot().scope] });
  });
  // A client created in Clientes comes back fresh in the route: select it, keep everything else.
  const returnedClient = routeInput && typeof params.clientId === 'string' ? params.clientId : null;
  const [adoptedClient, setAdoptedClient] = useState<string | null>(null);
  if (returnedClient && returnedClient !== adoptedClient) {
    setAdoptedClient(returnedClient);
    // Not an edit: the device copy typed before leaving must still be readable for restoration.
    setInputState((prev) => ({ ...prev, clientId: returnedClient }));
  }
  // Coming back from Clientes the router may open a fresh form: the values typed before leaving
  // are this person's validated device copy, restored for review (nothing is sent) with the new
  // client on top. Any other retained copy keeps the explicit Restaurar offer.
  const [restoredForReturn, setRestoredForReturn] = useState(false);
  if (returnedClient && draft.retained && !restoredForReturn) {
    const retained = draft.retained.input;
    setRestoredForReturn(true);
    draft.dismissRetained();
    setInput({ ...retained, clientId: returnedClient });
  }
  // The list read before the client existed must not clear it: read it again after the return,
  // and judge the returned client only against that fresh list.
  const [clientsReadFor, setClientsReadFor] = useState<string | null>(null);
  const { refetch: refetchClients } = clients;
  useEffect(() => {
    if (!adoptedClient) return;
    let active = true;
    void refetchClients().then(() => {
      if (active) setClientsReadFor(adoptedClient);
    });
    return () => {
      active = false;
    };
  }, [adoptedClient, refetchClients]);
  const clientsCurrent = !adoptedClient || clientsReadFor === adoptedClient;
  // "Corrigir valor/conta/tipo/data": a new transaction from a fresh read of the cancelled one.
  const copyOf = routeInput && typeof params.copyOf === 'string' ? params.copyOf : null;
  const source = useQuery({
    queryKey: ['ledger', snapshot.scope, 'copy-of', copyOf],
    queryFn: async () => (await getTransactionTransactionId(copyOf!, { client })).data,
    enabled: !!copyOf,
    retry: false,
  });
  const [copied, setCopied] = useState<string | null>(null);
  if (source.data && copied !== source.data.id) {
    const row = source.data;
    setCopied(row.id);
    setInput((prev) => ({
      ...prev,
      typeId: row.transactionTypeId,
      accountId: row.accountId,
      value: digitsFromWire(row.value),
      clientId: row.clientId ?? null,
      description: row.description ?? '',
      dueDate: null,
    }));
  }
  const creatable = useMemo(() => creatableTypes(types.data ?? []), [types.data]);
  const type = creatable.find((t) => t.id === input.typeId) ?? null;
  const offered = accountsForType(type, accounts.data ?? []);
  const account = offered.find((a) => a.id === input.accountId) ?? null;
  // Retained input never silently changes meaning: a choice no longer offered is cleared and named.
  if (types.status === 'success' && accounts.status === 'success') {
    const result = reconcileChoices(input, {
      types: new Set(creatable.map((t) => t.id)),
      accounts: new Set(offered.map((a) => a.id)),
      clients:
        clients.data && clientsCurrent && !clients.isFetching
          ? new Set(clients.data.map((c) => c.id))
          : null,
    });
    if (result.cleared.length) {
      setCleared(result.cleared);
      setInput(result.input);
    }
  }
  const cashbox = cashboxOf(account);
  const cashboxAccount =
    cashbox.kind === 'cashbox'
      ? ((accounts.data ?? []).find((a) => a.id === cashbox.id) ?? null)
      : null;
  const binding = useSessionBinding({
    cashboxId: cashbox.kind === 'cashbox' ? cashbox.id : null,
    cashboxName: cashboxAccount?.name ?? 'o caixa pareado',
    date: input.date,
    testID: 'create',
  });
  const onBehalfAllowed = manage && shell.mode === 'management';
  const operatorRequired = manage && !own;
  const missing = createMissing(input, type, { operatorRequired });
  const bindingBlocks = ['loading', 'error', 'blocked', 'incomplete'].includes(binding.status);
  // The preview checks the turno like the write: without the confirmed opening it would only be
  // refused, so it waits for the binding instead of showing a misleading failure.
  const previewMissing = bindingBlocks ? [...missing, 'conferir o turno do caixa'] : missing;
  const candidate =
    previewMissing.length === 0 && cashbox.kind !== 'unpaired'
      ? createRequest(input, binding.binding, false)
      : null;
  const signature = candidate ? JSON.stringify(candidate) : null;
  const preview = usePreview('create', signature, (signal) =>
    postTransactionPreview(candidate!, undefined, { client, signal }).then((r) => r.data),
  );
  const finalize = useTimeCommand();
  const [finalized, setFinalized] = useState<string | null>(null);
  const send = async (saveAsDraft: boolean) => {
    if (missing.length || bindingBlocks || cashbox.kind === 'unpaired' || intent.blocked) return;
    // What was on screen at the tap is what is sent, never input typed during the turno check.
    const tapped = input;
    const fresh = await binding.prepare();
    if (!fresh) return;
    await intent.submit({
      operation: 'transaction',
      payload: createRequest(tapped, fresh, saveAsDraft),
    });
  };
  if (receipt)
    return (
      <TransactionReceipt
        receipt={receipt}
        finalize={finalize}
        finalized={finalized === receipt.id}
        onFinalize={() =>
          void finalize.run(
            () =>
              postTransactionTransactionIdFinalize(
                receipt.id,
                { 'If-Match': toIfMatch(receipt.version) },
                { client: engine.client() },
              ),
            () => {
              setFinalized(receipt.id);
              void queries.invalidateQueries({ queryKey: ['ledger', snapshot.scope] });
              void queries.invalidateQueries({ queryKey: ['day', snapshot.scope] });
            },
          )
        }
        onNew={() => onNew(receipt.accountId)}
        onOpen={() => void shell.navigate(`/transacoes/${receipt.id}`, true)}
        onBack={() => void shell.back()}
      />
    );
  return (
    <>
      <RetainedNotice
        visible={!!draft.retained}
        onRestore={() => {
          const value = draft.retained!.input;
          draft.dismissRetained();
          setInput(value);
        }}
      />
      {draft.problem ? <Notice kind="warning" title={draft.problem} /> : null}
      {cleared.length ? (
        <Notice
          kind="warning"
          testID="create-cleared"
          title={`Escolha de novo: ${cleared.join(', ')}.`}
          body="O que estava escolhido não está mais disponível para você. O resto foi mantido."
          onDismiss={() => setCleared([])}
        />
      ) : null}
      {copyOf ? (
        source.status === 'error' ? (
          <Notice
            kind="danger"
            title="Não foi possível ler a transação de origem."
            messages={errorMessages(source.error)}
          />
        ) : (
          <Notice
            kind="info"
            testID="create-copy"
            title="Nova transação a partir de uma cancelada."
            body="Confira tipo, conta, valor e data: esta é uma operação nova e separada do cancelamento."
          />
        )
      ) : null}
      <IntentNotices
        intent={intent}
        testID="create-intent"
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
      <Card title="Dados da transação">
        <TypeField
          testID="create-type"
          types={creatable}
          value={input.typeId}
          status={types.status}
          retry={() => void types.refetch()}
          onChange={(typeId) => setInput((prev) => ({ ...prev, typeId }))}
        />
        <AccountField
          testID="create-account"
          accounts={offered}
          lookup={accounts}
          value={input.accountId}
          onChange={(accountId) => setInput((prev) => ({ ...prev, accountId }))}
          helper={
            type?.requiresTabAccountAndClient
              ? 'Venda fiado: escolha a conta fiado do cliente.'
              : undefined
          }
        />
        {type?.requiresTabAccountAndClient ? (
          <Notice
            kind="info"
            testID="create-fiado-pair"
            title="Venda fiado no balcão"
            body={
              cashbox.kind === 'unpaired'
                ? 'Esta conta fiado não tem um caixa pareado disponível para você. Sem ele a venda não pode ser registrada.'
                : `Ao salvar, a saída do caixa ${cashboxAccount?.name ?? 'pareado'} é registrada automaticamente na mesma operação. Não lance essa saída de novo.`
            }
          />
        ) : null}
        <AmountField
          testID="create-value"
          value={input.value}
          onChange={(value) => setInput((prev) => ({ ...prev, value }))}
          buffer={input.buffers.value}
          onBuffer={(raw) =>
            setInput((prev) => {
              const buffers = { ...prev.buffers };
              if (raw === undefined) delete buffers.value;
              else buffers.value = raw;
              return { ...prev, buffers };
            })
          }
        />
        <DateTimeFields
          testID="create"
          date={input.date}
          time={input.time}
          onDate={(date) => setInput((prev) => ({ ...prev, date }))}
          onTime={(time) => setInput((prev) => ({ ...prev, time }))}
        />
        {type && isChequeRuleType(type) ? (
          <>
            <FormField label="Bom para" optional helper="Data em que o cheque pode ser depositado.">
              <DateField
                testID="create-due"
                today={parseCalendarDate(today) ?? undefined}
                value={input.dueDate ? parseCalendarDate(input.dueDate) : null}
                onValueChange={(dueDate) => setInput((prev) => ({ ...prev, dueDate }))}
              />
            </FormField>
            <Button
              label="Vários cheques pré-datados? Usar o plano de cheques"
              variant="link"
              role="link"
              testID="create-to-plan"
              onPress={() => void shell.navigate('/transacoes/cheque-pre-datado/novo', true)}
            />
          </>
        ) : null}
        <ClientField
          testID="create-client"
          clients={clients.data}
          status={clients.status}
          retry={() => void clients.refetch()}
          required={!!type?.requiresTabAccountAndClient}
          value={input.clientId}
          onChange={(clientId) => setInput((prev) => ({ ...prev, clientId }))}
        />
        {input.clientId && type?.requiresTabAccountAndClient ? (
          <Button
            label="Ver extrato deste cliente"
            variant="link"
            role="link"
            onPress={() => void shell.navigate(`/clientes/${input.clientId}/extrato`, true)}
          />
        ) : null}
        <DescriptionField
          testID="create-description"
          value={input.description}
          onChange={(description) => setInput((prev) => ({ ...prev, description }))}
        />
        {onBehalfAllowed || operatorRequired ? (
          <OperatorField
            testID="create-operator"
            operators={operators.data}
            status={operators.status}
            required={operatorRequired}
            value={input.operatorId}
            onChange={(operatorId) => setInput((prev) => ({ ...prev, operatorId }))}
          />
        ) : null}
      </Card>
      {binding.view}
      <PreviewCard
        preview={preview}
        complete={!!candidate}
        missing={previewMissing}
        opening={binding.binding.kind === 'opening' ? (cashboxAccount?.name ?? 'o caixa') : null}
      />
      <Button
        label="Salvar"
        variant="cta"
        testID="create-save"
        fullWidth
        loading={intent.state.phase === 'sending'}
        loadingLabel="Salvando…"
        disabled={
          !!missing.length || bindingBlocks || cashbox.kind === 'unpaired' || intent.blocked
        }
        disabledReason={
          missing.length
            ? `Falta ${missing.join(', ')}.`
            : bindingBlocks
              ? 'Confira o turno do caixa acima.'
              : undefined
        }
        onPress={() => void send(false)}
      />
      <Button
        label="Salvar como rascunho"
        variant="secondary"
        testID="create-save-draft"
        fullWidth
        disabled={
          !!missing.length || bindingBlocks || cashbox.kind === 'unpaired' || intent.blocked
        }
        onPress={() => void send(true)}
      />
    </>
  );
}

/** The server's hypothetical impact for the complete candidate; never blocks and never saves. */
export function PreviewCard({
  preview,
  complete,
  missing,
  opening = null,
}: {
  preview: ReturnType<
    typeof usePreview<{ impact: Parameters<typeof impactLines>[0]; warnings: string[] }>
  >;
  complete: boolean;
  missing: readonly string[];
  /** The cashbox whose turno opens with this write: the hypothetical has no session yet. */
  opening?: string | null;
}) {
  if (!complete)
    return (
      <AppText variant="helper" testID="create-preview-idle">
        {missing.length
          ? `Prévia do impacto quando completar: ${missing.join(', ')}.`
          : 'Prévia do impacto indisponível.'}
      </AppText>
    );
  if (preview.pending) return <Skeleton label="Calculando a prévia no servidor…" />;
  if (preview.error)
    return (
      <Notice
        kind="info"
        blocking={false}
        testID="create-preview-error"
        title="Prévia indisponível. Você ainda pode salvar."
        body={failureCause(preview.error)}
        messages={errorMessages(preview.error)}
      />
    );
  if (!preview.data) return null;
  const lines = impactLines(preview.data.impact, agingBucketLabels, dailyCloseStatusLabels).map(
    (line) =>
      opening && line.key === 'cash-none'
        ? {
            key: 'cash-opening',
            text: `Entra no turno de ${opening}, aberto com esta operação. A diferença só aparece depois da conferência da contagem.`,
          }
        : line,
  );
  return (
    <Card title="Prévia do impacto (nada foi salvo)" testID="create-preview">
      {lines.map((line) => (
        <AppText key={line.key} testID={`create-preview-${line.key}`}>
          {line.text}
        </AppText>
      ))}
      {preview.data.warnings.map((warning) => (
        <Notice key={warning} kind="warning" blocking={false} title={warning} />
      ))}
    </Card>
  );
}

/** A confirmed financial result, kept through follow-up read/navigation/storage failures. */
export function TransactionReceipt({
  receipt,
  finalize,
  finalized,
  onFinalize,
  onNew,
  onOpen,
  onBack,
  title,
}: {
  receipt: ResponseCreateTransactionJson;
  finalize?: ReturnType<typeof useTimeCommand>;
  finalized?: boolean;
  onFinalize?: () => void;
  onNew?: () => void;
  onOpen: () => void;
  onBack: () => void;
  title?: string;
}) {
  const draft = receipt.status === 'Draft' && !finalized;
  return (
    <Card
      title={
        title ?? (draft ? 'Rascunho registrado no servidor' : 'Transação registrada no servidor')
      }
      testID="ledger-receipt"
    >
      <AppText testID="ledger-receipt-value">{`${formatBrlWire(receipt.value)} · ${day(receipt.date)?.split('-').reverse().join('/')}`}</AppText>
      <Badge
        kind="status"
        domain="transaction-status"
        value={finalized ? 'Active' : receipt.status}
        context="Situação:"
      />
      {receipt.generatedTransactionId ? (
        <AppText testID="ledger-receipt-generated">{`Movimentação ${generatedMovementMarker.toLowerCase()} registrada junto, na mesma operação.`}</AppText>
      ) : null}
      {receipt.startedSessionId ? (
        <AppText testID="ledger-receipt-started">
          O turno do caixa foi iniciado com esta operação.
        </AppText>
      ) : null}
      {receipt.openingDiscrepancyId ? (
        <AppText>
          A abertura foi registrada com diferença em relação ao proposto; a gerência confere.
        </AppText>
      ) : null}
      {draft ? (
        <>
          <AppText variant="helper">
            Um rascunho não entra no caixa nem nos relatórios até ser finalizado.
          </AppText>
          {finalize?.postWriteError || finalize?.uncertain || finalize?.error ? (
            <Notice
              kind={finalize.uncertain ? 'warning' : 'danger'}
              title={
                finalize.uncertain
                  ? 'Não foi possível confirmar a finalização. Abra a transação para conferir; nada será reenviado.'
                  : 'A finalização não foi aceita.'
              }
              messages={errorMessages(finalize.error)}
            />
          ) : null}
          {onFinalize ? (
            <Button
              label="Finalizar agora"
              testID="ledger-receipt-finalize"
              loading={finalize?.pending}
              disabled={finalize?.blocked}
              onPress={onFinalize}
            />
          ) : null}
        </>
      ) : finalized ? (
        <AppText testID="ledger-receipt-finalized">Finalizada no servidor.</AppText>
      ) : null}
      <Button
        label="Abrir transação"
        variant="secondary"
        testID="ledger-receipt-open"
        onPress={onOpen}
      />
      {onNew ? (
        <Button
          label="Nova transação"
          variant="secondary"
          testID="ledger-receipt-new"
          onPress={onNew}
        />
      ) : null}
      <Button
        label="Voltar"
        variant="link"
        role="link"
        testID="ledger-receipt-back"
        onPress={onBack}
      />
    </Card>
  );
}
