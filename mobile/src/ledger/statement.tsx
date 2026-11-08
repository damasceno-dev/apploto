import { useMemo, useRef, useState } from 'react';
import { Text, View } from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useQuery } from '@tanstack/react-query';
import { getClientClientId, getClientClientIdFiadoStatement } from '@loto/api';
import {
  directionLabels,
  formatBrlWire,
  formatCpf,
  formatDate,
  formatPhone,
  parseCalendarDate,
  presentFiadoBalanceWire,
  type CalendarDate,
} from '@loto/core';
import { useSession, useScopedApiClient } from '@/session/provider';
import { useShell } from '@/shell/provider';
import { useTheme } from '@/theme/provider';
import { spacing } from '@/theme/style';
import {
  AppText,
  Button,
  Card,
  DateRangeField,
  EmptyState,
  FormField,
  Notice,
  Pagination,
  Skeleton,
} from '@/ui';
import { STATEMENT_SETUP } from '@/admin/clients';
import { errorMessages, useLedgerReads, useStatementAccess } from './common';
import { day } from './contracts';

const businessDay = (value: string | null | undefined) => {
  const date = value ? parseCalendarDate(day(value)!) : null;
  return date ? formatDate(date) : '—';
};
/** The statement's balance mapping from core: debt, quitado, or client credit — never re-signed. */
function Balance({ value, testID, label }: { value: number; testID: string; label: string }) {
  const { colors } = useTheme();
  const shown = presentFiadoBalanceWire(value);
  return (
    <AppText
      testID={testID}
      color={
        colors.dom['fiado-balance'][
          shown.kind === 'credit' ? 'credit' : shown.kind === 'debt' ? 'debt' : 'settled'
        ].fg
      }
    >
      {`${label}: ${shown.text}`}
    </AppText>
  );
}

/**
 * Client statement (Extrato de fiado): access is checked BEFORE any read (Manager/Admin, or a
 * Member holding an explicit Tab grant); the balance, FIFO position, window and running balances
 * are the server's, shown through the core display mapping only.
 */
export function StatementScreen() {
  const allowed = useStatementAccess();
  if (allowed === null) return <Skeleton label="Conferindo seu acesso…" />;
  if (!allowed)
    return (
      <EmptyState
        kind="setup-needed"
        statement="Extrato de fiado indisponível para seu perfil."
        explanation={STATEMENT_SETUP}
      />
    );
  return <Statement />;
}

