import { useCallback, useRef, useState } from 'react';
import type { View } from 'react-native';
import { isServerId } from '@/session/credentials';
import { useFocusEffect, useLocalSearchParams, useRouter } from 'expo-router';
import { useGetReportOperatorSummary, useGetReportTimeentryBalance } from '@loto/api/queries';
import {
  formatBrlWire,
  formatSignedBrlWire,
  formatCount,
  formatDate,
  formatDuration,
  formatSignedDuration,
  type CalendarDate,
} from '@loto/core';
import { useScopedApiClient, useSession } from '@/session/provider';
import { useShell } from '@/shell/provider';
import { AppText, Badge, Button, Card, DateRangeField, Notice, moveFocusTo } from '@/ui';
import {
  datePart,
  dayTrail,
  periodFor,
  periodQuery,
  reportContext,
  type Period,
} from './contracts';
import { ReadState, useTimeReadsAllowed } from './common';

export function PeriodControls({
  period,
  apply,
}: {
  period: Period;
  apply: (period: Period) => void;
}) {
  const { snapshot } = useSession();
  const readEnabled = useTimeReadsAllowed();
  const [range, setRange] = useState<{ from: CalendarDate | null; to: CalendarDate | null }>(
    period,
  );
  const valid = range.from && range.to && periodFor(range.from, range.from, range.to);
  return (
    <Card title="Período">
      <DateRangeField
        disabled={!readEnabled}
        from={range.from}
        to={range.to}
        today={datePart(snapshot.clock!.branchLocalDate)!}
        onChange={setRange}
        testID="time-period"
      />
      {!valid ? <AppText>Escolha um período de até 366 dias.</AppText> : null}
      <Button
        label="Aplicar período"
        disabled={!valid || !readEnabled}
        onPress={() => {
          if (valid) apply(valid);
        }}
      />
    </Card>
  );
}
export function ReportScreen({ kind, own }: { kind: 'transactions' | 'balance'; own: boolean }) {
  const { snapshot } = useSession();
  const params = useLocalSearchParams<{
    id?: string;
    operatorId?: string;
    from?: string;
    to?: string;
    mine?: string;
    focus?: string;
  }>();
  if (
    (params.operatorId && !isServerId(params.operatorId)) ||
    (params.mine === 'true' && params.operatorId)
  )
    return (
      <Notice
        kind="danger"
        title="Escolha suas horas ou um operador válido, sem combinar os dois filtros."
      />
    );
  const today = datePart(snapshot.clock!.branchLocalDate)!;
  const period = periodFor(today, params.from, params.to);
  if (!period)
    return (
      <Notice
        kind="danger"
        title="Período inválido. Escolha datas válidas, em ordem, com até 366 dias."
      />
    );
  return (
    <ReportContent
      kind={kind}
      own={own || params.mine === 'true'}
      period={period}
      operatorId={params.id ?? params.operatorId}
      focus={params.focus}
    />
  );
}
function ReportContent({
  kind,
  own,
  period,
  operatorId,
  focus,
}: {
  kind: 'transactions' | 'balance';
  own: boolean;
  period: Period;
  operatorId?: string;
  focus?: string;
}) {
  const { snapshot } = useSession();
  const client = useScopedApiClient();
  const router = useRouter();
  const shell = useShell();
  const context = reportContext(snapshot.branch!.role, own, operatorId);
  const selected = own || snapshot.branch!.role === 'Member' || 'OperatorId' in context;
  const enabled = useTimeReadsAllowed();
  const query = { DateFrom: period.from, DateTo: period.to, ...context };
  const summary = useGetReportOperatorSummary(query, {
    request: { client },
    query: { enabled: enabled && kind === 'transactions' && selected, staleTime: 0 },
  });
  const balance = useGetReportTimeentryBalance(query, {
    request: { client },
    query: { enabled: enabled && kind === 'balance', staleTime: 0 },
  });
  const result = kind === 'transactions' ? summary : balance;
  const data = summary.data?.data;
  const hours = balance.data?.data;
  const rowRefs = useRef(new Map<string, View>());
  useFocusEffect(
    useCallback(() => {
      if (focus && hours) moveFocusTo(rowRefs.current.get(focus));
    }, [focus, hours]),
  );
  const linkQuery = periodQuery(period);
  if ('OperatorId' in context) linkQuery.set('operatorId', context.OperatorId!);
  else if ('Mine' in context) linkQuery.set('mine', 'true');
  const balancePath = own ? '/turno/banco-de-horas' : '/gestao/ponto/banco-de-horas';
  return (
    <>
      <PeriodControls
        period={period}
        apply={(next) => router.setParams({ from: next.from, to: next.to })}
      />
      <ReadState
        pending={result.isPending}
        error={result.error}
        retry={() => void result.refetch()}
      />
      {kind === 'transactions' && data ? (
        <>
          <AppText variant="section-title">
            {own ? 'Minhas transações' : `Transações de ${data.operatorName}`}
          </AppText>
          {!own && !data.operatorActive ? (
            <Badge kind="feedback" tone="neutral" label="Inativo" />
          ) : null}
          <Notice
            kind="info"
            title="Volumes de movimentações registradas"
            body="As automáticas já fazem parte dos totais. Estes volumes não representam receita ou lucro, nem um segundo recebimento do cliente."
          />
          {data.totalTransactionCount === 0 ? (
            <AppText>Nenhuma transação no período.</AppText>
          ) : null}
          <Card title="Transações">
            <AppText>{`${formatCount(data.totalTransactionCount)} transações, ${formatCount(data.generatedTransactionCount)} delas automáticas`}</AppText>
            <AppText>{`Entradas: ${formatBrlWire(data.totalInValue)} · ${formatBrlWire(data.generatedInValue)} automáticas`}</AppText>
            <AppText>{`Saídas: ${formatBrlWire(data.totalOutValue)} · ${formatBrlWire(data.generatedOutValue)} automáticas`}</AppText>
            <AppText>{`Líquido: ${formatSignedBrlWire(data.netValue)}`}</AppText>
          </Card>
          {data.byCategory.map((category) => (
            <Card key={category.categoryId} title={category.categoryName}>
              <AppText>{`${formatCount(category.count)} transações, ${formatCount(category.generatedCount)} delas automáticas`}</AppText>
              <AppText>{`Entradas: ${formatBrlWire(category.totalIn)} · ${formatBrlWire(category.generatedIn)} automáticas`}</AppText>
              <AppText>{`Saídas: ${formatBrlWire(category.totalOut)} · ${formatBrlWire(category.generatedOut)} automáticas`}</AppText>
            </Card>
          ))}
          <Button
            label="Ver transações"
            variant="link"
            onPress={() => void shell.navigate('/transacoes?' + linkQuery, true)}
          />
          <Button
            label={own ? 'Meu banco de horas' : `Banco de horas de ${data.operatorName}`}
            variant="link"
            onPress={() => void shell.navigate(balancePath + '?' + linkQuery, true)}
          />
        </>
      ) : null}
      {kind === 'balance' && hours ? (
        <>
          <AppText variant="section-title">
            {selected ? 'Banco de horas no período' : 'Banco de horas da equipe'}
          </AppText>
          <AppText>{`${formatDate(datePart(hours.dateFrom)!)} a ${formatDate(datePart(hours.dateTo)!)}`}</AppText>
          <AppText>O saldo corresponde ao período consultado.</AppText>
          {hours.operators.some((operator) => operator.containsInProgress) ? (
            <Notice kind="info" title="Há registros em andamento" />
          ) : null}
          {hours.operators.length === 0 ? (
            <AppText>
              {selected ? 'Nenhum registro no período.' : 'Nenhum operador no período.'}
            </AppText>
          ) : null}
          {hours.operators.map((operator) => (
            <Card key={operator.operatorId} title={operator.operatorName}>
              {!operator.operatorActive ? (
                <Badge kind="feedback" tone="neutral" label="Inativo" />
              ) : null}
              <AppText>{`Horas: ${formatDuration(operator.totalHours)} · Saldo do banco de horas: ${formatSignedDuration(operator.totalBalanceHours)}`}</AppText>
              <AppText>{`Presentes: ${formatCount(operator.presentDays)} · Ausências: ${formatCount(operator.absentDays)} · Devendo: ${formatCount(operator.owingDays)} · Abonados: ${formatCount(operator.abonadoDays)}`}</AppText>
              {!selected ? (
                <>
                  <Button
                    ref={(node) => {
                      if (node) rowRefs.current.set(operator.operatorId, node);
                      else rowRefs.current.delete(operator.operatorId);
                    }}
                    label={`Ver detalhes de ${operator.operatorName}`}
                    onPress={() =>
                      void shell.navigate(
                        '/gestao/ponto/banco-de-horas?' +
                          new URLSearchParams({
                            from: period.from,
                            to: period.to,
                            operatorId: operator.operatorId,
                            row: operator.operatorId,
                          }),
                        true,
                        false,
                        operator.operatorId,
                      )
                    }
                  />
                  <Button
                    label={`Transações de ${operator.operatorName}`}
                    variant="link"
                    onPress={() =>
                      void shell.navigate(
                        `/gestao/operadores/${operator.operatorId}/transacoes?` +
                          periodQuery(period),
                        true,
                      )
                    }
                  />
                </>
              ) : (
                <>
                  {operator.days.length === 0 ? (
                    <AppText>Nenhum registro no período.</AppText>
                  ) : null}
                  {dayTrail(
                    { from: datePart(hours.dateFrom)!, to: datePart(hours.dateTo)! },
                    operator.days,
                  ).map(({ date, entry }) => (
                    <Card key={date} title={formatDate(date)}>
                      {entry ? (
                        <>
                          <Badge kind="status" domain="time-entry-status" value={entry.status} />
                          <AppText>{`Horas: ${formatDuration(entry.totalHours)} · Meta do dia: ${formatDuration(entry.totalHours - entry.balanceHours)} · Saldo: ${formatSignedDuration(entry.balanceHours)}`}</AppText>
                          {entry.isInProgress ? <AppText>Em andamento</AppText> : null}
                        </>
                      ) : (
                        <AppText>Sem registro</AppText>
                      )}
                      {snapshot.branch!.role !== 'Member' ? (
                        <Button
                          ref={(node) => {
                            if (node) rowRefs.current.set(date, node);
                            else rowRefs.current.delete(date);
                          }}
                          label={`Abrir ponto de ${formatDate(date)}`}
                          variant="link"
                          onPress={() =>
                            void shell.navigate(
                              '/gestao/ponto?' +
                                new URLSearchParams({
                                  operatorId: operator.operatorId,
                                  from: date,
                                  to: date,
                                  date,
                                  row: date,
                                }),
                              true,
                              false,
                              date,
                            )
                          }
                        />
                      ) : date === datePart(snapshot.clock!.branchLocalDate) ? (
                        <Button
                          ref={(node) => {
                            if (node) rowRefs.current.set(date, node);
                            else rowRefs.current.delete(date);
                          }}
                          label="Abrir Ponto de hoje"
                          variant="link"
                          onPress={() => void shell.navigate('/turno/ponto', true, false, date)}
                        />
                      ) : null}
                    </Card>
                  ))}
                </>
              )}
            </Card>
          ))}
        </>
      ) : null}
    </>
  );
}
