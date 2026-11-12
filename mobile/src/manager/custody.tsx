import { useState } from 'react';
import { View } from 'react-native';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getSessionOpeningDiscrepancyDiscrepancyIdResponsibility,
  postSessionOpeningDiscrepancyAdjustment,
  postSessionOpeningDiscrepancyDiscrepancyIdAttribution,
  toIfMatch,
  type OpeningDiscrepancyAttribution,
  type ResponseManagerQueueCustodyJson,
  type ResponseOpeningDiscrepancyJson,
  type ResponseOpeningDiscrepancyAdjustmentJson,
} from '@loto/api';
import {
  formatSignedBrlWire,
  openingDiscrepancyAttributionLabels,
  parseCalendarDate,
  sessionHandoverStateLabels,
} from '@loto/core';
import { useSession, useScopedApiClient } from '@/session/provider';
import {
  AppText,
  Button,
  Card,
  DateField,
  FormField,
  Notice,
  Radio,
  RadioGroup,
  Skeleton,
  TextField,
} from '@/ui';
import {
  CommandState,
  OnceState,
  day,
  errorMessages,
  managerKey,
  money,
  plural,
  useDecisionCommand,
  useManagerReads,
  useOnceCommand,
} from './common';
import {
  attributionOptions,
  discrepancyReviewPath,
  sessionPath,
  type AttributionTarget,
} from './contracts';

/** Queue custody & responsibility region: only what the single queue read returned. */
export function CustodyPanel({
  custody,
  date,
  open,
  onChanged,
}: {
  custody: ResponseManagerQueueCustodyJson;
  date: string;
  open: (path: string, row?: string) => void;
  onChanged: () => void;
}) {
  const before = custody.openSessions.filter((s) => s.startedBeforeToday);
  const current = custody.openSessions.filter((s) => !s.startedBeforeToday);
  return (
    <Card title="Custódia e responsabilidade" testID="queue-custody">
      <AppText variant="body-strong">{`Sessões abertas (${current.length})`}</AppText>
      {current.map((s) => (
        <View key={s.sessionId} testID={`custody-open-${s.sessionId}`}>
          <AppText>{`${s.accountName}${s.sequence ? ` · sessão ${s.sequence}` : ''} · ${s.custodianOperatorName ?? 'sem responsável'}`}</AppText>
          <Button
            label="Gerenciar caixa"
            role="link"
            variant="secondary"
            accessibilityLabel={`Gerenciar caixa — ${s.accountName}`}
            onPress={() => open(sessionPath(s.sessionId), s.sessionId)}
          />
        </View>
      ))}
      <AppText variant="body-strong" testID="custody-before-title">
        {`Iniciadas antes de hoje (${before.length})`}
      </AppText>
      {before.map((s) => (
        <View key={s.sessionId} testID={`custody-before-${s.sessionId}`}>
          <AppText>{`${s.accountName} · ${day(s.date)} · ${s.custodianOperatorName ?? 'sem responsável'}`}</AppText>
          <Button
            label="Gerenciar caixa"
            role="link"
            variant="secondary"
            accessibilityLabel={`Gerenciar caixa — ${s.accountName} ${day(s.date)}`}
            onPress={() => open(sessionPath(s.sessionId), s.sessionId)}
          />
        </View>
      ))}
      <AppText variant="body-strong">{`Ofertas pendentes (${custody.pendingHandovers.length})`}</AppText>
      {custody.pendingHandovers.map((h) => (
        <View key={h.id} testID={`custody-offer-${h.id}`}>
          <AppText>{`Para ${h.intendedIncomingOperatorName} · ${sessionHandoverStateLabels[h.state]}`}</AppText>
          <Button
            label="Abrir passagem do caixa"
            role="link"
            variant="secondary"
            onPress={() => open(sessionPath(h.outgoingSessionId), h.id)}
          />
        </View>
      ))}
      <AppText variant="body-strong">
        {`Divergências de abertura (${custody.openingDiscrepanciesAwaitingAttribution.length})`}
      </AppText>
      {custody.openingDiscrepanciesAwaitingAttribution.map((d) => (
        <View key={d.openingDiscrepancyId} testID={`custody-discrepancy-${d.openingDiscrepancyId}`}>
          <AppText>{`${d.accountName} · ${day(d.date)} · ${d.custodianOperatorName ?? 'sem responsável'}`}</AppText>
          <AppText variant="helper">{`Diferença informada: ${formatSignedBrlWire(d.currentTotalDelta)} · ${openingDiscrepancyAttributionLabels[d.attribution]}`}</AppText>
          <Button
            label="Triar divergência"
            role="link"
            variant="secondary"
            accessibilityLabel={`Triar divergência — ${d.accountName} ${day(d.date)}`}
            testID={`custody-triage-${d.openingDiscrepancyId}`}
            onPress={() =>
              open(
                discrepancyReviewPath(d.sessionId, d.openingDiscrepancyId),
                d.openingDiscrepancyId,
              )
            }
          />
        </View>
      ))}
      {custody.unresolvedMappingIssueCount > 0 ? (
        <Notice
          kind="info"
          testID="custody-mapping"
          title={`${plural(custody.unresolvedMappingIssueCount, 'sessão com mapeamento não resolvido', 'sessões com mapeamento não resolvido')}.`}
          body="Somente leitura: a origem histórica ainda não foi resolvida pelo servidor."
        />
      ) : null}
      <Button label="Atualizar custódia" variant="link" onPress={onChanged} />
      <AppText variant="helper">{`Data consultada: ${day(date)}.`}</AppText>
    </Card>
  );
}

