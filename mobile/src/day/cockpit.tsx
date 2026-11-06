import { useMemo } from 'react';
import { View } from 'react-native';
import { useQuery } from '@tanstack/react-query';
import {
  getOperatorLinkRequestSelf,
  getSession,
  getSessionOpeningProposal,
  getSessionSessionId,
  getSessionSessionIdHistory,
  getTerminal,
  getTimeentry,
  getTransaction,
  type ApiClient,
  type ResponseOperatorAccountJson,
} from '@loto/api';
import {
  directionLabels,
  formatBrlWire,
  formatLongDate,
  operatorLinkRequestStatusLabels,
  timeEntryStatusLabels,
  type CalendarDate,
} from '@loto/core';
import { useSession, useScopedApiClient } from '@/session/provider';
import { useShell } from '@/shell/provider';
import { spacing } from '@/theme/style';
import { AppText, Badge, Button, Card, EmptyState, Notice, Skeleton } from '@/ui';
import {
  cashboxGrants,
  nextAction,
  pluralDrafts,
  resolveWorkstation,
  todaysSession,
  type CorrectionCandidate,
  type DraftCounts,
  type NextAction,
  type Read,
  type WorkstationFacts,
} from './contracts';
import { auditInstant, auditTime, businessDate, errorMessages, useDayReads } from './common';
import { useWorkstationChoice } from './workstation';

const toRead = <T,>(query: {
  status: 'pending' | 'error' | 'success';
  data?: T;
  error: unknown;
  refetch: () => unknown;
}): Read<T> =>
  query.status === 'success'
    ? { status: 'ok', data: query.data as T }
    : query.status === 'error'
      ? { status: 'error', error: query.error, retry: () => void query.refetch() }
      : { status: 'pending' };

type SessionFilter = Omit<NonNullable<Parameters<typeof getSession>[0]>, 'Page' | 'PageSize'>;
/** Hard ceiling on pages walked; a longer list is a failed read, never a silently partial one. */
const MAX_PAGES = 50;
/**
 * Every row of a session list (all pages): next-action priority and custody facts are only decided
 * on the complete list; a page that fails fails the whole read.
 */
export async function allSessions(client: ApiClient, filter: SessionFilter) {
  const rows = [];
  for (let page = 1; ; page++) {
    if (page > MAX_PAGES) throw new Error('A lista de fechamentos é longa demais para verificar.');
    const result = (await getSession({ ...filter, Page: page, PageSize: 50 }, { client })).data;
    rows.push(...result.items);
    if (!result.hasNext) return rows;
  }
}

/** P1 sources: own (recorder) Rejected or correction/opening-recheck Drafts, unlocked by the server. */
async function correctionCandidates(client: ApiClient, today: string) {
  const [rejected, drafts] = await Promise.all([
    allSessions(client, { Mine: true, DateTo: today, Status: 'Rejected' }),
    allSessions(client, { Mine: true, DateTo: today, Status: 'Draft' }),
  ]);
  const rows = [...rejected, ...drafts];
  // The list row lacks the correction markers; the session read has them and the server's own
  // lock-aware AllowedActions, so no lock date is inferred on the device.
  const details = await Promise.all(rows.map((row) => getSessionSessionId(row.id, { client })));
  return details
    .map((d) => d.data)
    .filter(
      (s) =>
        (s.status === 'Rejected' || !!s.rejectionReason || !!s.openingRecheckRequiredAt) &&
        !!s.allowedActions?.canSaveCount,
    )
    .sort((a, b) => a.date.localeCompare(b.date))
    .map((s): CorrectionCandidate => ({
      id: s.id,
      date: s.date,
      accountName: s.accountName,
      rejectionReason: s.rejectionReason ?? null,
      openingRecheck: !!s.openingRecheckRequiredAt,
    }));
}
async function workstationFacts(
  client: ApiClient,
  accountId: string,
  today: string,
  operatorId: string,
): Promise<WorkstationFacts> {
  const [proposal, list] = await Promise.all([
    getSessionOpeningProposal({ accountId }, { client }),
    allSessions(client, { AccountId: accountId, DateFrom: today, DateTo: today }),
  ]);
  const activeId = proposal.data.activeSessionId;
  const active = activeId ? (await getSessionSessionId(activeId, { client })).data : null;
  let handoverForMe = null;
  if (activeId && proposal.data.pendingHandoverId) {
    const history = await getSessionSessionIdHistory(activeId, { client });
    handoverForMe =
      history.data.handovers.find(
        (h) =>
          h.id === proposal.data.pendingHandoverId &&
          h.intendedIncomingOperatorId === operatorId &&
          (h.state === 'Offered' || h.state === 'Disputed'),
      ) ?? null;
  }
  return { proposal: proposal.data, active, today: list, handoverForMe };
}

