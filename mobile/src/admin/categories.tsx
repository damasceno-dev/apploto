import { useCallback, useRef, useState, type RefObject } from 'react';
import { useFocusEffect, useLocalSearchParams, useRouter } from 'expo-router';
import type { View } from 'react-native';
import {
  FiadoPaymentKind,
  MonthlyFinancialRole,
  SettlementInstrument,
  SettlementRule,
  getCategoryCategoryId,
  getTransactionTypeTransactionTypeId,
  type Direction,
  type ResponseCategoryJson,
  type ResponseTransactionTypeJson,
  type TransactionTypeSystemRole,
} from '@loto/api';
import {
  useDeleteCategoryCategoryId,
  useDeleteTransactionTypeTransactionTypeId,
  useGetCategory,
  useGetTransactionType,
  usePostCategory,
  usePostTransactionType,
  usePutCategoryCategoryId,
  usePutTransactionTypeTransactionTypeId,
} from '@loto/api/queries';
import {
  directionLabels,
  fiadoPaymentKindLabels,
  monthlyFinancialRoleLabels,
  settlementInstrumentLabels,
  settlementRuleLabels,
  transactionTypeSystemRoleLabels,
} from '@loto/core';
import { useSession, useScopedApiClient } from '@/session/provider';
import {
  AppText,
  Badge,
  Button,
  Card,
  ConfirmDialog,
  Disclosure,
  EmptyState,
  FormField,
  Notice,
  Overlay,
  Radio,
  RadioGroup,
  Select,
  Switch,
  Tabs,
  TextField,
  moveFocusTo,
  useToast,
} from '@/ui';
import {
  emptyType,
  fiadoKindAllowed,
  hostsSystemType,
  isCategoryInput,
  isSystemCategory,
  isTypeInput,
  monthlyRoleAllowed,
  reclassifies,
  typeCreatePayload,
  typeInput,
  typePayload,
  typeProblems,
  type CategoryInput,
  type TypeInput,
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
} from './common';

/**
 * Current v61 behavior: report classifications are read when a report is produced, so a change
 * re-labels the type's existing rows. Kept in one place for the later classification contract.
 */
export const RECLASSIFICATION_POLICY =
  'Recebível e Relatório financeiro mensal são lidos quando o relatório é gerado: mudar a classificação muda como os relatórios mostram os movimentos já registrados deste tipo, inclusive em meses bloqueados. Nenhum movimento, diferença oficial ou bloqueio muda.';
export const FIADO_KIND_CONDITION =
  'Disponível só para um tipo de entrada que exige conta fiado e cliente: ele passa a ser uma forma de recebimento de fiado.';
export const MONTHLY_ROLE_CONDITION =
  'Tarifa de bolão e Tarifa especial só valem em categoria de entrada; Sobra (perda comercial) só em categoria de saída.';
export const SYSTEM_TYPE_READ_ONLY =
  'Tipos automáticos não são criados, editados nem desativados manualmente.';
export const TYPE_DEACTIVATION =
  'O tipo sai das listas de escolha. Os movimentos já registrados continuam, e os relatórios seguem usando a classificação dele. Esta tela não oferece reativação.';
export const CATEGORY_DEACTIVATION =
  'A categoria e todos os tipos ativos dela saem das listas de escolha na mesma ação. Os movimentos já registrados continuam. Esta tela não oferece reativação.';
const systemSources: Record<TransactionTypeSystemRole, string> = {
  FiadoSaleCounterpart: 'Escrito pela venda fiado no balcão, no caixa pareado.',
  FiadoCashRepaymentCounterpart: 'Escrito pelo recebimento de fiado em dinheiro.',
  TerminalReportIn: 'Escrito só pelo campo Relatório do terminal do fechamento.',
  TerminalReportOut: 'Escrito só pelo campo Relatório do terminal do fechamento (valor negativo).',
  BolaoFee: 'Escrito só pelo campo Tarifa Bolão do fechamento.',
  SpecialFee: 'Escrito só pelo campo Tarifa Especial do fechamento.',
  SafeDepositOut: 'Escrito só pelo Depósito Dinheiro no cofre (saída do caixa).',
  SafeDepositIn: 'Escrito só pelo Depósito Dinheiro no cofre (entrada no cofre).',
  ChequeFiadoConversion: 'Escrito pela conversão de cheque em fiado, sem movimento no caixa.',
};
type TabKey = 'categorias' | 'tipos';
type Dialog =
  | { readonly kind: 'category-new' }
  | { readonly kind: 'category-edit'; readonly category: ResponseCategoryJson }
  | { readonly kind: 'category-off'; readonly category: ResponseCategoryJson }
  | { readonly kind: 'type-new' }
  | { readonly kind: 'type-edit'; readonly type: ResponseTransactionTypeJson }
  | { readonly kind: 'type-off'; readonly type: ResponseTransactionTypeJson };

