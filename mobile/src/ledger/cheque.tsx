import { useState } from 'react';
import { View } from 'react-native';
import { useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getReceivableTransactionId,
  type ResponseChequeOperationJson,
  type ResponseReceivableJson,
} from '@loto/api';
import {
  bankReceiptStatusLabels,
  centsFromWire,
  chequeLifecycleStateLabels,
  formatBrl,
  formatBrlWire,
  formatCount,
  formatDate,
  parseCalendarDate,
  receivableSettlementStatusLabels,
  settlementEventKindLabels,
  asCents,
  type CalendarDate,
} from '@loto/core';
import { useSession, useScopedApiClient } from '@/session/provider';
import { useShell } from '@/shell/provider';
import { spacing } from '@/theme/style';
import {
  AppText,
  Button,
  Card,
  DateField,
  FormField,
  Notice,
  Overlay,
  RadioGroup,
  Radio,
  Select,
  Skeleton,
  TextField,
} from '@/ui';
import {
  IntentNotices,
  errorMessages,
  useAccounts,
  useClients,
  useFinancialIntent,
  useLedgerReads,
} from './common';
import {
  chequeActions,
  convertRequest,
  day,
  identifiedClient,
  isWhole,
  returnRequest,
  type ChequeAction,
  type ChequeInput,
} from './contracts';
import type { FinancialIntent } from '@/data/operations';

const businessDay = (value: string | null | undefined) => {
  const date = value ? parseCalendarDate(day(value)!) : null;
  return date ? formatDate(date) : '—';
};
const actionLabels: Record<ChequeAction, string> = {
  custody: 'Levar ao cofre',
  deposit: 'Depositar',
  return: 'Registrar devolução',
  represent: 'Reapresentar',
  convert: 'Converter em fiado',
};
const consequence: Record<ChequeAction, string> = {
  custody: 'Guarda o cheque no cofre. Não é pagamento: o valor continua a receber.',
  deposit:
    'Registra o depósito e cria um recebimento bancário pendente. O dinheiro só é confirmado quando o extrato do banco for conciliado.',
  return:
    'Registra que o banco devolveu o cheque: o valor volta a ser esperado, com o cliente e a próxima data de depósito informados.',
  represent: 'Marca o cheque devolvido para ser depositado de novo.',
  convert:
    'Encerra o cheque e cria uma dívida fiado do cliente no mesmo valor, sem movimentar o caixa.',
};
const emptyInput = (today: string): ChequeInput => ({
  occurredOn: today,
  accountId: null,
  clientId: null,
  nextDate: null,
  reason: '',
  description: '',
});
const chequeKey = (id: string) => ['ledger', 'receivable', id];

/**
 * Cheque lifecycle for one concrete cheque (Manager/Admin): the receivable chain is read from the
 * server, and each command uses its exact version and one durable idempotent intent. An unknown
 * outcome is verified by the identical retry, never by sending another deposit.
 */
