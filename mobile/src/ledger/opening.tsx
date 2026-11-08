import { useState, type ReactNode } from 'react';
import { View } from 'react-native';
import { useQuery } from '@tanstack/react-query';
import {
  getSession,
  getSessionOpeningProposal,
  type ResponseSessionOpeningProposalJson,
} from '@loto/api';
import { formatBrlWire, sessionOpeningSourceKindLabels } from '@loto/core';
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
  Select,
  Skeleton,
  Switch,
  TextField,
} from '@/ui';
import { CountMoneyField } from '@/day/arithmetic';
import { businessDate } from '@/day/common';
import {
  openingAcknowledgment,
  proposalSource,
  zeroAcknowledgmentRequired,
  type Digits,
  type OpeningInput,
} from '@/day/contracts';
import { elevated, errorMessages, useLedgerReads, useOperators } from './common';
import type { SessionBinding } from './contracts';

const emptyOpening = (accountId: string): OpeningInput => ({
  v: 1,
  accountId,
  different: {},
  explicitZero: false,
  reason: '',
  buffers: {},
});
type Status = 'none' | 'loading' | 'error' | 'blocked' | 'incomplete' | 'ready';
export interface BindingState {
  readonly status: Status;
  /** The binding to send, composed from the current reads and the person's explicit choices. */
  readonly binding: SessionBinding;
  readonly view: ReactNode;
  /**
   * Fresh authority right before dispatch: re-read the proposal (or session list) and refuse —
   * keeping everything typed — when custody, the proposal source or the session changed.
   */
  readonly prepare: () => Promise<SessionBinding | null>;
}
/**
 * How a cashbox write binds to its turno. Today with active custody: nothing to send. Today with
 * no turno: the atomic inline opening (`OpeningAcknowledgment` on the same request, never a
 * separate Start) after the person explicitly confirms the opening. An earlier day: Manager/Admin
 * name one Draft session with a reason; Members cannot. Nothing here starts a session by itself.
 */
