import { useState } from 'react';
import { useLocalSearchParams } from 'expo-router';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getReceivableTransactionId,
  getTransactionTransactionId,
  parseETag,
  postTransactionTransactionIdCancel,
  postTransactionTransactionIdEditPreview,
  postTransactionTransactionIdFinalize,
  putTransactionTransactionId,
  toIfMatch,
  type ResponseTransactionJson,
} from '@loto/api';
import {
  agingBucketLabels,
  classificationProvenanceLabels,
  dailyCloseStatusLabels,
  directionLabels,
  financialClassLabels,
  formatBrlWire,
  formatDate,
  formatInstant,
  monthlyFinancialRoleLabels,
  parseCalendarDate,
  settlementInstrumentLabels,
  transactionGenerationKindLabels,
  type CalendarDate,
} from '@loto/core';
import { useSession, useScopedApiClient } from '@/session/provider';
import { useShell } from '@/shell/provider';
import {
  AppText,
  Badge,
  Button,
  Card,
  ConfirmDialog,
  DateField,
  DetailList,
  FormField,
  Notice,
  Skeleton,
  TextField,
  TimeField,
  useToast,
} from '@/ui';
import { RetainedNotice, useAdminDraft } from '@/admin/common';
import { useTimeCommand } from '@/time/common';
import { deviceZone } from '@/day/contracts';
import {
  elevated,
  errorMessages,
  useAccounts,
  useClients,
  useLedgerReads,
  usePreview,
  useTypes,
} from './common';
import {
  day,
  editGate,
  editInput,
  editRequest,
  impactLines,
  isEditInput,
  sameEdit,
  type EditInput,
} from './contracts';
import { ClientField } from './fields';
import { ChequeCard } from './cheque';

const businessDay = (value: string | null | undefined) => {
  const date = value ? parseCalendarDate(day(value)!) : null;
  return date ? formatDate(date) : '—';
};
const isConflict = (error: unknown) =>
  !!error &&
  typeof error === 'object' &&
  'status' in error &&
  (error as { status: number }).status === 409;
interface Loaded {
  readonly row: ResponseTransactionJson;
  readonly etag: number;
}

/**
 * Transaction — Edit (correction / audit): the current resource with its frozen classification,
 * history and the contract's narrow editable fields. Value, account, type and date are read-only;
 * changing them is an explicit cancel followed by a separate new transaction. The loaded strong
 * ETag lives in memory only; a stale write keeps the edit and asks for an explicit reread.
 */
export function TransactionDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { snapshot, engine } = useSession();
  const client = useScopedApiClient();
  const reads = useLedgerReads();
  const queries = useQueryClient();
  const resource = useQuery({
    queryKey: ['ledger', snapshot.scope, 'transaction', id],
    queryFn: async ({ signal }): Promise<Loaded> => {
      const response = await getTransactionTransactionId(id, { client, signal });
      const etag = parseETag(response.headers.get('etag'));
      if (etag === null || etag !== response.data.version)
        throw new Error('A versão da transação não pôde ser verificada.');
      return { row: response.data, etag };
    },
    enabled: reads && typeof id === 'string',
    retry: false,
    staleTime: 0,
  });
  if (resource.status === 'pending') return <Skeleton label="Carregando a transação…" />;
  // Only a transaction never read shows the error screen. A failed reread keeps the screen (and
  // any confirmed receipt it holds) mounted, with every command blocked until a reread succeeds.
  if (resource.status === 'error' && !resource.data)
    return (
      <Notice
        kind="danger"
        testID="detail-error"
        title="Não foi possível abrir a transação."
        messages={errorMessages(resource.error)}
        actions={<Button label="Tentar novamente" onPress={() => void resource.refetch()} />}
      />
    );
  return (
    <Detail
      key={resource.data.row.id}
      loaded={resource.data}
      readFailure={resource.isError ? resource.error : null}
      retryRead={() => void resource.refetch()}
      reread={async () => {
        const fresh = await resource.refetch();
        if (fresh.error) throw fresh.error;
        void queries.invalidateQueries({
          queryKey: ['ledger', engine.getSnapshot().scope, 'list'],
        });
        return fresh.data!;
      }}
    />
  );
}

