import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useLocalSearchParams } from 'expo-router';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getClientClientId,
  getClientClientIdFiadoStatement,
  type ResponseCreateTransactionJson,
} from '@loto/api';
import {
  fiadoPaymentKindLabels,
  formatDate,
  parseCalendarDate,
  presentFiadoBalanceWire,
} from '@loto/core';
import { useSession, useScopedApiClient } from '@/session/provider';
import { useShell } from '@/shell/provider';
import {
  AppText,
  Button,
  Card,
  EmptyState,
  FormField,
  Notice,
  Radio,
  RadioGroup,
  Skeleton,
  useToast,
} from '@/ui';
import { RetainedNotice, useAdminDraft } from '@/admin/common';
import { STATEMENT_SETUP, statementAllowed } from '@/admin/clients';
import { cashboxGrants, resolveWorkstation } from '@/day/contracts';
import { useWorkstationChoice } from '@/day/workstation';
import {
  IntentNotices,
  elevated,
  errorMessages,
  useAccounts,
  useFinancialIntent,
  useLedgerReads,
  useOperators,
  useStatementAccess,
  useTypes,
} from './common';
import {
  cashboxOf,
  day,
  isPaymentInput,
  paymentMissing,
  paymentRequest,
  repaymentMethods,
  type PaymentInput,
} from './contracts';
import {
  AccountField,
  AmountField,
  DateTimeFields,
  DescriptionField,
  OperatorField,
} from './fields';
import { useSessionBinding } from './opening';
import { TransactionReceipt, nowTime } from './create';

const businessDay = (value: string | null | undefined) => {
  const date = value ? parseCalendarDate(day(value)!) : null;
  return date ? formatDate(date) : '—';
};

/** Receber pagamento: same Tab-grant access as the statement, checked before any read. */
export function PaymentScreen() {
  const allowed = useStatementAccess();
  if (allowed === null) return <Skeleton label="Conferindo seu acesso…" />;
  if (!allowed)
    return (
      <EmptyState
        kind="setup-needed"
        statement="Receber pagamento indisponível para seu perfil."
        explanation={STATEMENT_SETUP}
      />
    );
  return <Payment />;
}

/**
 * The dedicated fiado-payment command: a repayment method with NO default, the receiving Tab, an
 * explicit amount and date. Dinheiro (`CountedCash`) also writes the paired cashbox entrada in the
 * same operation (so it may need the inline opening); any other method has no cashbox leg.
 */
