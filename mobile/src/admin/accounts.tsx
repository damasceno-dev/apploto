import { useCallback, useRef, useState, type ReactNode, type RefObject } from 'react';
import { useFocusEffect, useLocalSearchParams, useRouter } from 'expo-router';
import type { View } from 'react-native';
import {
  getAccountAccountId,
  type ResponseAccountJson,
  type ResponseTerminalJson,
} from '@loto/api';
import {
  useDeleteAccountAccountId,
  useDeleteAccountTerminalTerminalAccountIdTab,
  useGetAccount,
  useGetAccountAccountId,
  useGetTerminal,
  usePostAccountBank,
  usePostAccountPairTab,
  usePostAccountSafe,
  usePostAccountTab,
  usePostAccountTerminal,
  usePutAccountAccountId,
  usePutTerminalCashboxAccountId,
} from '@loto/api/queries';
import { accountTypeLabels } from '@loto/core';
import { useSession, useScopedApiClient } from '@/session/provider';
import { useShell } from '@/shell/provider';
import {
  AppText,
  Button,
  Card,
  ConfirmDialog,
  DetailList,
  EmptyState,
  FormField,
  Notice,
  Overlay,
  Radio,
  RadioGroup,
  Select,
  TextField,
  moveFocusTo,
  useToast,
} from '@/ui';
import {
  accountCreateRequest,
  accountDetail,
  accountEdit,
  accountPairing,
  accountUpdatePayload,
  emptyAccount,
  isAccountEdit,
  isAccountInput,
  unpairedTabs,
  type AccountEdit,
  type AccountInput,
  type AccountKind,
} from './contracts';
import {
  CommandNotices,
  ReadState,
  RetainedNotice,
  errorMessages,
  useAdminCommand,
  useAdminDraft,
  useAdminReads,
  useConfirmedWrite,
  useFreshGate,
} from './common';

export const PAIRING_GRANTS_NOTHING =
  'Parear não dá acesso a ninguém: atribua o caixa e a conta fiado separadamente em Contas dos operadores.';
export const NO_TAB_CONSEQUENCE =
  'Sem conta fiado, este caixa não pode registrar vendas fiado no balcão até ser pareado.';
export const SAFE_EXPLANATION =
  'O cofre não tem terminal CAIXA, conta fiado, turno nem contagem. Uma pessoa só o usa com uma atribuição explícita em Contas dos operadores.';
const TERMINAL_EXPLANATION =
  'O terminal CAIXA é o equipamento físico de referência do relatório; o caixa é a conta financeira do turno. Um mesmo terminal pode servir a mais de um caixa.';
const kindLabels: Record<AccountKind, string> = {
  Terminal: 'Caixa',
  BankAccount: 'Conta bancária',
  Safe: 'Cofre',
};
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
type Dialog =
  | { readonly kind: 'create'; readonly type: AccountKind }
  | {
      readonly kind: 'edit' | 'deactivate' | 'tab' | 'pair' | 'unpair' | 'terminal';
      readonly account: ResponseAccountJson;
    };

/** Terminal CAIXA reference of a caixa, resolved through the active terminal list. */
function terminalText(account: ResponseAccountJson, terminals: readonly ResponseTerminalJson[]) {
  if (!account.terminalId) return 'Sem terminal CAIXA';
  const terminal = terminals.find((t) => t.id === account.terminalId);
  return terminal
    ? `${terminal.name}${terminal.externalReference ? ` (${terminal.externalReference})` : ''}`
    : 'Terminal CAIXA não encontrado entre os terminais ativos';
}
function pairingText(account: ResponseAccountJson, accounts: readonly ResponseAccountJson[]) {
  const pairing = accountPairing(account, accounts);
  if (pairing.kind === 'none') return null;
  if (pairing.kind === 'unpaired')
    return account.type === 'Terminal' ? 'Sem conta fiado' : 'Sem caixa pareado';
  const name = pairing.with?.name ?? 'conta não encontrada nesta leitura';
  return account.type === 'Terminal' ? `Pareado com ${name}` : `Pareada com o caixa ${name}`;
}