export function CategoriesScreen() {
  const params = useLocalSearchParams<{ tab?: string; focus?: string }>();
  const router = useRouter();
  const client = useScopedApiClient();
  const reads = useAdminReads();
  const categories = useGetCategory({
    request: { client },
    query: { staleTime: 0, enabled: reads },
  });
  const types = useGetTransactionType({
    request: { client },
    query: { staleTime: 0, enabled: reads },
  });
  const tab: TabKey = params.tab === 'tipos' ? 'tipos' : 'categorias';
  const [dialog, setDialog] = useState<Dialog | null>(null);
  // Open type groups; a focused type's group opens so its row can take focus on return.
  const [open, setOpen] = useState<Readonly<Record<string, boolean>>>({});
  const trigger = useRef<View>(null);
  const rows = useRef(new Map<string, View>());
  const categoryList = categories.data?.data.items ?? [];
  const typeList = types.data?.data.items ?? [];
  useFocusEffect(
    useCallback(() => {
      if (params.focus && types.data && categories.data)
        moveFocusTo(rows.current.get(params.focus));
    }, [params.focus, types.data, categories.data]),
  );
  const refetch = async () => {
    const [a, b] = await Promise.all([categories.refetch(), types.refetch()]);
    if (a.error || b.error) throw a.error ?? b.error;
  };
  const loaded = categories.data && !categories.error && types.data && !types.error;
  const focusCategory = typeList.find((t) => t.id === params.focus)?.categoryId;
  const register = (key: string) => (node: View | null) => {
    if (node) rows.current.set(key, node);
  };
  return (
    <>
      <Tabs
        testID="categories-tabs"
        accessibilityLabel="Seções"
        value={tab}
        onValueChange={(next) => router.setParams({ tab: next, focus: '' })}
        tabs={[
          {
            key: 'categorias',
            label: 'Categorias',
            count: loaded ? categoryList.length : undefined,
          },
          {
            key: 'tipos',
            label: 'Tipos de transação',
            count: loaded ? typeList.length : undefined,
          },
        ]}
      />
      <ReadState
        pending={categories.isPending}
        error={categories.error}
        retry={() => void categories.refetch()}
      />
      <ReadState pending={types.isPending} error={types.error} retry={() => void types.refetch()} />
      {loaded && tab === 'categorias' ? (
        <>
          <Button
            ref={trigger}
            label="Adicionar categoria"
            testID="category-new"
            disabled={!reads}
            onPress={() => setDialog({ kind: 'category-new' })}
          />
          {categoryList.length === 0 ? (
            <EmptyState kind="no-records" statement="Nenhuma categoria ativa." />
          ) : null}
          {categoryList.map((category) => {
            const system = hostsSystemType(category.id, typeList);
            return (
              <Card key={category.id} title={category.name} testID={`category-row-${category.id}`}>
                <AppText>{`Direção padrão: ${directionLabels[category.defaultDirection]}`}</AppText>
                {isSystemCategory(category.id, typeList) ? (
                  <AppText variant="helper">
                    Categoria dos movimentos automáticos: recebe só tipos automáticos.
                  </AppText>
                ) : null}
                <Button
                  label={`Editar nome de ${category.name}`}
                  testID={`category-edit-${category.id}`}
                  disabled={!reads}
                  ref={register(category.id)}
                  onPress={() => setDialog({ kind: 'category-edit', category })}
                />
                <Button
                  label={`Desativar ${category.name}`}
                  testID={`category-deactivate-${category.id}`}
                  variant="danger"
                  disabled={!reads || system}
                  onPress={() => setDialog({ kind: 'category-off', category })}
                />
                {system ? (
                  <AppText variant="helper" testID={`category-system-${category.id}`}>
                    Esta categoria tem tipos automáticos e não pode ser desativada.
                  </AppText>
                ) : null}
              </Card>
            );
          })}
        </>
      ) : null}
      {loaded && tab === 'tipos' ? (
        <>
          <Button
            ref={trigger}
            label="Adicionar tipo de transação"
            testID="type-new"
            disabled={!reads || categoryList.length === 0}
            onPress={() => setDialog({ kind: 'type-new' })}
          />
          <AppText variant="helper">{RECLASSIFICATION_POLICY}</AppText>
          {typeList.length === 0 ? (
            <EmptyState
              kind="no-records"
              statement="Nenhum tipo de transação ativo."
              explanation={categoryList.length === 0 ? 'Crie uma categoria primeiro.' : undefined}
            />
          ) : null}
          {/* Grouped by category (compact blueprint): a branch seeds dozens of types, so each
              group opens on demand instead of rendering every card at once. */}
          {categoryList
            .map((category) => ({
              category,
              items: typeList.filter((t) => t.categoryId === category.id),
            }))
            .filter((group) => group.items.length > 0)
            .map(({ category, items }) => (
              <Disclosure
                key={category.id}
                testID={`type-group-${category.id}`}
                label={category.name}
                summary={`${items.length} ${items.length === 1 ? 'tipo' : 'tipos'} · ${directionLabels[category.defaultDirection]}`}
                expanded={open[category.id] ?? category.id === focusCategory}
                onExpandedChange={(next) =>
                  setOpen((current) => ({ ...current, [category.id]: next }))
                }
              >
                {items.map((type) => (
                  <Card key={type.id} title={type.name} testID={`type-row-${type.id}`}>
                    {type.systemRole ? <Badge kind="automatic" /> : null}
                    <AppText>{`Categoria: ${type.categoryName}`}</AppText>
                    <AppText>{`Regra de vencimento: ${settlementRuleLabels[type.settlementRule]}`}</AppText>
                    <AppText>{`Exige conta fiado e cliente: ${type.requiresTabAccountAndClient ? 'Sim' : 'Não'}`}</AppText>
                    <AppText testID={`type-fiado-${type.id}`}>
                      {`Forma de recebimento de fiado: ${type.fiadoPaymentKind ? fiadoPaymentKindLabels[type.fiadoPaymentKind] : '—'}`}
                    </AppText>
                    <Badge
                      kind="status"
                      domain="settlement-instrument"
                      value={type.settlementInstrument}
                      context="Recebível:"
                    />
                    <Badge
                      kind="status"
                      domain="monthly-financial-role"
                      value={type.monthlyFinancialRole}
                      context="Relatório financeiro mensal:"
                    />
                    {type.systemRole ? (
                      <>
                        <AppText testID={`type-system-${type.id}`}>
                          {transactionTypeSystemRoleLabels[type.systemRole]}
                        </AppText>
                        <AppText variant="helper">{`${systemSources[type.systemRole]} ${SYSTEM_TYPE_READ_ONLY}`}</AppText>
                      </>
                    ) : (
                      <>
                        <Button
                          label={`Editar ${type.name}`}
                          testID={`type-edit-${type.id}`}
                          disabled={!reads}
                          ref={register(type.id)}
                          onPress={() => setDialog({ kind: 'type-edit', type })}
                        />
                        <Button
                          label={`Desativar ${type.name}`}
                          testID={`type-deactivate-${type.id}`}
                          variant="danger"
                          disabled={!reads}
                          onPress={() => setDialog({ kind: 'type-off', type })}
                        />
                      </>
                    )}
                  </Card>
                ))}
              </Disclosure>
            ))}
        </>
      ) : null}
      {/* Kept mounted through a failed reread so a confirmed receipt and its recovery survive. */}
      {dialog && categories.data && types.data ? (
        dialog.kind === 'category-new' || dialog.kind === 'category-edit' ? (
          <CategoryEditor
            key={dialog.kind === 'category-new' ? 'new' : dialog.category.id}
            category={dialog.kind === 'category-edit' ? dialog.category : null}
            visible={reads}
            returnFocus={trigger}
            onClose={() => setDialog(null)}
            refetch={refetch}
          />
        ) : dialog.kind === 'type-new' || dialog.kind === 'type-edit' ? (
          <TypeEditor
            key={dialog.kind === 'type-new' ? 'new' : dialog.type.id}
            type={dialog.kind === 'type-edit' ? dialog.type : null}
            categories={categoryList}
            types={typeList}
            visible={reads}
            returnFocus={trigger}
            onClose={() => setDialog(null)}
            refetch={refetch}
          />
        ) : (
          <Deactivation
            dialog={dialog}
            visible={reads}
            onClose={() => setDialog(null)}
            refetch={refetch}
          />
        )
      ) : null}
    </>
  );
}

