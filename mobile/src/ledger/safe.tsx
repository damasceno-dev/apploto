import { useCallback, useRef, useState } from 'react';
import { useLocalSearchParams } from 'expo-router';
import { useQueryClient } from '@tanstack/react-query';
import type { ResponseCreateTransactionJson } from '@loto/api';
import { formatBrlWire } from '@loto/core';
import { useSession } from '@/session/provider';
import { useShell } from '@/shell/provider';
import { AppText, Button, Card, Notice, useToast } from '@/ui';
import { RetainedNotice, useAdminDraft } from '@/admin/common';
import { IntentNotices, elevated, useAccounts, useFinancialIntent, useOperators } from './common';
import { isSafeInput, safeMissing, safeRequest, type SafeInput } from './contracts';
import {
  AccountField,
  AmountField,
  DateTimeFields,
  DescriptionField,
  OperatorField,
} from './fields';
import { useSessionBinding } from './opening';
import { nowTime } from './create';

/**
 * Depósito Dinheiro: the protected two-leg transfer (cashbox Out on its turno + generated cofre In,
 * both or neither) through `POST /transaction/safe-deposit` — never two manual rows and never a bank
 * deposit. It is internal custody, not revenue, and the cofre has no counted variance.
 */
export function SafeDepositScreen() {
  const params = useLocalSearchParams<{ accountId?: string }>();
  const { snapshot, engine } = useSession();
  const shell = useShell();
  const queries = useQueryClient();
  const toast = useToast();
  const today = snapshot.clock!.branchLocalDate;
  const manage = elevated(snapshot.branch!.role);
  const own = snapshot.selfContext?.operatorId ?? null;
  const accounts = useAccounts();
  const operators = useOperators();
  const draft = useAdminDraft('ledger/safe-deposit', isSafeInput, async () => {
    await engine.verifyEntry();
    return engine.getSnapshot().status === 'branch';
  });
  const [input, setInputState] = useState<SafeInput>(() => ({
    v: 1,
    accountId: typeof params.accountId === 'string' ? params.accountId : null,
    safeAccountId: null,
    value: '',
    date: today,
    time: nowTime(snapshot.clock),
    description: '',
    operatorId: null,
    buffers: {},
  }));
  const latest = useRef(input);
  const setInput = useCallback(
    (update: (prev: SafeInput) => SafeInput) => {
      const next = update(latest.current);
      latest.current = next;
      setInputState(next);
      void draft.keep(next, null);
    },
    [draft],
  );
  const [receipt, setReceipt] = useState<
    (ResponseCreateTransactionJson & { safeName: string | null }) | null
  >(null);
  const cashboxes = (accounts.data ?? []).filter((a) => a.type === 'Terminal');
  const safes = (accounts.data ?? []).filter((a) => a.type === 'Safe');
  /** The cofre of the request actually sent (a later edit never renames the receipt). */
  const sentSafe = useRef<string | null>(null);
  const intent = useFinancialIntent('ledger/safe-deposit', async (data) => {
    const sent = sentSafe.current ?? latest.current.safeAccountId;
    setReceipt({
      ...(data as ResponseCreateTransactionJson),
      safeName: safes.find((s) => s.id === sent)?.name ?? null,
    });
    toast.show('Depósito no cofre registrado.');
    await draft.discard();
    void queries.invalidateQueries({ queryKey: ['ledger', engine.getSnapshot().scope] });
    void queries.invalidateQueries({ queryKey: ['day', engine.getSnapshot().scope] });
  });
  const source = cashboxes.find((a) => a.id === input.accountId) ?? null;
  const binding = useSessionBinding({
    cashboxId: source?.id ?? null,
    cashboxName: source?.name ?? 'o caixa',
    date: input.date,
    testID: 'safe',
  });
  const operatorRequired = manage && !own;
  const missing = safeMissing(input, { operatorRequired });
  const bindingBlocks = ['loading', 'error', 'blocked', 'incomplete'].includes(binding.status);
  const send = async () => {
    if (missing.length || bindingBlocks || intent.blocked) return;
    // What was on screen at the tap is what is sent, never input typed during the turno check.
    const tapped = input;
    const fresh = await binding.prepare();
    if (!fresh) return;
    sentSafe.current = tapped.safeAccountId;
    await intent.submit({ operation: 'safe-deposit', payload: safeRequest(tapped, fresh) });
  };
  if (receipt) {
    return (
      <Card title="Depósito no cofre registrado no servidor" testID="safe-receipt">
        <AppText testID="safe-receipt-legs">
          {`${formatBrlWire(receipt.value)} saíram de ${source?.name ?? 'o caixa'} e entraram em ${receipt.safeName ?? 'o cofre'}, na mesma operação.`}
        </AppText>
        <AppText variant="helper">
          É uma transferência interna: não é receita e o cofre não tem diferença de contagem.
        </AppText>
        {receipt.startedSessionId ? (
          <AppText>O turno do caixa foi iniciado com esta operação.</AppText>
        ) : null}
        <Button
          label="Abrir saída do caixa"
          variant="secondary"
          testID="safe-open-out"
          onPress={() => void shell.navigate(`/transacoes/${receipt.id}`, true)}
        />
        {receipt.generatedTransactionId ? (
          <Button
            label="Abrir entrada no cofre"
            variant="secondary"
            testID="safe-open-in"
            onPress={() =>
              void shell.navigate(`/transacoes/${receipt.generatedTransactionId}`, true)
            }
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
      <IntentNotices
        intent={intent}
        testID="safe-intent"
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
      <Card title="Depósito Dinheiro">
        <AppText variant="helper">
          Leva dinheiro do caixa para o cofre. Para o banco, o recolhimento do cofre é outra
          operação.
        </AppText>
        <AccountField
          testID="safe-source"
          label="Caixa de origem"
          accounts={cashboxes}
          lookup={accounts}
          value={input.accountId}
          onChange={(accountId) => setInput((p) => ({ ...p, accountId }))}
        />
        <AccountField
          testID="safe-destination"
          label="Cofre de destino"
          accounts={safes}
          lookup={accounts}
          value={input.safeAccountId}
          onChange={(safeAccountId) => setInput((p) => ({ ...p, safeAccountId }))}
        />
        <AmountField
          testID="safe-value"
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
          testID="safe"
          date={input.date}
          time={input.time}
          onDate={(date) => setInput((p) => ({ ...p, date }))}
          onTime={(time) => setInput((p) => ({ ...p, time }))}
        />
        <DescriptionField
          testID="safe-description"
          value={input.description}
          onChange={(description) => setInput((p) => ({ ...p, description }))}
        />
        {manage && (shell.mode === 'management' || operatorRequired) ? (
          <OperatorField
            testID="safe-operator"
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
        label="Registrar depósito no cofre"
        variant="cta"
        testID="safe-confirm"
        fullWidth
        loading={intent.state.phase === 'sending'}
        disabled={!!missing.length || bindingBlocks || intent.blocked}
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