export function AccountsScreen() {
  const params = useLocalSearchParams<{ accountId?: string; focus?: string }>();
  const router = useRouter();
  const shell = useShell();
  const client = useScopedApiClient();
  const reads = useAdminReads();
  const accounts = useGetAccount({ request: { client }, query: { staleTime: 0, enabled: reads } });
  const terminals = useGetTerminal({
    request: { client },
    query: { staleTime: 0, enabled: reads },
  });
  const [dialog, setDialog] = useState<Dialog | null>(null);
  const trigger = useRef<View>(null);
  const rows = useRef(new Map<string, View>());
  const selected = params.accountId && uuid.test(params.accountId) ? params.accountId : null;
  // The detail is a resource: a fresh read proves it still belongs to this branch and is active.
  const detail = useGetAccountAccountId(selected ?? '', {
    request: { client },
    query: { staleTime: 0, enabled: reads && !!selected, retry: false },
  });
  useFocusEffect(
    useCallback(() => {
      if (params.focus && accounts.data) moveFocusTo(rows.current.get(params.focus));
    }, [params.focus, accounts.data]),
  );
  const list = accounts.data?.data.accounts ?? [];
  const terminalList = terminals.data?.data ?? [];
  const refetch = async () => {
    const [a, t] = await Promise.all([accounts.refetch(), terminals.refetch()]);
    if (selected) {
      const d = await detail.refetch();
      if (d.error) throw d.error;
    }
    if (a.error || t.error) throw a.error ?? t.error;
  };
  const loaded = accounts.data && !accounts.error && terminals.data && !terminals.error;
  const open = (next: Dialog) => setDialog(next);
  const actions = (account: ResponseAccountJson) => {
    const pairing = accountPairing(account, list);
    return (
      <>
        <Button
          label={`Editar ${account.name}`}
          testID={`account-edit-${account.id}`}
          disabled={!reads}
          onPress={() => open({ kind: 'edit', account })}
        />
        {account.type === 'Terminal' && pairing.kind === 'unpaired' ? (
          <>
            <Button
              label={`Criar conta fiado para ${account.name}`}
              testID={`account-tab-${account.id}`}
              variant="secondary"
              disabled={!reads}
              onPress={() => open({ kind: 'tab', account })}
            />
            <Button
              label={`Parear ${account.name} com uma conta fiado`}
              testID={`account-pair-${account.id}`}
              variant="secondary"
              disabled={!reads}
              onPress={() => open({ kind: 'pair', account })}
            />
          </>
        ) : null}
        {account.type === 'Terminal' && pairing.kind === 'paired' ? (
          <Button
            label={`Desfazer pareamento de ${account.name}`}
            testID={`account-unpair-${account.id}`}
            variant="secondary"
            disabled={!reads}
            onPress={() => open({ kind: 'unpair', account })}
          />
        ) : null}
        {account.type === 'Terminal' ? (
          <Button
            label={`Trocar terminal CAIXA de ${account.name}`}
            testID={`account-terminal-${account.id}`}
            variant="secondary"
            disabled={!reads}
            onPress={() => open({ kind: 'terminal', account })}
          />
        ) : null}
        <Button
          label={`Desativar ${account.name}`}
          testID={`account-deactivate-${account.id}`}
          variant="danger"
          disabled={!reads}
          onPress={() => open({ kind: 'deactivate', account })}
        />
      </>
    );
  };
  return (
    <>
      {/* A requested detail comes first so it is on screen without scrolling. */}
      {selected ? (
        <AccountDetail
          query={detail}
          accounts={list}
          terminals={terminalList}
          onClose={() => router.setParams({ accountId: '' })}
          onAssignments={() =>
            void shell.navigate('/gestao/contas-dos-operadores', true, false, selected)
          }
          actions={actions}
        />
      ) : null}
      <AppText variant="helper">{TERMINAL_EXPLANATION}</AppText>
      {(['Terminal', 'BankAccount', 'Safe'] as const).map((type, index) => (
        <Button
          key={type}
          ref={index === 0 ? trigger : undefined}
          label={`Adicionar ${kindLabels[type].toLowerCase()}`}
          testID={`account-new-${type}`}
          variant={index === 0 ? 'primary' : 'secondary'}
          disabled={!reads || !loaded}
          onPress={() => open({ kind: 'create', type })}
        />
      ))}
      <ReadState
        pending={accounts.isPending}
        error={accounts.error}
        retry={() => void accounts.refetch()}
      />
      <ReadState
        pending={terminals.isPending}
        error={terminals.error}
        retry={() => void terminals.refetch()}
      />
      {loaded ? (
        <>
          {list.length === 0 ? (
            <EmptyState
              kind="no-records"
              statement="Nenhuma conta ativa."
              explanation="Crie o caixa, a conta bancária ou o cofre da filial."
            />
          ) : null}
          {list.map((account) => {
            const pairing = pairingText(account, list);
            const detailText = accountDetail(account);
            return (
              <Card key={account.id} title={account.name} testID={`account-row-${account.id}`}>
                <AppText testID={`account-type-${account.id}`}>
                  {`Tipo: ${accountTypeLabels[account.type]}`}
                </AppText>
                {detailText ? <AppText>{`Instituição / número: ${detailText}`}</AppText> : null}
                {account.type === 'Terminal' ? (
                  <AppText testID={`account-terminal-text-${account.id}`}>
                    {`Terminal CAIXA: ${terminalText(account, terminalList)}`}
                  </AppText>
                ) : null}
                {pairing ? (
                  <AppText testID={`account-pairing-${account.id}`}>{pairing}</AppText>
                ) : null}
                {account.type === 'Terminal' && !account.tabAccountId ? (
                  <AppText variant="helper">{NO_TAB_CONSEQUENCE}</AppText>
                ) : null}
                {account.type === 'Safe' ? (
                  <AppText variant="helper">{SAFE_EXPLANATION}</AppText>
                ) : null}
                <Button
                  label={`Detalhes de ${account.name}`}
                  testID={`account-open-${account.id}`}
                  variant="link"
                  ref={(node) => {
                    if (node) rows.current.set(account.id, node);
                  }}
                  onPress={() => router.setParams({ accountId: account.id, focus: account.id })}
                />
                {actions(account)}
              </Card>
            );
          })}
        </>
      ) : null}
      {/* Kept mounted through a failed reread so a confirmed receipt and its recovery survive. */}
      {dialog && accounts.data && terminals.data ? (
        <AccountDialog
          key={`${dialog.kind}:${dialog.kind === 'create' ? dialog.type : dialog.account.id}`}
          dialog={dialog}
          visible={reads}
          accounts={list}
          terminals={terminalList}
          returnFocus={trigger}
          onClose={() => setDialog(null)}
          refetch={refetch}
        />
      ) : null}
    </>
  );
}

