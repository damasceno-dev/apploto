import { useEffect, useMemo, useRef, useState } from 'react';
import { View } from 'react-native';
import { useLocalSearchParams } from 'expo-router';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getAccount,
  getOperator,
  getSessionOpeningProposal,
  getSessionSessionId,
  getSessionSessionIdHistory,
  getSessionSessionIdReview,
  postSessionHandoverHandoverIdAccept,
  postSessionHandoverHandoverIdDispute,
  postSessionSessionIdCustodyException,
  postSessionStart,
  toIfMatch,
  type ResponseDailyCloseJson,
  type ResponseDailyCloseReviewJson,
  type ResponseOpeningDiscrepancyJson,
  type ResponseSessionHandoverJson,
  type ResponseSessionOpeningProposalJson,
} from '@loto/api';
import { formatBrlWire, formatSignedBrlWire, sessionOpeningSourceKindLabels } from '@loto/core';
import { useSession, useScopedApiClient } from '@/session/provider';
import { useShell } from '@/shell/provider';
import { spacing } from '@/theme/style';
import {
  AppText,
  Button,
  Card,
  Checkbox,
  FormField,
  Notice,
  Radio,
  RadioGroup,
  Select,
  Skeleton,
  Switch,
  TextField,
  useToast,
} from '@/ui';
import { useAdminDraft, RetainedNotice } from '@/admin/common';
import { CountMoneyField } from './arithmetic';
import {
  calendarPart,
  cashboxGrants,
  handoverAcceptance,
  isOpeningInput,
  openingAcknowledgment,
  proposalSource,
  zeroAcknowledgmentRequired,
  type Digits,
  type OpeningInput,
} from './contracts';
import {
  CommandState,
  auditInstant,
  businessDate,
  errorMessages,
  useDayCommand,
  useDayReads,
} from './common';

const emptyOpening = (accountId: string): OpeningInput => ({
  v: 1,
  accountId,
  different: {},
  explicitZero: false,
  reason: '',
  buffers: {},
});

/**
 * Open day = **Conferir abertura e iniciar** (`POST /session/start`): an explicit new custody
 * acknowledgment of the server's proposal, never a silent zero and never proof that a pending sale
 * ran. When the cashbox already has an active turno it shows that state instead: an offered
 * handover for this operator is acknowledged here, and Gestão resolves an absent custodian only
 * through the reasoned custody exception.
 */
export function OpenDayScreen() {
  const params = useLocalSearchParams<{ accountId?: string }>();
  const { snapshot } = useSession();
  const shell = useShell();
  const client = useScopedApiClient();
  const reads = useDayReads();
  const role = snapshot.branch!.role;
  const management = shell.mode === 'management' && role !== 'Member';
  const grants = useMemo(() => cashboxGrants(snapshot.selfContext), [snapshot.selfContext]);
  const accounts = useQuery({
    queryKey: ['day', snapshot.scope, 'cashboxes'],
    queryFn: async () =>
      (await getAccount({ client })).data.accounts.filter((a) => a.type === 'Terminal'),
    enabled: reads && management,
    retry: false,
  });
  const requested = typeof params.accountId === 'string' ? params.accountId : null;
  const [chosen, setChosen] = useState<string | null>(requested);
  const accountId = management
    ? chosen
    : requested && grants.some((g) => g.accountId === requested)
      ? requested
      : grants.length === 1
        ? grants[0]!.accountId
        : null;
  if (!management && snapshot.selfContext && !snapshot.selfContext.operatorId)
    return (
      <Notice
        kind="danger"
        title="Seu login não está vinculado a um operador desta filial."
        body="Só um operador vinculado inicia o próprio turno."
      />
    );
  if (!management && requested && !grants.some((g) => g.accountId === requested))
    return (
      <Notice
        kind="danger"
        testID="open-not-granted"
        title="Este caixa não está atribuído a você."
        body="Escolha um caixa atribuído em Hoje ou peça à gerência para conferir suas contas."
        actions={<Button label="Voltar" onPress={() => void shell.back()} />}
      />
    );
  return (
    <View style={{ gap: spacing.stack.md }} testID="open-day">
      {management ? (
        <>
          <FormField label="Caixa" required>
            {accounts.status === 'pending' ? (
              <Skeleton label="Carregando caixas…" />
            ) : accounts.status === 'error' ? (
              <Notice
                kind="danger"
                title="Não foi possível carregar os caixas."
                messages={errorMessages(accounts.error)}
                actions={
                  <Button label="Tentar novamente" onPress={() => void accounts.refetch()} />
                }
              />
            ) : (
              <Select
                testID="open-account"
                value={accountId}
                onValueChange={setChosen}
                options={accounts.data.map((a) => ({ value: a.id, label: a.name }))}
              />
            )}
          </FormField>
          <Notice
            kind="info"
            title="Turno passado não é aberto como atual."
            body="Um intervalo já encerrado é declarado pela gerência na revisão de turnos, que estará disponível em uma próxima etapa. Aqui só começa o turno de hoje."
          />
        </>
      ) : grants.length > 1 && !accountId ? (
        <FormField label="Caixa" required>
          <Select
            testID="open-account"
            value={null}
            onValueChange={(value) =>
              void shell.replacePreservingOrigin(`/fechamentos/abrir?accountId=${value}`)
            }
            options={grants.map((g) => ({ value: g.accountId, label: g.accountName }))}
          />
        </FormField>
      ) : null}
      {accountId ? (
        <Proposal key={accountId} accountId={accountId} management={management} />
      ) : null}
    </View>
  );
}

