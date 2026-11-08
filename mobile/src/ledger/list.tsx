import { forwardRef, useCallback, useMemo, useRef, useState } from 'react';
import { Text, View } from 'react-native';
import { useFocusEffect, useLocalSearchParams, useRouter } from 'expo-router';
import { useQuery } from '@tanstack/react-query';
import {
  getTransaction,
  type ResponseListTransactionItemJson,
  type TransactionStatus,
} from '@loto/api';
import {
  financialClassLabels,
  formatCount,
  formatDate,
  parseCalendarDate,
  transactionStatusLabels,
  type CalendarDate,
} from '@loto/core';
import { useSession, useScopedApiClient } from '@/session/provider';
import { useShell } from '@/shell/provider';
import { useTheme } from '@/theme/provider';
import { spacing } from '@/theme/style';
import {
  AppText,
  Badge,
  Button,
  Checkbox,
  DateRangeField,
  EmptyState,
  FilterBar,
  FormField,
  Notice,
  Pagination,
  Select,
  Skeleton,
  Switch,
  moveFocusTo,
} from '@/ui';
import { FocusRing, PressableBox, useFocusState } from '@/ui/focus';
import { pluralDrafts } from '@/day/contracts';
import {
  elevated,
  errorMessages,
  useAccounts,
  useClients,
  useLedgerReads,
  useOperators,
  useTypes,
} from './common';
import {
  PAGE_SIZES,
  day,
  draftBlocker,
  filterParams,
  listQuery,
  parseListFilter,
  signedValue,
  type ListFilter,
} from './contracts';
import { ChequeBatch } from './cheque';

const businessDay = (value: string | null) => {
  const date = value ? parseCalendarDate(value) : null;
  return date ? formatDate(date) : '—';
};
const isChequeRow = (row: ResponseListTransactionItemJson) =>
  row.status === 'Active' &&
  !row.generationKind &&
  (row.classification?.settlementInstrument === 'Cheque' || !!row.originTransactionId);

/**
 * Transactions list: the server's pages, ordering and filters only (no local search, no totals).
 * The filter lives in the route, so opening a row and returning restores filter, page and focus.
 */