function Detail({
  loaded,
  reread,
  readFailure,
  retryRead,
}: {
  loaded: Loaded;
  reread: () => Promise<Loaded>;
  /** The latest read of this transaction failed: nothing may be sent until a reread succeeds. */
  readFailure: unknown;
  retryRead: () => void;
}) {
  const { snapshot, engine } = useSession();
  const shell = useShell();
  const toast = useToast();
  const queries = useQueryClient();
  const client = useScopedApiClient();
  const role = snapshot.branch!.role;
  const manage = elevated(role);
  const own = snapshot.selfContext?.operatorId ?? null;
  const today = snapshot.clock!.branchLocalDate;
  const row = loaded.row;
  const types = useTypes();
  const accounts = useAccounts();
  const clients = useClients();
  const type = (types.data ?? []).find((t) => t.id === row.transactionTypeId) ?? null;
  const gate = editGate(row, { role, operatorId: own, today });
  /** The baseline the edit was typed against; replaced only by an explicit reread/adoption. */
  const [base, setBase] = useState<Loaded>(loaded);
  const [input, setInputState] = useState<EditInput>(() => editInput(loaded.row));
  const draft = useAdminDraft(`ledger/edit/${row.id}`, isEditInput, async () => {
    const fresh = await getTransactionTransactionId(row.id, { client: engine.client() });
    return fresh.data.id === row.id;
  });
  const setInput = (update: (prev: EditInput) => EditInput) => {
    setInputState((prev) => {
      const next = update(prev);
      void draft.keep(next, null);
      return next;
    });
  };
  const save = useTimeCommand();
  const lifecycle = useTimeCommand();
  const [stale, setStale] = useState<Loaded | null>(null);
  const [receipt, setReceipt] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState<'cancel' | 'recreate' | null>(null);
  const [reason, setReason] = useState('');
  const [rereadError, setRereadError] = useState<unknown>(null);
  const baseline = editInput(base.row);
  const dirty = !sameEdit(input, baseline);
  // Manager/Admin can read the receivable chain: a command event (custody, deposit, return,
  // conversion) fixes the client. Members cannot read it; their refusal comes verbatim from the server.
  const receivable = useQuery({
    queryKey: ['ledger', 'receivable', row.id, snapshot.scope],
    queryFn: async ({ signal }) =>
      (await getReceivableTransactionId(row.id, undefined, { client, signal })).data,
    enabled:
      manage &&
      row.status === 'Active' &&
      !row.generationKind &&
      !!row.classification &&
      row.classification.settlementInstrument !== 'None',
    retry: false,
    staleTime: 0,
  });
  const clientLocked = !!receivable.data?.events.some((event) => event.provenance === 'Command');
  const request = editRequest(input);
  const signature =
    manage && gate.editable && dirty && input.dueDate
      ? JSON.stringify([row.id, base.etag, request])
      : null;
  const preview = usePreview('edit', signature, (signal) =>
    postTransactionTransactionIdEditPreview(row.id, request, undefined, { client, signal }).then(
      (r) => r.data,
    ),
  );
  // A conflict refusal keeps the buffer; nothing else is sent until an explicit reread compares versions.
  const writesBlocked =
    !!readFailure ||
    save.blocked ||
    lifecycle.blocked ||
    !!stale ||
    isConflict(save.error) ||
    isConflict(lifecycle.error);
  const refresh = async () => {
    setRereadError(null);
    try {
      const fresh = await reread();
      if (!dirty) {
        setBase(fresh);
        setInputState(editInput(fresh.row));
      } else if (fresh.etag !== base.etag) setStale(fresh);
      return fresh;
    } catch (error) {
      setRereadError(error);
      return null;
    }
  };
  const afterWrite = async (message: string) => {
    setReceipt(message);
    toast.show(message);
    void queries.invalidateQueries({ queryKey: ['day', snapshot.scope] });
    const fresh = await reread();
    setBase(fresh);
    setInputState(editInput(fresh.row));
    await draft.discard();
  };
  /**
   * Explicit reconciliation after a conflict or an unknown outcome: the server version decides.
   * Unchanged version → nothing was applied (the person may send again); a new version is shown
   * for comparison and adopted only by an explicit choice — never merged automatically.
   */
  const reconcile = async () => {
    setRereadError(null);
    try {
      const fresh = await reread();
      if (fresh.etag === base.etag) {
        save.clear();
        lifecycle.clear();
        return;
      }
      if (fresh.row.status === base.row.status && sameEdit(editInput(fresh.row), input)) {
        setBase(fresh);
        save.clear();
        lifecycle.clear();
        setReceipt('A correção consta no servidor.');
        await draft.discard();
        return;
      }
      setStale(fresh);
    } catch (error) {
      setRereadError(error);
    }
  };
  const plan = row.originTransactionId;
  return (
    <>
      {readFailure ? (
        <Notice
          kind="warning"
          testID="detail-read-failed"
          title="Não foi possível reler esta transação. Os dados abaixo podem estar desatualizados."
          body="Nada será enviado até a releitura funcionar."
          messages={errorMessages(readFailure)}
          actions={<Button label="Reler a transação" onPress={retryRead} />}
        />
      ) : null}
      <RetainedNotice
        visible={!!draft.retained && gate.editable}
        onRestore={() => {
          const value = draft.retained!.input;
          draft.dismissRetained();
          setInputState(value);
        }}
      />
      {receipt ? (
        <Notice
          kind="success"
          testID="detail-receipt"
          title={receipt}
          onDismiss={() => setReceipt(null)}
        />
      ) : null}
      <Card title={type?.name ?? row.transactionTypeId} testID="detail-summary">
        <AppText variant="section-title" testID="detail-value">
          {`${directionLabels[row.direction]} · ${formatBrlWire(row.value)}`}
        </AppText>
        <Badge kind="status" domain="transaction-status" value={row.status} context="Situação:" />
        {row.generationKind ? <Badge kind="automatic" /> : null}
        <DetailList
          rows={[
            { label: 'Data', value: businessDay(row.date) },
            {
              label: 'Conta',
              value:
                (accounts.data ?? []).find((a) => a.id === row.accountId)?.name ??
                'Conta fora das suas contas',
            },
            ...(row.clientId
              ? [
                  {
                    label: 'Cliente',
                    value:
                      (clients.data ?? []).find((c) => c.id === row.clientId)?.name ?? 'Cliente',
                  },
                ]
              : []),
            { label: 'Vencimento', value: businessDay(row.dueDate) },
            ...(row.transactionTime
              ? [{ label: 'Hora', value: row.transactionTime.slice(0, 5) }]
              : []),
            ...(row.description ? [{ label: 'Descrição', value: row.description }] : []),
            {
              label: 'Registrada por',
              value: row.recordedByOperatorId === own ? 'Seu operador' : 'Outro operador',
            },
            { label: 'Criada em', value: formatInstant(row.createdAt, deviceZone()) },
            ...(row.updatedAt
              ? [{ label: 'Alterada em', value: formatInstant(row.updatedAt, deviceZone()) }]
              : []),
            ...(row.cancelledAt
              ? [
                  { label: 'Cancelada em', value: formatInstant(row.cancelledAt, deviceZone()) },
                  { label: 'Motivo do cancelamento', value: row.cancellationReason ?? '—' },
                ]
              : []),
          ]}
        />
        {row.classification ? (
          <DetailList
            testID="detail-classification"
            rows={[
              {
                label: 'Classe financeira (congelada no lançamento)',
                value: financialClassLabels[row.classification.financialClass],
              },
              {
                label: 'Instrumento',
                value: settlementInstrumentLabels[row.classification.settlementInstrument],
              },
              {
                label: 'Papel mensal',
                value: monthlyFinancialRoleLabels[row.classification.monthlyFinancialRole],
              },
              {
                label: 'Origem da classificação',
                value: classificationProvenanceLabels[row.classification.provenance],
              },
            ]}
          />
        ) : (
          <AppText variant="helper">
            Sem classificação: ela é congelada quando a transação é finalizada.
          </AppText>
        )}
      </Card>
      {row.generationKind ? (
        <Notice
          kind="info"
          testID="detail-generated"
          title={`Gerada automaticamente: ${transactionGenerationKindLabels[row.generationKind]}.`}
          body="Correções e cancelamento são feitos na operação de origem, que leva esta movimentação junto."
          actions={
            row.generatedFromTransactionId ? (
              <Button
                label="Abrir operação de origem"
                variant="link"
                role="link"
                testID="detail-open-source"
                onPress={() =>
                  void shell.navigate(`/transacoes/${row.generatedFromTransactionId}`, true)
                }
              />
            ) : undefined
          }
        />
      ) : null}
      {row.generatedTransactionId ? (
        <Button
          label="Abrir movimentação automática ligada"
          variant="link"
          role="link"
          testID="detail-open-generated"
          onPress={() => void shell.navigate(`/transacoes/${row.generatedTransactionId}`, true)}
        />
      ) : null}
      {row.sessionId ? (
        <Button
          label="Abrir o fechamento do turno"
          variant="link"
          role="link"
          testID="detail-open-session"
          onPress={() => void shell.navigate(`/fechamentos/${row.sessionId}`, true)}
        />
      ) : null}
      {plan ? (
        <Button
          label="Abrir plano completo"
          variant="link"
          role="link"
          testID="detail-open-plan"
          onPress={() =>
            void shell.navigate(`/transacoes/cheque-pre-datado/${plan}?focus=${row.id}`, true)
          }
        />
      ) : null}
      {row.clientId && type?.requiresTabAccountAndClient ? (
        <Button
          label="Ver extrato do cliente"
          variant="link"
          role="link"
          onPress={() => void shell.navigate(`/clientes/${row.clientId}/extrato`, true)}
        />
      ) : null}
      {stale ? (
        <Notice
          kind="warning"
          testID="detail-stale"
          title="Esta transação mudou no servidor desde que você abriu."
          body={`Versão atual: ${formatBrlWire(stale.row.value)}, ${businessDay(stale.row.dueDate)}, ${stale.row.description ?? 'sem descrição'}. Sua edição continua na tela; nada foi salvo.`}
          actions={
            <>
              <Button
                label="Aplicar minha edição sobre a versão atual"
                testID="detail-stale-adopt"
                onPress={() => {
                  setBase(stale);
                  setStale(null);
                  save.clear();
                  lifecycle.clear();
                }}
              />
              <Button
                label="Descartar minha edição"
                variant="secondary"
                onPress={() => {
                  setBase(stale);
                  setInputState(editInput(stale.row));
                  setStale(null);
                  save.clear();
                  lifecycle.clear();
                  void draft.discard();
                }}
              />
            </>
          }
        />
      ) : null}
      {rereadError ? (
        <Notice
          kind="warning"
          title="Não foi possível reler a transação."
          messages={errorMessages(rereadError)}
          actions={<Button label="Reler" onPress={() => void refresh()} />}
        />
      ) : null}
      {gate.editable ? (
        <Card title="Correção" testID="detail-edit">
          <AppText variant="helper">
            Valor, conta, tipo e data não mudam aqui. Para corrigi-los, cancele e registre outra
            transação.
          </AppText>
          <FormField label="Vencimento" required>
            <DateField
              testID="detail-due"
              today={parseCalendarDate(today) ?? undefined}
              value={input.dueDate ? (parseCalendarDate(input.dueDate) as CalendarDate) : null}
              onValueChange={(dueDate) => setInput((p) => ({ ...p, dueDate }))}
            />
          </FormField>
          <FormField label="Hora" optional>
            <TimeField
              testID="detail-time"
              value={input.time}
              onValueChange={(time) => setInput((p) => ({ ...p, time }))}
            />
          </FormField>
          <ClientField
            testID="detail-client"
            clients={clients.data}
            status={clients.status}
            retry={() => void clients.refetch()}
            required={!!type?.requiresTabAccountAndClient}
            value={input.clientId}
            onChange={(clientId) => setInput((p) => ({ ...p, clientId }))}
            locked={clientLocked}
            lockedReason="O cliente fica fixo depois de custódia, depósito, devolução ou conversão do recebível."
          />
          <FormField label="Descrição" optional>
            <TextField
              kind="textarea"
              testID="detail-description"
              value={input.description}
              maxLength={500}
              onChangeText={(description) => setInput((p) => ({ ...p, description }))}
            />
          </FormField>
          {manage && dirty ? <EditPreview preview={preview} /> : null}
          <ErrorState command={save} label="A correção" reconcile={() => void reconcile()} />
          <Button
            label="Salvar correção"
            testID="detail-save"
            loading={save.pending}
            disabled={!dirty || !input.dueDate || writesBlocked}
            disabledReason={!dirty ? 'Nada mudou.' : undefined}
            onPress={() =>
              void save.run(
                () =>
                  putTransactionTransactionId(
                    row.id,
                    request,
                    { 'If-Match': toIfMatch(base.etag) },
                    { client: engine.client() },
                  ),
                () => afterWrite('Correção salva no servidor.'),
              )
            }
          />
        </Card>
      ) : (
        <Notice kind="info" testID="detail-readonly" title={gate.reason ?? 'Somente leitura.'} />
      )}
      {!row.generationKind && row.status !== 'Cancelled' && gate.editable ? (
        <Card title="Situação" testID="detail-lifecycle">
          {cancelling === null ? (
            <ErrorState command={lifecycle} label="A ação" reconcile={() => void reconcile()} />
          ) : null}
          {row.status === 'Draft' && plan ? (
            <AppText variant="helper">
              Parcelas de um plano em rascunho são finalizadas juntas, no plano.
            </AppText>
          ) : row.status === 'Draft' ? (
            <Button
              label="Finalizar rascunho"
              testID="detail-finalize"
              loading={lifecycle.pending}
              disabled={writesBlocked || dirty}
              disabledReason={
                dirty ? 'Salve ou descarte a correção antes de finalizar.' : undefined
              }
              onPress={() =>
                void lifecycle.run(
                  () =>
                    postTransactionTransactionIdFinalize(
                      row.id,
                      { 'If-Match': toIfMatch(base.etag) },
                      { client: engine.client() },
                    ),
                  () => afterWrite('Rascunho finalizado no servidor.'),
                )
              }
            />
          ) : null}
          <Button
            label={plan ? 'Cancelar só esta parcela' : 'Cancelar transação'}
            variant="secondary"
            testID="detail-cancel"
            disabled={writesBlocked}
            onPress={() => setCancelling('cancel')}
          />
          {!plan ? (
            <Button
              label="Corrigir valor, conta, tipo ou data"
              variant="link"
              testID="detail-recreate"
              disabled={writesBlocked}
              onPress={() => setCancelling('recreate')}
            />
          ) : null}
        </Card>
      ) : null}
      {manage &&
      row.status === 'Active' &&
      !row.generationKind &&
      (row.classification?.settlementInstrument === 'Cheque' || plan) ? (
        <ChequeCard
          transactionId={row.id}
          blocked={!!readFailure}
          onChanged={() => void refresh()}
        />
      ) : null}
      <ConfirmDialog
        visible={cancelling !== null}
        testID="detail-cancel-dialog"
        title={
          cancelling === 'recreate'
            ? 'Cancelar e registrar outra transação?'
            : 'Cancelar esta transação?'
        }
        consequence={
          cancelling === 'recreate'
            ? 'São duas operações separadas: primeiro o cancelamento desta; depois você confere e salva a nova. Se a nova não for salva, esta continua cancelada.'
            : `A transação deixa de contar no caixa e nos relatórios${row.generatedTransactionId ? ', junto com a movimentação automática ligada' : ''}. O cancelamento fica registrado com o motivo.`
        }
        confirmLabel={cancelling === 'recreate' ? 'Cancelar e continuar' : 'Cancelar transação'}
        busyLabel="Cancelando…"
        destructive
        busy={lifecycle.pending}
        onDismiss={() => setCancelling(null)}
        confirmDisabled={writesBlocked && !lifecycle.pending}
        confirmDisabledReason={
          readFailure
            ? 'A transação não pôde ser relida: nada é enviado até a releitura funcionar.'
            : 'Resolva o aviso da transação antes de cancelar.'
        }
        onConfirm={() => {
          // Checked at confirmation time too: a dialog opened before a read failure never sends.
          if (!reason.trim() || writesBlocked) return;
          const next = cancelling;
          void lifecycle.run(
            () =>
              postTransactionTransactionIdCancel(
                row.id,
                { cancellationReason: reason.trim() },
                { 'If-Match': toIfMatch(base.etag) },
                { client: engine.client() },
              ),
            async () => {
              setCancelling(null);
              setReason('');
              await afterWrite('Transação cancelada no servidor.');
              if (next === 'recreate')
                void shell.navigate(`/transacoes/nova?copyOf=${row.id}`, true);
            },
          );
        }}
      >
        <FormField label="Motivo do cancelamento" required>
          <TextField
            kind="textarea"
            testID="detail-cancel-reason"
            value={reason}
            maxLength={500}
            onChangeText={setReason}
          />
        </FormField>
        <ErrorState command={lifecycle} label="O cancelamento" reconcile={() => void reconcile()} />
      </ConfirmDialog>
    </>
  );
}