interface Receipt {
  readonly title: string;
  readonly discrepancy: ResponseOpeningDiscrepancyJson | null;
  readonly sessionId: string | null;
}
/** A confirmed result stays on screen even when the follow-up reads move the cashbox's state. */
function ReceiptCard({ receipt }: { receipt: Receipt }) {
  const shell = useShell();
  return (
    <Card title="Resultado" testID="open-receipt">
      <Notice kind="success" testID="open-started" title={receipt.title} />
      <Discrepancy discrepancy={receipt.discrepancy} />
      {receipt.sessionId ? (
        <Button
          label="Ir para o fechamento"
          testID="open-go-close"
          onPress={() => void shell.replacePreservingOrigin(`/fechamentos/${receipt.sessionId}`)}
        />
      ) : null}
      <Button
        label="Voltar"
        variant="secondary"
        testID="open-done"
        onPress={() => void shell.back()}
      />
    </Card>
  );
}

function Proposal({ accountId, management }: { accountId: string; management: boolean }) {
  const [receipt, setReceipt] = useState<Receipt | null>(null);
  const { snapshot } = useSession();
  const client = useScopedApiClient();
  const reads = useDayReads();
  const proposal = useQuery({
    queryKey: ['day', snapshot.scope, 'proposal', accountId],
    queryFn: async () => (await getSessionOpeningProposal({ accountId }, { client })).data,
    enabled: reads,
    retry: false,
    staleTime: 0,
  });
  const activeId = proposal.data?.activeSessionId ?? null;
  const active = useQuery({
    queryKey: ['day', snapshot.scope, 'active', activeId],
    queryFn: async () => {
      const [session, history] = await Promise.all([
        getSessionSessionId(activeId!, { client }),
        getSessionSessionIdHistory(activeId!, { client }),
      ]);
      return { session: session.data, handovers: history.data.handovers };
    },
    enabled: reads && !!activeId,
    retry: false,
    staleTime: 0,
  });
  if (receipt) return <ReceiptCard receipt={receipt} />;
  if (proposal.status === 'pending') return <Skeleton label="Carregando a abertura proposta…" />;
  if (proposal.status === 'error')
    return (
      <Notice
        kind="danger"
        testID="open-proposal-error"
        title="Não foi possível carregar a abertura proposta."
        body="Sem esta leitura nada pode ser iniciado; ela não significa que o caixa está livre."
        messages={errorMessages(proposal.error)}
        actions={<Button label="Tentar novamente" onPress={() => void proposal.refetch()} />}
      />
    );
  const data = proposal.data;
  const refetch = async () => {
    await proposal.refetch();
    if (activeId) await active.refetch();
  };
  if (activeId) {
    if (active.status === 'pending') return <Skeleton label="Carregando o turno atual…" />;
    if (active.status === 'error')
      return (
        <Notice
          kind="danger"
          title="Não foi possível carregar o turno atual deste caixa."
          messages={errorMessages(active.error)}
          actions={<Button label="Tentar novamente" onPress={() => void active.refetch()} />}
        />
      );
    return (
      <ActiveCustody
        proposal={data}
        session={active.data.session}
        handovers={active.data.handovers}
        management={management}
        refetch={refetch}
        onReceipt={setReceipt}
      />
    );
  }
  if (data.hasUnresolvedHistory)
    return (
      <Notice
        kind="warning"
        testID="open-unresolved"
        title="Este caixa tem histórico sem conferência."
        body={
          management
            ? 'Resolva os fechamentos antigos pendentes deste caixa na fila de trabalho antes de iniciar um novo turno.'
            : 'A gerência precisa resolver fechamentos antigos deste caixa antes de um novo turno começar.'
        }
      />
    );
  if (!data.terminalId)
    return (
      <Notice
        kind="warning"
        testID="open-no-terminal"
        title="Este caixa não está associado a um terminal CAIXA."
        body="Associe o caixa a um terminal em Gestão → Contas antes de iniciar o turno."
      />
    );
  return (
    <StartForm proposal={data} management={management} refetch={refetch} onReceipt={setReceipt} />
  );
}

