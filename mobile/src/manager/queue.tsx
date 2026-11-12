import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { View } from 'react-native';
import { useLocalSearchParams } from 'expo-router';
import { useQuery } from '@tanstack/react-query';
import {
  getOperatorLinkRequest,
  getReportManagerQueue,
  getReportMonthlyReconciliationYearMonth,
  getSetting,
  type ResponseManagerQueueCloseJson,
  type ResponseManagerQueueJson,
  type ResponseManagerQueueNotSubmittedJson,
} from '@loto/api';
import { parseCalendarDate } from '@loto/core';
import { useSession, useScopedApiClient } from '@/session/provider';
import { useShell } from '@/shell/provider';
import { spacing } from '@/theme/style';
import {
  AppText,
  Badge,
  Button,
  Card,
  DayStepper,
  EmptyState,
  Notice,
  moveFocusTo,
  Skeleton,
  SummaryStrip,
} from '@/ui';
import { CustodyPanel } from './custody';
import { BatchApproval } from './batch';
import {
  day,
  errorMessages,
  managerKey,
  money,
  plural,
  useManagerAccess,
  useManagerReads,
} from './common';
import {
  allClear,
  calendarPart,
  closeRoute,
  draftTransactionsPath,
  linkRequestsPath,
  noLockSet,
  notSubmittedRoute,
  queueGroups,
  queuePath,
  selectedQueueDate,
} from './contracts';

/** Rows register their node so a return from a resolver can put focus back on the same row. */
const RowRegistry = createContext<(id: string, view: View | null) => void>(() => undefined);

/** Submitted sessions grouped by cashbox (first appearance order); every session stays its own row. */
function pendingByBox(rows: readonly ResponseManagerQueueCloseJson[]) {
  const map = new Map<string, ResponseManagerQueueCloseJson[]>();
  for (const row of rows) map.set(row.accountId, [...(map.get(row.accountId) ?? []), row]);
  return [...map.values()];
}

type Fail = { title: string; error: unknown; retry: () => void };

function PartialFailure({ failure }: { failure: Fail }) {
  return (
    <Notice
      kind="warning"
      title={failure.title}
      messages={errorMessages(failure.error)}
      actions={<Button label="Tentar novamente" variant="secondary" onPress={failure.retry} />}
    />
  );
}
function Identity({ row, group }: { row: ResponseManagerQueueCloseJson; group: string }) {
  const recorded = row.recordedByOperatorName ?? row.recordedByUserName;
  const submitted = row.submittedByOperatorName ?? row.submittedByUserName;
  return (
    <View testID={`queue-identity-${group}-${row.dailyCloseId}`} style={{ gap: spacing.stack.xs }}>
      {recorded ? <AppText variant="helper">{`Contado por ${recorded}`}</AppText> : null}
      {submitted ? <AppText variant="helper">{`Enviado por ${submitted}`}</AppText> : null}
    </View>
  );
}
const OPEN_LABEL = {
  Submitted: 'Conferir',
  Approved: 'Ver conferência',
  Draft: 'Fechar dia',
  Rejected: 'Corrigir e reenviar',
} as const;
function CloseRow({
  row,
  group,
  onOpen,
  showVariance,
}: {
  row: ResponseManagerQueueCloseJson;
  group: string;
  onOpen: (row: ResponseManagerQueueCloseJson) => void;
  showVariance?: boolean;
}) {
  const label = OPEN_LABEL[row.status];
  const register = useContext(RowRegistry);
  return (
    <View
      ref={(view) => register(row.dailyCloseId, view)}
      testID={`queue-row-${group}-${row.dailyCloseId}`}
      style={{ gap: spacing.stack.xs, paddingVertical: spacing.stack.sm }}
    >
      <AppText variant="body-strong">{row.accountName}</AppText>
      <Badge kind="status" domain="daily-close-status" value={row.status} context="Situação" />
      <Identity row={row} group={group} />
      {showVariance || row.varianceValue ? (
        <AppText variant="helper" testID={`queue-variance-${group}-${row.dailyCloseId}`}>
          {`Diferença informada pelo servidor: ${money(row.varianceValue)}`}
        </AppText>
      ) : null}
      <Button
        label={label}
        role="link"
        variant="secondary"
        accessibilityLabel={`${label} — ${row.accountName}`}
        testID={`queue-open-${group}-${row.dailyCloseId}`}
        onPress={() => onOpen(row)}
      />
    </View>
  );
}
function NotSubmittedRow({
  row,
  onOpen,
}: {
  row: ResponseManagerQueueNotSubmittedJson;
  onOpen: () => void;
}) {
  const label = !row.dailyCloseId
    ? 'Abrir dia'
    : row.status === 'Rejected'
      ? 'Corrigir e reenviar'
      : 'Fechar dia';
  const id = row.dailyCloseId ?? `${row.accountId}-${row.operatorId}`;
  const register = useContext(RowRegistry);
  return (
    <View
      ref={(view) => register(id, view)}
      testID={`queue-missing-${id}`}
      style={{ gap: spacing.stack.xs, paddingVertical: spacing.stack.sm }}
    >
      <AppText variant="body-strong">{row.accountName}</AppText>
      <AppText variant="helper">{`Operador: ${row.operatorName}`}</AppText>
      {row.status ? (
        <Badge kind="status" domain="daily-close-status" value={row.status} context="Situação" />
      ) : (
        <AppText variant="helper">Sem fechamento aberto.</AppText>
      )}
      <Button
        label={label}
        role="link"
        variant="secondary"
        accessibilityLabel={`${label} — ${row.accountName}`}
        testID={`queue-missing-open-${id}`}
        onPress={onOpen}
      />
    </View>
  );
}
function Group({
  title,
  testID,
  empty,
  count,
  children,
}: {
  title: string;
  testID: string;
  empty: string;
  count: number;
  children: ReactNode;
}) {
  return (
    <Card title={`${title} (${count})`} testID={testID}>
      {count === 0 ? <AppText variant="helper">{empty}</AppText> : children}
    </Card>
  );
}