function AccountDetail({
  query,
  accounts,
  terminals,
  onClose,
  onAssignments,
  actions,
}: {
  query: {
    readonly isPending: boolean;
    readonly error: unknown;
    readonly data?: { readonly data: ResponseAccountJson };
  };
  accounts: readonly ResponseAccountJson[];
  terminals: readonly ResponseTerminalJson[];
  onClose: () => void;
  onAssignments: () => void;
  actions: (account: ResponseAccountJson) => ReactNode;
}) {
  if (query.isPending) return <ReadState pending error={null} retry={() => undefined} />;
  if (query.error || !query.data)
    return (
      <Notice
        kind="warning"
        title="Esta conta não está disponível nesta filial."
        body="Ela pode ter sido desativada. A lista abaixo mostra as contas ativas."
        messages={errorMessages(query.error)}
        actions={<Button label="Fechar detalhes" onPress={onClose} />}
      />
    );
  const account = query.data.data;
  return (
    <Card title={`Detalhes: ${account.name}`} testID="account-detail">
      <DetailList
        rows={[
          { label: 'Tipo', value: accountTypeLabels[account.type] },
          { label: 'Instituição', value: account.institution ?? null },
          { label: 'Número', value: account.number ?? null },
          ...(account.type === 'Terminal'
            ? [{ label: 'Terminal CAIXA', value: terminalText(account, terminals) }]
            : []),
          ...(account.type === 'Terminal' || account.type === 'Tab'
            ? [{ label: 'Pareamento', value: pairingText(account, accounts) }]
            : []),
        ]}
      />
      <AppText variant="helper">O tipo da conta não muda depois de criada.</AppText>
      {actions(account)}
      <Button label="Ver atribuições aos operadores" variant="secondary" onPress={onAssignments} />
      <Button
        label="Fechar detalhes"
        variant="link"
        testID="account-detail-close"
        onPress={onClose}
      />
    </Card>
  );
}

