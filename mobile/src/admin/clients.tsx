import { useCallback, useEffect, useRef, useState, type RefObject } from 'react';
import { useFocusEffect, useLocalSearchParams } from 'expo-router';
import type { View } from 'react-native';
import { getClientClientId, type ResponseClientJson } from '@loto/api';
import {
  useDeleteClientClientId,
  useGetClient,
  usePostClient,
  usePutClientClientId,
} from '@loto/api/queries';
import { formatCount, formatCpf, formatOptional, formatPhone } from '@loto/core';
import { useSession, useScopedApiClient } from '@/session/provider';
import { useShell } from '@/shell/provider';
import { matchDestination } from '@/shell/policy';
import {
  AppText,
  Button,
  Card,
  ConfirmDialog,
  EmptyState,
  FormField,
  IdentifierInput,
  Notice,
  Overlay,
  TextField,
  moveFocusTo,
  useToast,
} from '@/ui';
import {
  clientCountLabel,
  clientInput,
  clientPayload,
  emptyClient,
  isClientInput,
  searchClients,
  type ClientInput,
} from './contracts';
import {
  CommandNotices,
  ReadState,
  RetainedNotice,
  useAdminCommand,
  useAdminDraft,
  useAdminReads,
} from './common';

/**
 * In-memory search text per engine, verified identity, branch and scope: presentation only, never
 * written to the device, and never shared across sign-ins or branches.
 */
const searches = new WeakMap<object, Map<string, string>>();
function searchMemory(engine: object, key: string) {
  const own = searches.get(engine) ?? new Map<string, string>();
  searches.set(engine, own);
  return {
    get: () => own.get(key) ?? '',
    set: (value: string) => void own.set(key, value),
  };
}
/** Statement access is not client CRUD: Manager/Admin, or a Member holding an active Tab grant. */
export function statementAllowed(snapshot: ReturnType<typeof useSession>['snapshot']): boolean {
  const role = snapshot.branch?.role;
  if (role === 'Admin' || role === 'Manager') return true;
  return (
    role === 'Member' &&
    snapshot.selfStatus === 'ready' &&
    !!snapshot.selfContext?.availableAccounts.some((account) => account.accountType === 'Tab')
  );
}
export const STATEMENT_SETUP =
  'O extrato de fiado exige uma conta fiado atribuída ao seu operador. Peça à administração da filial para conferir suas contas.';
/** Validated create-return: only a known create origin may receive a freshly read client id. */
export const clientReturnOrigins = ['create', 'plan-new'] as const;

type Editing =
  { readonly kind: 'new' } | { readonly kind: 'edit'; readonly client: ResponseClientJson };
