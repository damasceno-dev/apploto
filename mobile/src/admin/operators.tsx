import { useCallback, useEffect, useRef, useState, type RefObject } from 'react';
import { useFocusEffect, useLocalSearchParams } from 'expo-router';
import type { View } from 'react-native';
import { getOperatorOperatorId, type ResponseOperatorJson } from '@loto/api';
import {
  useDeleteOperatorOperatorId,
  useGetBranchUsers,
  useGetOperator,
  usePostOperator,
  usePutOperatorOperatorId,
} from '@loto/api/queries';
import { roleLabels } from '@loto/core';
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
  Overlay,
  Select,
  TextField,
  moveFocusTo,
  useToast,
} from '@/ui';
import {
  isOperatorInput,
  linkableMembers,
  operatorLogin,
  operatorPayload,
  type OperatorInput,
} from './contracts';
import {
  CommandNotices,
  ReadState,
  RetainedNotice,
  useAdminCommand,
  useAdminDraft,
  useAdminReads,
} from './common';

export const UNLINK_CONSEQUENCE =
  'Remover o login desfaz somente o vínculo. O funcionário, o histórico, as contas atribuídas e o acesso da pessoa à filial continuam como estão.';
const LINK_GRANTS_NOTHING =
  'Vincular um login não atribui contas nem altera o acesso à filial. Atribua terminais em Contas dos operadores.';
export const SELF_LINK_RECEIPT = 'Seu vínculo de operador nesta filial foi atualizado no servidor.';
/**
 * A self-link change makes the engine replace the scope (and the shell beneath it), so the
 * confirmed receipt is kept per engine, identity and branch in memory until dismissed.
 */
const receipts = new WeakMap<object, Set<string>>();
type Editing =
  { readonly kind: 'new' } | { readonly kind: 'edit'; readonly operator: ResponseOperatorJson };

export function OperatorsScreen() {
  const params = useLocalSearchParams<{ focus?: string }>();
  const { snapshot, engine } = useSession();
  const client = useScopedApiClient();
  const shell = useShell();
  const reads = useAdminReads();
  const operators = useGetOperator({
    request: { client },
    query: { staleTime: 0, enabled: reads },
  });
  const members = useGetBranchUsers({
    request: { client },
    query: { staleTime: 0, enabled: reads },
  });
  const [editing, setEditing] = useState<Editing | null>(null);
  const [deactivating, setDeactivating] = useState<ResponseOperatorJson | null>(null);
  const receiptOwner = `${snapshot.user?.userId}:${snapshot.branch?.id}`;
  const [receipt, setReceipt] = useState(() => !!receipts.get(engine)?.has(receiptOwner));
  const rows = useRef(new Map<string, View>());
  const trigger = useRef<View>(null);
  useFocusEffect(
    useCallback(() => {
      if (params.focus && operators.data) moveFocusTo(rows.current.get(params.focus));
    }, [params.focus, operators.data]),
  );
  const selfId =
    snapshot.selfStatus === 'ready' ? (snapshot.selfContext?.operatorId ?? null) : null;
  const list = operators.data?.data.operators ?? [];
  const memberList = members.data?.data.branchUsers ?? [];
  const refetch = async () => {
    const [a, b] = await Promise.all([operators.refetch(), members.refetch()]);
    if (a.error || b.error) throw a.error ?? b.error;
  };
  /**
   * A confirmed change to the caller's own link: the receipt is recorded immediately (it outlives
   * the scope the engine replaces when the link changes), then self-context re-resolves.
   */
  const afterWrite = async (
    touched: readonly (string | null | undefined)[],
    operatorId: string,
    phase: 'receipt' | 'settle' = 'settle',
  ) => {
    const me = engine.getSnapshot().user?.userId;
    if (operatorId === selfId || (me && touched.includes(me))) {
      const owner = `${me}:${engine.getSnapshot().branch?.id}`;
      receipts.set(engine, (receipts.get(engine) ?? new Set()).add(owner));
      setReceipt(true);
      if (phase === 'receipt') return;
      await engine.recheckSelfContext();
    }
  };
  return (
    <>
      <Button
        label="Adicionar funcionário"
        onPress={() => void shell.navigate('/gestao/funcionarios/novo', true)}
      />
      <Button
        ref={trigger}
        label="Novo operador sem login"
        testID="operator-new"
        variant="secondary"
        disabled={!reads}
        onPress={() => setEditing({ kind: 'new' })}
      />
      {receipt ? (
        <Notice
          kind="success"
          title={SELF_LINK_RECEIPT}
          testID="operator-self-receipt"
          onDismiss={() => {
            receipts.get(engine)?.delete(receiptOwner);
            setReceipt(false);
          }}
        />
      ) : null}
      {snapshot.selfStatus === 'failed' ? (
        <Notice
          kind="warning"
          title="Não foi possível identificar seu operador nesta filial."
          messages={snapshot.selfMessages}
        />
      ) : null}
      <ReadState
        pending={operators.isPending}
        error={operators.error}
        retry={() => void operators.refetch()}
      />
      <ReadState
        pending={members.isPending}
        error={members.error}
        retry={() => void members.refetch()}
      />
      {operators.data && !operators.error && members.data && !members.error ? (
        <>
          {list.length === 0 ? (
            <EmptyState
              kind="no-records"
              statement="Nenhum operador ativo."
              explanation="Adicione um funcionário para começar a registrar transações."
            />
          ) : null}
          {list.map((operator) => {
            const login = operatorLogin(operator, memberList);
            return (
              <Card key={operator.id} title={operator.name} testID={`operator-row-${operator.id}`}>
                {operator.id === selfId ? <Badge kind="feedback" tone="info" label="você" /> : null}
                <AppText testID={`operator-login-${operator.id}`}>
                  {login.kind === 'member'
                    ? `Login: ${login.member.name} · ${login.member.email} · ${roleLabels[login.member.role]}`
                    : login.kind === 'none'
                      ? 'Login: Sem login'
                      : 'Login: vinculado, mas não encontrado entre os membros ativos desta filial'}
                </AppText>
                <Button
                  label={`Editar ${operator.name}`}
                  testID={`operator-edit-${operator.id}`}
                  disabled={!reads}
                  ref={(node) => {
                    if (node) rows.current.set(operator.id, node);
                  }}
                  onPress={() => setEditing({ kind: 'edit', operator })}
                />
                <Button
                  label={`Transações de ${operator.name}`}
                  testID={`operator-summary-${operator.id}`}
                  variant="secondary"
                  onPress={() =>
                    void shell.navigate(
                      `/gestao/operadores/${operator.id}/transacoes`,
                      true,
                      false,
                      operator.id,
                    )
                  }
                />
                <Button
                  label={`Contas de ${operator.name}`}
                  variant="secondary"
                  onPress={() =>
                    void shell.navigate(
                      '/gestao/contas-dos-operadores?' +
                        new URLSearchParams({ operatorId: operator.id }),
                      true,
                      false,
                      operator.id,
                    )
                  }
                />
                <Button
                  label={`Desligar ${operator.name}`}
                  variant="secondary"
                  onPress={() =>
                    void shell.navigate(
                      `/gestao/funcionarios/${operator.id}/desligar`,
                      true,
                      false,
                      operator.id,
                    )
                  }
                />
                <Button
                  label={`Desativar ${operator.name}`}
                  testID={`operator-deactivate-${operator.id}`}
                  variant="danger"
                  disabled={!reads}
                  onPress={() => setDeactivating(operator)}
                />
              </Card>
            );
          })}
        </>
      ) : null}
      {editing && operators.data && members.data ? (
        <OperatorEditor
          key={editing.kind === 'new' ? 'new' : editing.operator.id}
          editing={editing}
          visible={reads}
          returnFocus={trigger}
          operators={list}
          members={memberList}
          onClose={() => setEditing(null)}
          refetch={refetch}
          afterWrite={afterWrite}
        />
      ) : null}
      {deactivating ? (
        <OperatorDeactivation
          operator={deactivating}
          visible={reads}
          onClose={() => setDeactivating(null)}
          refetch={refetch}
          afterWrite={afterWrite}
        />
      ) : null}
    </>
  );
}