export function ChequeCard({
  transactionId,
  onChanged,
  blocked = false,
}: {
  transactionId: string;
  onChanged: () => void;
  /** The parent transaction read failed: keep receipts, offer no command until it succeeds. */
  blocked?: boolean;
}) {
  const { snapshot } = useSession();
  const client = useScopedApiClient();
  const reads = useLedgerReads();
  const queries = useQueryClient();
  const shell = useShell();
  const receivable = useQuery({
    queryKey: [...chequeKey(transactionId), snapshot.scope],
    queryFn: async ({ signal }) =>
      (await getReceivableTransactionId(transactionId, undefined, { client, signal })).data,
    enabled: reads,
    retry: false,
    staleTime: 0,
  });
  const [action, setAction] = useState<ChequeAction | null>(null);
  const [result, setResult] = useState<ResponseChequeOperationJson | null>(null);
  // Confirmed from the first send or from an explicit verification: one receipt, then a fresh read.
  const intent = useFinancialIntent(
    `ledger/cheque/${transactionId}`,
    async (data) => {
      setAction(null);
      setResult(data as ResponseChequeOperationJson);
      await queries.invalidateQueries({ queryKey: ['ledger'] });
      await receivable.refetch();
      onChanged();
    },
    true,
  );
  const accounts = useAccounts();
  const nameOf = (id: string | null | undefined) =>
    (accounts.data ?? []).find((a) => a.id === id)?.name ?? (id ? 'Conta' : '—');
  // A confirmed command's receipt survives any later read failure; without a current read no
  // further command is offered (the last known state is not presented as current).
  const resultView = result ? (
    <ChequeResult
      result={result}
      onOpenConverted={(id) => void shell.navigate(`/transacoes/${id}`, true)}
    />
  ) : null;
  if (receivable.status === 'pending')
    return resultView ? (
      <Card title="Cheque" testID="cheque-card">
        {resultView}
        <Skeleton label="Relendo a situação do cheque…" />
      </Card>
    ) : (
      <Skeleton label="Carregando a situação do cheque…" />
    );
  if (receivable.status === 'error')
    return (
      <Card title="Cheque" testID="cheque-card">
        {resultView}
        <Notice
          kind={resultView ? 'warning' : 'danger'}
          testID="cheque-error"
          title={
            resultView
              ? 'A operação foi registrada, mas a situação atual do cheque não pôde ser relida.'
              : 'Não foi possível carregar a situação do cheque.'
          }
          body={
            resultView
              ? 'Nada será enviado de novo. Novas ações ficam disponíveis depois de reler.'
              : undefined
          }
          messages={errorMessages(receivable.error)}
          actions={<Button label="Reler o cheque" onPress={() => void receivable.refetch()} />}
        />
      </Card>
    );
  const data = receivable.data;
  if (!data.isCheque) return null;
  const available = chequeActions(data.chequeState);
  const reread = async () => {
    await receivable.refetch();
    onChanged();
  };
  return (
    <Card title="Cheque" testID="cheque-card">
      <AppText testID="cheque-state" variant="body-strong">
        {chequeLifecycleStateLabels[data.chequeState]}
      </AppText>
      <AppText>{`${receivableSettlementStatusLabels[data.status]} · a receber ${formatBrlWire(data.outstandingValue)} de ${formatBrlWire(data.value)}`}</AppText>
      <AppText variant="helper">{`Bom para ${businessDay(data.dueDate)} · situação em ${businessDay(data.cutoffDate)} (por data de lançamento)`}</AppText>
      {data.custodyAccountId ? (
        <AppText variant="helper">{`Guardado em: ${nameOf(data.custodyAccountId)}`}</AppText>
      ) : null}
      {data.depositAttempts > 0 ? (
        <AppText variant="helper">{`Depósitos feitos: ${formatCount(data.depositAttempts)}`}</AppText>
      ) : null}
      <Events receivable={data} nameOf={nameOf} />
      {resultView}
      <IntentNotices
        intent={intent}
        testID="cheque-intent"
        verify={<Button label="Reler o cheque" variant="secondary" onPress={() => void reread()} />}
      />
      {available.length === 0 ? (
        <AppText variant="helper">Nenhuma ação disponível para este cheque agora.</AppText>
      ) : (
        <View
          style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.size['touch-target-gap'] }}
        >
          {available.map((key) => (
            <Button
              key={key}
              label={actionLabels[key]}
              variant="secondary"
              testID={`cheque-action-${key}`}
              disabled={intent.blocked || blocked}
              onPress={() => {
                setResult(null);
                setAction(key);
              }}
            />
          ))}
        </View>
      )}
      {action ? (
        <ChequeActionSheet
          action={action}
          receivable={data}
          intent={intent}
          blocked={blocked}
          onClose={() => setAction(null)}
        />
      ) : null}
    </Card>
  );
}

function Events({
  receivable,
  nameOf,
}: {
  receivable: ResponseReceivableJson;
  nameOf: (id: string | null | undefined) => string;
}) {
  if (receivable.events.length === 0)
    return (
      <AppText variant="helper">
        Nenhum evento registrado: o cheque está no caixa onde foi recebido.
      </AppText>
    );
  return (
    <View accessibilityRole="list" style={{ gap: spacing.stack.xs }} testID="cheque-events">
      {receivable.events.map((event) => (
        <View key={event.id} accessibilityRole="text" style={{ gap: spacing.stack.xs }}>
          <AppText variant="body-strong">{`${event.sequence}. ${settlementEventKindLabels[event.kind]} · ${formatBrlWire(event.amount)}`}</AppText>
          <AppText variant="helper">
            {[
              `Ocorrido em ${businessDay(event.occurredOn)}`,
              event.postedOn !== event.occurredOn
                ? `lançado em ${businessDay(event.postedOn)}`
                : null,
              event.isLatePosting ? 'lançamento atrasado' : null,
              event.custodyToAccountId ? `para ${nameOf(event.custodyToAccountId)}` : null,
              event.nextExpectedDepositDate
                ? `novo depósito previsto ${businessDay(event.nextExpectedDepositDate)}`
                : null,
              event.returnReason ? `alínea/motivo: ${event.returnReason}` : null,
            ]
              .filter(Boolean)
              .join(' · ')}
          </AppText>
        </View>
      ))}
    </View>
  );
}

