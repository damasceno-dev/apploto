import { useCallback, useRef, useState, type RefObject } from 'react';
import { useFocusEffect, useLocalSearchParams, useRouter } from 'expo-router';
import type { View } from 'react-native';
import type { ResponseAccountJson, ResponseOperatorAccountJson } from '@loto/api';
import {
  useDeleteOperatorOperatorIdAccountsAccountId,
  useGetAccount,
  useGetOperator,
  useGetOperatorOperatorIdAccounts,
  usePostOperatorOperatorIdAccounts,
  usePutOperatorOperatorIdAccountsAccountIdPrimary,
} from '@loto/api/queries';
import { accountTypeLabels } from '@loto/core';
import { useSession, useScopedApiClient } from '@/session/provider';
import { useShell } from '@/shell/provider';
import {
  AppText,
  Badge,
  Button,
  Card,
  ConfirmDialog,
  EmptyState,
  FormField,
  Notice,
  Select,
  moveFocusTo,
  useToast,
} from '@/ui';
import { accountDetail, assignableAccounts } from './contracts';
import {
  CommandNotices,
  ReadState,
  errorMessages,
  useAdminCommand,
  useAdminReads,
  useConfirmedWrite,
  useFreshGate,
} from './common';

export const SEPARATE_GRANTS =
  'A conta fiado é uma autorização separada; ela não vem junto com o terminal.';
export const NO_ACCOUNTS =
  'Este operador ainda não tem contas. Sem um terminal atribuído, ele não consegue registrar transações.';
export const SELF_ACCOUNTS_RECEIPT = 'Suas contas nesta filial foram atualizadas no servidor.';
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
/**
 * A grant change on the caller's own operator makes the engine replace the scope, so the
 * confirmed receipt is kept per engine, identity and branch in memory until dismissed.
 */
const receipts = new WeakMap<object, Set<string>>();
type Dialog =
  | { readonly kind: 'add'; readonly accountId?: string }
  | { readonly kind: 'remove' | 'primary'; readonly link: ResponseOperatorAccountJson };