export function CockpitScreen() {
  const { snapshot } = useSession();
  const client = useScopedApiClient();
  const reads = useDayReads();
  const shell = useShell();
  const today = snapshot.clock!.branchLocalDate;
  const role = snapshot.branch!.role;
  const self = snapshot.selfContext;
  const operatorId = self?.operatorId ?? null;
  const grants = useMemo(() => cashboxGrants(self), [self]);
  const choice = useWorkstationChoice(today);
  const resolved =
    choice.status === 'loading' ? null : resolveWorkstation(grants, choice.choice, today);
  const workstation = resolved && typeof resolved === 'object' ? resolved : null;
  const base = ['day', snapshot.scope, today] as const;
  const linked = !!operatorId;
  const correction = useQuery({
    queryKey: [...base, 'correction', operatorId],
    queryFn: () => correctionCandidates(client, today),
    enabled: reads && linked,
    retry: false,
    staleTime: 0,
  });
  const facts = useQuery({
    queryKey: [...base, 'workstation', workstation?.accountId, operatorId],
    queryFn: () => workstationFacts(client, workstation!.accountId, today, operatorId!),
    enabled: reads && linked && !!workstation,
    retry: false,
    staleTime: 0,
  });
  const activeDraft =
    facts.data?.active &&
    facts.data.active.custody.custodianOperatorId === operatorId &&
    (facts.data.active.status === 'Draft' || facts.data.active.status === 'Rejected');
  const drafts = useQuery({
    queryKey: [...base, 'drafts', workstation?.accountId],
    queryFn: async (): Promise<DraftCounts> => {
      const filter = {
        AccountId: workstation!.accountId,
        DateFrom: today,
        DateTo: today,
        Status: 'Draft' as const,
        Page: 1,
        PageSize: 1,
      };
      const [all, mine] = await Promise.all([
        getTransaction(filter, { client }),
        getTransaction({ ...filter, Mine: true }, { client }),
      ]);
      return { total: all.data.totalCount, mine: mine.data.totalCount };
    },
    enabled: reads && linked && !!workstation,
    retry: false,
    staleTime: 0,
  });
  const others = useQuery({
    queryKey: [...base, 'others', operatorId],
    queryFn: () => allSessions(client, { DateFrom: today, DateTo: today }),
    enabled: reads && linked,
    retry: false,
    staleTime: 0,
  });
  const movements = useQuery({
    queryKey: [...base, 'movements', workstation?.accountId],
    queryFn: async () =>
      (
        await getTransaction(
          {
            AccountId: workstation!.accountId,
            DateFrom: today,
            DateTo: today,
            Page: 1,
            PageSize: 5,
          },
          { client },
        )
      ).data,
    enabled: reads && linked && !!workstation,
    retry: false,
    staleTime: 0,
  });
  const ponto = useQuery({
    queryKey: [...base, 'ponto'],
    queryFn: async () =>
      (
        await getTimeentry(
          { Mine: true, DateFrom: today, DateTo: today, Page: 1, PageSize: 1 },
          { client },
        )
      ).data.items[0] ?? null,
    enabled: reads && linked && role === 'Member',
    retry: false,
    staleTime: 0,
  });
  const terminals = useQuery({
    queryKey: [...base, 'terminals'],
    queryFn: async () => (await getTerminal({ client })).data,
    enabled: reads && linked && !!workstation,
    retry: false,
    staleTime: 60000,
  });
  const needsSetup = !linked || resolved === 'none';
  const linkRequest = useQuery({
    queryKey: [...base, 'link-request'],
    queryFn: async () => (await getOperatorLinkRequestSelf({ client })).data.request ?? null,
    enabled: reads && role === 'Member' && needsSetup,
    retry: false,
    staleTime: 0,
  });
  const action: NextAction | null =
    linked && workstation
      ? nextAction({
          operatorId: operatorId!,
          role,
          correction: toRead(correction),
          workstation: toRead(facts),
          drafts: activeDraft ? toRead(drafts) : null,
          others: toRead(others),
        })
      : null;
  const todays = todaysSession(facts.data ?? null, today);
  const terminalName = facts.data?.proposal.terminalId
    ? terminals.data?.find((t) => t.id === facts.data!.proposal.terminalId)?.name
    : undefined;
  const go = (path: string) => void shell.navigate(path, true);

  if (!linked)
    return (
      <SetupNeeded
        kind="link"
        role={role}
        request={linkRequest}
        onAssignments={() => go('/gestao/contas-dos-operadores')}
      />
    );
  if (resolved === null) return <Skeleton label="Carregando seu caixa…" />;
  if (resolved === 'none')
    return (
      <SetupNeeded
        kind="account"
        role={role}
        request={linkRequest}
        onAssignments={() => void shell.navigate('/gestao/contas-dos-operadores')}
      />
    );
  if (resolved === 'choose')
    return (
      <Card title="Em qual caixa você vai operar hoje?" testID="cockpit-choose">
        <AppText>
          Você tem mais de um caixa atribuído. A escolha vale neste aparelho até o fim do dia da
          filial.
        </AppText>
        {choice.problem ? <Notice kind="warning" title={choice.problem} /> : null}
        {grants.map((grant) => (
          <Button
            key={grant.accountId}
            label={grant.accountName}
            variant="secondary"
            testID={`cockpit-choose-${grant.accountId}`}
            onPress={() => void choice.choose(grant.accountId)}
            fullWidth
          />
        ))}
      </Card>
    );
  return (
    <View style={{ gap: spacing.stack.md }} testID="cockpit">
      <AppText testID="cockpit-context">
        {`${formatLongDate(today as CalendarDate)} · ${workstation!.accountName}${terminalName ? ` · terminal ${terminalName}` : ''}`}
      </AppText>
      {grants.length > 1 ? (
        <Button
          label="Trocar caixa"
          variant="secondary"
          testID="cockpit-switch"
          onPress={() => void choice.clear()}
        />
      ) : null}
      {choice.problem ? <Notice kind="warning" title={choice.problem} /> : null}
      <NextActionCard
        action={action}
        workstation={workstation!}
        role={role}
        today={today}
        go={go}
      />
      {todays ? (
        <Card title="Fechamento de hoje" testID="cockpit-today">
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.inline.sm }}>
            <AppText>{workstation!.accountName}</AppText>
            <Badge
              kind="status"
              domain="daily-close-status"
              value={todays.status}
              context="Situação"
            />
          </View>
          {action && !['open', 'drafts', 'submitted', 'approved'].includes(action.kind) ? (
            <AppText variant="helper">
              O próximo passo acima tem prioridade, mas não bloqueia: a contagem de hoje abre por
              aqui.
            </AppText>
          ) : null}
          <Button
            label="Ver fechamento de hoje"
            variant="link"
            role="link"
            testID="cockpit-today-open"
            onPress={() => go(`/fechamentos/${todays.id}`)}
          />
        </Card>
      ) : null}
      <OtherCashboxes
        query={others}
        operatorId={operatorId!}
        workstation={workstation!.accountId}
        go={go}
      />
      {role === 'Member' ? (
        <Card title="Ponto" testID="cockpit-ponto">
          {ponto.status === 'pending' ? (
            <Skeleton label="Carregando o ponto…" />
          ) : ponto.status === 'error' ? (
            <Notice
              kind="warning"
              title="Não foi possível carregar o ponto de hoje."
              messages={errorMessages(ponto.error)}
              actions={<Button label="Tentar novamente" onPress={() => void ponto.refetch()} />}
            />
          ) : (
            <AppText testID="cockpit-ponto-state">
              {ponto.data
                ? ponto.data.isInProgress
                  ? 'Ponto em andamento hoje.'
                  : `Ponto de hoje: ${timeEntryStatusLabels[ponto.data.status]}.`
                : 'Fora do ponto: nenhum registro hoje.'}
            </AppText>
          )}
          <Button
            label="Abrir ponto"
            variant="link"
            role="link"
            onPress={() => go('/turno/ponto')}
          />
        </Card>
      ) : null}
      <Card title={`Transações de hoje no ${workstation!.accountName}`} testID="cockpit-movements">
        {movements.status === 'pending' ? (
          <Skeleton label="Carregando transações…" />
        ) : movements.status === 'error' ? (
          <Notice
            kind="warning"
            title="Não foi possível carregar as transações de hoje."
            messages={errorMessages(movements.error)}
            actions={<Button label="Tentar novamente" onPress={() => void movements.refetch()} />}
          />
        ) : movements.data.items.length === 0 ? (
          <AppText>Nenhuma transação registrada hoje neste caixa.</AppText>
        ) : (
          <>
            {movements.data.items.map((row) => (
              <View
                key={row.id}
                testID={`cockpit-movement-${row.id}`}
                style={{ gap: spacing.stack.xs }}
              >
                <AppText variant="body-strong">{row.transactionTypeName}</AppText>
                <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.inline.sm }}>
                  <AppText>{`${directionLabels[row.direction]} · ${formatBrlWire(row.value)}`}</AppText>
                  <Badge
                    kind="status"
                    domain="transaction-status"
                    value={row.status}
                    context="Situação"
                  />
                  {row.generationKind ? <Badge kind="automatic" /> : null}
                </View>
                <AppText variant="helper">{`Registrada às ${auditTime(row.createdAt)}`}</AppText>
              </View>
            ))}
            <AppText variant="helper">{`${movements.data.items.length} de ${movements.data.totalCount} · somente as primeiras transações`}</AppText>
          </>
        )}
        {drafts.status === 'success' && drafts.data.total > 0 ? (
          <AppText testID="cockpit-draft-split">
            {`${pluralDrafts(drafts.data.total)}: ${drafts.data.mine} ${drafts.data.mine === 1 ? 'sua' : 'suas'} · ${drafts.data.total - drafts.data.mine} de outros operadores`}
          </AppText>
        ) : null}
        <Button
          label="Ver todas"
          variant="link"
          role="link"
          onPress={() =>
            go(`/transacoes?accountId=${workstation!.accountId}&from=${today}&to=${today}`)
          }
        />
        <Button
          label="Nova transação"
          variant="secondary"
          onPress={() => go(`/transacoes/nova?accountId=${workstation!.accountId}`)}
        />
      </Card>
      <Card title="Meus números">
        <Button
          label="Minhas transações"
          variant="link"
          role="link"
          onPress={() => go('/turno/minhas-transacoes')}
        />
        <Button
          label="Meu banco de horas"
          variant="link"
          role="link"
          onPress={() => go('/turno/banco-de-horas')}
        />
      </Card>
    </View>
  );
}