function ChequeResult({
  result,
  onOpenConverted,
}: {
  result: ResponseChequeOperationJson;
  onOpenConverted: (id: string) => void;
}) {
  return (
    <Notice
      kind="success"
      testID="cheque-result"
      title="Registrado no servidor."
      body={[
        ...result.receivables.map(
          (r) => `Cheque agora: ${chequeLifecycleStateLabels[r.chequeState]}.`,
        ),
        result.bankReceipt
          ? `Recebimento bancário de ${formatBrlWire(result.bankReceipt.amount)} em ${result.bankReceipt.bankAccountName}: ${bankReceiptStatusLabels[result.bankReceipt.status].toLowerCase()}.`
          : null,
        result.returnBankReceipt
          ? `Devolução de ${formatBrlWire(result.returnBankReceipt.amount)} registrada no banco conciliado.`
          : null,
        result.convertedTransactionId
          ? 'Dívida fiado criada para o cliente, sem movimentar o caixa.'
          : null,
      ]
        .filter(Boolean)
        .join(' ')}
      actions={
        result.convertedTransactionId ? (
          <Button
            label="Abrir a dívida fiado"
            variant="link"
            role="link"
            onPress={() => onOpenConverted(result.convertedTransactionId!)}
          />
        ) : undefined
      }
    />
  );
}