function Statement() {
  const params = useLocalSearchParams<{ id: string; from?: string; to?: string; page?: string }>();
  const router = useRouter();
  const { snapshot } = useSession();
  const shell = useShell();
  const client = useScopedApiClient();
  const reads = useLedgerReads();
  const today = snapshot.clock!.branchLocalDate;
  const window = useMemo(
    () => ({
      from: params.from && parseCalendarDate(params.from) ? params.from : null,
      to: params.to && parseCalendarDate(params.to) ? params.to : null,
      page: Number(params.page) >= 1 ? Number(params.page) : 1,
    }),
    [params.from, params.to, params.page],
  );
  const [staged, setStaged] = useState(window);
  const person = useQuery({
    queryKey: ['ledger', snapshot.scope, 'client', params.id],
    queryFn: async ({ signal }) => (await getClientClientId(params.id, { client, signal })).data,
    enabled: reads,
    retry: false,
    staleTime: 0,
  });
  const statement = useQuery({
    queryKey: ['ledger', snapshot.scope, 'statement', params.id, window],
    queryFn: async ({ signal }) =>
      (
        await getClientClientIdFiadoStatement(
          params.id,
          {
            ...(window.from ? { DateFrom: window.from } : {}),
            ...(window.to ? { DateTo: window.to } : {}),
            Page: window.page,
            PageSize: 50,
          },
          { client, signal },
        )
      ).data,
    enabled: reads,
    retry: false,
    staleTime: 0,
  });
  const heading = useRef<Text>(null);
  const apply = (next: typeof window) =>
    router.setParams({ from: next.from ?? '', to: next.to ?? '', page: String(next.page) });
  const data = statement.data;
  return (
    <>
      {person.status === 'success' ? (
        <Card title={person.data.name} testID="statement-client">
          <AppText>{formatPhone(person.data.phone)}</AppText>
          {person.data.cpf ? <AppText>{`CPF ${formatCpf(person.data.cpf)}`}</AppText> : null}
        </Card>
      ) : person.status === 'error' ? (
        <Notice
          kind="danger"
          title="Não foi possível carregar o cliente."
          messages={errorMessages(person.error)}
        />
      ) : (
        <Skeleton label="Carregando o cliente…" />
      )}
      {statement.status === 'pending' ? (
        <Skeleton label="Carregando o extrato…" />
      ) : statement.status === 'error' ? (
        <Notice
          kind="danger"
          testID="statement-error"
          title="Não foi possível carregar o extrato."
          messages={errorMessages(statement.error)}
          actions={<Button label="Tentar novamente" onPress={() => void statement.refetch()} />}
        />
      ) : (
        <>
          <Card title="Saldo de fiado" testID="statement-balance">
            <Balance
              testID="statement-outstanding"
              value={data!.outstandingTotal}
              label={`Hoje (${businessDay(data!.asOfDate)})`}
            />
            <AppText testID="statement-oldest">
              {`Data da venda mais antiga em aberto: ${data!.oldestUnpaidDate ? businessDay(data!.oldestUnpaidDate) : 'nenhuma'}`}
            </AppText>
            <Button
              label="Receber pagamento"
              testID="statement-receive"
              onPress={() => void shell.navigate(`/clientes/${params.id}/receber`, true)}
            />
          </Card>
          <FormField label="Período do extrato">
            <DateRangeField
              testID="statement-range"
              today={parseCalendarDate(today) ?? undefined}
              from={staged.from ? (parseCalendarDate(staged.from) as CalendarDate) : null}
              to={staged.to ? (parseCalendarDate(staged.to) as CalendarDate) : null}
              onChange={(range) => setStaged((s) => ({ ...s, from: range.from, to: range.to }))}
            />
          </FormField>
          <Button
            label="Aplicar período"
            variant="secondary"
            testID="statement-apply"
            onPress={() => apply({ ...staged, page: 1 })}
          />
          <Card
            title={`Período: ${data!.dateFrom ? businessDay(data!.dateFrom) : 'todo o histórico'} a ${businessDay(data!.dateTo)}`}
            testID="statement-window"
          >
            <Balance
              testID="statement-opening"
              value={data!.openingBalance}
              label="Saldo antes do período"
            />
            <Balance
              testID="statement-closing"
              value={data!.closingBalance}
              label="Saldo no fim do período"
            />
          </Card>
          <AppText ref={heading} variant="section-title" accessibilityRole="header">
            Movimentações
          </AppText>
          {data!.items.length === 0 ? (
            <EmptyState
              kind="all-clear"
              statement="Nenhuma venda fiado ou pagamento neste período."
            />
          ) : (
            <View accessibilityRole="list" testID="statement-rows">
              {data!.items.map((item) => (
                <View
                  key={item.transactionId}
                  accessibilityRole="text"
                  style={{ paddingVertical: spacing.stack.sm, gap: spacing.stack.xs }}
                  testID={`statement-row-${item.transactionId}`}
                >
                  <AppText variant="body-strong">{`${businessDay(item.date)} · ${directionLabels[item.direction]} ${formatBrlWire(item.value)}`}</AppText>
                  <AppText variant="helper">{`${item.transactionTypeName} · ${item.accountName}${item.description ? ` · ${item.description}` : ''}`}</AppText>
                  <Balance
                    testID={`statement-running-${item.transactionId}`}
                    value={item.runningBalance}
                    label="Saldo depois"
                  />
                  <Button
                    label="Abrir transação"
                    variant="link"
                    role="link"
                    onPress={() =>
                      void shell.navigate(
                        `/transacoes/${item.transactionId}`,
                        true,
                        false,
                        item.transactionId,
                      )
                    }
                  />
                </View>
              ))}
            </View>
          )}
          <Pagination
            listName="movimentações do extrato"
            envelope={data!}
            loading={statement.isFetching}
            focusTarget={heading}
            onPageChange={(page) => apply({ ...window, page })}
          />
        </>
      )}
    </>
  );
}