function NextActionCard({
  action,
  workstation,
  role,
  today,
  go,
}: {
  action: NextAction | null;
  today: string;
  workstation: ResponseOperatorAccountJson;
  role: string;
  go: (path: string) => void;
}) {
  if (!action)
    return (
      <Card variant="next-action" eyebrow="Próximo passo" testID="cockpit-next-loading">
        <Skeleton label="Verificando o próximo passo…" />
      </Card>
    );
  const card = (
    title: string,
    body: string,
    cta?: { label: string; path: string },
    secondary?: { label: string; path: string },
    extra?: React.ReactNode,
  ) => (
    <Card
      variant="next-action"
      eyebrow="Próximo passo"
      title={title}
      testID={`cockpit-next-${action.kind}`}
    >
      <AppText>{body}</AppText>
      {extra}
      {cta ? (
        <Button
          label={cta.label}
          variant="cta"
          testID="cockpit-next-cta"
          onPress={() => go(cta.path)}
          fullWidth
        />
      ) : null}
      {secondary ? (
        <Button
          label={secondary.label}
          variant="secondary"
          onPress={() => go(secondary.path)}
          fullWidth
        />
      ) : null}
    </Card>
  );
  const account = workstation.accountId;
  switch (action.kind) {
    case 'unknown':
      return (
        <Card variant="next-action" eyebrow="Próximo passo" testID="cockpit-next-unknown">
          <Notice
            kind="warning"
            title={`Não foi possível verificar ${action.what}.`}
            body="O próximo passo aparece quando a leitura funcionar. Nada foi considerado vazio."
            actions={<Button label="Tentar novamente" onPress={action.retry} />}
          />
        </Card>
      );
    case 'correction':
      return card(
        `Fechamento de ${businessDate(action.session.date)} precisa de correção`,
        action.session.rejectionReason
          ? `${action.session.accountName}. Observação da gerência: ${action.session.rejectionReason}`
          : `${action.session.accountName}. O fechamento anterior foi corrigido; reconte este.`,
        { label: 'Corrigir fechamento', path: `/fechamentos/${action.session.id}/correcao` },
      );
    case 'not-open':
      if (action.blocked === 'unresolved-history')
        return card(
          'Caixa com histórico pendente',
          'Este caixa tem fechamentos antigos sem conferência da gerência. A gerência precisa resolvê-los antes de um novo turno começar.',
        );
      if (action.blocked === 'no-terminal')
        return card(
          'Caixa sem terminal CAIXA',
          role === 'Member'
            ? 'Este caixa ainda não está associado a um terminal CAIXA. Peça à gerência para associá-lo em Contas.'
            : 'Associe este caixa a um terminal CAIXA em Gestão → Contas antes de iniciar o turno.',
        );
      return card(
        'Dia ainda não aberto',
        `Abra o dia do ${workstation.accountName} para registrar movimentações.`,
        action.canStart
          ? { label: 'Abrir dia', path: `/fechamentos/abrir?accountId=${account}` }
          : undefined,
      );
    case 'receive':
      return card(
        'Receber o caixa',
        `Há uma passagem de caixa para você no ${workstation.accountName}. Confira o que está no caixa antes de aceitar.`,
        { label: 'Conferir e receber o caixa', path: `/fechamentos/abrir?accountId=${account}` },
      );
    case 'held':
      return card(
        `${workstation.accountName} está em uso`,
        `O turno atual deste caixa é de ${action.custodian ?? 'outro operador'}. Você só inicia um turno quando ele encerrar ou passar o caixa para você.`,
      );
    case 'drafts': {
      const others = action.counts.total - action.counts.mine;
      const memberOthersOnly = role === 'Member' && action.counts.mine === 0;
      // The Draft blocker list is the same (account, today) the close is judged on.
      const base = `/transacoes?accountId=${account}&from=${today}&to=${today}&status=Draft`;
      if (memberOthersOnly)
        return card(
          'Pendência do terminal',
          `O fechamento está bloqueado por ${others === 1 ? '1 rascunho de outro operador' : `${others} rascunhos de outros operadores`}. O operador que registrou ou a gerência precisa finalizar ou cancelar.`,
          { label: others === 1 ? 'Ver pendência' : 'Ver pendências', path: base },
        );
      if (role === 'Member' && others > 0)
        return card(
          pluralDrafts(action.counts.total),
          `${action.counts.mine} ${action.counts.mine === 1 ? 'sua' : 'suas'} · ${others} de outro operador. Finalize ou cancele os rascunhos antes de fechar o caixa.`,
          { label: 'Resolver meus rascunhos', path: `${base}&mine=true` },
          { label: 'Ver pendências de outros operadores', path: base },
        );
      return card(
        pluralDrafts(action.counts.total),
        'Finalize ou cancele os rascunhos antes de fechar o caixa.',
        { label: 'Finalizar movimentações', path: base },
      );
    }
    case 'open':
      return card(
        'Dia aberto',
        action.custodyActive
          ? 'Registre as movimentações e feche o caixa no fim do turno.'
          : 'O turno foi encerrado pela gerência, mas a contagem ainda está em rascunho.',
        { label: 'Fechar caixa', path: `/fechamentos/${action.sessionId}` },
        { label: 'Nova transação', path: `/transacoes/nova?accountId=${account}` },
      );
    case 'other-open':
      return card(
        `${action.session.accountName} ainda está aberto`,
        'Você tem outro caixa aberto hoje. Feche-o antes de encerrar o turno.',
        {
          label: `Ir para ${action.session.accountName}`,
          path: `/fechamentos/${action.session.id}`,
        },
      );
    case 'submitted':
      return card(
        'Aguardando aprovação do gerente',
        `${action.session.submittedAt ? `Enviado às ${auditTime(action.session.submittedAt)}.` : 'Enviado.'}${action.custodyActive ? ' O caixa continua sob sua responsabilidade até você encerrar o turno ou passar o caixa.' : ''}`,
        {
          label: action.custodyActive ? 'Encerrar ou passar o caixa' : 'Ver fechamento',
          path: `/fechamentos/${action.session.id}`,
        },
      );
    case 'approved':
      return card(
        'Dia encerrado e aprovado',
        action.custodyActive
          ? 'O fechamento foi aprovado. Encerre o turno ou passe o caixa quando terminar.'
          : 'Nada pendente hoje.',
        action.custodyActive
          ? { label: 'Encerrar ou passar o caixa', path: `/fechamentos/${action.session.id}` }
          : undefined,
        undefined,
        action.custodyActive ? null : (
          <Button
            label="Ver fechamento"
            variant="link"
            role="link"
            onPress={() => go(`/fechamentos/${action.session.id}`)}
          />
        ),
      );
  }
}