function ProposalRows({
  rows,
  input,
  different,
  change,
  locked,
}: {
  rows: readonly { productId: string; productName: string; value: number | null }[];
  input: OpeningInput;
  different: boolean;
  change: (update: (previous: OpeningInput) => OpeningInput) => void;
  locked: boolean;
}) {
  return (
    <View style={{ gap: spacing.stack.sm }}>
      {rows.map((row) => (
        <View
          key={row.productId}
          style={{ gap: spacing.stack.xs }}
          testID={`open-row-${row.productId}`}
        >
          <AppText variant="body-strong">{row.productName || 'Produto sem nome ativo'}</AppText>
          <AppText>
            {`Proposto: ${row.value === null ? 'não informado' : formatBrlWire(row.value)}`}
          </AppText>
          {different ? (
            <CountMoneyField
              label={`Observado — ${row.productName}`}
              testID={`open-observed-${row.productId}`}
              value={input.different[row.productId] ?? ''}
              disabled={locked}
              onValueChange={(digits: Digits) =>
                change((prev) => ({
                  ...prev,
                  different: { ...prev.different, [row.productId]: digits },
                }))
              }
              buffer={input.buffers[`item:${row.productId}`]}
              onBufferChange={(raw) =>
                change((prev) => {
                  const buffers = { ...prev.buffers };
                  if (raw === undefined) delete buffers[`item:${row.productId}`];
                  else buffers[`item:${row.productId}`] = raw;
                  return { ...prev, buffers };
                })
              }
            />
          ) : null}
        </View>
      ))}
    </View>
  );
}

function Discrepancy({
  discrepancy,
}: {
  discrepancy: ResponseOpeningDiscrepancyJson | null | undefined;
}) {
  if (!discrepancy) return null;
  return (
    <Notice
      kind="info"
      testID="open-discrepancy"
      title={`Abertura registrada com diferença de ${formatSignedBrlWire(discrepancy.currentTotalDelta)} em relação ao proposto.`}
      body="A diferença fica registrada para a gerência, sem atribuição a ninguém."
    />
  );
}

function useOpeningDraft(
  accountId: string,
  proposal: ResponseSessionOpeningProposalJson,
  resource: string,
) {
  const { engine } = useSession();
  return useAdminDraft(resource, isOpeningInput, async () => {
    const fresh = await getSessionOpeningProposal({ accountId }, { client: engine.client() });
    return fresh.data.accountId === proposal.accountId;
  });
}