function ChequeActionSheet({
  action,
  receivable,
  intent,
  onClose,
  blocked,
}: {
  action: ChequeAction;
  receivable: ResponseReceivableJson;
  intent: ReturnType<typeof useFinancialIntent>;
  onClose: () => void;
  /** The parent transaction read failed after the sheet opened: nothing is sent. */
  blocked: boolean;
}) {
  const { snapshot } = useSession();
  const today = snapshot.clock!.branchLocalDate;
  const accounts = useAccounts();
  const clients = useClients();
  const identified = identifiedClient(receivable);
  const [input, setInput] = useState<ChequeInput>(() => ({
    ...emptyInput(today),
    clientId: identified,
  }));
  const accountType =
    action === 'custody'
      ? 'Safe'
      : action === 'deposit'
        ? 'BankAccount'
        : action === 'convert'
          ? 'Tab'
          : null;
  const options = (accounts.data ?? []).filter((a) => a.type === accountType);
  const whole = isWhole(receivable);
  const missing =
    !input.occurredOn ||
    (accountType && !input.accountId) ||
    ((action === 'return' || action === 'convert') && !input.clientId) ||
    (action === 'return' && !input.nextDate) ||
    ((action === 'deposit' || action === 'convert') && !whole);
  const item = { transactionId: receivable.transactionId, expectedVersion: receivable.version };
  const build = (): FinancialIntent => {
    switch (action) {
      case 'custody':
        return {
          operation: 'cheque-custody',
          payload: {
            safeAccountId: input.accountId!,
            occurredOn: input.occurredOn!,
            items: [item],
          },
        };
      case 'deposit':
        return {
          operation: 'cheque-deposit',
          payload: {
            bankAccountId: input.accountId!,
            occurredOn: input.occurredOn!,
            description: input.description.trim() || null,
            items: [item],
          },
        };
      case 'return':
        return {
          operation: 'cheque-return',
          target: receivable.transactionId,
          payload: returnRequest(receivable, input),
        };
      case 'represent':
        return {
          operation: 'cheque-represent',
          target: receivable.transactionId,
          payload: { expectedVersion: receivable.version, occurredOn: input.occurredOn! },
        };
      case 'convert':
        return {
          operation: 'cheque-convert',
          target: receivable.transactionId,
          payload: convertRequest(receivable, input),
        };
    }
  };
  const sending = intent.state.phase === 'sending';
  return (
    <Overlay
      visible
      variant="bottom-sheet"
      title={actionLabels[action]}
      description={consequence[action]}
      busy={sending}
      onDismiss={onClose}
      testID={`cheque-sheet-${action}`}
      footer={
        <>
          <Button
            label={`${actionLabels[action]} — cheque de ${formatBrlWire(receivable.value)}`}
            testID="cheque-sheet-confirm"
            loading={sending}
            disabled={!!missing || intent.blocked || blocked}
            disabledReason={
              blocked ? 'A transação não pôde ser relida: nada é enviado até reler.' : undefined
            }
            onPress={() => {
              if (blocked || missing || intent.blocked) return;
              void intent.submit(build());
            }}
          />
          <Button label="Voltar" variant="secondary" disabled={sending} onPress={onClose} />
        </>
      }
    >
      {!whole && (action === 'deposit' || action === 'convert') ? (
        <Notice
          kind="warning"
          title="O cheque não está inteiro em aberto: esta ação vale só para o valor total do cheque."
        />
      ) : null}
      {accountType ? (
        <FormField
          label={
            accountType === 'Safe'
              ? 'Cofre'
              : accountType === 'BankAccount'
                ? 'Conta bancária'
                : 'Conta fiado'
          }
          required
        >
          <Select
            testID="cheque-sheet-account"
            value={input.accountId}
            onValueChange={(accountId) => setInput((p) => ({ ...p, accountId }))}
            emptyText="Nenhuma conta deste tipo cadastrada."
            options={options.map((a) => ({ value: a.id, label: a.name }))}
          />
        </FormField>
      ) : null}
      {action === 'return' || action === 'convert' ? (
        <FormField
          label="Cliente"
          required
          readOnly={!!identified}
          readOnlyReason={identified ? 'O cliente já foi identificado neste cheque.' : undefined}
        >
          <Select
            testID="cheque-sheet-client"
            searchable
            resultNoun="clientes"
            value={input.clientId}
            onValueChange={(clientId) => setInput((p) => ({ ...p, clientId }))}
            options={(clients.data ?? []).map((c) => ({ value: c.id, label: c.name }))}
          />
        </FormField>
      ) : null}
      {action === 'return' ? (
        <>
          <FormField label="Próxima data de depósito" required>
            <DateField
              testID="cheque-sheet-next"
              today={parseCalendarDate(today) ?? undefined}
              value={input.nextDate ? (parseCalendarDate(input.nextDate) as CalendarDate) : null}
              onValueChange={(nextDate) => setInput((p) => ({ ...p, nextDate }))}
            />
          </FormField>
          <FormField label="Alínea ou motivo" optional>
            <TextField
              testID="cheque-sheet-reason"
              value={input.reason}
              maxLength={100}
              onChangeText={(reason) => setInput((p) => ({ ...p, reason }))}
            />
          </FormField>
        </>
      ) : null}
      <FormField
        label="Data do evento"
        required
        helper="Quando aconteceu (pode ser antes de hoje)."
      >
        <DateField
          testID="cheque-sheet-occurred"
          today={parseCalendarDate(today) ?? undefined}
          value={input.occurredOn ? (parseCalendarDate(input.occurredOn) as CalendarDate) : null}
          onValueChange={(occurredOn) => setInput((p) => ({ ...p, occurredOn }))}
        />
      </FormField>
      {action === 'deposit' || action === 'convert' ? (
        <FormField label="Descrição" optional>
          <TextField
            testID="cheque-sheet-description"
            value={input.description}
            maxLength={500}
            onChangeText={(description) => setInput((p) => ({ ...p, description }))}
          />
        </FormField>
      ) : null}
      <IntentNotices intent={intent} testID="cheque-sheet-intent" />
    </Overlay>
  );
}

/**
 * Batch custody or deposit for explicitly selected cheques: each one is re-read for its current
 * state and version; only eligible whole cheques are sent, all or none, under one durable intent.
 */