export function ClientsScreen() {
  const params = useLocalSearchParams<{ section?: string; focus?: string }>();
  const { snapshot, engine } = useSession();
  const client = useScopedApiClient();
  const shell = useShell();
  const reads = useAdminReads();
  const memory = searchMemory(
    engine,
    `${snapshot.user?.userId}:${snapshot.branch?.id}:${snapshot.scope}`,
  );
  const elevated = snapshot.branch?.role !== 'Member';
  const list = useGetClient({ request: { client }, query: { staleTime: 0, enabled: reads } });
  const [query, setQueryState] = useState(memory.get);
  const setQuery = (value: string) => {
    memory.set(value);
    setQueryState(value);
  };
  const [editing, setEditing] = useState<Editing | null>(
    params.section === 'new' ? { kind: 'new' } : null,
  );
  const [deactivating, setDeactivating] = useState<ResponseClientJson | null>(null);
  const [created, setCreated] = useState<ResponseClientJson | null>(null);
  const rows = useRef(new Map<string, View>());
  const trigger = useRef<View>(null);
  useFocusEffect(
    useCallback(() => {
      if (params.focus && list.data) moveFocusTo(rows.current.get(params.focus));
    }, [params.focus, list.data]),
  );
  const origin = shell.origins.at(-1);
  const originKey = origin ? matchDestination(origin.split('?')[0]!)?.key : undefined;
  const returnsToCreate = clientReturnOrigins.some((key) => key === originKey);
  const all = list.data?.data.clients ?? [];
  const shown = searchClients(all, query);
  const canStatement = statementAllowed(snapshot);
  return (
    <>
      <Button
        ref={trigger}
        label="Adicionar cliente"
        testID="client-new"
        disabled={!reads}
        onPress={() => setEditing({ kind: 'new' })}
      />
      {created ? (
        <Notice
          kind="success"
          title={`Cliente ${created.name} salvo no servidor.`}
          onDismiss={() => setCreated(null)}
          actions={
            returnsToCreate ? (
              <Button
                label="Voltar com este cliente"
                testID="client-return"
                onPress={() => void shell.back({ clientId: created.id })}
              />
            ) : undefined
          }
        />
      ) : null}
      <FormField label="Buscar por nome">
        <TextField
          kind="search"
          testID="client-search"
          value={query}
          onChangeText={setQuery}
          resultCountLabel={list.data ? clientCountLabel(shown.length) : undefined}
        />
      </FormField>
      {!canStatement ? (
        <Notice kind="info" title="Extrato de fiado" body={STATEMENT_SETUP} />
      ) : null}
      <ReadState pending={list.isPending} error={list.error} retry={() => void list.refetch()} />
      {list.data && !list.error ? (
        <>
          <AppText testID="client-count" accessibilityLiveRegion="polite">
            {query.trim()
              ? clientCountLabel(shown.length)
              : `${formatCount(all.length)} ${all.length === 1 ? 'cliente' : 'clientes'}`}
          </AppText>
          {all.length === 0 ? (
            <EmptyState
              kind="no-records"
              statement="Nenhum cliente cadastrado."
              explanation="Cadastre o primeiro cliente para registrar vendas fiado."
            />
          ) : shown.length === 0 ? (
            <EmptyState kind="no-matches" statement="Nenhum cliente com este nome." />
          ) : null}
          {shown.map((row) => (
            <Card key={row.id} title={row.name} testID={`client-row-${row.id}`}>
              <AppText>{`Telefone: ${formatPhone(row.phone)}`}</AppText>
              <AppText>{`CPF: ${formatOptional(row.cpf, formatCpf)}`}</AppText>
              {canStatement ? (
                <Button
                  label={`Ver extrato de ${row.name}`}
                  variant="secondary"
                  ref={(node) => {
                    if (node) rows.current.set(row.id, node);
                  }}
                  onPress={() =>
                    void shell.navigate(`/clientes/${row.id}/extrato`, true, false, row.id)
                  }
                />
              ) : null}
              <Button
                label={`Editar ${row.name}`}
                testID={`client-edit-${row.id}`}
                disabled={!reads}
                ref={
                  canStatement
                    ? undefined
                    : (node) => {
                        if (node) rows.current.set(row.id, node);
                      }
                }
                onPress={() => setEditing({ kind: 'edit', client: row })}
              />
              {elevated ? (
                <Button
                  label={`Desativar ${row.name}`}
                  testID={`client-deactivate-${row.id}`}
                  variant="danger"
                  disabled={!reads}
                  onPress={() => setDeactivating(row)}
                />
              ) : null}
            </Card>
          ))}
        </>
      ) : null}
      {editing ? (
        <ClientEditor
          key={editing.kind === 'new' ? 'new' : editing.client.id}
          editing={editing}
          visible={reads}
          returnFocus={trigger}
          onClose={() => setEditing(null)}
          onCreated={(value) => {
            setCreated(value);
            setEditing(null);
          }}
          refetch={async () => {
            const result = await list.refetch();
            if (result.error || !result.data)
              throw result.error ?? new Error('Lista indisponível.');
            return result.data.data.clients;
          }}
        />
      ) : null}
      {deactivating && elevated ? (
        <ClientDeactivation
          client={deactivating}
          visible={reads}
          onClose={() => setDeactivating(null)}
          refetch={async () => {
            const result = await list.refetch();
            if (result.error) throw result.error;
          }}
        />
      ) : null}
    </>
  );
}