function StartForm({
  proposal,
  management,
  refetch,
  onReceipt,
}: {
  proposal: ResponseSessionOpeningProposalJson;
  management: boolean;
  refetch: () => Promise<void>;
  onReceipt: (receipt: Receipt) => void;
}) {
  const { snapshot, engine } = useSession();
  const toast = useToast();
  const queries = useQueryClient();
  const client = useScopedApiClient();
  const reads = useDayReads();
  const own = snapshot.selfContext?.operatorId ?? null;
  const operators = useQuery({
    queryKey: ['day', snapshot.scope, 'operators'],
    queryFn: async () => (await getOperator({ client })).data.operators,
    enabled: reads && management,
    retry: false,
  });
  const [custodian, setCustodian] = useState<string | null>(management ? null : own);
  const [input, setInput] = useState<OpeningInput>(() => emptyOpening(proposal.accountId));
  const [different, setDifferent] = useState(false);
  const [base] = useState(() => proposalSource(proposal));
  const [started, setStarted] = useState<ResponseOpeningDiscrepancyJson | null | false>(false);
  const command = useDayCommand();
  const draft = useOpeningDraft(proposal.accountId, proposal, `day/open/${proposal.accountId}`);
  const latest = useRef(input);
  const change = (update: OpeningInput | ((previous: OpeningInput) => OpeningInput)) => {
    const next = typeof update === 'function' ? update(latest.current) : update;
    latest.current = next;
    setInput(next);
    void draft.keep(next, base);
  };
  const sourceChanged = JSON.stringify(proposalSource(proposal)) !== JSON.stringify(base);
  const onBehalf = management && custodian !== null && custodian !== own;
  const zero = zeroAcknowledgmentRequired(proposal, input);
  const body = {
    accountId: proposal.accountId,
    opening: openingAcknowledgment(proposal, input, management ? custodian : null),
  };
  const fingerprint = JSON.stringify(body);
  const incomplete =
    (management && !custodian) ||
    (onBehalf && input.reason.trim() === '') ||
    (zero && !input.explicitZero) ||
    Object.keys(input.buffers).length > 0;
  const locked = command.pending || started !== false;
  const submit = () =>
    void command.run(
      fingerprint,
      (key) => postSessionStart(body, { 'Idempotency-Key': key }, { client: engine.client() }),
      async (result) => {
        setStarted(result.data.openingDiscrepancy ?? null);
        onReceipt({
          title: 'Turno iniciado. A abertura foi registrada.',
          discrepancy: result.data.openingDiscrepancy ?? null,
          sessionId: result.data.session.id,
        });
        toast.show('Turno iniciado.');
        await draft.discard();
        await queries.invalidateQueries({ queryKey: ['day', snapshot.scope] });
      },
    );
  const rows = proposal.items.map((item) => ({
    productId: item.productId,
    productName: item.productName,
    value: item.value ?? null,
  }));
  return (
    <Card title={`Abrir dia para ${proposal.accountName}`} testID="open-start">
      <RetainedNotice
        visible={!!draft.retained}
        onRestore={() => {
          const value = draft.retained!.input;
          change(value);
          setDifferent(Object.keys(value.different).length > 0);
          draft.dismissRetained();
        }}
      />
      {draft.problem ? <Notice kind="warning" title={draft.problem} /> : null}
      {sourceChanged ? (
        <Notice
          kind="warning"
          testID="open-source-changed"
          title="A abertura proposta mudou desde que você começou."
          body="Confira os valores novos. O que você digitou continua na tela e será conferido de novo."
        />
      ) : null}
      {management ? (
        <FormField
          label="Responsável pelo turno"
          required
          helper="Iniciar para outro operador é uma exceção registrada com motivo; o operador precisa ter este caixa atribuído."
        >
          {operators.status === 'success' ? (
            <Select
              testID="open-custodian"
              value={custodian}
              onValueChange={setCustodian}
              options={operators.data.map((o) => ({
                value: o.id,
                label: o.id === own ? `${o.name} (você)` : o.name,
              }))}
            />
          ) : operators.status === 'error' ? (
            <Notice
              kind="danger"
              title="Não foi possível carregar os operadores."
              messages={errorMessages(operators.error)}
              actions={<Button label="Tentar novamente" onPress={() => void operators.refetch()} />}
            />
          ) : (
            <Skeleton label="Carregando operadores…" />
          )}
        </FormField>
      ) : (
        <AppText testID="open-custodian-self">{`Responsável pelo turno: ${snapshot.selfContext?.operatorName ?? 'você'}`}</AppText>
      )}
      <AppText testID="open-source">
        {proposal.isMissing
          ? 'Não há abertura registrada para este caixa.'
          : `Abertura proposta: ${sessionOpeningSourceKindLabels[proposal.sourceKind!]}${proposal.sourceDate ? ` de ${businessDate(proposal.sourceDate)}` : ''}`}
      </AppText>
      {proposal.isMissing ? (
        <AppText variant="helper">
          Sem abertura registrada, o turno só começa com uma abertura zero confirmada por você.
        </AppText>
      ) : (
        <>
          <ProposalRows
            rows={rows}
            input={input}
            different={different}
            change={change}
            locked={locked}
          />
          <Switch
            label="Abertura diferente"
            description="Informe só os produtos que você conferiu com valor diferente do proposto."
            testID="open-different"
            value={different}
            disabled={locked}
            onValueChange={(next) => {
              setDifferent(next);
              if (!next) change({ ...input, different: {}, buffers: {} });
            }}
          />
        </>
      )}
      {zero ? (
        <Checkbox
          testID="open-explicit-zero"
          label="Confirmo que o caixa começa com R$ 0,00"
          checked={input.explicitZero}
          disabled={locked}
          onCheckedChange={(explicitZero) => change({ ...input, explicitZero })}
        />
      ) : null}
      <FormField
        label="Motivo"
        required={onBehalf}
        optional={!onBehalf}
        helper={onBehalf ? 'Obrigatório quando você inicia o turno de outro operador.' : undefined}
      >
        <TextField
          kind="textarea"
          testID="open-reason"
          value={input.reason}
          maxLength={500}
          disabled={locked}
          onChangeText={(reason) => change({ ...input, reason })}
        />
      </FormField>
      {Object.keys(input.buffers).length ? (
        <AppText variant="helper">Use ou descarte as contas abertas antes de iniciar.</AppText>
      ) : null}
      <CommandState
        command={command}
        receipt="Turno iniciado no servidor."
        testID="open-command"
        reread={() =>
          void refetch()
            .then(() => command.clear())
            .catch(() => undefined)
        }
      />
      <Button
        label="Conferir abertura e iniciar"
        variant={management ? 'primary' : 'cta'}
        testID="open-submit"
        loading={command.pending}
        loadingLabel="Iniciando…"
        disabled={command.blocked || incomplete || started !== false}
        disabledReason={incomplete ? 'Complete os dados da abertura antes de iniciar.' : undefined}
        onPress={submit}
        fullWidth
      />
    </Card>
  );
}