function CategoryEditor({
  category,
  visible,
  returnFocus,
  onClose,
  refetch,
}: {
  category: ResponseCategoryJson | null;
  visible: boolean;
  returnFocus: RefObject<View | null>;
  onClose: () => void;
  refetch: () => Promise<void>;
}) {
  const { engine } = useSession();
  const client = useScopedApiClient();
  const toast = useToast();
  const base: CategoryInput = category
    ? { name: category.name, defaultDirection: category.defaultDirection }
    : { name: '', defaultDirection: '' };
  const [input, setInput] = useState(base);
  const inputRef = useRef(input);
  const command = useAdminCommand();
  const create = usePostCategory({ request: { client } });
  const update = usePutCategoryCategoryId({ request: { client } });
  const [saved, setSaved] = useState(false);
  // A create closes once a reread succeeds; a rename stays open showing its receipt.
  const written = useConfirmedWrite(command, onClose);
  const draft = useAdminDraft(
    `admin/category/${category?.id ?? 'new'}`,
    isCategoryInput,
    async () => {
      if (engine.getSnapshot().branch?.role === 'Member') return false;
      if (!category) return true;
      const fresh = await getCategoryCategoryId(category.id, { client: engine.client() });
      return fresh.data.active && fresh.data.branchId === engine.getSnapshot().branch?.id;
    },
  );
  const change = (next: CategoryInput) => {
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
        const result = category
          ? await update.mutateAsync({ categoryId: category.id, data: { name: candidate.name } })
          : await create.mutateAsync({
              data: {
                name: candidate.name,
                defaultDirection: candidate.defaultDirection as Direction,
              },
            });
        return { kept, result };
      },
      async ({ kept }) => {
        // Recorded before anything else can fail: recovery never makes a create sendable again.
        setSaved(true);
        if (!category) written.confirm();
        await draft.acknowledge(kept);
        toast.show('Categoria salva no servidor.');
        await refetch();
        if (!category) written.settle();
      },
    );
  };
  const blocked =
    command.blocked ||
    written.confirmed ||
    input.name.trim() === '' ||
    (!category && input.defaultDirection === '');
  return (
    <Overlay
      visible={visible}
      title={category ? `Editar nome de ${category.name}` : 'Nova categoria'}
      busy={command.pending}
      returnFocus={returnFocus}
      onDismiss={cancel}
      testID="category-editor"
      footer={
        <>
          <Button
            label="Salvar"
            testID="category-save"
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
      {saved && !command.postWriteError ? (
        <Notice kind="success" title="Categoria salva no servidor." />
      ) : null}
      <CommandNotices
        command={command}
        receipt="Categoria salva no servidor."
        reconcile={() =>
          void refetch()
            .then(() => {
              command.clear();
              written.recovered();
            })
            .catch(() => undefined)
        }
      />
      <FormField label="Nome" required>
        <TextField
          testID="category-name"
          value={input.name}
          maxLength={255}
          onChangeText={(name) => change({ ...input, name })}
        />
      </FormField>
      {category ? (
        <AppText testID="category-direction-readonly">
          {`Direção padrão: ${directionLabels[category.defaultDirection]} (não muda depois de criada; os movimentos guardam a direção de quando foram registrados)`}
        </AppText>
      ) : (
        <FormField
          label="Direção padrão"
          required
          helper="Não pode ser mudada depois. Os tipos desta categoria serão de entrada ou de saída."
        >
          <RadioGroup
            testID="category-direction"
            value={input.defaultDirection || null}
            onValueChange={(value) => change({ ...input, defaultDirection: value as Direction })}
          >
            <Radio value="In" label={directionLabels.In} testID="category-direction-In" />
            <Radio value="Out" label={directionLabels.Out} testID="category-direction-Out" />
          </RadioGroup>
        </FormField>
      )}
    </Overlay>
  );
}