export function TransactionsScreen() {
  const params = useLocalSearchParams<Record<string, string>>();
  const router = useRouter();
  const { snapshot } = useSession();
  const shell = useShell();
  const client = useScopedApiClient();
  const reads = useLedgerReads();
  const role = snapshot.branch!.role;
  const manage = elevated(role);
  const linked = !!snapshot.selfContext?.operatorId;
  const today = snapshot.clock!.branchLocalDate;
  const filter = useMemo(
    () => parseListFilter(params, { today, role, shift: shell.mode === 'shift', linked }),
    [params, today, role, shell.mode, linked],
  );
  const [staged, setStaged] = useState<ListFilter>(filter);
  const [stagedFor, setStagedFor] = useState(JSON.stringify(filter));
  if (stagedFor !== JSON.stringify(filter)) {
    setStagedFor(JSON.stringify(filter));
    setStaged(filter);
  }
  const query = useQuery({
    queryKey: ['ledger', snapshot.scope, 'list', listQuery(filter)],
    queryFn: async ({ signal }) =>
      (await getTransaction(listQuery(filter), { client, signal })).data,
    enabled: reads,
    retry: false,
    staleTime: 0,
  });
  const blocker = draftBlocker(filter);
  const mineCount = useQuery({
    queryKey: [
      'ledger',
      snapshot.scope,
      'list-mine',
      listQuery({ ...filter, mine: true, page: 1, pageSize: 20 }),
    ],
    queryFn: async ({ signal }) =>
      (
        await getTransaction(
          { ...listQuery({ ...filter, mine: true, operatorId: null }), Page: 1, PageSize: 1 },
          { client, signal },
        )
      ).data.totalCount,
    enabled: reads && blocker && linked && !filter.mine,
    retry: false,
    staleTime: 0,
  });
  const filterKey = JSON.stringify(filter);
  // Every return to the list reads fresh: a create/finalize/cancel elsewhere never shows stale rows.
  useFocusEffect(
    useCallback(() => {
      if (reads) {
        void query.refetch();
        if (blocker && linked && !filter.mine) void mineCount.refetch();
      }
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [reads, filterKey]),
  );
  const types = useTypes();
  const accounts = useAccounts();
  const clients = useClients();
  const operators = useOperators();
  const apply = (next: ListFilter) =>
    router.setParams({ ...filterParams(next), row: '', focus: '' });
  const rows = useRef(new Map<string, View>());
  const heading = useRef<Text>(null);
  useFocusEffect(
    useCallback(() => {
      if (params.focus && query.data) moveFocusTo(rows.current.get(params.focus));
    }, [params.focus, query.data]),
  );
  const [selecting, setSelecting] = useState(false);
  const [selected, setSelected] = useState<readonly string[]>([]);
  const [batch, setBatch] = useState(false);
  const accountName = (id: string | null) =>
    (accounts.data ?? []).find((a) => a.id === id)?.name ?? (id ? 'Conta' : null);
  const summary = [
    filter.from === filter.to
      ? businessDay(filter.from)
      : `${businessDay(filter.from)} a ${businessDay(filter.to)}`,
    filter.status ? transactionStatusLabels[filter.status] : 'Todas as situações',
    ...(filter.accountId ? [accountName(filter.accountId) ?? 'Conta'] : []),
    ...(filter.typeId
      ? [(types.data ?? []).find((t) => t.id === filter.typeId)?.name ?? 'Tipo escolhido']
      : []),
    ...(filter.clientId
      ? [(clients.data ?? []).find((c) => c.id === filter.clientId)?.name ?? 'Cliente escolhido']
      : []),
    ...(filter.operatorId
      ? [
          (operators.data ?? []).find((o) => o.id === filter.operatorId)?.name ??
            'Operador escolhido',
        ]
      : []),
    ...(filter.mine ? ['Somente minhas'] : []),
  ];
  const active =
    Number(filter.from !== today || filter.to !== today) +
    Number(!!filter.status) +
    Number(!!filter.accountId) +
    Number(!!filter.typeId) +
    Number(!!filter.clientId) +
    Number(!!filter.operatorId) +
    Number(filter.mine);
  const data = query.data;
  const open = (row: ResponseListTransactionItemJson) => {
    // A row of a plan that still has Draft installments opens the plan editor in view.
    const path =
      row.originTransactionId && row.status === 'Draft'
        ? `/transacoes/cheque-pre-datado/${row.originTransactionId}?focus=${row.id}`
        : `/transacoes/${row.id}`;
    void shell.navigate(path, true, false, row.id);
  };
  return (
    <>
      <View
        style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.size['touch-target-gap'] }}
      >
        <Button
          label="Nova transação"
          testID="list-new"
          onPress={() => void shell.navigate('/transacoes/nova', true)}
        />
        <Button
          label="Cheques pré-datados"
          variant="secondary"
          testID="list-plan"
          onPress={() => void shell.navigate('/transacoes/cheque-pre-datado/novo', true)}
        />
        <Button
          label="Depósito Dinheiro"
          variant="secondary"
          testID="list-safe"
          onPress={() => void shell.navigate('/transacoes/deposito-cofre', true)}
        />
      </View>
      <FilterBar
        title="Filtros de transações"
        testID="list-filters"
        appliedSummary={summary}
        activeCount={active}
        dirty={JSON.stringify(staged) !== JSON.stringify(filter)}
        applying={query.isFetching}
        onApply={() => apply({ ...staged, page: 1 })}
        onClear={() =>
          apply({ ...parseListFilter({}, { today, role, shift: false, linked }), mine: false })
        }
        resultAnnouncement={
          data
            ? `${formatCount(data.totalCount)} ${data.totalCount === 1 ? 'transação' : 'transações'}`
            : undefined
        }
      >
        <FormField label="Período">
          <DateRangeField
            testID="list-range"
            today={parseCalendarDate(today) ?? undefined}
            from={staged.from ? (parseCalendarDate(staged.from) as CalendarDate | null) : null}
            to={staged.to ? (parseCalendarDate(staged.to) as CalendarDate | null) : null}
            onChange={(range) => setStaged((s) => ({ ...s, from: range.from, to: range.to }))}
          />
        </FormField>
        <FormField label="Situação">
          <Select
            testID="list-status"
            value={staged.status ?? 'all'}
            onValueChange={(v) =>
              setStaged((s) => ({ ...s, status: v === 'all' ? null : (v as TransactionStatus) }))
            }
            options={[
              { value: 'all', label: 'Todas' },
              ...(['Draft', 'Active', 'Cancelled'] as const).map((s) => ({
                value: s,
                label: transactionStatusLabels[s],
              })),
            ]}
          />
        </FormField>
        <FormField label="Conta">
          <Select
            testID="list-account"
            value={staged.accountId ?? 'all'}
            state={
              accounts.status === 'error'
                ? 'failed'
                : accounts.status === 'pending'
                  ? 'loading'
                  : 'ready'
            }
            onValueChange={(v) => setStaged((s) => ({ ...s, accountId: v === 'all' ? null : v }))}
            options={[
              { value: 'all', label: manage ? 'Todas as contas' : 'Todas as minhas contas' },
              ...(accounts.data ?? []).map((a) => ({ value: a.id, label: a.name })),
            ]}
          />
        </FormField>
        <FormField label="Tipo de transação">
          <Select
            testID="list-type"
            searchable
            resultNoun="tipos"
            value={staged.typeId ?? 'all'}
            state={
              types.status === 'error' ? 'failed' : types.status === 'pending' ? 'loading' : 'ready'
            }
            onValueChange={(v) => setStaged((s) => ({ ...s, typeId: v === 'all' ? null : v }))}
            missingLabel="Tipo fora da lista atual"
            options={[
              { value: 'all', label: 'Todos' },
              ...(types.data ?? [])
                .filter((t) => t.active)
                .map((t) => ({ value: t.id, label: t.name })),
            ]}
          />
        </FormField>
        <FormField label="Cliente">
          <Select
            testID="list-client"
            searchable
            resultNoun="clientes"
            value={staged.clientId ?? 'all'}
            state={
              clients.status === 'error'
                ? 'failed'
                : clients.status === 'pending'
                  ? 'loading'
                  : 'ready'
            }
            onValueChange={(v) => setStaged((s) => ({ ...s, clientId: v === 'all' ? null : v }))}
            options={[
              { value: 'all', label: 'Todos' },
              ...(clients.data ?? []).map((c) => ({ value: c.id, label: c.name })),
            ]}
          />
        </FormField>
        {manage ? (
          <FormField label="Operador">
            <Select
              testID="list-operator"
              searchable
              resultNoun="operadores"
              value={staged.operatorId ?? 'all'}
              disabled={staged.mine}
              state={
                operators.status === 'error'
                  ? 'failed'
                  : operators.status === 'pending'
                    ? 'loading'
                    : 'ready'
              }
              onValueChange={(v) =>
                setStaged((s) => ({ ...s, operatorId: v === 'all' ? null : v, mine: false }))
              }
              options={[
                { value: 'all', label: 'Todos' },
                ...(operators.data ?? []).map((o) => ({ value: o.id, label: o.name })),
              ]}
            />
          </FormField>
        ) : null}
        {linked ? (
          <Switch
            label="Somente minhas"
            description="Transações registradas pelo seu operador."
            testID="list-mine"
            value={staged.mine}
            onValueChange={(mine) =>
              setStaged((s) => ({ ...s, mine, operatorId: mine ? null : s.operatorId }))
            }
          />
        ) : null}
      </FilterBar>
      {blocker ? (
        <BlockerContext
          filter={filter}
          total={data?.totalCount ?? null}
          mine={filter.mine ? (data?.totalCount ?? null) : (mineCount.data ?? null)}
          linked={linked}
          fresh={query.isSuccess && !query.isFetching}
          accountName={accountName(filter.accountId) ?? 'este caixa'}
          onBack={() => void shell.back()}
        />
      ) : null}
      {manage ? (
        <View style={{ gap: spacing.stack.sm }}>
          <Switch
            label="Selecionar cheques"
            description="Para levar ao cofre ou depositar vários cheques desta página."
            testID="list-select-cheques"
            value={selecting}
            onValueChange={(next) => {
              setSelecting(next);
              if (!next) setSelected([]);
            }}
          />
          {selecting ? (
            <Button
              label={`Levar ao cofre ou depositar (${formatCount(selected.length)})`}
              testID="list-batch"
              disabled={selected.length === 0}
              onPress={() => setBatch(true)}
            />
          ) : null}
        </View>
      ) : null}
      <AppText
        ref={heading}
        variant="section-title"
        accessibilityRole="header"
        testID="list-heading"
      >
        {data
          ? `${formatCount(data.totalCount)} ${data.totalCount === 1 ? 'transação' : 'transações'}`
          : 'Transações'}
      </AppText>
      {query.status === 'pending' ? (
        <Skeleton label="Carregando transações…" />
      ) : query.status === 'error' ? (
        <Notice
          kind="danger"
          testID="list-error"
          title="Não foi possível carregar as transações."
          messages={errorMessages(query.error)}
          actions={<Button label="Tentar novamente" onPress={() => void query.refetch()} />}
        />
      ) : data!.items.length === 0 ? (
        <EmptyState
          kind={blocker ? 'all-clear' : 'no-matches'}
          statement={
            blocker
              ? 'Nenhum rascunho pendente neste caixa e dia.'
              : 'Nenhuma transação com estes filtros.'
          }
          explanation={
            manage || filter.accountId
              ? 'Mude o período ou os filtros para ver outras transações.'
              : 'Você vê as transações das contas atribuídas ao seu operador.'
          }
        />
      ) : (
        <View accessibilityRole="list" testID="list-rows">
          {data!.items.map((row) => (
            <Row
              key={row.id}
              row={row}
              ref={(node: View | null) => {
                if (node) rows.current.set(row.id, node);
                else rows.current.delete(row.id);
              }}
              typeName={row.transactionTypeName}
              onOpen={() => open(row)}
              selectable={selecting && isChequeRow(row)}
              selected={selected.includes(row.id)}
              onSelect={(checked) =>
                setSelected((prev) =>
                  checked ? [...prev, row.id] : prev.filter((id) => id !== row.id),
                )
              }
            />
          ))}
        </View>
      )}
      {data ? (
        <Pagination
          testID="list-pagination"
          listName="transações"
          envelope={data}
          loading={query.isFetching}
          focusTarget={heading}
          pageSizes={PAGE_SIZES}
          onPageChange={(page) => apply({ ...filter, page })}
          onPageSizeChange={(pageSize) => apply({ ...filter, pageSize, page: 1 })}
        />
      ) : null}
      {batch ? (
        <ChequeBatch
          ids={selected}
          onClose={(changed) => {
            setBatch(false);
            if (changed) {
              setSelected([]);
              void query.refetch();
            }
          }}
        />
      ) : null}
    </>
  );
}