function OtherCashboxes({
  query,
  operatorId,
  workstation,
  go,
}: {
  query: {
    status: string;
    data?: readonly import('@loto/api').ResponseListDailyCloseItemJson[];
    error: unknown;
    refetch: () => unknown;
  };
  operatorId: string;
  workstation: string;
  go: (path: string) => void;
}) {
  if (query.status !== 'success') return null;
  const rows = query.data!.filter(
    (row) =>
      row.accountId !== workstation &&
      row.custody.custodianOperatorId === operatorId &&
      (row.custody.custodyState === 'Active' ||
        row.status === 'Draft' ||
        row.status === 'Rejected'),
  );
  if (!rows.length) return null;
  return (
    <Card title="Outros caixas" testID="cockpit-others">
      {rows.map((row) => (
        <View key={row.id} style={{ gap: spacing.stack.xs }}>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.inline.sm }}>
            <AppText>{row.accountName}</AppText>
            <Badge
              kind="status"
              domain="daily-close-status"
              value={row.status}
              context="Situação"
            />
          </View>
          <Button
            label={`Ir para ${row.accountName}`}
            variant="link"
            role="link"
            onPress={() => go(`/fechamentos/${row.id}`)}
          />
        </View>
      ))}
    </Card>
  );
}

function SetupNeeded({
  kind,
  role,
  request,
  onAssignments,
}: {
  kind: 'link' | 'account';
  role: string;
  request: {
    status: string;
    data?: import('@loto/api').ResponseOperatorLinkRequestJson | null;
    error: unknown;
    refetch: () => unknown;
  };
  onAssignments: () => void;
}) {
  const member = role === 'Member';
  return (
    <View style={{ gap: spacing.stack.md }} testID={`cockpit-setup-${kind}`}>
      <EmptyState
        kind="setup-needed"
        statement={
          kind === 'link'
            ? 'Seu login ainda não está vinculado a um operador desta filial.'
            : 'Você ainda não tem um caixa atribuído.'
        }
        explanation={
          kind === 'link'
            ? 'Meu turno depende de um operador ativo ligado ao seu login. Enquanto isso, nenhum caixa pode ser aberto ou fechado por você.'
            : member
              ? 'Seu operador está ativo, mas ainda não recebeu um caixa. A gerência atribui caixas em Contas dos operadores.'
              : 'Atribua um caixa ao seu operador em Gestão → Contas dos operadores para usar Meu turno.'
        }
      />
      {member ? (
        <>
          {request.status === 'success' && request.data ? (
            <Notice
              kind="info"
              testID="cockpit-link-request"
              title={`Solicitação de vínculo: ${operatorLinkRequestStatusLabels[request.data.status]}`}
              body={
                request.data.status === 'Pending'
                  ? `Solicitação enviada em ${auditInstant(request.data.createdAt)}.`
                  : undefined
              }
            />
          ) : request.status === 'error' ? (
            <Notice
              kind="warning"
              title="Não foi possível verificar sua solicitação de vínculo."
              messages={errorMessages(request.error)}
              actions={<Button label="Tentar novamente" onPress={() => void request.refetch()} />}
            />
          ) : null}
          <Button
            label="Solicitar vínculo"
            disabled
            testID="cockpit-request-link"
            disabledReason="O pedido de vínculo pelo aplicativo estará disponível em uma próxima etapa. Enquanto isso, fale com a gerência da filial."
          />
        </>
      ) : (
        <Button label="Ir para Contas dos operadores" onPress={onAssignments} />
      )}
    </View>
  );
}