function AccountDialog({
  dialog,
  ...rest
}: {
  dialog: Dialog;
  visible: boolean;
  accounts: readonly ResponseAccountJson[];
  terminals: readonly ResponseTerminalJson[];
  returnFocus: RefObject<View | null>;
  onClose: () => void;
  refetch: () => Promise<void>;
}) {
  if (dialog.kind === 'create') return <AccountCreate type={dialog.type} {...rest} />;
  if (dialog.kind === 'edit') return <AccountEditor account={dialog.account} {...rest} />;
  return <AccountCommand kind={dialog.kind} account={dialog.account} {...rest} />;
}

function AccountCreate({
  type,
  visible,
  accounts,
  terminals,
  returnFocus,
  onClose,
  refetch,
}: {
  type: AccountKind;
  visible: boolean;
  accounts: readonly ResponseAccountJson[];
  terminals: readonly ResponseTerminalJson[];
  returnFocus: RefObject<View | null>;
  onClose: () => void;
  refetch: () => Promise<void>;
}) {
  const { engine } = useSession();
  const client = useScopedApiClient();
  const toast = useToast();
  const base = emptyAccount(type);
  const [input, setInput] = useState<AccountInput>(base);
  const inputRef = useRef(input);
  const command = useAdminCommand();
  const bank = usePostAccountBank({ request: { client } });
  const safe = usePostAccountSafe({ request: { client } });
  const terminal = usePostAccountTerminal({ request: { client } });
  const written = useConfirmedWrite(command, onClose);
  const [problem, setProblem] = useState<string | null>(null);
  const [checkedName, setCheckedName] = useState<string | null>(null);
  const checked =
    checkedName === null ? null : accounts.filter((a) => a.name.trim() === checkedName);
  const draft = useAdminDraft(`admin/account/new/${type}`, isAccountInput, async () => {
    return engine.getSnapshot().branch?.role !== 'Member';
  });
  const tabs = unpairedTabs(accounts);
  const change = (next: AccountInput) => {
    inputRef.current = next;
    setInput(next);
    setProblem(null);
    void draft.keep(next, base);
  };
  const cancel = () => {
    if (!command.uncertain && !command.postWriteError) void draft.discard();
    onClose();
  };
  const submit = () => {
    const candidate = inputRef.current;
    const composed = accountCreateRequest(candidate);
    if (!composed.ok) return setProblem(composed.reason);
    void command.run(
      async () => {
        const kept = await draft.keep(candidate, base);
        const { request } = composed;
        const result =
          request.route === 'bank'
            ? await bank.mutateAsync({ data: request.data })
            : request.route === 'safe'
              ? await safe.mutateAsync({ data: request.data })
              : await terminal.mutateAsync({ data: request.data });
        return { result, kept };
      },
      async ({ result, kept }) => {
        // The 201 is the receipt: record it before anything else can fail, so no recovery path
        // makes this non-idempotent create sendable again. Close only after a reread succeeds.
        written.confirm();
        await draft.acknowledge(kept);
        toast.show(`Conta ${result.data.name} criada no servidor.`);
        await refetch();
        written.settle();
      },
    );
  };
  const reconcile = () =>
    void refetch()
      .then(() => {
        command.clear();
        // A confirmed create only settles and closes; it never becomes sendable again.
        if (written.confirmed) return written.recovered();
        // Unknown outcome: the reread names possible matches; nothing is resent and the input
        // stays for review.
        setCheckedName(inputRef.current.name.trim());
      })
      .catch(() => undefined);
  const busy = command.pending;
  return (
    <Overlay
      visible={visible}
      title={`Adicionar ${kindLabels[type].toLowerCase()}`}
      busy={busy}
      returnFocus={returnFocus}
      onDismiss={cancel}
      testID="account-editor"
      footer={
        <>
          <Button
            label="Salvar"
            testID="account-save"
            loading={busy}
            loadingLabel="Salvando…"
            disabled={command.blocked || written.confirmed || input.name.trim() === ''}
            onPress={submit}
            fullWidth
          />
          <Button label="Cancelar" variant="secondary" disabled={busy} onPress={cancel} fullWidth />
        </>
      }
    >
      <RetainedNotice
        visible={!!draft.retained}
        onRestore={() => {
          const value = draft.retained!.input;
          inputRef.current = value;
          setInput(value);
          draft.dismissRetained();
        }}
      />
      {draft.problem ? <Notice kind="warning" title={draft.problem} /> : null}
      {checked ? (
        <Notice
          kind="info"
          testID="account-reconciled"
          title={
            checked.length
              ? `Contas ativas com este nome: ${checked.map((a) => `${a.name} (${accountTypeLabels[a.type]})`).join('; ')}.`
              : 'Nenhuma conta ativa com este nome na releitura.'
          }
          body="Confira antes de salvar de novo; nada é reenviado automaticamente."
        />
      ) : null}
      <CommandNotices
        command={command}
        receipt="Conta criada no servidor."
        reconcile={reconcile}
        reconcileLabel="Reler contas para conferir"
      />
      {problem ? <Notice kind="danger" title={problem} /> : null}
      {type === 'Safe' ? <AppText variant="helper">{SAFE_EXPLANATION}</AppText> : null}
      <FormField label="Nome" required>
        <TextField
          testID="account-name"
          value={input.name}
          maxLength={255}
          onChangeText={(name) => change({ ...input, name })}
        />
      </FormField>
      <FormField label="Instituição">
        <TextField
          testID="account-institution"
          value={input.institution}
          onChangeText={(institution) => change({ ...input, institution })}
        />
      </FormField>
      <FormField label="Número">
        <TextField
          testID="account-number"
          value={input.number}
          onChangeText={(number) => change({ ...input, number })}
        />
      </FormField>
      {type === 'Terminal' ? (
        <>
          <FormField label="Conta fiado" helper={PAIRING_GRANTS_NOTHING}>
            <RadioGroup
              testID="account-pairing"
              value={input.pairing}
              onValueChange={(pairing) =>
                change({ ...input, pairing: pairing as AccountInput['pairing'], tabAccountId: '' })
              }
            >
              <Radio
                value="new"
                label="Criar conta fiado nova"
                description="Uma conta fiado nova, com o mesmo nome, nasce pareada a este caixa."
                testID="account-pairing-new"
              />
              <Radio
                value="existing"
                label="Usar uma conta fiado existente"
                description={
                  tabs.length ? undefined : 'Nenhuma conta fiado sem caixa nesta leitura.'
                }
                disabled={tabs.length === 0}
                testID="account-pairing-existing"
              />
              <Radio
                value="none"
                label="Sem conta fiado"
                description={NO_TAB_CONSEQUENCE}
                testID="account-pairing-none"
              />
            </RadioGroup>
          </FormField>
          {input.pairing === 'existing' ? (
            <FormField label="Conta fiado existente" required>
              <Select
                testID="account-existing-tab"
                value={input.tabAccountId || null}
                onValueChange={(tabAccountId) => change({ ...input, tabAccountId })}
                options={tabs.map((t) => ({ value: t.id, label: t.name }))}
                emptyText="Nenhuma conta fiado sem caixa."
              />
            </FormField>
          ) : null}
          <FormField label="Terminal CAIXA" helper={TERMINAL_EXPLANATION}>
            <Select
              testID="account-terminal-id"
              value={input.terminalId}
              onValueChange={(terminalId) => change({ ...input, terminalId })}
              options={[
                { value: '', label: 'Criar referência com o nome do caixa' },
                ...terminals.map((t) => ({
                  value: t.id,
                  label: t.externalReference ? `${t.name} (${t.externalReference})` : t.name,
                })),
              ]}
            />
          </FormField>
        </>
      ) : null}
    </Overlay>
  );
}