function OperatorEditor({
  editing,
  visible,
  operators,
  members,
  onClose,
  refetch,
  afterWrite,
  returnFocus,
}: {
  editing: Editing;
  visible: boolean;
  operators: readonly ResponseOperatorJson[];
  members: Parameters<typeof linkableMembers>[0];
  onClose: () => void;
  refetch: () => Promise<void>;
  afterWrite: (
    touched: readonly (string | null | undefined)[],
    operatorId: string,
    phase?: 'receipt' | 'settle',
  ) => Promise<void>;
  returnFocus: RefObject<View | null>;
}) {
  const { engine } = useSession();
  const client = useScopedApiClient();
  const toast = useToast();
  const id = editing.kind === 'edit' ? editing.operator.id : null;
  const original = editing.kind === 'edit' ? (editing.operator.userId ?? null) : null;
  const base: OperatorInput =
    editing.kind === 'edit'
      ? { name: editing.operator.name, userId: original }
      : { name: '', userId: null };
  const [input, setInput] = useState(base);
  const inputRef = useRef(input);
  const command = useAdminCommand();
  const create = usePostOperator({ request: { client } });
  const update = usePutOperatorOperatorId({ request: { client } });
  const [saved, setSaved] = useState(false);
  const [unlinking, setUnlinking] = useState(false);
  const draft = useAdminDraft(`admin/operator/${id ?? 'new'}`, isOperatorInput, async () => {
    if (engine.getSnapshot().branch?.role === 'Member') return false;
    if (!id) return true;
    const fresh = await getOperatorOperatorId(id, { client: engine.client() });
    return fresh.data.branchId === engine.getSnapshot().branch?.id;
  });
  // Explicit cancel abandons unsent input; an unresolved outcome keeps it for reconciliation.
  const cancel = () => {
    if (!command.uncertain && !command.postWriteError) void draft.discard();
    onClose();
  };
  const options = linkableMembers(members, operators, id);
  const change = (next: OperatorInput) => {
    inputRef.current = next;
    setInput(next);
    setSaved(false);
    void draft.keep(next, base);
  };
  const submit = () => {
    const candidate = inputRef.current;
    void command.run(
      async () => {
        const kept = await draft.keep(candidate, base);
        const data = operatorPayload(candidate);
        const result = id
          ? await update.mutateAsync({ operatorId: id, data })
          : await create.mutateAsync({ data });
        await afterWrite([original, result.data.userId], result.data.id, 'receipt');
        return { result, kept };
      },
      async ({ result, kept }) => {
        setSaved(true);
        await draft.acknowledge(kept);
        toast.show('Operador salvo no servidor.');
        await afterWrite([original, result.data.userId], result.data.id);
        await refetch();
        // A new record closes only after the reread; a failure keeps the receipt here.
        if (!id) onClose();
      },
    );
  };
  const blocked = command.blocked || (!id && saved);
  return (
    <Overlay
      visible={visible}
      title={
        id ? `Editar ${editing.kind === 'edit' ? editing.operator.name : ''}` : 'Novo operador'
      }
      busy={command.pending}
      returnFocus={returnFocus}
      onDismiss={cancel}
      testID="operator-editor"
      footer={
        <>
          <Button
            label="Salvar"
            testID="operator-save"
            loading={command.pending}
            loadingLabel="Salvando…"
            disabled={blocked || input.name.trim() === ''}
            onPress={() =>
              id && original && input.userId === null ? setUnlinking(true) : submit()
            }
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
        <Notice kind="success" title="Operador salvo no servidor." />
      ) : null}
      <CommandNotices
        command={command}
        receipt="Operador salvo no servidor."
        reconcile={() =>
          void refetch()
            .then(() => command.clear())
            .catch(() => undefined)
        }
      />
      <FormField label="Nome" required>
        <TextField
          testID="operator-name"
          value={input.name}
          maxLength={255}
          onChangeText={(name) => change({ ...input, name })}
        />
      </FormField>
      <FormField label="Login" helper={LINK_GRANTS_NOTHING}>
        <Select
          testID="operator-login"
          value={input.userId ?? ''}
          onValueChange={(value) => change({ ...input, userId: value === '' ? null : value })}
          options={[
            { value: '', label: 'Sem login' },
            ...options.map((m) => ({
              value: m.userId,
              label: `${m.name} · ${m.email} · ${roleLabels[m.role]}`,
            })),
          ]}
        />
      </FormField>
      {id && input.userId ? (
        <Button
          label="Remover login"
          testID="operator-unlink"
          variant="secondary"
          disabled={command.blocked}
          onPress={() => change({ ...input, userId: null })}
        />
      ) : null}
      {id && original && input.userId === null ? (
        <AppText variant="helper">{UNLINK_CONSEQUENCE}</AppText>
      ) : null}
      <ConfirmDialog
        visible={visible && unlinking}
        title="Remover o login deste operador?"
        consequence={UNLINK_CONSEQUENCE}
        confirmLabel="Remover login e salvar"
        busy={command.pending}
        onDismiss={() => setUnlinking(false)}
        onConfirm={() => {
          setUnlinking(false);
          submit();
        }}
      />
    </Overlay>
  );
}