export function useSessionBinding({
  cashboxId,
  cashboxName,
  date,
  testID,
}: {
  cashboxId: string | null;
  cashboxName: string;
  date: string | null;
  testID: string;
}): BindingState {
  const { snapshot } = useSession();
  const shell = useShell();
  const client = useScopedApiClient();
  const reads = useLedgerReads();
  const today = snapshot.clock?.branchLocalDate ?? null;
  const manage = elevated(snapshot.branch?.role);
  const own = snapshot.selfContext?.operatorId ?? null;
  const isToday = !!date && date === today;
  const proposal = useQuery({
    queryKey: ['ledger', snapshot.scope, 'proposal', cashboxId],
    queryFn: async ({ signal }) =>
      (await getSessionOpeningProposal({ accountId: cashboxId! }, { client, signal })).data,
    enabled: reads && !!cashboxId && isToday,
    retry: false,
    staleTime: 0,
  });
  const historical = useQuery({
    queryKey: ['ledger', snapshot.scope, 'historical-sessions', cashboxId, date],
    queryFn: async ({ signal }) =>
      (
        await getSession(
          {
            AccountId: cashboxId!,
            DateFrom: date!,
            DateTo: date!,
            Status: 'Draft',
            Page: 1,
            PageSize: 50,
          },
          { client, signal },
        )
      ).data.items,
    enabled: reads && !!cashboxId && !!date && !isToday && manage,
    retry: false,
    staleTime: 0,
  });
  const operators = useOperators();
  const [input, setInput] = useState<OpeningInput>(() => emptyOpening(cashboxId ?? ''));
  const [different, setDifferent] = useState(false);
  const [custodian, setCustodian] = useState<string | null>(own);
  /** The confirmation is bound to the exact proposal source and vector the person saw. */
  const [confirmedFor, setConfirmedFor] = useState<string | null>(null);
  const [historicalId, setHistoricalId] = useState<string | null>(null);
  const [reason, setReason] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  if (input.accountId !== (cashboxId ?? '')) {
    // A different cashbox is a different opening: never carry an acknowledgment across.
    setInput(emptyOpening(cashboxId ?? ''));
    setConfirmedFor(null);
    setDifferent(false);
  }
  const none: BindingState = {
    status: 'none',
    binding: { kind: 'none' },
    view: null,
    prepare: async () => ({ kind: 'none' }),
  };
  if (!cashboxId || !date) return none;
  if (!isToday) {
    if (!manage)
      return {
        ...none,
        status: 'blocked',
        view: (
          <Notice
            kind="info"
            testID={`${testID}-historical-member`}
            title="Lançamentos em dias anteriores no caixa são feitos pela gerência."
            body="Escolha a data de hoje ou peça a um gerente."
          />
        ),
      };
    const sessions = historical.data ?? [];
    const chosen = sessions.find((s) => s.id === historicalId) ?? null;
    const ready = !!chosen && reason.trim().length > 0;
    const binding: SessionBinding = ready
      ? { kind: 'historical', sessionId: chosen.id, reason }
      : { kind: 'none' };
    return {
      status:
        historical.status === 'pending'
          ? 'loading'
          : historical.status === 'error'
            ? 'error'
            : ready
              ? 'ready'
              : 'incomplete',
      binding,
      prepare: async () => {
        const fresh = await historical.refetch();
        if (!fresh.data?.some((s) => s.id === historicalId)) {
          setProblem('O turno escolhido não está mais em rascunho. Escolha de novo.');
          setHistoricalId(null);
          return null;
        }
        setProblem(null);
        return binding;
      },
      view: (
        <Card title={`Lançamento retroativo em ${cashboxName}`} testID={`${testID}-historical`}>
          <AppText>
            Em um dia anterior, a movimentação entra num turno em rascunho desse dia, escolhido por
            você, com o motivo registrado.
          </AppText>
          {historical.status === 'pending' ? (
            <Skeleton label="Carregando turnos do dia…" />
          ) : historical.status === 'error' ? (
            <Notice
              kind="danger"
              title="Não foi possível carregar os turnos desse dia."
              messages={errorMessages(historical.error)}
              actions={
                <Button label="Tentar novamente" onPress={() => void historical.refetch()} />
              }
            />
          ) : sessions.length === 0 ? (
            <Notice
              kind="warning"
              title="Não há turno em rascunho neste caixa nesse dia."
              body="Sem um turno aberto do dia, nada pode ser lançado nele."
            />
          ) : (
            <FormField label="Turno do dia" required>
              <Select
                testID={`${testID}-historical-session`}
                value={historicalId}
                onValueChange={setHistoricalId}
                options={sessions.map((s) => ({
                  value: s.id,
                  label: `${s.custody.custodianOperatorName ?? 'Turno sem responsável'}${s.custody.sequence ? ` · turno ${s.custody.sequence}` : ''} · ${businessDate(s.date)}`,
                }))}
              />
            </FormField>
          )}
          <FormField label="Motivo do lançamento retroativo" required>
            <TextField
              kind="textarea"
              testID={`${testID}-historical-reason`}
              value={reason}
              maxLength={500}
              onChangeText={setReason}
            />
          </FormField>
          {problem ? (
            <Notice kind="warning" testID={`${testID}-opening-problem`} title={problem} />
          ) : null}
        </Card>
      ),
    };
  }
  if (proposal.status === 'pending')
    return {
      ...none,
      status: 'loading',
      view: <Skeleton label={`Conferindo o turno de ${cashboxName}…`} />,
    };
  if (proposal.status === 'error')
    return {
      ...none,
      status: 'error',
      view: (
        <Notice
          kind="danger"
          testID={`${testID}-proposal-error`}
          title={`Não foi possível conferir o turno de ${cashboxName}.`}
          body="Sem esta leitura nada é enviado; ela não significa que o caixa está livre."
          messages={errorMessages(proposal.error)}
          actions={<Button label="Tentar novamente" onPress={() => void proposal.refetch()} />}
        />
      ),
    };
  const data = proposal.data;
  const prepareActive = async (): Promise<SessionBinding | null> => {
    const fresh = await proposal.refetch();
    if (!fresh.data) return null;
    if (!fresh.data.activeSessionId) {
      setProblem(`O turno de ${cashboxName} foi encerrado. Confira a abertura antes de continuar.`);
      return null;
    }
    setProblem(null);
    return { kind: 'none' };
  };
  if (data.activeSessionId)
    return {
      status: 'none',
      binding: { kind: 'none' },
      prepare: prepareActive,
      view: (
        <>
          <AppText testID={`${testID}-active`} variant="helper">
            {`Entra no turno ativo de ${cashboxName}.`}
          </AppText>
          {data.pendingHandoverId ? (
            <Notice
              kind="info"
              title={`Há uma passagem de caixa pendente em ${cashboxName}.`}
              body="Se ela é para você, aceite-a em Abrir dia antes de registrar movimentações."
              actions={
                <Button
                  label="Ir para Abrir dia"
                  variant="link"
                  role="link"
                  onPress={() =>
                    void shell.navigate(`/fechamentos/abrir?accountId=${cashboxId}`, true)
                  }
                />
              }
            />
          ) : null}
          {problem ? (
            <Notice kind="warning" testID={`${testID}-opening-problem`} title={problem} />
          ) : null}
        </>
      ),
    };
  if (data.hasUnresolvedHistory || !data.terminalId || !data.canStart)
    return {
      ...none,
      status: 'blocked',
      view: (
        <Notice
          kind="warning"
          testID={`${testID}-cannot-open`}
          title={`${cashboxName} não tem turno ativo e não pode ser aberto agora.`}
          body={
            data.hasUnresolvedHistory
              ? 'Há fechamentos antigos sem conferência neste caixa; a gerência precisa resolvê-los.'
              : !data.terminalId
                ? 'O caixa não está associado a um terminal CAIXA.'
                : 'Seu acesso não permite iniciar o turno deste caixa.'
          }
        />
      ),
    };
  return openingState({
    data,
    cashboxName,
    testID,
    manage,
    own,
    operators,
    input,
    setInput,
    different,
    setDifferent,
    custodian,
    setCustodian,
    confirmedFor,
    setConfirmedFor,
    problem,
    setProblem,
    refetch: () => proposal.refetch(),
  });
}