function AccountEditor({
  account,
  visible,
  returnFocus,
  onClose,
  refetch,
}: {
  account: ResponseAccountJson;
  visible: boolean;
  returnFocus: RefObject<View | null>;
  onClose: () => void;
  refetch: () => Promise<void>;
}) {
  const { engine } = useSession();
  const client = useScopedApiClient();
  const toast = useToast();
  const base = accountEdit(account);
  const [input, setInput] = useState<AccountEdit>(base);
  const inputRef = useRef(input);
  const command = useAdminCommand();
  const update = usePutAccountAccountId({ request: { client } });
  const [saved, setSaved] = useState(false);
  const draft = useAdminDraft(`admin/account/${account.id}`, isAccountEdit, async () => {
    if (engine.getSnapshot().branch?.role === 'Member') return false;
    const fresh = await getAccountAccountId(account.id, { client: engine.client() });
    return fresh.data.branchId === engine.getSnapshot().branch?.id;
  });
  const change = (next: AccountEdit) => {
    inputRef.current = next;
    setInput(next);
    setSaved(false);
    void draft.keep(next, base);
  };
  const cancel = () => {
    if (!command.uncertain && !command.postWriteError) void draft.discard();
    onClose();
  };
  const submit = () => {
    const candidate = inputRef.current;
    void command.run(
      async () => {
        const kept = await draft.keep(candidate, base);
        const result = await update.mutateAsync({
          accountId: account.id,
          data: accountUpdatePayload(candidate),
        });
        return { kept, result };
      },
      async ({ kept }) => {
        setSaved(true);
        await draft.acknowledge(kept);
        toast.show('Conta salva no servidor.');
        await refetch();
      },
    );
  };
  return (
    <Overlay
      visible={visible}
      title={`Editar ${account.name}`}
      busy={command.pending}
      returnFocus={returnFocus}
      onDismiss={cancel}
      testID="account-editor"
      footer={
        <>
          <Button
            label="Salvar"
            testID="account-save"
            loading={command.pending}
            loadingLabel="Salvando…"
            disabled={command.blocked || input.name.trim() === ''}
            onPress={submit}
            fullWidth
          />
          <Button
            label="Cancelar"
            variant="secondary"
            disabled={command.pending}
            onPress={cancel}
            fullWidth
          />
        </>
      }
    >
      <RetainedNotice
        visible={!!draft.retained}
        onRestore={() => {
          const value = draft.retained!.input;
          inputRef.current = value;
          setInput(value);
          draft.dismissRetained();
        }}
      />
      {draft.problem ? <Notice kind="warning" title={draft.problem} /> : null}
      {saved && !command.postWriteError ? (
        <Notice kind="success" title="Conta salva no servidor." />
      ) : null}
      <CommandNotices
        command={command}
        receipt="Conta salva no servidor."
        reconcile={() =>
          void refetch()
            .then(() => command.clear())
            .catch(() => undefined)
        }
      />
      <AppText testID="account-type-readonly">{`Tipo: ${accountTypeLabels[account.type]} (não muda depois de criada)`}</AppText>
      <FormField label="Nome" required>
        <TextField
          testID="account-name"
          value={input.name}
          maxLength={255}
          onChangeText={(name) => change({ ...input, name })}
        />
      </FormField>
      <FormField label="Instituição">
        <TextField
          testID="account-institution"
          value={input.institution}
          onChangeText={(institution) => change({ ...input, institution })}
        />
      </FormField>
      <FormField label="Número">
        <TextField
          testID="account-number"
          value={input.number}
          onChangeText={(number) => change({ ...input, number })}
        />
      </FormField>
    </Overlay>
  );
}