function EditPreview({
  preview,
}: {
  preview: ReturnType<
    typeof usePreview<{ impact: Parameters<typeof impactLines>[0]; warnings: string[] }>
  >;
}) {
  if (preview.pending) return <Skeleton label="Calculando o impacto da correção…" />;
  if (preview.error)
    return (
      <Notice
        kind="info"
        blocking={false}
        title="Prévia do impacto indisponível. Você ainda pode salvar."
        messages={errorMessages(preview.error)}
      />
    );
  if (!preview.data) return null;
  return (
    <Card title="Impacto da correção (nada foi salvo)" testID="detail-preview">
      {impactLines(preview.data.impact, agingBucketLabels, dailyCloseStatusLabels).map((line) => (
        <AppText key={line.key}>{line.text}</AppText>
      ))}
      {preview.data.warnings.map((w) => (
        <Notice key={w} kind="warning" blocking={false} title={w} />
      ))}
    </Card>
  );
}

function ErrorState({
  command,
  label,
  reconcile,
}: {
  command: ReturnType<typeof useTimeCommand>;
  label: string;
  reconcile: () => void;
}) {
  const action = <Button label="Reler a transação" testID="detail-reconcile" onPress={reconcile} />;
  if (command.postWriteError)
    return (
      <Notice
        kind="warning"
        title={`${label} foi confirmada, mas a releitura falhou.`}
        body="Nada será enviado de novo. Releia a transação para continuar."
        messages={errorMessages(command.postWriteError)}
        actions={action}
      />
    );
  if (command.uncertain)
    return (
      <Notice
        kind="warning"
        testID="detail-uncertain"
        title="Não foi possível confirmar o resultado."
        body="Nada será reenviado. Releia a transação para ver o que o servidor registrou."
        messages={errorMessages(command.error)}
        actions={action}
      />
    );
  if (command.error)
    return (
      <Notice
        kind="danger"
        testID="detail-refused"
        title="O servidor não aceitou."
        body={
          isConflict(command.error)
            ? 'O que você digitou continua na tela. Releia a transação antes de enviar de novo.'
            : 'O que você digitou continua na tela.'
        }
        messages={errorMessages(command.error)}
        actions={isConflict(command.error) ? action : undefined}
      />
    );
  return null;
}