function ActiveCustody({
  proposal,
  session,
  handovers,
  management,
  refetch,
  onReceipt,
}: {
  proposal: ResponseSessionOpeningProposalJson;
  session: ResponseDailyCloseJson;
  handovers: readonly ResponseSessionHandoverJson[];
  management: boolean;
  refetch: () => Promise<void>;
  onReceipt: (receipt: Receipt) => void;
}) {
  const { snapshot } = useSession();
  const shell = useShell();
  const own = snapshot.selfContext?.operatorId ?? null;
  const today = snapshot.clock!.branchLocalDate;
  const pending =
    handovers.find(
      (h) =>
        h.id === proposal.pendingHandoverId && (h.state === 'Offered' || h.state === 'Disputed'),
    ) ?? null;
  const mine = session.custody.custodianOperatorId === own;
  return (
    <View style={{ gap: spacing.stack.md }}>
      <Notice
        kind="info"
        testID="open-active"
        title={
          mine ? 'Você já tem um turno neste caixa.' : 'Este caixa já tem um turno em andamento.'
        }
        body={`Responsável: ${session.custody.custodianOperatorName ?? 'operador não identificado'} · desde ${auditInstant(session.custody.startedAt) ?? '—'}${calendarPart(session.date) < today ? ` · turno de ${businessDate(session.date)} ainda ativo` : ''}`}
        actions={
          mine || management ? (
            <Button
              label="Ir para o fechamento"
              variant="secondary"
              onPress={() => void shell.navigate(`/fechamentos/${session.id}`, true)}
            />
          ) : undefined
        }
      />
      {pending && pending.intendedIncomingOperatorId === own && !management ? (
        <HandoverReceipt
          handover={pending}
          session={session}
          proposal={proposal}
          refetch={refetch}
          onReceipt={onReceipt}
        />
      ) : null}
      {pending && pending.intendedIncomingOperatorId !== own ? (
        <AppText testID="open-pending-other">{`Passagem oferecida para ${pending.intendedIncomingOperatorName}.`}</AppText>
      ) : null}
      {management ? (
        <CustodyException
          session={session}
          pending={pending}
          proposal={proposal}
          refetch={refetch}
          onReceipt={onReceipt}
        />
      ) : null}
    </View>
  );
}

const productNames = (proposal: ResponseSessionOpeningProposalJson) =>
  new Map(proposal.items.map((item) => [item.productId, item.productName]));