const commandCopy = {
  tab: {
    title: (a: ResponseAccountJson) => `Criar conta fiado para ${a.name}?`,
    confirm: 'Criar conta fiado',
    receipt: 'Conta fiado criada e pareada no servidor.',
  },
  pair: {
    title: (a: ResponseAccountJson) => `Parear ${a.name} com uma conta fiado`,
    confirm: 'Parear',
    receipt: 'Pareamento salvo no servidor.',
  },
  unpair: {
    title: (a: ResponseAccountJson) => `Desfazer o pareamento de ${a.name}?`,
    confirm: 'Desfazer pareamento',
    receipt: 'Pareamento desfeito no servidor.',
  },
  terminal: {
    title: (a: ResponseAccountJson) => `Trocar o terminal CAIXA de ${a.name}`,
    confirm: 'Trocar terminal',
    receipt: 'Terminal CAIXA trocado no servidor.',
  },
  deactivate: {
    title: (a: ResponseAccountJson) => `Desativar ${a.name}?`,
    confirm: 'Desativar conta',
    receipt: 'Conta desativada no servidor.',
  },
} as const;
export function deactivationConsequence(
  account: ResponseAccountJson,
  accounts: readonly ResponseAccountJson[],
) {
  const pairing = accountPairing(account, accounts);
  const released =
    pairing.kind === 'paired'
      ? account.type === 'Terminal'
        ? ` A conta fiado ${pairing.with?.name ?? ''} é liberada do pareamento na mesma ação e pode ser pareada a outro caixa.`.replace(
            '  ',
            ' ',
          )
        : ` O caixa ${pairing.with?.name ?? ''} fica sem conta fiado na mesma ação.`.replace(
            '  ',
            ' ',
          )
      : '';
  const custody =
    account.type === 'Terminal'
      ? ' Com um turno aberto ou uma passagem pendente neste caixa, o servidor recusa a desativação.'
      : '';
  return `A conta sai da lista de contas ativas e as atribuições dela aos operadores são removidas.${released}${custody} O histórico de movimentos continua registrado.`;
}