function Payment() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { snapshot, engine } = useSession();
  const shell = useShell();
  const client = useScopedApiClient();
  const reads = useLedgerReads();
  const queries = useQueryClient();
  const toast = useToast();
  const today = snapshot.clock!.branchLocalDate;
  const manage = elevated(snapshot.branch!.role);
  const own = snapshot.selfContext?.operatorId ?? null;
  const person = useQuery({
    queryKey: ['ledger', snapshot.scope, 'client', id],
    queryFn: async ({ signal }) => (await getClientClientId(id, { client, signal })).data,
    enabled: reads,
    retry: false,
  });
  const balance = useQuery({
    queryKey: ['ledger', snapshot.scope, 'statement', id, 'balance'],
    queryFn: async ({ signal }) =>
      (await getClientClientIdFiadoStatement(id, { Page: 1, PageSize: 1 }, { client, signal }))
        .data,
    enabled: reads,
    retry: false,
    staleTime: 0,
  });
  const types = useTypes();
  const accounts = useAccounts();
  const operators = useOperators();
  const methods = useMemo(() => repaymentMethods(types.data ?? []), [types.data]);
  const tabs = (accounts.data ?? []).filter((a) => a.type === 'Tab');
  const draft = useAdminDraft(`ledger/payment/${id}`, isPaymentInput, async () => {
    await engine.verifyEntry();
    return statementAllowed(engine.getSnapshot());
  });
  // Default receiving Tab: the workstation cashbox's paired Tab, only when it is one of your own grants.
  const workstation = useWorkstationChoice(today);
  const station =
    shell.mode === 'shift'
      ? resolveWorkstation(cashboxGrants(snapshot.selfContext), workstation.choice, today)
      : 'none';
  const stationTab = typeof station === 'object' ? (station.pairedTabAccountId ?? null) : null;
  const defaultTab =
    stationTab && memberTabGrant(snapshot.selfContext, stationTab) ? stationTab : null;
  const [input, setInputState] = useState<PaymentInput>(() => ({
    v: 1,
    accountId: null,
    methodId: null,
    value: '',
    date: today,
    time: nowTime(snapshot.clock),
    description: '',
    operatorId: null,
    buffers: {},
  }));
  const latest = useRef(input);
  const setInput = useCallback(
    (update: (prev: PaymentInput) => PaymentInput) => {
      const next = update(latest.current);
      latest.current = next;
      setInputState(next);
      void draft.keep(next, null);
    },
    [draft],
  );
  useEffect(() => {
    if (defaultTab && !latest.current.accountId) setInput((p) => ({ ...p, accountId: defaultTab }));
  }, [defaultTab, setInput]);
  const [receipt, setReceipt] = useState<ResponseCreateTransactionJson | null>(null);
  /**
   * The balance after the payment comes only from a read that succeeded after the confirmation —
   * never from the cached pre-payment statement (a failed reread says so instead).
   */
  const [after, setAfter] = useState<
    { status: 'pending' } | { status: 'ok'; total: number } | { status: 'error'; error: unknown }
  >({ status: 'pending' });
  const readAfter = async () => {
    setAfter({ status: 'pending' });
    try {
      const fresh = await getClientClientIdFiadoStatement(
        id,
        { Page: 1, PageSize: 1 },
        { client: engine.client() },
      );
      setAfter({ status: 'ok', total: fresh.data.outstandingTotal });
    } catch (error) {
      setAfter({ status: 'error', error });
    }
  };
  const intent = useFinancialIntent(`ledger/payment/${id}`, async (data) => {
    setReceipt(data as ResponseCreateTransactionJson);
    void readAfter();
    toast.show('Pagamento registrado.');
    await draft.discard();
    void queries.invalidateQueries({ queryKey: ['ledger', engine.getSnapshot().scope] });
    void queries.invalidateQueries({ queryKey: ['day', engine.getSnapshot().scope] });
  });
  const [noMethod, setNoMethod] = useState(false);
  const method = methods.find((m) => m.id === input.methodId) ?? null;
  const tab = tabs.find((t) => t.id === input.accountId) ?? null;
  const cash = method?.fiadoPaymentKind === 'CountedCash';
  const cashbox = cash ? cashboxOf(tab) : { kind: 'none' as const };
  const cashboxName =
    cashbox.kind === 'cashbox'
      ? ((accounts.data ?? []).find((a) => a.id === cashbox.id)?.name ?? 'o caixa pareado')
      : '';
  const binding = useSessionBinding({
    cashboxId: cashbox.kind === 'cashbox' ? cashbox.id : null,
    cashboxName,
    date: input.date,
    testID: 'payment',
  });
  const operatorRequired = manage && !own;
  const missing = paymentMissing(input, { operatorRequired });
  const bindingBlocks = ['loading', 'error', 'blocked', 'incomplete'].includes(binding.status);
  const send = async () => {
    if (!input.methodId) {
      setNoMethod(true);
      return;
    }
    if (missing.length || bindingBlocks || cashbox.kind === 'unpaired' || intent.blocked) return;
    // What was on screen at the tap is what is sent, never input typed during the turno check.
    const tapped = input;
    const fresh = await binding.prepare();
    if (!fresh) return;
    await intent.submit({
      operation: 'fiado-payment',
      payload: paymentRequest(tapped, id, fresh),
    });
  };
  const current = balance.data ? presentFiadoBalanceWire(balance.data.outstandingTotal) : null;
  if (receipt)
    return (
      <>
        <TransactionReceipt
          receipt={receipt}
          title="Pagamento registrado no servidor"
          onOpen={() => void shell.navigate(`/transacoes/${receipt.id}`, true)}
          onBack={() => void shell.back()}
        />
        <Card title="Saldo depois do pagamento" testID="payment-after">
          {after.status === 'pending' ? (
            <Skeleton label="Relendo o saldo no servidor…" />
          ) : after.status === 'ok' ? (
            <AppText testID="payment-after-balance">
              {presentFiadoBalanceWire(after.total).text}
            </AppText>
          ) : (
            <Notice
              kind="warning"
              testID="payment-after-error"
              title="O pagamento foi registrado, mas o saldo não pôde ser relido."
              body="Nada será enviado de novo. O saldo mostrado antes do pagamento não vale mais."
              messages={errorMessages(after.error)}
              actions={<Button label="Reler saldo" onPress={() => void readAfter()} />}
            />
          )}
          <Button
            label="Ver extrato"
            variant="link"
            role="link"
            onPress={() => void shell.navigate(`/clientes/${id}/extrato`, true)}
          />
        </Card>
      </>
    );
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
      <Card title={person.data?.name ?? 'Cliente'} testID="payment-client">
        {balance.status === 'pending' ? (
          <Skeleton label="Carregando o saldo…" />
        ) : balance.status === 'error' ? (
          <Notice
            kind="danger"
            title="Não foi possível carregar o saldo."
            messages={errorMessages(balance.error)}
            actions={<Button label="Tentar novamente" onPress={() => void balance.refetch()} />}
          />
        ) : (
          <>
            <AppText testID="payment-balance">{`Saldo atual: ${current!.text}`}</AppText>
            <AppText variant="helper">{`Venda mais antiga em aberto: ${balance.data.oldestUnpaidDate ? businessDay(balance.data.oldestUnpaidDate) : 'nenhuma'}`}</AppText>
          </>
        )}
      </Card>
      <IntentNotices
        intent={intent}
        testID="payment-intent"
        verify={
          <Button
            label="Conferir no extrato"
            variant="secondary"
            onPress={() => void shell.navigate(`/clientes/${id}/extrato`, true)}
          />
        }
      />
      <Card title="Pagamento">
        <AccountField
          testID="payment-account"
          label="Conta fiado que recebe"
          accounts={tabs}
          lookup={accounts}
          value={input.accountId}
          onChange={(accountId) => setInput((p) => ({ ...p, accountId }))}
          helper="A dívida é do cliente na filial: qualquer conta fiado sua pode receber."
        />
        <AmountField
          testID="payment-value"
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
        <FormField
          label="Forma de pagamento"
          required
          error={noMethod && !input.methodId ? 'Escolha a forma de pagamento.' : undefined}
        >
          {types.status === 'pending' ? (
            <Skeleton label="Carregando as formas de pagamento…" />
          ) : types.status === 'error' ? (
            <Notice
              kind="danger"
              title="Não foi possível carregar as formas de pagamento."
              messages={errorMessages(types.error)}
            />
          ) : methods.length === 0 ? (
            <Notice
              kind="warning"
              title="Nenhuma forma de pagamento configurada."
              body="A administração configura em Categorias e tipos."
            />
          ) : (
            <RadioGroup
              testID="payment-method"
              accessibilityLabel="Forma de pagamento"
              value={input.methodId}
              onValueChange={(methodId) => setInput((p) => ({ ...p, methodId }))}
            >
              {methods.map((m) => (
                <Radio
                  key={m.id}
                  testID={`payment-method-${m.id}`}
                  value={m.id}
                  label={m.name}
                  description={fiadoPaymentKindLabels[m.fiadoPaymentKind!]}
                />
              ))}
            </RadioGroup>
          )}
        </FormField>
        {method ? (
          <Notice
            kind="info"
            blocking={false}
            testID="payment-effect"
            title={cash ? 'Dinheiro entra no caixa' : 'Não entra no caixa'}
            body={
              cash
                ? cashbox.kind === 'unpaired'
                  ? 'Esta conta fiado não tem caixa pareado disponível para você: pagamento em dinheiro não pode ser registrado nela.'
                  : `Também registra, na mesma operação, a entrada automática do dinheiro em ${cashboxName}. Não lance essa entrada de novo.`
                : 'Reduz a dívida do cliente sem movimentar o dinheiro contado do caixa. O recebimento no banco é tratado à parte.'
            }
          />
        ) : null}
        <DateTimeFields
          testID="payment"
          date={input.date}
          time={input.time}
          onDate={(date) => setInput((p) => ({ ...p, date }))}
          onTime={(time) => setInput((p) => ({ ...p, time }))}
        />
        <DescriptionField
          testID="payment-description"
          value={input.description}
          onChange={(description) => setInput((p) => ({ ...p, description }))}
        />
        {manage && (shell.mode === 'management' || operatorRequired) ? (
          <OperatorField
            testID="payment-operator"
            operators={operators.data}
            status={operators.status}
            required={operatorRequired}
            value={input.operatorId}
            onChange={(operatorId) => setInput((p) => ({ ...p, operatorId }))}
          />
        ) : null}
      </Card>
      {binding.view}
      <Button
        label="Confirmar pagamento"
        variant="cta"
        testID="payment-confirm"
        fullWidth
        loading={intent.state.phase === 'sending'}
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
        onPress={() => void send()}
      />
    </>
  );
}

function memberTabGrant(
  self: { availableAccounts: { accountId: string; accountType: string }[] } | null | undefined,
  tabId: string,
) {
  return !!self?.availableAccounts.some((a) => a.accountType === 'Tab' && a.accountId === tabId);
}