function ClientEditor({
  editing,
  visible,
  onClose,
  onCreated,
  refetch,
  returnFocus,
}: {
  editing: Editing;
  visible: boolean;
  onClose: () => void;
  onCreated: (client: ResponseClientJson) => void;
  refetch: () => Promise<readonly ResponseClientJson[]>;
  returnFocus: RefObject<View | null>;
}) {
  const { engine } = useSession();
  const client = useScopedApiClient();
  const toast = useToast();
  const id = editing.kind === 'edit' ? editing.client.id : null;
  const base = editing.kind === 'edit' ? clientInput(editing.client) : emptyClient;
  const [input, setInput] = useState<ClientInput>(base);
  const inputRef = useRef(input);
  const command = useAdminCommand();
  const create = usePostClient({ request: { client } });
  const update = usePutClientClientId({ request: { client } });
  const [saved, setSaved] = useState<ResponseClientJson | null>(null);
  const [matches, setMatches] = useState<readonly ResponseClientJson[] | null>(null);
  const draft = useAdminDraft(`admin/client/${id ?? 'new'}`, isClientInput, async () => {
    if (!id) return true;
    const fresh = await getClientClientId(id, { client: engine.client() });
    return fresh.data.branchId === engine.getSnapshot().branch?.id;
  });
  // Explicit cancel abandons unsent input; an unresolved outcome keeps it for reconciliation.
  const cancel = () => {
    if (!command.uncertain && !command.postWriteError) void draft.discard();
    onClose();
  };
  const change = (key: keyof ClientInput, value: string) => {
    const next = { ...inputRef.current, [key]: value };
    inputRef.current = next;
    setInput(next);
    void draft.keep(next, base);
  };
  const reconcile = () => {
    void (async () => {
      try {
        const current = await refetch();
        const candidate = clientPayload(inputRef.current);
        if (id) setSaved((await getClientClientId(id, { client })).data);
        else
          setMatches(
            current.filter(
              (c) =>
                c.name.trim() === candidate.name.trim() ||
                (!!candidate.cpf && c.cpf === candidate.cpf),
            ),
          );
        command.clear();
      } catch {
        // The notice remains; another explicit reread is available.
      }
    })();
  };
  const submit = () => {
    const candidate = inputRef.current;
    setMatches(null);
    void command.run(
      async () => {
        const kept = await draft.keep(candidate, base);
        const data = clientPayload(candidate);
        const result = id
          ? await update.mutateAsync({ clientId: id, data })
          : await create.mutateAsync({ data });
        return { result, kept };
      },
      async ({ result, kept }) => {
        // The response is the receipt: show it before any acknowledgement or reread can fail.
        setSaved(result.data);
        await draft.acknowledge(kept);
        if (!id) {
          toast.show('Cliente salvo no servidor.');
          onCreated(result.data);
        }
        await refetch();
      },
    );
  };
  const blocked = command.blocked || (!id && !!saved);
  return (
    <Overlay
      visible={visible}
      title={id ? `Editar ${editing.kind === 'edit' ? editing.client.name : ''}` : 'Novo cliente'}
      busy={command.pending}
      returnFocus={returnFocus}
      onDismiss={cancel}
      testID="client-editor"
      footer={
        <>
          <Button
            label="Salvar"
            testID="client-save"
            loading={command.pending}
            loadingLabel="Salvando…"
            disabled={blocked}
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
      {saved && id && !command.postWriteError ? (
        <Notice kind="success" title="Cliente salvo no servidor." />
      ) : null}
      <CommandNotices
        command={command}
        receipt="Cliente salvo no servidor."
        reconcile={reconcile}
      />
      {matches ? (
        <Notice
          kind={matches.length ? 'warning' : 'info'}
          title={
            matches.length
              ? 'A lista atual já tem cliente com este nome ou CPF.'
              : 'A lista atual não mostra este cliente.'
          }
          body={
            matches.length
              ? `${matches.map((m) => `${m.name} · ${formatPhone(m.phone)}`).join('; ')}. Confira antes de salvar de novo; nada é reenviado automaticamente.`
              : 'Nada é reenviado automaticamente. Salve de novo somente se o cliente ainda não existir.'
          }
        />
      ) : null}
      <FormField label="Nome" required>
        <TextField
          testID="client-name"
          value={input.name}
          maxLength={255}
          onChangeText={(value) => change('name', value)}
        />
      </FormField>
      <FormField label="Telefone" required>
        <IdentifierInput
          kind="phone"
          testID="client-phone"
          value={input.phone}
          onChangeText={(value) => change('phone', value)}
        />
      </FormField>
      <FormField label="CPF" helper="Opcional. Enviado somente com os números.">
        <IdentifierInput
          kind="cpf"
          testID="client-cpf"
          value={input.cpf}
          onChangeText={(value) => change('cpf', value)}
        />
      </FormField>
      <FormField label="CEP" helper="Opcional.">
        <IdentifierInput
          kind="cep"
          testID="client-cep"
          value={input.cep}
          onChangeText={(value) => change('cep', value)}
        />
      </FormField>
      <FormField label="Endereço" helper="Opcional.">
        <TextField value={input.address} onChangeText={(value) => change('address', value)} />
      </FormField>
      <FormField label="Telefone 2" helper="Opcional.">
        <IdentifierInput
          kind="phone"
          value={input.phoneSecondary}
          onChangeText={(value) => change('phoneSecondary', value)}
        />
      </FormField>
      <FormField label="E-mail" helper="Opcional.">
        <TextField
          kind="email"
          testID="client-email"
          value={input.email}
          onChangeText={(value) => change('email', value)}
        />
      </FormField>
      <FormField label="Observações" helper="Opcional.">
        <TextField
          kind="textarea"
          value={input.notes}
          onChangeText={(value) => change('notes', value)}
        />
      </FormField>
      {id ? (
        <AppText variant="helper">
          Campos opcionais deixados em branco são apagados do cadastro ao salvar.
        </AppText>
      ) : null}
    </Overlay>
  );
}

function ClientDeactivation({
  client: row,
  visible,
  onClose,
  refetch,
}: {
  client: ResponseClientJson;
  visible: boolean;
  onClose: () => void;
  refetch: () => Promise<void>;
}) {
  const client = useScopedApiClient();
  const toast = useToast();
  const command = useAdminCommand();
  const remove = useDeleteClientClientId({ request: { client } });
  const [done, setDone] = useState(false);
  useEffect(() => {
    if (done && !command.postWriteError) onClose();
  }, [done, command.postWriteError, onClose]);
  return (
    <ConfirmDialog
      visible={visible}
      title={`Desativar ${row.name}`}
      destructive
      busy={command.pending}
      busyLabel="Desativando…"
      confirmLabel="Desativar cliente"
      consequence="O cliente sai da lista e não pode ser escolhido em novas vendas fiado. As transações já registradas continuam no histórico."
      onDismiss={onClose}
      onConfirm={() => {
        if (command.blocked || done) return;
        void command.run(
          () => remove.mutateAsync({ clientId: row.id }),
          async () => {
            toast.show('Cliente desativado.');
            // Close only after the reread: a failure keeps the confirmed receipt and its recovery.
            await refetch();
            setDone(true);
          },
        );
      }}
    >
      <CommandNotices
        command={command}
        receipt="Cliente desativado no servidor."
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