/**
 * The M5 Draft-blocker source: one cashbox and business day. The own/other split comes from the
 * server (`Mine`), and "nothing pending" is said only after a fresh successful read.
 */
function BlockerContext({
  filter,
  total,
  mine,
  linked,
  fresh,
  accountName,
  onBack,
}: {
  filter: ListFilter;
  total: number | null;
  mine: number | null;
  linked: boolean;
  fresh: boolean;
  accountName: string;
  onBack: () => void;
}) {
  return (
    <Notice
      kind={fresh && total === 0 ? 'success' : 'info'}
      testID="list-blocker"
      title={
        fresh && total === 0
          ? `Nenhum rascunho pendente em ${accountName} em ${businessDay(filter.from)} (lido agora).`
          : `Rascunhos que bloqueiam o fechamento de ${accountName} em ${businessDay(filter.from)}`
      }
      body={
        total === null
          ? 'Conferindo os rascunhos…'
          : total > 0
            ? `${pluralDrafts(total)}${linked && mine !== null && !filter.mine ? ` · ${mine} ${mine === 1 ? 'sua' : 'suas'} · ${total - mine} de outros operadores` : ''}. Finalize ou cancele cada um; o fechamento só libera quando o servidor não tiver mais rascunhos.`
            : 'Volte ao fechamento para continuar; o que você digitou lá continua guardado.'
      }
      actions={
        <Button
          label="Voltar ao fechamento"
          variant="secondary"
          testID="list-blocker-back"
          onPress={onBack}
        />
      }
    />
  );
}