export function QueueScreen() {
  const { snapshot } = useSession();
  const client = useScopedApiClient();
  const shell = useShell();
  const access = useManagerAccess();
  const reads = useManagerReads();
  const params = useLocalSearchParams<{ date?: string; focus?: string }>();
  const rows = useRef(new Map<string, View | null>());
  const root = useRef<View>(null);
  const register = useCallback((id: string, view: View | null) => {
    if (view) rows.current.set(id, view);
    else rows.current.delete(id);
  }, []);
  const today = snapshot.clock!.branchLocalDate;
  // Today picks the INITIAL day only; a branch-clock refresh at midnight never moves the selection.
  const [mountedDay] = useState(today);
  const date = params.date ? selectedQueueDate(params.date, today) : mountedDay;
  const [year, month] = date.split('-').map(Number) as [number, number];
  const scope = snapshot.scope;

  const queue = useQuery({
    queryKey: managerKey(scope, 'queue', date),
    queryFn: ({ signal }) => getReportManagerQueue({ Date: date }, { client, signal }),
    select: (r) => r.data,
    enabled: reads,
    retry: false,
    staleTime: 0,
  });
  // Separate reads by design: the queue never embeds the lock date, reconciliation or requests.
  const setting = useQuery({
    queryKey: managerKey(scope, 'setting'),
    queryFn: ({ signal }) => getSetting({ client, signal }),
    select: (r) => r.data,
    enabled: reads,
    retry: false,
    staleTime: 0,
  });
  const reconciliation = useQuery({
    queryKey: managerKey(scope, 'reconciliation', year, month),
    queryFn: ({ signal }) =>
      getReportMonthlyReconciliationYearMonth(year, month, { client, signal }),
    select: (r) => r.data,
    enabled: reads,
    retry: false,
    staleTime: 0,
  });
  const requests = useQuery({
    queryKey: managerKey(scope, 'link-requests'),
    queryFn: ({ signal }) =>
      getOperatorLinkRequest({ Status: 'Pending', Page: 1, PageSize: 5 }, { client, signal }),
    select: (r) => r.data,
    enabled: reads,
    retry: false,
    staleTime: 0,
  });
  const [batchReceipt, setBatchReceipt] = useState<string | null>(null);
  const goDate = useCallback(
    (next: string) => void shell.navigate(queuePath(next), false, true),
    [shell],
  );
  const open = useCallback(
    (path: string, row?: string) => void shell.navigate(path, true, false, row),
    [shell],
  );
  const refreshAll = useCallback(() => {
    void queue.refetch();
    void setting.refetch();
    void reconciliation.refetch();
    void requests.refetch();
  }, [queue, setting, reconciliation, requests]);
  const focusId = params.focus;
  const loaded = !!queue.data;
  // Returning from a resolver: focus the same row, or the page when a fresh read removed it.
  useEffect(() => {
    if (!focusId || !loaded) return;
    const timer = setTimeout(() => moveFocusTo(rows.current.get(focusId) ?? root.current), 0);
    return () => clearTimeout(timer);
  }, [focusId, loaded]);
  const groups = useMemo(() => (queue.data ? queueGroups(queue.data) : null), [queue.data]);

  if (!access) return <Notice kind="danger" title="Seu perfil não tem acesso a este destino." />;
  const data: ResponseManagerQueueJson | undefined = queue.data;
  const failures: Fail[] = [];
  if (setting.isError)
    failures.push({
      title: 'Não foi possível ler a data de bloqueio.',
      error: setting.error,
      retry: () => void setting.refetch(),
    });
  if (reconciliation.isError)
    failures.push({
      title: 'Não foi possível ler as pendências do fechamento do mês.',
      error: reconciliation.error,
      retry: () => void reconciliation.refetch(),
    });
  if (requests.isError)
    failures.push({
      title: 'Não foi possível ler as solicitações de vínculo.',
      error: requests.error,
      retry: () => void requests.refetch(),
    });
  // A failed reconciliation read leaves the blockers UNKNOWN — never an empty list.
  const blockers =
    reconciliation.data && !reconciliation.isError ? reconciliation.data.blockers : null;
  const parsed = parseCalendarDate(date)!;

  return (
    <RowRegistry.Provider value={register}>
      <View ref={root} style={{ gap: spacing.stack.md }} testID="manager-queue">
        <DayStepper
          value={parsed}
          max={parseCalendarDate(today)!}
          onValueChange={(next) => goDate(String(next))}
          testID="queue-day-stepper"
        />
        <AppText testID="queue-date-context" variant="helper">
          {date === today ? `Hoje · ${day(date)}` : `${day(date)} (hoje é ${day(today)})`}
        </AppText>
        <Button label="Atualizar fila" variant="link" testID="queue-refresh" onPress={refreshAll} />
        {batchReceipt ? (
          <Notice
            kind="success"
            testID="queue-batch-receipt"
            title={batchReceipt}
            onDismiss={() => setBatchReceipt(null)}
          />
        ) : null}
        {queue.isPending ? <Skeleton label="Carregando a fila de trabalho…" /> : null}
        {queue.isError ? (
          <Notice
            kind="danger"
            testID="queue-error"
            title="Não foi possível carregar a fila de trabalho."
            messages={errorMessages(queue.error)}
            actions={<Button label="Tentar novamente" onPress={refreshAll} />}
          />
        ) : null}
        {failures.map((failure) => (
          <PartialFailure key={failure.title} failure={failure} />
        ))}
        {data && groups ? (
          <>
            <Backlog data={data} date={date} goDate={goDate} />
            <SummaryStrip
              testID="queue-summary"
              items={[
                { label: 'Aguardando conferência', value: String(groups.pending.length) },
                { label: 'Diferença total do dia', value: money(data.totalVariance) },
                { label: 'Diferença média do dia', value: money(data.meanVariance) },
              ]}
            />
            {allClear(
              data,
              setting.isError || requests.isError || queue.isError
                ? null
                : (blockers?.length ?? null),
            ) ? (
              <EmptyState
                kind="all-clear"
                testID="queue-all-clear"
                statement="Nada pendente na fila."
                explanation="Não há fechamentos, bloqueios, rascunhos, solicitações ou custódias aguardando você."
              />
            ) : null}
            <Group
              title="Aguardando conferência"
              testID="queue-group-pending"
              empty="Nenhum fechamento enviado aguardando conferência."
              count={groups.pending.length}
            >
              {pendingByBox(groups.pending).map((box) => (
                <View
                  key={box[0]!.accountId}
                  testID={`queue-box-${box[0]!.accountId}`}
                  style={{ gap: spacing.stack.xs }}
                >
                  {box.map((row) => (
                    <CloseRow
                      group="pending"
                      key={row.dailyCloseId}
                      row={row}
                      onOpen={(r) => open(closeRoute(r), r.dailyCloseId)}
                    />
                  ))}
                  <BatchApproval
                    pending={box}
                    date={date}
                    onDone={refreshAll}
                    onReceipt={setBatchReceipt}
                    open={open}
                  />
                </View>
              ))}
            </Group>
            <Group
              title="Reprovados"
              testID="queue-group-rejected"
              empty="Nenhum fechamento reprovado aguardando correção."
              count={groups.rejected.length}
            >
              {groups.rejected.map((row) => (
                <CloseRow
                  group="rejected"
                  key={row.dailyCloseId}
                  row={row}
                  onOpen={(r) => open(closeRoute(r), r.dailyCloseId)}
                />
              ))}
            </Group>
            <Group
              title="Não enviados"
              testID="queue-group-not-submitted"
              empty="Todos os fechamentos esperados foram enviados."
              count={groups.notSubmitted.length}
            >
              {groups.notSubmitted.map((row) => (
                <NotSubmittedRow
                  key={row.dailyCloseId ?? `${row.accountId}-${row.operatorId}`}
                  row={row}
                  onOpen={() => open(notSubmittedRoute(row, date), row.dailyCloseId ?? undefined)}
                />
              ))}
            </Group>
            <Group
              title="Diferenças de caixa"
              testID="queue-group-variance"
              empty="Nenhuma diferença de caixa informada para este dia."
              count={groups.variance.length}
            >
              {groups.variance.map((row) => (
                <CloseRow
                  group="variance"
                  key={row.dailyCloseId}
                  row={row}
                  showVariance
                  onOpen={(r) => open(closeRoute(r), r.dailyCloseId)}
                />
              ))}
            </Group>
            <MonthContext
              lockDate={setting.isError ? null : (setting.data?.lockDate ?? null)}
              lockReady={reconciliation.isError ? null : (reconciliation.data?.lockReady ?? null)}
              blockers={blockers}
              year={year}
              month={month}
              open={open}
            />
            <Group
              title="Rascunhos de transações"
              testID="queue-group-drafts"
              empty="Nenhuma transação em rascunho sem bloqueio."
              count={data.unlockedDraftTransactionCount}
            >
              <AppText variant="helper" testID="queue-draft-window">
                {`${plural(data.unlockedDraftTransactionCount, 'transação em rascunho', 'transações em rascunho')} em períodos ainda não bloqueados, independente do dia selecionado.`}
              </AppText>
              <Button
                label="Abrir rascunhos de transações"
                role="link"
                variant="secondary"
                testID="queue-open-drafts"
                onPress={() => open(draftTransactionsPath())}
              />
            </Group>
            <Group
              title="Solicitações de vínculo"
              testID="queue-group-requests"
              empty="Nenhuma solicitação de vínculo pendente."
              count={data.pendingOperatorLinkRequestCount}
            >
              {(requests.data?.items ?? []).map((r) => (
                <AppText
                  key={r.id}
                  variant="helper"
                >{`${r.userName} · ${day(r.createdAt)}`}</AppText>
              ))}
              <AppText variant="helper" testID="queue-requests-boundary">
                A resolução de cada solicitação chega no M11; aqui você vê quem pediu e abre
                Operadores.
              </AppText>
              <Button
                label="Abrir Operadores"
                role="link"
                variant="secondary"
                testID="queue-open-requests"
                onPress={() => open(linkRequestsPath())}
              />
            </Group>
            <PersonalAlert data={data} />
            <CustodyPanel custody={data.custody} date={date} open={open} onChanged={refreshAll} />
          </>
        ) : null}
      </View>
    </RowRegistry.Provider>
  );
}