function AccountCommand({
  kind,
  account,
  visible,
  accounts,
  terminals,
  onClose,
  refetch,
}: {
  kind: 'tab' | 'pair' | 'unpair' | 'terminal' | 'deactivate';
  account: ResponseAccountJson;
  visible: boolean;
  accounts: readonly ResponseAccountJson[];
  terminals: readonly ResponseTerminalJson[];
  onClose: () => void;
  refetch: () => Promise<void>;
}) {
  const client = useScopedApiClient();
  const toast = useToast();
  const command = useAdminCommand();
  const written = useConfirmedWrite(command, onClose);
  const gate = useFreshGate(refetch, command, written);
  const createTab = usePostAccountTab({ request: { client } });
  const pair = usePostAccountPairTab({ request: { client } });
  const unpair = useDeleteAccountTerminalTerminalAccountIdTab({ request: { client } });
  const reassign = usePutTerminalCashboxAccountId({ request: { client } });
  const remove = useDeleteAccountAccountId({ request: { client } });
  const [choice, setChoice] = useState<string | null>(null);
  const copy = commandCopy[kind];
  const pairing = accountPairing(account, accounts);
  const tabs = unpairedTabs(accounts);
  const terminalOptions = terminals.filter((t) => t.id !== account.terminalId);
  const consequence =
    kind === 'tab'
      ? `Uma conta fiado nova nasce pareada a ${account.name}. ${PAIRING_GRANTS_NOTHING}`
      : kind === 'pair'
        ? PAIRING_GRANTS_NOTHING
        : kind === 'unpair'
          ? `${account.name} deixa de ter conta fiado e não pode registrar vendas fiado no balcão até ser pareado de novo. A conta fiado ${pairing.kind === 'paired' ? (pairing.with?.name ?? '') : ''} continua ativa, com o saldo e o histórico, e pode ser pareada a outro caixa.`
          : kind === 'terminal'
            ? `Os turnos anteriores mantêm o terminal em que foram registrados. Com um turno aberto ou uma passagem pendente neste caixa, o servidor recusa a troca.`
            : deactivationConsequence(account, accounts);
  const needsChoice = kind === 'pair' || kind === 'terminal';
  const confirm = () => {
    if (gate.stale) return void gate.reread();
    if (command.blocked || written.confirmed || (needsChoice && !choice)) return;
    void command.run(
      async () => {
        if (kind === 'tab')
          return createTab.mutateAsync({ data: { terminalAccountId: account.id } });
        if (kind === 'pair')
          return pair.mutateAsync({
            data: { terminalAccountId: account.id, tabAccountId: choice! },
          });
        if (kind === 'unpair') return unpair.mutateAsync({ terminalAccountId: account.id });
        if (kind === 'terminal')
          return reassign.mutateAsync({ accountId: account.id, data: { terminalId: choice! } });
        return remove.mutateAsync({ accountId: account.id });
      },
      async () => {
        written.confirm();
        toast.show(copy.receipt);
        await refetch();
        written.settle();
      },
    );
  };
  return (
    <ConfirmDialog
      visible={visible}
      title={copy.title(account)}
      destructive={kind === 'deactivate'}
      busy={command.pending || gate.rereading}
      busyLabel={gate.rereading ? 'Relendo…' : 'Salvando…'}
      confirmLabel={gate.stale ? 'Reler contas e conferir' : copy.confirm}
      consequence={consequence}
      onDismiss={onClose}
      onConfirm={confirm}
      testID={`account-command-${kind}`}
    >
      {gate.stale && !command.uncertain && !command.postWriteError ? (
        <Notice
          kind="warning"
          title="As contas mudaram ou estão em uso por outra alteração."
          body="Releia as contas antes de tentar de novo. Nada foi reenviado."
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
      {kind === 'pair' ? (
        <FormField label="Conta fiado sem caixa" required>
          <Select
            testID="account-pair-choice"
            value={choice}
            onValueChange={setChoice}
            options={tabs.map((t) => ({ value: t.id, label: t.name }))}
            emptyText="Nenhuma conta fiado sem caixa. Crie uma conta fiado para este caixa."
          />
        </FormField>
      ) : null}
      {kind === 'terminal' ? (
        <>
          <AppText>{`Terminal atual: ${terminalText(account, terminals)}`}</AppText>
          <FormField label="Novo terminal CAIXA" required>
            <Select
              testID="account-terminal-choice"
              value={choice}
              onValueChange={setChoice}
              options={terminalOptions.map((t) => ({
                value: t.id,
                label: t.externalReference ? `${t.name} (${t.externalReference})` : t.name,
              }))}
              emptyText="Nenhum outro terminal CAIXA ativo."
            />
          </FormField>
        </>
      ) : null}
    </ConfirmDialog>
  );
}