/**
 * One exact opening discrepancy: comparison, legal targets and the right command. A current-period
 * discrepancy is attributed with the current revision + If-Match; a locked source takes the M9.7
 * adjustment (open PostedOn, chain, target, reason). Server values only — no payroll or variance
 * effect is computed on the device.
 */
export function DiscrepancyPanel({
  discrepancy,
  productNames,
  onChanged,
}: {
  discrepancy: ResponseOpeningDiscrepancyJson;
  productNames: ReadonlyMap<string, string>;
  onChanged: () => void;
}) {
  const { snapshot, engine } = useSession();
  const client = useScopedApiClient();
  const reads = useManagerReads();
  const queries = useQueryClient();
  const today = snapshot.clock!.branchLocalDate;
  const responsibility = useQuery({
    queryKey: managerKey(snapshot.scope, 'responsibility', discrepancy.id, discrepancy.version),
    queryFn: ({ signal }) =>
      getSessionOpeningDiscrepancyDiscrepancyIdResponsibility(
        discrepancy.id,
        { AfterSequence: 0, PageSize: 20 },
        { client, signal },
      ),
    select: (r) => r.data,
    enabled: reads,
    retry: false,
    staleTime: 0,
  });
  const attribute = useOnceCommand();
  const adjust = useDecisionCommand();
  const [chosen, setTarget] = useState<AttributionTarget | null>(null);
  const [reason, setReason] = useState('');
  const [posted, setPosted] = useState<string>(today);
  const [receipt, setReceipt] = useState<string | null>(null);
  // The specific, current read is the evidence: legal targets, revision, version and amounts come
  // from it; the review's embedded copy only labels the panel while that read is loading.
  const current = responsibility.data?.discrepancy ?? null;
  const shown = current ?? discrepancy;
  const options = current ? attributionOptions(current) : [];
  const target = chosen && options.includes(chosen) ? chosen : null;
  const locked = responsibility.data?.sourceLocked ?? false;
  // Further pages belong to ONE read of the specific evidence; a fresh read discards them.
  const loadedAt = responsibility.dataUpdatedAt;
  const [paged, setPaged] = useState<{
    at: number;
    list: ResponseOpeningDiscrepancyAdjustmentJson[];
    cursor: number | null;
    error: unknown;
  } | null>(null);
  const live = paged && paged.at === loadedAt ? paged : null;
  const older = live?.list ?? [];
  const cursor = live ? live.cursor : (responsibility.data?.nextAfterSequence ?? null);
  const olderError = live?.error ?? null;
  const loadMore = async () => {
    if (cursor === null) return;
    try {
      const page = (
        await getSessionOpeningDiscrepancyDiscrepancyIdResponsibility(
          discrepancy.id,
          { AfterSequence: cursor, PageSize: 20 },
          { client },
        )
      ).data;
      setPaged({
        at: loadedAt,
        list: [...older, ...page.adjustments],
        cursor: page.nextAfterSequence ?? null,
        error: null,
      });
    } catch (error) {
      setPaged({ at: loadedAt, list: older, cursor, error });
    }
  };

  const settled = async () => {
    await queries.invalidateQueries({ queryKey: ['manager', snapshot.scope] });
    onChanged();
  };
  const submitAttribution = () => {
    if (!current || responsibility.isError || !target || !reason.trim() || locked) return;
    void attribute.run(
      () =>
        postSessionOpeningDiscrepancyDiscrepancyIdAttribution(
          current.id,
          { discrepancyRevision: current.currentRevision, target, reason: reason.trim() },
          { 'If-Match': toIfMatch(current.version) },
          { client: engine.client() },
        ),
      async () => {
        setReceipt('Responsabilidade atribuída no servidor.');
        await settled();
      },
    );
  };
  const latest = responsibility.data?.latestAdjustment ?? null;
  const submitAdjustment = () => {
    if (
      !current ||
      responsibility.isError ||
      !target ||
      !reason.trim() ||
      !locked ||
      !parseCalendarDate(posted)
    )
      return;
    const body = {
      discrepancyId: current.id,
      expectedVersion: current.version,
      comparisonRevision: current.currentRevision,
      expectedAdjustmentSequence: latest?.sequence ?? 0,
      previousAdjustmentId: latest?.id ?? null,
      priorAttributionId: responsibility.data?.latestAttributionId ?? null,
      target,
      postedOn: posted,
      reason: reason.trim(),
    };
    void adjust.run(
      JSON.stringify(['adjust', body]),
      (key) =>
        postSessionOpeningDiscrepancyAdjustment(
          body,
          { 'Idempotency-Key': key },
          { client: engine.client() },
        ),
      async () => {
        setReceipt('Ajuste de responsabilidade registrado no servidor.');
        await settled();
      },
    );
  };
  const busy = attribute.pending || adjust.pending;
  const done = !!receipt;
  return (
    <Card title="Divergência de abertura" testID="discrepancy-panel">
      <AppText variant="helper">
        {`Revisão atual ${shown.currentRevision} · ${openingDiscrepancyAttributionLabels[shown.attribution]} · ${formatSignedBrlWire(shown.currentTotalDelta)}`}
      </AppText>
      {shown.items.map((i) => (
        <AppText key={i.productId} variant="helper" testID={`discrepancy-item-${i.productId}`}>
          {`${productNames.get(i.productId) ?? 'Produto'}: proposto ${money(i.proposedValue)} · reconhecido ${money(i.acknowledgedValue)} · diferença ${formatSignedBrlWire(i.delta)}`}
        </AppText>
      ))}
      {!shown.countsTowardPerson ? (
        <AppText variant="helper">Esta divergência não conta para uma pessoa.</AppText>
      ) : null}
      {responsibility.isPending ? <Skeleton label="Lendo a responsabilidade…" /> : null}
      {responsibility.isError ? (
        <Notice
          kind="danger"
          title="Não foi possível ler a responsabilidade."
          messages={errorMessages(responsibility.error)}
          actions={
            <Button label="Tentar novamente" onPress={() => void responsibility.refetch()} />
          }
        />
      ) : null}
      {responsibility.data ? (
        <>
          <AppText variant="helper" testID="discrepancy-source-date">
            {`Data de origem: ${day(responsibility.data.sourceDate)}${locked ? ' (período bloqueado)' : ''}`}
          </AppText>
          {[...responsibility.data.adjustments, ...older].map((a) => (
            <View key={a.id} testID={`adjustment-${a.id}`}>
              <AppText variant="helper">
                {`Ajuste ${a.sequence} · origem ${day(a.sourceDate)} · lançado em ${day(a.postedOn)} · ${openingDiscrepancyAttributionLabels[a.target]}`}
              </AppText>
              <AppText variant="helper">
                {`Antes: ${formatSignedBrlWire(a.previousSignedAmount)} · Depois: ${formatSignedBrlWire(a.signedAmount)} (informativo)`}
              </AppText>
              <AppText variant="helper">{a.reason}</AppText>
            </View>
          ))}
          {cursor !== null ? (
            <Button
              label="Carregar ajustes anteriores"
              variant="secondary"
              testID="adjustments-more"
              onPress={() => void loadMore()}
            />
          ) : null}
          {olderError ? (
            <Notice
              kind="danger"
              title="Não foi possível ler mais ajustes."
              messages={errorMessages(olderError)}
            />
          ) : null}
          <FormField label="Atribuir a" required>
            <RadioGroup
              value={target}
              onValueChange={(v) => setTarget(v as AttributionTarget)}
              disabled={done || busy}
              testID="discrepancy-target"
            >
              {options.map((o: AttributionTarget) => (
                <Radio
                  key={o}
                  value={o}
                  label={openingDiscrepancyAttributionLabels[o as OpeningDiscrepancyAttribution]}
                  testID={`discrepancy-target-${o}`}
                />
              ))}
            </RadioGroup>
          </FormField>
          {options.length === 0 ? (
            <Notice kind="info" title="O servidor não oferece destinos para esta divergência." />
          ) : null}
          {locked ? (
            <FormField label="Lançar em" required>
              <DateField
                testID="adjust-posted-on"
                value={parseCalendarDate(posted)}
                max={parseCalendarDate(today) ?? undefined}
                today={parseCalendarDate(today) ?? undefined}
                onValueChange={(d) => setPosted(d ? String(d) : '')}
                disabled={done || busy}
              />
            </FormField>
          ) : null}
          <FormField label="Motivo" required>
            <TextField
              kind="textarea"
              testID="discrepancy-reason"
              value={reason}
              maxLength={500}
              onChangeText={setReason}
              disabled={done || busy}
            />
          </FormField>
          <Button
            label={locked ? 'Registrar ajuste de responsabilidade' : 'Atribuir responsabilidade'}
            loading={busy}
            loadingLabel="Enviando…"
            testID="discrepancy-submit"
            disabled={
              done ||
              busy ||
              responsibility.isError ||
              responsibility.isFetching ||
              !target ||
              !reason.trim() ||
              attribute.blocked ||
              adjust.blocked ||
              (locked && !parseCalendarDate(posted))
            }
            disabledReason={
              responsibility.isError || responsibility.isFetching
                ? 'Aguarde a leitura atual da responsabilidade.'
                : !target
                  ? 'Escolha um destino.'
                  : !reason.trim()
                    ? 'Informe o motivo.'
                    : undefined
            }
            onPress={locked ? submitAdjustment : submitAttribution}
          />
        </>
      ) : null}
      {receipt ? <Notice kind="success" testID="discrepancy-receipt" title={receipt} /> : null}
      <OnceState
        command={attribute}
        receipt="Responsabilidade atribuída no servidor."
        testID="attribute"
        reread={() => {
          void responsibility.refetch().then(() => attribute.clear());
          onChanged();
        }}
      />
      <CommandState
        command={adjust}
        receipt="Ajuste registrado no servidor."
        testID="adjust"
        reread={() => {
          void responsibility.refetch().then(() => adjust.clear());
          onChanged();
        }}
      />
    </Card>
  );
}