function Backlog({
  data,
  date,
  goDate,
}: {
  data: ResponseManagerQueueJson;
  date: string;
  goDate: (date: string) => void;
}) {
  const b = data.backlog;
  const total = b.submittedCount + b.rejectedCount + b.draftCount;
  if (total === 0) return null;
  const oldest = b.oldestPendingDate ? calendarPart(b.oldestPendingDate) : null;
  return (
    <Notice
      kind="warning"
      testID="queue-backlog"
      title="Há dias anteriores sem desfecho."
      body={`${plural(b.submittedCount, 'enviado', 'enviados')} · ${plural(b.rejectedCount, 'reprovado', 'reprovados')} · ${plural(b.draftCount, 'rascunho', 'rascunhos')}${oldest ? ` · mais antigo: ${day(oldest)}` : ''}`}
      actions={
        oldest && oldest !== date ? (
          <Button
            label={`Ir para ${day(oldest)}`}
            testID="queue-backlog-jump"
            onPress={() => goDate(oldest)}
          />
        ) : undefined
      }
    />
  );
}
function MonthContext({
  lockDate,
  lockReady,
  blockers,
  year,
  month,
  open,
}: {
  lockDate: string | null;
  lockReady: boolean | null;
  blockers:
    | readonly {
        type: string;
        day: number;
        accountName?: string | null;
        dailyCloseId?: string | null;
      }[]
    | null;
  year: number;
  month: number;
  open: (path: string, row?: string) => void;
}) {
  return (
    <Card
      title={blockers === null ? 'Bloqueios do mês' : `Bloqueios do mês (${blockers.length})`}
      testID="queue-group-month"
    >
      {blockers === null ? (
        <AppText variant="helper" testID="queue-blockers-unknown">
          As pendências do mês estão indisponíveis; isto não significa que não haja nenhuma.
        </AppText>
      ) : null}
      <AppText variant="helper" testID="queue-lock-date">
        {lockDate === null
          ? 'Data de bloqueio indisponível.'
          : noLockSet(lockDate)
            ? 'Nenhum período bloqueado.'
            : `Dados bloqueados até ${day(lockDate)}.`}
      </AppText>
      {lockReady !== null ? (
        <AppText variant="helper" testID="queue-lock-ready">
          {lockReady
            ? 'O servidor indica que o mês está pronto para bloquear (informação de apoio).'
            : 'O servidor indica pendências antes de bloquear o mês.'}
        </AppText>
      ) : null}
      {(blockers ?? []).map((blocker, index) => (
        <AppText key={`${blocker.type}-${blocker.day}-${index}`} variant="helper">
          {`Dia ${blocker.day}${blocker.accountName ? ` · ${blocker.accountName}` : ''} · ${blockerLabel(blocker.type)}`}
        </AppText>
      ))}
      <Button
        label="Fechamento do mês"
        role="link"
        variant="secondary"
        testID="queue-open-month"
        onPress={() => open(`/relatorios/fechamento-mensal/${year}/${month}`)}
      />
    </Card>
  );
}
const blockerLabel = (type: string) =>
  type === 'UnapprovedClose'
    ? 'fechamento não aprovado'
    : type === 'DraftTransactions'
      ? 'transações em rascunho'
      : type === 'MissingExpectedClose'
        ? 'fechamento esperado ausente'
        : type;