export function AssignmentsScreen() {
  const params = useLocalSearchParams<{ operatorId?: string; focus?: string }>();
  const router = useRouter();
  const shell = useShell();
  const { snapshot, engine } = useSession();
  const client = useScopedApiClient();
  const reads = useAdminReads();
  const operators = useGetOperator({
    request: { client },
    query: { staleTime: 0, enabled: reads },
  });
  const accounts = useGetAccount({ request: { client }, query: { staleTime: 0, enabled: reads } });
  const requested = params.operatorId && uuid.test(params.operatorId) ? params.operatorId : null;
  const operatorList = operators.data?.data.operators ?? [];
  // The selected id is a resource: only an operator in the fresh active list is loaded.
  const operator = operators.data ? operatorList.find((o) => o.id === requested) : undefined;
  const assigned = useGetOperatorOperatorIdAccounts(operator?.id ?? '', {
    request: { client },
    query: { staleTime: 0, enabled: reads && !!operator, retry: false },
  });
  const [dialog, setDialog] = useState<Dialog | null>(null);
  const trigger = useRef<View>(null);
  const rows = useRef(new Map<string, View>());
  const receiptOwner = `${snapshot.user?.userId}:${snapshot.branch?.id}`;
  const [receipt, setReceipt] = useState(() => !!receipts.get(engine)?.has(receiptOwner));
  const selfId =
    snapshot.selfStatus === 'ready' ? (snapshot.selfContext?.operatorId ?? null) : null;
  useFocusEffect(
    useCallback(() => {
      if (params.focus && assigned.data) moveFocusTo(rows.current.get(params.focus));
    }, [params.focus, assigned.data]),
  );
  const links = assigned.data?.data.operatorAccounts ?? [];
  const accountList = accounts.data?.data.accounts ?? [];
  const refetch = async () => {
    const [a, b] = await Promise.all([accounts.refetch(), operators.refetch()]);
    const c = operator ? await assigned.refetch() : null;
    if (a.error || b.error || c?.error) throw a.error ?? b.error ?? c?.error;
  };
  /** Records the receipt before the engine may replace the scope, then re-resolves self-context. */
  const afterWrite = async (phase: 'receipt' | 'settle') => {
    if (!operator || operator.id !== selfId) return;
    const owner = `${engine.getSnapshot().user?.userId}:${engine.getSnapshot().branch?.id}`;
    receipts.set(engine, (receipts.get(engine) ?? new Set()).add(owner));
    setReceipt(true);
    if (phase === 'settle') await engine.recheckSelfContext();
  };
  const ready = !!operator && assigned.data && !assigned.error && accounts.data && !accounts.error;
  return (
    <>
      {receipt ? (
        <Notice
          kind="success"
          title={SELF_ACCOUNTS_RECEIPT}
          testID="assignment-self-receipt"
          onDismiss={() => {
            receipts.get(engine)?.delete(receiptOwner);
            setReceipt(false);
          }}
        />
      ) : null}
      <ReadState
        pending={operators.isPending}
        error={operators.error}
        retry={() => void operators.refetch()}
      />
      {operators.data && !operators.error ? (
        <FormField label="Operador" required>
          <Select
            testID="assignment-operator"
            value={operator?.id ?? null}
            onValueChange={(operatorId) => router.setParams({ operatorId, focus: '' })}
            options={operatorList.map((o) => ({
              value: o.id,
              label: o.id === selfId ? `${o.name} (você)` : o.name,
            }))}
            emptyText="Nenhum operador ativo. Crie um em Operadores."
          />
        </FormField>
      ) : null}
      {operators.data && requested && !operator ? (
        <Notice
          kind="warning"
          testID="assignment-operator-missing"
          title="Este operador não está entre os operadores ativos desta filial."
          body="Escolha um operador da lista."
        />
      ) : null}
      {operators.data && !requested ? (
        <AppText variant="helper">Escolha um operador para ver e mudar as contas dele.</AppText>
      ) : null}
      {operator ? (
        <>
          <ReadState
            pending={assigned.isPending}
            error={assigned.error}
            retry={() => void assigned.refetch()}
          />
          <ReadState
            pending={accounts.isPending}
            error={accounts.error}
            retry={() => void accounts.refetch()}
          />
        </>
      ) : null}
      {ready ? (
        <>
          <Card title={`Contas de ${operator.name}`} testID="assignment-list">
            {operator.id === selfId ? <Badge kind="feedback" tone="info" label="você" /> : null}
            <AppText variant="helper">{SEPARATE_GRANTS}</AppText>
            <Button
              ref={trigger}
              label="Adicionar conta"
              testID="assignment-add"
              disabled={!reads}
              onPress={() => setDialog({ kind: 'add' })}
            />
          </Card>
          {links.length === 0 ? (
            <EmptyState kind="setup-needed" statement={NO_ACCOUNTS} testID="assignment-empty" />
          ) : null}
          {links.map((link) => {
            const detail = accountDetail({
              institution: link.accountInstitution,
              number: link.accountNumber,
            });
            const pairedTab = link.pairedTabAccountId
              ? accountList.find((a) => a.id === link.pairedTabAccountId)
              : null;
            const tabHeld = links.some((l) => l.accountId === link.pairedTabAccountId);
            return (
              <Card
                key={link.id}
                title={link.accountName}
                testID={`assignment-row-${link.accountId}`}
              >
                {link.isPrimary ? <Badge kind="feedback" tone="success" label="Principal" /> : null}
                <AppText>{`Tipo: ${accountTypeLabels[link.accountType]}`}</AppText>
                {detail ? <AppText>{`Instituição / número: ${detail}`}</AppText> : null}
                {link.pairedTabAccountId ? (
                  <AppText testID={`assignment-paired-${link.accountId}`}>
                    {`Conta fiado pareada: ${pairedTab?.name ?? 'não encontrada nesta leitura'} · ${
                      tabHeld ? 'também atribuída a este operador' : 'não atribuída a este operador'
                    }`}
                  </AppText>
                ) : null}
                {link.pairedTabAccountId && !tabHeld && pairedTab ? (
                  <Button
                    label={`Atribuir também ${pairedTab.name}`}
                    testID={`assignment-add-tab-${link.accountId}`}
                    variant="secondary"
                    disabled={!reads}
                    onPress={() => setDialog({ kind: 'add', accountId: pairedTab.id })}
                  />
                ) : null}
                {!link.isPrimary ? (
                  <Button
                    label={`Definir ${link.accountName} como principal`}
                    testID={`assignment-primary-${link.accountId}`}
                    variant="secondary"
                    disabled={!reads}
                    onPress={() => setDialog({ kind: 'primary', link })}
                  />
                ) : null}
                <Button
                  label={`Ver conta ${link.accountName}`}
                  testID={`assignment-account-${link.accountId}`}
                  variant="link"
                  ref={(node) => {
                    if (node) rows.current.set(link.accountId, node);
                  }}
                  onPress={() =>
                    void shell.navigate(
                      '/gestao/contas?' + new URLSearchParams({ accountId: link.accountId }),
                      true,
                      false,
                      link.accountId,
                    )
                  }
                />
                <Button
                  label={`Remover ${link.accountName}`}
                  testID={`assignment-remove-${link.accountId}`}
                  variant="danger"
                  disabled={!reads}
                  onPress={() => setDialog({ kind: 'remove', link })}
                />
              </Card>
            );
          })}
        </>
      ) : null}
      {/* Kept mounted through a failed reread so a confirmed receipt and its recovery survive. */}
      {dialog && operator && assigned.data && accounts.data ? (
        <AssignmentDialog
          key={
            dialog.kind === 'add'
              ? `add:${dialog.accountId ?? ''}`
              : `${dialog.kind}:${dialog.link.id}`
          }
          dialog={dialog}
          operatorId={operator.id}
          operatorName={operator.name}
          visible={reads}
          links={links}
          accounts={accountList}
          returnFocus={trigger}
          onClose={() => setDialog(null)}
          refetch={refetch}
          afterWrite={afterWrite}
        />
      ) : null}
    </>
  );
}