const Row = forwardRef<
  View,
  {
    row: ResponseListTransactionItemJson;
    typeName: string;
    onOpen: () => void;
    selectable: boolean;
    selected: boolean;
    onSelect: (checked: boolean) => void;
  }
>(function Row({ row, typeName, onOpen, selectable, selected, onSelect }, ref) {
  const { colors } = useTheme();
  const [focused, handlers] = useFocusState();
  const muted = row.status !== 'Active';
  const label = `Abrir transação: ${typeName}, ${signedValue(row.value, row.direction)}, ${businessDay(day(row.date))}, ${transactionStatusLabels[row.status]}${row.generationKind ? ', automática' : ''}`;
  return (
    <View
      testID={`list-row-${row.id}`}
      style={{
        borderBottomWidth: spacing.size['border-width'],
        borderBottomColor: colors.sem.border.subtle,
        paddingVertical: spacing.stack.sm,
        gap: spacing.stack.xs,
      }}
    >
      <PressableBox
        ref={ref}
        accessibilityRole="link"
        accessibilityLabel={label}
        onPress={onOpen}
        {...handlers}
        style={(pressed) => ({
          minHeight: spacing.size['touch-target-comfortable'],
          gap: spacing.stack.xs,
          backgroundColor: pressed ? colors.sem.surface.hover : undefined,
        })}
      >
        <View
          style={{
            flexDirection: 'row',
            flexWrap: 'wrap',
            gap: spacing.inline.sm,
            alignItems: 'center',
          }}
        >
          <AppText variant="body-strong" color={colors.sem.text.link} style={{ flexShrink: 1 }}>
            {typeName}
          </AppText>
          {row.generationKind ? <Badge kind="automatic" /> : null}
          <Badge kind="status" domain="transaction-status" value={row.status} />
        </View>
        <AppText
          variant="table"
          tone={muted ? 'secondary' : undefined}
          testID={`list-row-value-${row.id}`}
        >
          {signedValue(row.value, row.direction)}
        </AppText>
        <AppText variant="helper" tone="secondary">
          {[
            businessDay(day(row.date)),
            row.accountName,
            row.clientName,
            row.classification ? financialClassLabels[row.classification.financialClass] : null,
            row.originTransactionId ? 'Parcela de plano' : null,
            row.classification?.settlementInstrument === 'Cheque' || row.originTransactionId
              ? `Bom para ${businessDay(day(row.dueDate))}`
              : null,
            row.sessionId ? 'Em turno de caixa' : null,
          ]
            .filter(Boolean)
            .join(' · ')}
        </AppText>
        {row.description ? <AppText variant="helper">{row.description}</AppText> : null}
        <FocusRing visible={focused} />
      </PressableBox>
      {selectable ? (
        <Checkbox
          testID={`list-select-${row.id}`}
          label={`Selecionar cheque ${signedValue(row.value, row.direction)}`}
          checked={selected}
          onCheckedChange={onSelect}
        />
      ) : null}
    </View>
  );
});