function PersonalAlert({ data }: { data: ResponseManagerQueueJson }) {
  const p = data.personalDifferences;
  const threshold = p.currentShortageThreshold;
  const alerts = p.people.filter((person) => person.currentThresholdAlert);
  if (
    p.unassignedSourceCount === 0 &&
    (threshold === null || threshold === undefined) &&
    !alerts.length
  )
    return null;
  return (
    <Card title="Diferenças pessoais" testID="queue-personal">
      <AppText variant="helper">{`Janela: ${day(p.dateFrom)} a ${day(p.dateTo)} (até ${day(p.asOfDate)}).`}</AppText>
      {threshold === null || threshold === undefined ? (
        <AppText variant="helper">Alerta não configurado.</AppText>
      ) : (
        <AppText variant="helper">{`Limite de faltas atual: ${money(threshold)}.`}</AppText>
      )}
      {alerts.length ? (
        <View testID="queue-personal-alerts" style={{ gap: spacing.stack.sm }}>
          <AppText variant="body-strong">Limite de faltas atingido</AppText>
          {alerts.map((person, index) => (
            <View
              key={person.operatorId ?? index}
              testID={`queue-personal-alert-${person.operatorId ?? index}`}
              style={{ gap: spacing.stack.xs }}
            >
              <AppText variant="body-strong">{person.operatorName ?? 'Sem nome'}</AppText>
              <AppText variant="helper">{`Faltas: ${money(person.shortage)} · Sobras: ${money(person.overage)} · Faltas consideradas no limite: ${money(person.thresholdShortageBasis)} · ${plural(person.sourceCount, 'origem', 'origens')}`}</AppText>
            </View>
          ))}
          <AppText variant="helper" testID="queue-personal-boundary">
            A tela de diferenças por pessoa chega no M8.
          </AppText>
        </View>
      ) : null}
      {p.unassignedSourceCount > 0 ? (
        <AppText variant="helper" testID="queue-personal-unassigned">
          {`${plural(p.unassignedSourceCount, 'origem sem responsável', 'origens sem responsável')} nesta janela.`}
        </AppText>
      ) : null}
      <AppText variant="helper">
        {p.automaticPayrollDeduction
          ? 'O servidor desconta automaticamente na folha.'
          : 'Informativo. Não há desconto automático em folha.'}
      </AppText>
    </Card>
  );
}