function AssignmentDialog({
  dialog,
  operatorId,
  operatorName,
  visible,
  links,
  accounts,
  returnFocus,
  onClose,
  refetch,
  afterWrite,
}: {
  dialog: Dialog;
  operatorId: string;
  operatorName: string;
  visible: boolean;
  links: readonly ResponseOperatorAccountJson[];
  accounts: readonly ResponseAccountJson[];
  returnFocus: RefObject<View | null>;
  onClose: () => void;
  refetch: () => Promise<void>;
  afterWrite: (phase: 'receipt' | 'settle') => Promise<void>;
}) {
  const client = useScopedApiClient();
  const toast = useToast();
  const command = useAdminCommand();
  const written = useConfirmedWrite(command, onClose);
  const gate = useFreshGate(refetch, command, written);
  const assign = usePostOperatorOperatorIdAccounts({ request: { client } });
  const unassign = useDeleteOperatorOperatorIdAccountsAccountId({ request: { client } });
  const primary = usePutOperatorOperatorIdAccountsAccountIdPrimary({ request: { client } });
  const [choice, setChoice] = useState<string | null>(
    dialog.kind === 'add' ? (dialog.accountId ?? null) : null,
  );
  const options = assignableAccounts(accounts, links);
  const chosen = accounts.find((a) => a.id === choice);
  const name = dialog.kind === 'add' ? (chosen?.name ?? '') : dialog.link.accountName;
  const copy =
    dialog.kind === 'add'
      ? {
          title: `Adicionar conta para ${operatorName}`,
          confirm: 'Atribuir conta',
          receipt: `${name} atribuída a ${operatorName} no servidor.`,
          consequence: `${operatorName} passa a poder usar a conta escolhida. ${SEPARATE_GRANTS}`,
        }
      : dialog.kind === 'primary'
        ? {
            title: `Definir ${name} como principal?`,
            confirm: 'Definir como principal',
            receipt: `${name} é a conta principal de ${operatorName}.`,
            consequence:
              'A conta principal abre por padrão no lançamento rápido. Cada operador tem uma só; a anterior deixa de ser principal.',
          }
        : {
            title: `Remover ${name} de ${operatorName}?`,
            confirm: 'Remover conta',
            receipt: `${name} removida de ${operatorName} no servidor.`,
            consequence:
              `${operatorName} deixa de poder usar ${name}. Movimentos já registrados continuam. ${
                dialog.link.pairedTabAccountId
                  ? 'A conta fiado pareada é uma autorização separada e não é removida junto.'
                  : ''
              }`.trim(),
          };
  const confirm = () => {
    if (gate.stale) return void gate.reread();
    if (command.blocked || written.confirmed || (dialog.kind === 'add' && !choice)) return;
    void command.run(
      async () => {
        const result =
          dialog.kind === 'add'
            ? await assign.mutateAsync({ operatorId, data: { accountId: choice! } })
            : dialog.kind === 'primary'
              ? await primary.mutateAsync({ operatorId, accountId: dialog.link.accountId })
              : await unassign.mutateAsync({ operatorId, accountId: dialog.link.accountId });
        await afterWrite('receipt');
        return result;
      },
      async () => {
        written.confirm();
        toast.show(copy.receipt);
        await afterWrite('settle');
        await refetch();
        written.settle();
      },
    );
  };
  return (
    <ConfirmDialog
      visible={visible}
      title={copy.title}
      destructive={dialog.kind === 'remove'}
      busy={command.pending || gate.rereading}
      busyLabel={gate.rereading ? 'Relendo…' : 'Salvando…'}
      confirmLabel={gate.stale ? 'Reler e conferir' : copy.confirm}
      consequence={copy.consequence}
      returnFocus={returnFocus}
      onDismiss={onClose}
      onConfirm={confirm}
      testID={`assignment-command-${dialog.kind}`}
    >
      {gate.stale && !command.uncertain && !command.postWriteError ? (
        <Notice
          kind="warning"
          title="As contas deste operador mudaram desde a última leitura."
          body="Releia antes de tentar de novo. Nada foi reenviado."
          messages={errorMessages(command.error)}
        />
      ) : (
        <CommandNotices
          command={command}
          receipt={copy.receipt}
          reconcile={() => void gate.reread()}
          reconcileLabel="Reler contas para conferir"
        />
      )}
      {gate.failed ? (
        <Notice
          kind="danger"
          title="Não foi possível reler as contas."
          messages={errorMessages(gate.failed)}
        />
      ) : null}
      {dialog.kind === 'add' ? (
        <FormField label="Conta" required helper={SEPARATE_GRANTS}>
          <Select
            testID="assignment-account-choice"
            value={choice}
            onValueChange={setChoice}
            options={options.map((a) => ({
              value: a.id,
              label: `${a.name} · ${accountTypeLabels[a.type]}`,
              description: accountDetail(a) || undefined,
            }))}
            emptyText="Todas as contas ativas já estão atribuídas a este operador."
          />
        </FormField>
      ) : null}
      {dialog.kind === 'add' && chosen?.type === 'Terminal' && chosen.tabAccountId ? (
        <AppText variant="helper" testID="assignment-tab-separate">
          {`A conta fiado pareada a ${chosen.name} não é atribuída junto. Atribua-a depois, se ${operatorName} for vender fiado.`}
        </AppText>
      ) : null}
      {dialog.kind === 'add' && chosen?.type === 'Safe' ? (
        <AppText variant="helper">
          Atribuir o cofre não abre turno nem dá acesso a um caixa.
        </AppText>
      ) : null}
    </ConfirmDialog>
  );
}