const problemText = {
  name: 'Informe o nome.',
  category: 'Escolha a categoria.',
  settlement: 'Escolha a regra de vencimento.',
  'fiado-unchosen': 'Diga se este tipo é uma forma de recebimento de fiado.',
  'fiado-condition': FIADO_KIND_CONDITION,
  'monthly-direction': MONTHLY_ROLE_CONDITION,
} as const;
const sameType = (a: TypeInput, b: TypeInput) => JSON.stringify(a) === JSON.stringify(b);

function TypeEditor({
  type,
  categories,
  types,
  visible,
  returnFocus,
  onClose,
  refetch,
}: {
  type: ResponseTransactionTypeJson | null;
  categories: readonly ResponseCategoryJson[];
  types: readonly ResponseTransactionTypeJson[];
  visible: boolean;
  returnFocus: RefObject<View | null>;
  onClose: () => void;
  refetch: () => Promise<void>;
}) {
  const { engine } = useSession();
  const client = useScopedApiClient();
  const toast = useToast();
  const [base, setBase] = useState<TypeInput>(() => (type ? typeInput(type) : emptyType()));
  const [input, setInput] = useState(base);
  const inputRef = useRef(input);
  const command = useAdminCommand();
  const create = usePostTransactionType({ request: { client } });
  const update = usePutTransactionTypeTransactionTypeId({ request: { client } });
  const [saved, setSaved] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [changed, setChanged] = useState<TypeInput | null>(null);
  const [checking, setChecking] = useState(false);
  const [checkError, setCheckError] = useState<unknown>(null);
  // A create closes once a reread succeeds; an edit stays open showing its receipt.
  const written = useConfirmedWrite(command, onClose);
  const draft = useAdminDraft(`admin/type/${type?.id ?? 'new'}`, isTypeInput, async () => {
    if (engine.getSnapshot().branch?.role === 'Member') return false;
    if (!type) return true;
    const fresh = await getTransactionTypeTransactionTypeId(type.id, { client: engine.client() });
    return fresh.data.active && !fresh.data.systemRole && fresh.data.categoryId === type.categoryId;
  });
  const open = categories.filter((c) => !isSystemCategory(c.id, types));
  const category = categories.find((c) => c.id === input.categoryId);
  const direction = category?.defaultDirection;
  const kindAllowed = fiadoKindAllowed(direction, input.requiresTabAccountAndClient);
  const problems = typeProblems(input, direction);
  const change = (next: TypeInput) => {
    inputRef.current = next;
    setInput(next);
    setSaved(false);
    void draft.keep(next, base);
  };
  const cancel = () => {
    if (!command.uncertain && !command.postWriteError) void draft.discard();
    onClose();
  };
  const dispatch = () => {
    const candidate = inputRef.current;
    void command.run(
      async () => {
        const kept = await draft.keep(candidate, base);
        const result = type
          ? await update.mutateAsync({ transactionTypeId: type.id, data: typePayload(candidate) })
          : await create.mutateAsync({ data: typeCreatePayload(candidate) });
        return { kept, result };
      },
      async ({ kept, result }) => {
        // Recorded before anything else can fail: the saved values become the accepted
        // reference and a confirmed create is never sendable again.
        setSaved(true);
        if (type) setBase(typeInput(result.data));
        else written.confirm();
        await draft.acknowledge(kept);
        toast.show('Tipo de transação salvo no servidor.');
        await refetch();
        if (!type) written.settle();
      },
    );
  };
  /**
   * An edit first rereads the type (advisory; the API has no version precondition): a change by
   * someone else since the form opened is shown for review instead of being overwritten blindly.
   */
  const save = async () => {
    if (command.blocked || problems.length) return;
    if (!type) return dispatch();
    setChecking(true);
    setCheckError(null);
    try {
      const fresh = typeInput(
        (await getTransactionTypeTransactionTypeId(type.id, { client: engine.client() })).data,
      );
      if (!sameType(fresh, base)) return setChanged(fresh);
      // Against the accepted reference just confirmed by the server (it equals `base`, which
      // adopting or keeping a newer server version moved), never the values the dialog opened with.
      if (reclassifies(fresh, inputRef.current)) return setConfirming(true);
    } catch (error) {
      return setCheckError(error);
    } finally {
      setChecking(false);
    }
    dispatch();
  };
  const busy = command.pending || checking;
  return (
    <Overlay
      visible={visible}
      title={type ? `Editar ${type.name}` : 'Novo tipo de transação'}
      busy={busy}
      returnFocus={returnFocus}
      onDismiss={cancel}
      testID="type-editor"
      footer={
        <>
          <Button
            label="Salvar"
            testID="type-save"
            loading={busy}
            loadingLabel={checking ? 'Conferindo…' : 'Salvando…'}
            disabled={command.blocked || written.confirmed || problems.length > 0 || !!changed}
            onPress={() => void save()}
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
      {saved && !command.postWriteError ? (
        <Notice kind="success" title="Tipo de transação salvo no servidor." testID="type-saved" />
      ) : null}
      {checkError ? (
        <Notice
          kind="danger"
          title="Não foi possível conferir o tipo antes de salvar. Nada foi enviado."
          messages={errorMessages(checkError)}
        />
      ) : null}
      {changed ? (
        <Notice
          kind="warning"
          testID="type-changed"
          title="Este tipo mudou no servidor desde que o formulário abriu. Nada foi enviado."
          body={`Agora: ${changed.name} · ${settlementRuleLabels[changed.settlementRule as SettlementRule]} · Recebível: ${settlementInstrumentLabels[changed.settlementInstrument]} · Relatório mensal: ${monthlyFinancialRoleLabels[changed.monthlyFinancialRole]}.`}
          actions={
            <>
              <Button
                label="Usar os valores atuais"
                testID="type-adopt-server"
                onPress={() => {
                  setBase(changed);
                  inputRef.current = changed;
                  setInput(changed);
                  setChanged(null);
                }}
              />
              <Button
                label="Manter minhas alterações para revisar"
                variant="secondary"
                testID="type-keep-mine"
                onPress={() => {
                  setBase(changed);
                  setChanged(null);
                }}
              />
            </>
          }
        />
      ) : null}
      <CommandNotices
        command={command}
        receipt="Tipo de transação salvo no servidor."
        reconcile={() =>
          void refetch()
            .then(() => {
              command.clear();
              written.recovered();
            })
            .catch(() => undefined)
        }
      />
      {type ? (
        <AppText testID="type-category-readonly">{`Categoria: ${type.categoryName} (${directionLabels[categories.find((c) => c.id === type.categoryId)?.defaultDirection ?? 'In']}) — não muda depois de criado`}</AppText>
      ) : (
        <FormField
          label="Categoria"
          required
          helper="A categoria define a direção e não muda depois. As categorias de movimentos automáticos não recebem tipos novos."
        >
          <Select
            testID="type-category"
            value={input.categoryId || null}
            onValueChange={(categoryId) => change({ ...input, categoryId, fiadoPaymentKind: '' })}
            options={open.map((c) => ({
              value: c.id,
              label: `${c.name} · ${directionLabels[c.defaultDirection]}`,
            }))}
          />
        </FormField>
      )}
      <FormField label="Nome" required>
        <TextField
          testID="type-name"
          value={input.name}
          maxLength={255}
          onChangeText={(name) => change({ ...input, name })}
        />
      </FormField>
      <FormField label="Regra de vencimento" required>
        <Select
          testID="type-settlement"
          value={input.settlementRule || null}
          onValueChange={(value) => change({ ...input, settlementRule: value as SettlementRule })}
          options={Object.values(SettlementRule).map((value) => ({
            value,
            label: settlementRuleLabels[value],
          }))}
        />
      </FormField>
      <Switch
        testID="type-requires-tab"
        label="Exige conta fiado e cliente"
        value={input.requiresTabAccountAndClient}
        onValueChange={(requiresTabAccountAndClient) =>
          change({
            ...input,
            requiresTabAccountAndClient,
            fiadoPaymentKind: fiadoKindAllowed(direction, requiresTabAccountAndClient)
              ? ''
              : 'none',
          })
        }
      />
      <FormField
        label="Forma de recebimento de fiado"
        required={kindAllowed}
        helper={kindAllowed ? 'Sem padrão: escolha uma opção.' : FIADO_KIND_CONDITION}
        error={problems.includes('fiado-condition') ? FIADO_KIND_CONDITION : undefined}
      >
        <RadioGroup
          testID="type-fiado-kind"
          value={input.fiadoPaymentKind || null}
          onValueChange={(value) =>
            change({ ...input, fiadoPaymentKind: value as TypeInput['fiadoPaymentKind'] })
          }
          disabled={!kindAllowed}
        >
          <Radio
            value="none"
            label="Não é forma de recebimento de fiado"
            testID="type-fiado-none"
          />
          {Object.values(FiadoPaymentKind).map((value) => (
            <Radio
              key={value}
              value={value}
              label={fiadoPaymentKindLabels[value]}
              testID={`type-fiado-${value}`}
            />
          ))}
        </RadioGroup>
      </FormField>
      <FormField
        label="Recebível"
        helper="Independe da regra de vencimento. Sem classificação limpa a marcação."
      >
        <Select
          testID="type-instrument"
          value={input.settlementInstrument}
          onValueChange={(value) =>
            change({ ...input, settlementInstrument: value as SettlementInstrument })
          }
          options={Object.values(SettlementInstrument).map((value) => ({
            value,
            label: settlementInstrumentLabels[value],
          }))}
        />
      </FormField>
      <FormField
        label="Relatório financeiro mensal"
        helper={`${MONTHLY_ROLE_CONDITION} Nenhuma limpa a marcação.`}
        error={problems.includes('monthly-direction') ? MONTHLY_ROLE_CONDITION : undefined}
      >
        <Select
          testID="type-monthly"
          value={input.monthlyFinancialRole}
          onValueChange={(value) =>
            change({ ...input, monthlyFinancialRole: value as MonthlyFinancialRole })
          }
          options={Object.values(MonthlyFinancialRole).map((value) => ({
            value,
            label: monthlyFinancialRoleLabels[value],
            description: monthlyRoleAllowed(value, direction)
              ? undefined
              : direction
                ? `Indisponível em categoria de ${directionLabels[direction].toLowerCase()}.`
                : 'Escolha a categoria primeiro.',
          }))}
        />
      </FormField>
      <AppText variant="helper">{RECLASSIFICATION_POLICY}</AppText>
      {problems.filter((p) => p !== 'name').length && (input.name !== '' || type) ? (
        <Notice
          kind="info"
          testID="type-problems"
          title="Antes de salvar:"
          messages={problems.filter((p) => p !== 'name').map((p) => problemText[p])}
        />
      ) : null}
      <ConfirmDialog
        visible={visible && confirming}
        title="Mudar a classificação deste tipo?"
        consequence={RECLASSIFICATION_POLICY}
        confirmLabel="Confirmar e salvar"
        busy={command.pending}
        onDismiss={() => setConfirming(false)}
        onConfirm={() => {
          setConfirming(false);
          dispatch();
        }}
        testID="type-reclassify-confirm"
      />
    </Overlay>
  );
}

function Deactivation({
  dialog,
  visible,
  onClose,
  refetch,
}: {
  dialog: Extract<Dialog, { kind: 'category-off' | 'type-off' }>;
  visible: boolean;
  onClose: () => void;
  refetch: () => Promise<void>;
}) {
  const client = useScopedApiClient();
  const toast = useToast();
  const command = useAdminCommand();
  const removeCategory = useDeleteCategoryCategoryId({ request: { client } });
  const removeType = useDeleteTransactionTypeTransactionTypeId({ request: { client } });
  const written = useConfirmedWrite(command, onClose);
  const name = dialog.kind === 'category-off' ? dialog.category.name : dialog.type.name;
  const receipt =
    dialog.kind === 'category-off'
      ? 'Categoria desativada no servidor.'
      : 'Tipo desativado no servidor.';
  return (
    <ConfirmDialog
      visible={visible}
      title={`Desativar ${name}?`}
      destructive
      busy={command.pending}
      busyLabel="Desativando…"
      confirmLabel={dialog.kind === 'category-off' ? 'Desativar categoria' : 'Desativar tipo'}
      consequence={dialog.kind === 'category-off' ? CATEGORY_DEACTIVATION : TYPE_DEACTIVATION}
      onDismiss={onClose}
      testID={`${dialog.kind}-confirm`}
      onConfirm={() => {
        if (command.blocked || written.confirmed) return;
        void command.run(
          () =>
            dialog.kind === 'category-off'
              ? removeCategory.mutateAsync({ categoryId: dialog.category.id })
              : removeType.mutateAsync({ transactionTypeId: dialog.type.id }),
          async () => {
            written.confirm();
            toast.show(receipt);
            await refetch();
            written.settle();
          },
        );
      }}
    >
      <CommandNotices
        command={command}
        receipt={receipt}
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