export function ChequeBatch({
  ids,
  onClose,
}: {
  ids: readonly string[];
  onClose: (changed: boolean) => void;
}) {
  const { snapshot } = useSession();
  const client = useScopedApiClient();
  const today = snapshot.clock!.branchLocalDate;
  const accounts = useAccounts();
  const [result, setResult] = useState<ResponseChequeOperationJson | null>(null);
  const intent = useFinancialIntent('ledger/cheque-batch', (data) =>
    setResult(data as ResponseChequeOperationJson),
  );
  const reads = useQueries({
    queries: ids.map((id) => ({
      queryKey: [...chequeKey(id), snapshot.scope, 'batch'],
      queryFn: async ({ signal }: { signal: AbortSignal }) =>
        (await getReceivableTransactionId(id, undefined, { client, signal })).data,
      retry: false,
      staleTime: 0,
    })),
  });
  const [kind, setKind] = useState<'custody' | 'deposit'>('custody');
  const [accountId, setAccountId] = useState<string | null>(null);
  const [occurredOn, setOccurredOn] = useState<string | null>(today);
  const [description, setDescription] = useState('');
  const loaded = reads.every((r) => r.status === 'success');
  const failed = reads.find((r) => r.status === 'error');
  const receivables = reads.map((r) => r.data).filter((r): r is ResponseReceivableJson => !!r);
  const eligible = receivables.filter(
    (r) =>
      r.isCheque &&
      chequeActions(r.chequeState).includes(kind) &&
      (kind === 'custody' || isWhole(r)) &&
      r.custodyAccountId !== (kind === 'custody' ? accountId : undefined),
  );
  const excluded = receivables.length - eligible.length;
  const total = eligible.reduce((sum, r) => sum + centsFromWire(r.outstandingValue), 0);
  const options = (accounts.data ?? []).filter(
    (a) => a.type === (kind === 'custody' ? 'Safe' : 'BankAccount'),
  );
  const sending = intent.state.phase === 'sending';
  const items = eligible.map((r) => ({
    transactionId: r.transactionId,
    expectedVersion: r.version,
  }));
  const submit = () =>
    void intent.submit(
      kind === 'custody'
        ? {
            operation: 'cheque-custody',
            payload: { safeAccountId: accountId!, occurredOn: occurredOn!, items },
          }
        : {
            operation: 'cheque-deposit',
            payload: {
              bankAccountId: accountId!,
              occurredOn: occurredOn!,
              description: description.trim() || null,
              items,
            },
          },
    );
  return (
    <Overlay
      visible
      variant="bottom-sheet"
      title="Cheques selecionados"
      busy={sending}
      onDismiss={() => onClose(!!result)}
      testID="cheque-batch"
      footer={
        result ? (
          <Button label="Concluir" testID="cheque-batch-done" onPress={() => onClose(true)} />
        ) : (
          <>
            <Button
              label={`${kind === 'custody' ? 'Levar ao cofre' : 'Depositar'} ${formatCount(eligible.length)} · ${formatBrl(asCents(total))}`}
              testID="cheque-batch-confirm"
              loading={sending}
              disabled={
                !loaded || eligible.length === 0 || !accountId || !occurredOn || intent.blocked
              }
              onPress={submit}
            />
            <Button
              label="Voltar"
              variant="secondary"
              disabled={sending}
              onPress={() => onClose(false)}
            />
          </>
        )
      }
    >
      {result ? (
        <ChequeResult result={result} onOpenConverted={() => undefined} />
      ) : (
        <>
          {failed ? (
            <Notice
              kind="danger"
              title="Não foi possível ler todos os cheques escolhidos."
              messages={errorMessages(failed.error)}
            />
          ) : !loaded ? (
            <Skeleton label="Conferindo cada cheque no servidor…" />
          ) : null}
          <RadioGroup
            accessibilityLabel="O que fazer"
            value={kind}
            onValueChange={(v) => {
              setKind(v as 'custody' | 'deposit');
              setAccountId(null);
            }}
          >
            <Radio value="custody" label="Levar ao cofre" description={consequence.custody} />
            <Radio value="deposit" label="Depositar no banco" description={consequence.deposit} />
          </RadioGroup>
          <FormField label={kind === 'custody' ? 'Cofre' : 'Conta bancária'} required>
            <Select
              testID="cheque-batch-account"
              value={accountId}
              onValueChange={setAccountId}
              emptyText="Nenhuma conta deste tipo cadastrada."
              options={options.map((a) => ({ value: a.id, label: a.name }))}
            />
          </FormField>
          <FormField label="Data do evento" required>
            <DateField
              testID="cheque-batch-occurred"
              today={parseCalendarDate(today) ?? undefined}
              value={occurredOn ? (parseCalendarDate(occurredOn) as CalendarDate) : null}
              onValueChange={setOccurredOn}
            />
          </FormField>
          {kind === 'deposit' ? (
            <FormField label="Descrição" optional>
              <TextField value={description} maxLength={500} onChangeText={setDescription} />
            </FormField>
          ) : null}
          {loaded && excluded > 0 ? (
            <Notice
              kind="info"
              testID="cheque-batch-excluded"
              title={`${formatCount(excluded)} ${excluded === 1 ? 'cheque não pode' : 'cheques não podem'} entrar nesta ação e ${excluded === 1 ? 'ficou' : 'ficaram'} de fora.`}
              body={receivables
                .filter((r) => !eligible.includes(r))
                .map(
                  (r) => `${formatBrlWire(r.value)} — ${chequeLifecycleStateLabels[r.chequeState]}`,
                )
                .join('; ')}
            />
          ) : null}
          <IntentNotices intent={intent} testID="cheque-batch-intent" />
        </>
      )}
    </Overlay>
  );
}