function openingState(props: {
  data: ResponseSessionOpeningProposalJson;
  cashboxName: string;
  testID: string;
  manage: boolean;
  own: string | null;
  operators: ReturnType<typeof useOperators>;
  input: OpeningInput;
  setInput: (update: (prev: OpeningInput) => OpeningInput) => void;
  different: boolean;
  setDifferent: (v: boolean) => void;
  custodian: string | null;
  setCustodian: (v: string | null) => void;
  confirmedFor: string | null;
  setConfirmedFor: (v: string | null) => void;
  problem: string | null;
  setProblem: (v: string | null) => void;
  refetch: () => Promise<{ data?: ResponseSessionOpeningProposalJson }>;
}): BindingState {
  const { data, input, manage, own, custodian, testID } = props;
  const zero = zeroAcknowledgmentRequired(data, input);
  const onBehalf = manage && custodian !== null && custodian !== own;
  const acknowledgment = openingAcknowledgment(data, input, manage ? custodian : null);
  const seen = JSON.stringify([proposalSource(data), acknowledgment]);
  const incomplete =
    (manage && !custodian) ||
    (onBehalf && input.reason.trim() === '') ||
    (zero && !input.explicitZero) ||
    Object.keys(input.buffers).length > 0;
  const confirmed = props.confirmedFor === seen;
  const ready = !incomplete && confirmed;
  const binding: SessionBinding = ready ? { kind: 'opening', acknowledgment } : { kind: 'none' };
  const prepare = async (): Promise<SessionBinding | null> => {
    const fresh = await props.refetch();
    const next = fresh.data;
    if (!next) return null;
    if (next.activeSessionId) {
      props.setConfirmedFor(null);
      props.setProblem(
        `Outro turno começou em ${props.cashboxName} enquanto você digitava. Nada foi enviado; confira e envie de novo.`,
      );
      return null;
    }
    if (JSON.stringify(proposalSource(next)) !== JSON.stringify(proposalSource(data))) {
      props.setConfirmedFor(null);
      props.setProblem('A abertura proposta mudou. Confira os valores novos e confirme de novo.');
      return null;
    }
    props.setProblem(null);
    return binding;
  };
  const rows = data.items;
  return {
    status: ready ? 'ready' : 'incomplete',
    binding,
    prepare,
    view: (
      <Card
        title={`Abrir o dia de ${props.cashboxName} com esta operação`}
        testID={`${testID}-opening`}
      >
        <AppText>
          {`${props.cashboxName} não tem turno ativo hoje. Para registrar, confira a abertura: o turno começa junto com esta operação, no mesmo envio — ou nenhum dos dois acontece.`}
        </AppText>
        {manage ? (
          <FormField
            label="Responsável pelo turno"
            required
            helper="Abrir para outro operador é uma exceção registrada com motivo."
          >
            {props.operators.status === 'success' ? (
              <Select
                testID={`${testID}-opening-custodian`}
                value={custodian}
                onValueChange={(v) => {
                  props.setCustodian(v);
                  props.setConfirmedFor(null);
                }}
                options={props.operators.data.map((o) => ({
                  value: o.id,
                  label: o.id === own ? `${o.name} (você)` : o.name,
                }))}
              />
            ) : props.operators.status === 'error' ? (
              <Notice
                kind="danger"
                title="Não foi possível carregar os operadores."
                messages={errorMessages(props.operators.error)}
              />
            ) : (
              <Skeleton label="Carregando operadores…" />
            )}
          </FormField>
        ) : null}
        <AppText testID={`${testID}-opening-source`}>
          {data.isMissing
            ? 'Não há abertura registrada para este caixa.'
            : `Abertura proposta: ${sessionOpeningSourceKindLabels[data.sourceKind!]}${data.sourceDate ? ` de ${businessDate(data.sourceDate)}` : ''}`}
        </AppText>
        {!data.isMissing ? (
          <View style={{ gap: spacing.stack.sm }}>
            {rows.map((row) => (
              <View key={row.productId} style={{ gap: spacing.stack.xs }}>
                <AppText variant="body-strong">
                  {row.productName || 'Produto sem nome ativo'}
                </AppText>
                <AppText>{`Proposto: ${row.value == null ? 'não informado' : formatBrlWire(row.value)}`}</AppText>
                {props.different ? (
                  <CountMoneyField
                    label={`Observado — ${row.productName}`}
                    testID={`${testID}-opening-observed-${row.productId}`}
                    value={input.different[row.productId] ?? ''}
                    onValueChange={(digits: Digits) =>
                      props.setInput((prev) => ({
                        ...prev,
                        different: { ...prev.different, [row.productId]: digits },
                      }))
                    }
                    buffer={input.buffers[`item:${row.productId}`]}
                    onBufferChange={(raw) =>
                      props.setInput((prev) => {
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
            <Switch
              label="Abertura diferente"
              description="Informe só os produtos que você conferiu com valor diferente do proposto."
              testID={`${testID}-opening-different`}
              value={props.different}
              onValueChange={(next) => {
                props.setDifferent(next);
                if (!next) props.setInput((prev) => ({ ...prev, different: {}, buffers: {} }));
              }}
            />
          </View>
        ) : null}
        {zero ? (
          <Checkbox
            testID={`${testID}-opening-zero`}
            label="Confirmo que o caixa começa com R$ 0,00"
            checked={input.explicitZero}
            onCheckedChange={(explicitZero) =>
              props.setInput((prev) => ({ ...prev, explicitZero }))
            }
          />
        ) : null}
        <FormField label="Motivo" required={onBehalf} optional={!onBehalf}>
          <TextField
            kind="textarea"
            testID={`${testID}-opening-reason`}
            value={input.reason}
            maxLength={500}
            onChangeText={(reason) => props.setInput((prev) => ({ ...prev, reason }))}
          />
        </FormField>
        <Checkbox
          testID={`${testID}-opening-confirm`}
          label="Conferi a abertura acima: iniciar o turno com esta operação"
          checked={confirmed}
          disabled={incomplete}
          onCheckedChange={(checked) => props.setConfirmedFor(checked ? seen : null)}
        />
        {/* Always present (same height): toggling a box never shifts the form under the finger. */}
        <AppText variant="helper" testID={`${testID}-opening-hint`}>
          {incomplete
            ? 'Complete a abertura (responsável, valores e confirmações) antes de confirmar.'
            : 'Abertura completa: confirme que conferiu para iniciar o turno com esta operação.'}
        </AppText>
        {props.problem ? (
          <Notice kind="warning" testID={`${testID}-opening-problem`} title={props.problem} />
        ) : null}
      </Card>
    ),
  };
}