function OperatorDeactivation({
  operator,
  visible,
  onClose,
  refetch,
  afterWrite,
}: {
  operator: ResponseOperatorJson;
  visible: boolean;
  onClose: () => void;
  refetch: () => Promise<void>;
  afterWrite: (
    touched: readonly (string | null | undefined)[],
    operatorId: string,
    phase?: 'receipt' | 'settle',
  ) => Promise<void>;
}) {
  const client = useScopedApiClient();
  const toast = useToast();
  const command = useAdminCommand();
  const remove = useDeleteOperatorOperatorId({ request: { client } });
  const [done, setDone] = useState(false);
  useEffect(() => {
    if (done && !command.postWriteError) onClose();
  }, [done, command.postWriteError, onClose]);
  return (
    <ConfirmDialog
      visible={visible}
      title={`Desativar ${operator.name}`}
      destructive
      busy={command.pending}
      busyLabel="Desativando…"
      confirmLabel="Desativar operador"
      consequence="O operador sai da lista de operadores ativos e as contas atribuídas a ele são removidas. Transações, fechamentos e registros de ponto continuam com o nome. O login e o acesso da pessoa à filial não mudam."
      onDismiss={onClose}
      onConfirm={() => {
        if (command.blocked || done) return;
        void command.run(
          async () => {
            const result = await remove.mutateAsync({ operatorId: operator.id });
            await afterWrite([operator.userId], operator.id, 'receipt');
            return result;
          },
          async () => {
            toast.show('Operador desativado.');
            await afterWrite([operator.userId], operator.id);
            // Close only after the reread: a failure keeps the confirmed receipt and its recovery.
            await refetch();
            setDone(true);
          },
        );
      }}
    >
      <CommandNotices
        command={command}
        receipt="Operador desativado no servidor."
        reconcile={() =>
          void refetch()
            .then(() => {
              command.clear();
              onClose();
            })
            .catch(() => undefined)
        }
      />
    </ConfirmDialog>
  );
}