/** Incoming acknowledgment: the immutable offered vector, observed differences only, or a dispute. */
function HandoverReceipt({
  handover,
  session,
  proposal,
  refetch,
  onReceipt,
}: {
  handover: ResponseSessionHandoverJson;
  session: ResponseDailyCloseJson;
  proposal: ResponseSessionOpeningProposalJson;
  refetch: () => Promise<void>;
  onReceipt: (receipt: Receipt) => void;
}) {
  const { engine, snapshot } = useSession();
  const queries = useQueryClient();
  const toast = useToast();
  const shell = useShell();
  const names = productNames(proposal);
  const [input, setInput] = useState<OpeningInput>(() => emptyOpening(proposal.accountId));
  const [different, setDifferent] = useState(false);
  const [disputing, setDisputing] = useState(false);
  const [done, setDone] = useState<null | {
    discrepancy: ResponseOpeningDiscrepancyJson | null;
    disputed: boolean;
  }>(null);
  const accept = useDayCommand();
  const dispute = useDayCommand();
  const draft = useOpeningDraft(proposal.accountId, proposal, `day/handover/${handover.id}`);
  const latest = useRef(input);
  const change = (update: OpeningInput | ((previous: OpeningInput) => OpeningInput)) => {
    const next = typeof update === 'function' ? update(latest.current) : update;
    latest.current = next;
    setInput(next);
    void draft.keep(next, { handoverId: handover.id, offered: handover.offeredCountRevision });
  };
  const body = handoverAcceptance(handover, input);
  const zero = body.items.every((i) => Number(i.value) === 0);
  const locked = accept.pending || dispute.pending || done !== null;
  const rows = handover.items.map((item) => ({
    productId: item.productId,
    productName: names.get(item.productId) ?? '',
    value: item.value,
  }));
  return (
    <Card title="Receber o caixa" testID="handover-receive">
      <RetainedNotice
        visible={!!draft.retained}
        onRestore={() => {
          change(draft.retained!.input);
          setDifferent(Object.keys(draft.retained!.input.different).length > 0);
          draft.dismissRetained();
        }}
      />
      <AppText>{`${session.custody.custodianOperatorName ?? 'O operador anterior'} ofereceu o caixa em ${auditInstant(handover.offeredAt)}.`}</AppText>
      {handover.state === 'Disputed' ? (
        <Notice
          kind="warning"
          title="Você contestou esta passagem."
          body={handover.reason ?? undefined}
        />
      ) : null}
      <AppText variant="helper">
        O caixa continua com quem ofereceu até você aceitar. Uma diferença na passagem fica para a
        gerência, sem atribuição automática.
      </AppText>
      {done ? (
        <>
          <Notice
            kind="success"
            testID="handover-done"
            title={
              done.disputed
                ? 'Contestação registrada. O caixa continua com quem ofereceu.'
                : 'Caixa recebido. Seu turno começou.'
            }
          />
          <Discrepancy discrepancy={done.discrepancy} />
          <Button label="Voltar" onPress={() => void shell.back()} />
        </>
      ) : null}
      <ProposalRows
        rows={rows}
        input={input}
        different={different}
        change={change}
        locked={locked}
      />
      <Switch
        label="Abertura diferente"
        description="Informe só os produtos que você conferiu com valor diferente do oferecido."
        testID="handover-different"
        value={different}
        disabled={locked}
        onValueChange={(next) => {
          setDifferent(next);
          if (!next) change({ ...input, different: {}, buffers: {} });
        }}
      />
      {zero ? (
        <Checkbox
          testID="handover-explicit-zero"
          label="Confirmo que recebo o caixa com R$ 0,00"
          checked={input.explicitZero}
          disabled={locked}
          onCheckedChange={(explicitZero) => change({ ...input, explicitZero })}
        />
      ) : null}
      <FormField
        label="Motivo"
        optional={!disputing}
        required={disputing}
        helper={disputing ? 'Obrigatório para contestar.' : undefined}
      >
        <TextField
          kind="textarea"
          testID="handover-reason"
          value={input.reason}
          maxLength={500}
          disabled={locked}
          onChangeText={(reason) => change({ ...input, reason })}
        />
      </FormField>
      <CommandState
        command={accept}
        receipt="Caixa recebido no servidor."
        testID="handover-accept"
        reread={() =>
          void refetch()
            .then(() => accept.clear())
            .catch(() => undefined)
        }
      />
      <CommandState
        command={dispute}
        receipt="Contestação registrada no servidor."
        testID="handover-dispute"
        reread={() =>
          void refetch()
            .then(() => dispute.clear())
            .catch(() => undefined)
        }
      />
      <Button
        label="Conferir abertura e receber o caixa"
        variant="cta"
        testID="handover-accept-submit"
        loading={accept.pending}
        loadingLabel="Recebendo…"
        disabled={
          accept.blocked ||
          dispute.blocked ||
          locked ||
          (zero && !input.explicitZero) ||
          Object.keys(input.buffers).length > 0
        }
        onPress={() =>
          void accept.run(
            JSON.stringify([handover.id, handover.version, body]),
            (key) =>
              postSessionHandoverHandoverIdAccept(
                handover.id,
                body,
                { 'Idempotency-Key': key, 'If-Match': toIfMatch(handover.version) },
                { client: engine.client() },
              ),
            async (result) => {
              setDone({
                discrepancy: result.data.incoming.openingDiscrepancy ?? null,
                disputed: false,
              });
              onReceipt({
                title: 'Caixa recebido. Seu turno começou.',
                discrepancy: result.data.incoming.openingDiscrepancy ?? null,
                sessionId: result.data.incoming.session.id,
              });
              toast.show('Caixa recebido.');
              await draft.discard();
              await queries.invalidateQueries({ queryKey: ['day', snapshot.scope] });
            },
          )
        }
        fullWidth
      />
      {handover.state === 'Offered' ? (
        <Button
          label={disputing ? 'Enviar contestação' : 'Contestar a passagem'}
          variant="secondary"
          testID="handover-dispute-submit"
          loading={dispute.pending}
          loadingLabel="Registrando…"
          disabled={
            accept.blocked || dispute.blocked || locked || (disputing && input.reason.trim() === '')
          }
          onPress={() => {
            if (!disputing) return setDisputing(true);
            void dispute.run(
              JSON.stringify(['dispute', handover.id, handover.version, input.reason]),
              () =>
                postSessionHandoverHandoverIdDispute(
                  handover.id,
                  { reason: input.reason.trim() },
                  { 'If-Match': toIfMatch(handover.version) },
                  { client: engine.client() },
                ),
              async () => {
                setDone({ discrepancy: null, disputed: true });
                onReceipt({
                  title: 'Contestação registrada. O caixa continua com quem ofereceu.',
                  discrepancy: null,
                  sessionId: null,
                });
                await queries.invalidateQueries({ queryKey: ['day', snapshot.scope] });
              },
            );
          }}
          fullWidth
        />
      ) : null}
    </Card>
  );
}

/**
 * Gestão: an absent or stalled custodian is resolved only by the reasoned custody exception —
 * Finish (F1 included: a Draft turno left active past midnight stays a review blocker) or
 * AcceptAndStart for a named incoming operator with an observed vector. It is recorded as a manager
 * exception, never as the operator's own acknowledgment.
 */
function CustodyException({
  session,
  pending,
  proposal,
  refetch,
  onReceipt,
}: {
  session: ResponseDailyCloseJson;
  pending: ResponseSessionHandoverJson | null;
  proposal: ResponseSessionOpeningProposalJson;
  refetch: () => Promise<void>;
  onReceipt: (receipt: Receipt) => void;
}) {
  const { engine, snapshot } = useSession();
  const client = useScopedApiClient();
  const reads = useDayReads();
  const queries = useQueryClient();
  const toast = useToast();
  const [action, setAction] = useState<'Finish' | 'AcceptAndStart' | null>(null);
  const [incoming, setIncoming] = useState<string | null>(
    pending?.intendedIncomingOperatorId ?? null,
  );
  const [input, setInput] = useState<OpeningInput>(() => emptyOpening(proposal.accountId));
  const [different, setDifferent] = useState(false);
  const [done, setDone] = useState<null | { discrepancy: ResponseOpeningDiscrepancyJson | null }>(
    null,
  );
  const command = useDayCommand();
  const operators = useQuery({
    queryKey: ['day', snapshot.scope, 'operators'],
    queryFn: async () => (await getOperator({ client })).data.operators,
    enabled: reads,
    retry: false,
  });
  // Without a pending offer, AcceptAndStart starts from the outgoing turno's own saved count
  // (the next proposal's source); the server re-checks it under the cashbox coordination.
  const outgoing = useQuery({
    queryKey: ['day', snapshot.scope, 'review', session.id],
    queryFn: async () => (await getSessionSessionIdReview(session.id, { client })).data,
    enabled: reads && action === 'AcceptAndStart' && !pending,
    retry: false,
  });
  const vector = useMemo(() => offeredVector(pending, outgoing.data), [pending, outgoing.data]);
  const names = productNames(proposal);
  const accepted = vector ? handoverAcceptance({ items: vector }, input) : null;
  const zero = !!accepted && accepted.items.every((i) => Number(i.value) === 0);
  const firstRender = useRef(true);
  useEffect(() => {
    if (firstRender.current) {
      firstRender.current = false;
      return;
    }
    setInput((current) => ({ ...current, different: {}, buffers: {}, explicitZero: false }));
    setDifferent(false);
  }, [action]);
  const reason = input.reason.trim();
  const body =
    action === 'Finish'
      ? { action, reason }
      : action === 'AcceptAndStart' && accepted
        ? {
            action,
            reason,
            incomingOperatorId: incoming,
            items: accepted.items,
            explicitZero: accepted.explicitZero,
          }
        : null;
  const ready =
    !!body &&
    reason !== '' &&
    (action === 'Finish' ||
      (!!incoming && (!zero || input.explicitZero) && Object.keys(input.buffers).length === 0));
  return (
    <Card title="Exceção de custódia" testID="custody-exception">
      <AppText variant="helper">
        Use só quando o responsável não pode encerrar ou passar o caixa. A ação fica registrada com
        o seu nome e o motivo, como exceção da gerência.
      </AppText>
      {done ? (
        <>
          <Notice
            kind="success"
            testID="custody-exception-done"
            title="Exceção registrada no servidor."
          />
          <Discrepancy discrepancy={done.discrepancy} />
        </>
      ) : null}
      <RadioGroup
        testID="custody-exception-action"
        value={action}
        onValueChange={(value) => setAction(value as 'Finish' | 'AcceptAndStart')}
        disabled={command.pending || !!done}
        accessibilityLabel="Ação da exceção"
      >
        <Radio
          value="Finish"
          label="Encerrar o turno"
          description={
            session.status === 'Draft'
              ? 'O fechamento em rascunho continua pendente de revisão.'
              : undefined
          }
          testID="custody-exception-finish"
        />
        <Radio
          value="AcceptAndStart"
          label="Encerrar e iniciar o turno do próximo operador"
          disabled={!pending && session.status !== 'Submitted'}
          description={
            !pending && session.status !== 'Submitted'
              ? 'Disponível depois que o fechamento deste turno for enviado.'
              : undefined
          }
          testID="custody-exception-accept"
        />
      </RadioGroup>
      {action === 'AcceptAndStart' ? (
        <>
          <FormField label="Próximo operador" required>
            {pending ? (
              <AppText testID="custody-exception-incoming-fixed">
                {pending.intendedIncomingOperatorName}
              </AppText>
            ) : operators.status === 'success' ? (
              <Select
                testID="custody-exception-incoming"
                value={incoming}
                onValueChange={setIncoming}
                options={operators.data
                  .filter((o) => o.id !== session.custody.custodianOperatorId)
                  .map((o) => ({ value: o.id, label: o.name }))}
              />
            ) : (
              <Skeleton label="Carregando operadores…" />
            )}
          </FormField>
          {vector ? (
            <>
              <ProposalRows
                rows={vector.map((v) => ({
                  productId: v.productId,
                  productName: names.get(v.productId) ?? '',
                  value: v.value,
                }))}
                input={input}
                different={different}
                change={setInput}
                locked={command.pending || !!done}
              />
              <Switch
                label="Abertura diferente"
                testID="custody-exception-different"
                value={different}
                disabled={command.pending || !!done}
                onValueChange={(next) => {
                  setDifferent(next);
                  if (!next) setInput({ ...input, different: {}, buffers: {} });
                }}
              />
              {zero ? (
                <Checkbox
                  label="Confirmo a abertura de R$ 0,00"
                  testID="custody-exception-zero"
                  checked={input.explicitZero}
                  onCheckedChange={(explicitZero) => setInput({ ...input, explicitZero })}
                />
              ) : null}
            </>
          ) : outgoing.status === 'error' ? (
            <Notice
              kind="danger"
              title="Não foi possível carregar a contagem do turno."
              messages={errorMessages(outgoing.error)}
              actions={<Button label="Tentar novamente" onPress={() => void outgoing.refetch()} />}
            />
          ) : (
            <Skeleton label="Carregando a contagem do turno…" />
          )}
        </>
      ) : null}
      <FormField label="Motivo" required>
        <TextField
          kind="textarea"
          testID="custody-exception-reason"
          value={input.reason}
          maxLength={500}
          onChangeText={(value) => setInput({ ...input, reason: value })}
        />
      </FormField>
      <CommandState
        command={command}
        receipt="Exceção registrada no servidor."
        testID="custody-exception-command"
        reread={() =>
          void refetch()
            .then(() => command.clear())
            .catch(() => undefined)
        }
      />
      <Button
        label="Registrar exceção"
        variant="primary"
        testID="custody-exception-submit"
        loading={command.pending}
        loadingLabel="Registrando…"
        disabled={!ready || command.blocked || !!done}
        onPress={() => {
          if (!body) return;
          void command.run(
            JSON.stringify([session.id, session.version, body]),
            (key) =>
              postSessionSessionIdCustodyException(
                session.id,
                body,
                { 'Idempotency-Key': key, 'If-Match': toIfMatch(session.version) },
                { client: engine.client() },
              ),
            async (result) => {
              setDone({ discrepancy: result.data.incoming?.openingDiscrepancy ?? null });
              onReceipt({
                title: 'Exceção registrada no servidor.',
                discrepancy: result.data.incoming?.openingDiscrepancy ?? null,
                sessionId: result.data.incoming?.session.id ?? result.data.outgoingSession.id,
              });
              toast.show('Exceção registrada.');
              await queries.invalidateQueries({ queryKey: ['day', snapshot.scope] });
            },
          );
        }}
        fullWidth
      />
    </Card>
  );
}
function offeredVector(
  pending: ResponseSessionHandoverJson | null,
  review: ResponseDailyCloseReviewJson | undefined,
) {
  if (pending) return pending.items;
  if (!review) return null;
  return review.items
    .filter((row) => !row.isCashVarianceProduct && row.closingValue != null)
    .map((row) => ({ productId: row.productId, value: row.closingValue! }));
}
