'use client';
import { useState } from 'react';
import { type RequestGoLiveResolutionJson, type ResponseGoLiveImportJson } from '@loto/api';
import { useGetGoLiveImportsId } from '@loto/api/queries';
import {
  centsFromMoneyDigits,
  centsFromWire,
  moneyDigitsFromCents,
  isWireMoney,
  wireFromCents,
  formatSignedBrlWire,
  formatDate,
  formatDuration,
  formatSignedDuration,
  sessionCloseFieldLabels,
  timeEntryStatusLabels,
  payItemKindLabels,
  parseCalendarDate as asCalendarDate,
} from '@loto/core';
import { useDeviceDraft } from '@/client/drafts/use-device-draft';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { FormField } from '@/components/ui/form-field';
import { Input, Textarea } from '@/components/ui/input';
import { DateField, TimeField } from '@/components/ui/date-picker';
import { MoneyInput, blankMoney } from '@/components/ui/money-input';
import { Notice } from '@/components/ui/notice';
import { useScopedApiClient } from '@/client/query/scoped-query-provider';
import { importStatusLabels, Picker, useCommand, useEntities } from './go-live-controls';
const sourceActions: Record<string, readonly string[]> = {
  TblLancamentos: ['Transaction', 'ImportedDebt', 'OpeningFiado', 'OpeningCheque', 'Exclude'],
  TblEstoque: ['CountedSession', 'SessionField', 'Exclude'],
  TblRegistroPonto: ['Attendance', 'Exclude'],
  TblMovimentacaoBancoHoras: [
    'HourOpening',
    'HourPayment',
    'HourAdjustment',
    'AttendanceEvidence',
    'Exclude',
  ],
  TblRelMensal: ['PayItem', 'Exclude'],
  TblLoteriaEspecial: ['SpecialDraw', 'Exclude'],
  TblConfiguracao: ['LockBoundary'],
  TblContas: ['Map', 'Exclude'],
  TblClientes: ['Map', 'Exclude'],
  TblTipoCategoria: ['Map', 'Exclude'],
  TblProduto: ['Map', 'Exclude'],
  TblUsuario: ['Map', 'Exclude'],
  TblItemRel: ['Map', 'Exclude'],
};
export function ImportEditor({ id, today }: { id: string; today: string }) {
  const client = useScopedApiClient();
  const query = useGetGoLiveImportsId(id, { request: { client } });
  const [snapshot, setSnapshot] = useState<ResponseGoLiveImportJson | null>(null);
  const imported = snapshot ?? query.data?.data;
  const entities = useEntities();
  const [edits, setEdits] = useState<Record<string, RequestGoLiveResolutionJson>>({});
  const [invalidDates, setInvalidDates] = useState<Record<string, boolean>>({});
  const datesInvalid = Object.values(invalidDates).some(Boolean);
  const [signEdits, setSignEdits] = useState<Record<string, boolean>>({});
  const [draftFailed, setDraftFailed] = useState(false);
  const [filter, setFilter] = useState(''),
    [page, setPage] = useState(0);
  const [confirmed, setConfirmed] = useState(false);
  const [rangeTable, setRangeTable] = useState('TblLancamentos');
  const [fromId, setFromId] = useState('');
  const [toId, setToId] = useState('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [rangeReason, setRangeReason] = useState('');
  const [disposition, setDisposition] = useState('UnresolvedHistory');
  const [rangeConfirmed, setRangeConfirmed] = useState(false);
  const draft = useDeviceDraft<{
    revision: number;
    edits: Record<string, RequestGoLiveResolutionJson>;
  }>(`go-live-map-${id}`, {
    authorityVerified: query.isSuccess,
  });
  const command = useCommand(`go-live-import-${id}`, (value) => {
    if ('sourceDigest' in value) {
      setSnapshot(value);
      setConfirmed(false);
      setEdits({});
      draft.clear();
    }
  });
  if (!imported)
    return (
      <Notice
        kind="info"
        title={query.isError ? 'Não foi possível carregar a importação.' : 'Carregando importação…'}
      >
        <Button onClick={() => void query.refetch()}>Atualizar</Button>
      </Notice>
    );
  const rows = Object.entries(imported.tables).flatMap(([table, records]) =>
    records.map((record) => ({ table, record, sourceId: Object.values(record)[0] ?? '' })),
  );
  const saved = Object.fromEntries(
    imported.resolutions.map((item) => [`${item.table}:${item.sourceId}`, item]),
  );
  const combined = { ...saved, ...edits };
  const change = (key: string, value: RequestGoLiveResolutionJson) => {
    const next = { ...edits, [key]: value };
    setEdits(next);
    setDraftFailed(!draft.save({ revision: imported.mappingRevision, edits: next }).ok);
    setConfirmed(false);
  };
  const rangeRows = rows.filter((row) => {
    if (row.table !== rangeTable || row.table === 'TblConfiguracao') return false;
    if (fromId && (!/^\d+$/.test(fromId) || BigInt(row.sourceId) < BigInt(fromId))) return false;
    if (toId && (!/^\d+$/.test(toId) || BigInt(row.sourceId) > BigInt(toId))) return false;
    const rawDate =
      row.record.lco_data ??
      row.record.est_data ??
      row.record.pon_data ??
      row.record.mov_data ??
      '';
    const date = rawDate.slice(0, 10).replaceAll('/', '-');
    if (fromDate && (!date || date < fromDate)) return false;
    if (toDate && (!date || date > toDate)) return false;
    return true;
  });
  const shown = rows.filter(
    (row) =>
      !filter ||
      row.table.includes(filter) ||
      imported.preview?.issues.some(
        (issue) =>
          issue.table === row.table &&
          issue.sourceId === row.sourceId &&
          (issue.code.includes(filter) || issue.message.includes(filter)),
      ),
  );
  return (
    <Card title="Revisar mapeamento e resolução">
      <p>
        Revisão {imported.mappingRevision} ·{' '}
        {importStatusLabels[imported.status] ?? imported.status}. Alterações invalidam a prévia
        anterior.
      </p>
      <Button
        onClick={async () => {
          const fresh = await query.refetch();
          if (fresh.data) {
            setSnapshot(fresh.data.data);
            setConfirmed(false);
          }
        }}
      >
        Atualizar estado do servidor
      </Button>
      {draftFailed && (
        <Notice kind="warning" title="O rascunho não foi salvo neste dispositivo.">
          Mantenha esta página aberta e confira o salvamento no servidor antes de sair.
        </Notice>
      )}
      {entities.failed && (
        <Notice kind="warning" title="Não foi possível consultar todos os cadastros.">
          <Button onClick={() => void entities.refresh()}>
            Atualizar cadastros da implantação
          </Button>
        </Notice>
      )}
      <p>
        O rascunho não é ativado automaticamente. Ao atualizar o estado, revise novamente alterações
        locais antes de salvar.
      </p>
      {draft.state.status === 'loaded' &&
        draft.state.result.status === 'found' &&
        draft.state.result.data.revision !== imported.mappingRevision && (
          <Notice kind="warning" title="Este rascunho pertence a outra revisão.">
            Ao restaurar, confira cada alteração contra o estado atual antes de salvar e gerar uma
            nova prévia.
          </Notice>
        )}
      {draft.state.status === 'loaded' && draft.state.result.status === 'found' && (
        <Button
          onClick={() => {
            if (draft.state.status === 'loaded' && draft.state.result.status === 'found')
              if (
                draft.state.result.data?.edits &&
                typeof draft.state.result.data.edits === 'object'
              )
                setEdits(draft.state.result.data.edits);
              else setDraftFailed(true);
          }}
        >
          Restaurar rascunho
        </Button>
      )}
      <details>
        <summary>Excluir ou colocar um recorte em quarentena</summary>
        <fieldset
          disabled={command.busy || imported.status === 'Activated'}
          className="flex flex-col gap-stack-md"
          onChange={() => setRangeConfirmed(false)}
        >
          <Picker
            label="Tabela do recorte"
            value={rangeTable}
            options={Object.keys(imported.tables)
              .filter((x) => x !== 'TblConfiguracao')
              .map((value) => ({ value, label: value }))}
            onChange={setRangeTable}
          />
          <FormField label="Identificador inicial" optional>
            <Input inputMode="numeric" value={fromId} onChange={(e) => setFromId(e.target.value)} />
          </FormField>
          <FormField label="Identificador final" optional>
            <Input inputMode="numeric" value={toId} onChange={(e) => setToId(e.target.value)} />
          </FormField>
          <FormField label="Data inicial do recorte" optional>
            <DateField
              value={asCalendarDate(fromDate)}
              today={asCalendarDate(today)!}
              onValueChange={(value) => setFromDate(value ?? '')}
            />
          </FormField>
          <FormField label="Data final do recorte" optional>
            <DateField
              value={asCalendarDate(toDate)}
              today={asCalendarDate(today)!}
              onValueChange={(value) => setToDate(value ?? '')}
            />
          </FormField>
          <Picker
            label="Tratamento do recorte"
            value={disposition}
            options={[
              { value: 'UnresolvedHistory', label: 'Histórico desconhecido — bloqueia ativação' },
              { value: 'OutsideScope', label: 'Fora do recorte aprovado' },
              { value: 'KnownIrrelevant', label: 'Sem efeito operacional' },
            ]}
            onChange={setDisposition}
          />
          <FormField label="Motivo comprovado do recorte">
            <Textarea value={rangeReason} onChange={(e) => setRangeReason(e.target.value)} />
          </FormField>
          <p>
            {rangeRows.length} registros serão alterados no rascunho. A prévia mostrará cada
            identidade excluída e seu motivo.
          </p>
        </fieldset>
        <label>
          <input
            type="checkbox"
            checked={rangeConfirmed}
            disabled={command.busy || imported.status === 'Activated'}
            onChange={(e) => setRangeConfirmed(e.target.checked)}
          />{' '}
          Revisei os limites e a quantidade deste recorte.
        </label>
        <Button
          disabled={
            !rangeConfirmed ||
            !rangeReason.trim() ||
            !rangeRows.length ||
            command.busy ||
            imported.status === 'Activated'
          }
          onClick={() => {
            const next = { ...edits };
            for (const row of rangeRows)
              next[`${row.table}:${row.sourceId}`] = {
                table: row.table,
                sourceId: row.sourceId,
                action: 'Exclude',
                reason: rangeReason,
                exclusionDisposition: disposition,
              };
            setEdits(next);
            setDraftFailed(!draft.save({ revision: imported.mappingRevision, edits: next }).ok);
            setRangeConfirmed(false);
            setConfirmed(false);
          }}
        >
          Aplicar recorte ao rascunho
        </Button>
      </details>
      <Button
        onClick={() => {
          const file = new Blob(
            [
              JSON.stringify(
                {
                  sourceDigest: imported.sourceDigest,
                  mappingRevision: imported.mappingRevision,
                  resolutions: imported.resolutions,
                },
                null,
                2,
              ),
            ],
            { type: 'application/json' },
          );
          const url = URL.createObjectURL(file);
          const link = document.createElement('a');
          link.href = url;
          link.download = 'mapeamento-implantacao.json';
          link.click();
          URL.revokeObjectURL(url);
        }}
      >
        Baixar mapeamento salvo para conferência privada
      </Button>
      <p>
        A quantidade de registros suportados refere-se às linhas operacionais. Catálogos, versões e
        registros de exclusão permanecem como evidência; suas associações não criam lançamentos.
      </p>
      <FormField label="Filtrar tabela ou pendência">
        <Input
          value={filter}
          onChange={(e) => {
            setFilter(e.target.value);
            setPage(0);
          }}
        />
      </FormField>
      <fieldset
        disabled={command.busy || imported.status === 'Activated'}
        className="flex flex-col gap-stack-md"
      >
        {shown.slice(page * 20, page * 20 + 20).map(({ table, record, sourceId }) => {
          const key = `${table}:${sourceId}`;
          const resolution = combined[key] ?? { table, sourceId, action: '', reason: '' };
          const negative =
            signEdits[key] ??
            String(resolution.targetValue ?? record.est_valor ?? record.lco_valor ?? '').startsWith(
              '-',
            );
          const update = (patch: Partial<RequestGoLiveResolutionJson>) =>
            change(key, { ...resolution, ...patch });
          return (
            <details key={key} className="rounded-panel border border-border-default p-4">
              <summary>
                {table} · registro {sourceId} · {resolution.action || 'Pendente'}
              </summary>
              <dl className="grid gap-2 medium:grid-cols-2">
                {Object.entries(record).map(([column, value]) => (
                  <div key={column} className="min-w-0 break-words">
                    <dt className="text-text-muted">{column}</dt>
                    <dd>{value || 'Não informado'}</dd>
                  </div>
                ))}
              </dl>
              <Picker
                label="Destino do registro"
                value={resolution.action}
                options={[
                  { value: 'Map', label: 'Associar cadastro existente' },
                  { value: 'CountedSession', label: 'Contagem de turno histórico' },
                  { value: 'SessionField', label: 'Relatório ou taxa do turno' },
                  { value: 'Transaction', label: 'Lançamento histórico' },
                  { value: 'ImportedDebt', label: 'Dívida fiado histórica' },
                  { value: 'PayItem', label: 'Item mensal de remuneração' },
                  { value: 'HourOpening', label: 'Saldo inicial de horas' },
                  { value: 'HourPayment', label: 'Pagamento de horas' },
                  { value: 'HourAdjustment', label: 'Ajuste de horas' },
                  { value: 'Attendance', label: 'Registro de ponto' },
                  { value: 'AttendanceEvidence', label: 'Evidência do ponto, sem novo efeito' },
                  { value: 'SpecialDraw', label: 'Loteria especial' },
                  { value: 'LockBoundary', label: 'Confirmar bloqueio legado' },
                  { value: 'OpeningFiado', label: 'Obrigação fiado inicial' },
                  { value: 'OpeningCheque', label: 'Cheque inicial' },
                  { value: 'Exclude', label: 'Excluir da ativação' },
                ].filter((option) => (sourceActions[table] ?? ['Exclude']).includes(option.value))}
                onChange={(action) =>
                  action === 'Exclude'
                    ? change(key, {
                        table,
                        sourceId,
                        action,
                        reason: resolution.reason,
                        exclusionDisposition:
                          resolution.exclusionDisposition ?? 'UnresolvedHistory',
                      })
                    : update({ action })
                }
              />
              {imported.suggestions.find((x) => x.table === table && x.sourceId === sourceId) && (
                <Button
                  onClick={() => {
                    const suggestion = imported.suggestions.find(
                      (x) => x.table === table && x.sourceId === sourceId,
                    )!;
                    change(key, { ...resolution, ...suggestion });
                  }}
                >
                  Usar sugestão M9.5 para revisar
                </Button>
              )}
              {resolution.action === 'Exclude' && (
                <Picker
                  label="Tratamento da exclusão"
                  value={resolution.exclusionDisposition}
                  options={[
                    {
                      value: 'KnownIrrelevant',
                      label: 'Sem efeito operacional, evidência preservada',
                    },
                    { value: 'OutsideScope', label: 'Fora do recorte aprovado' },
                    {
                      value: 'UnresolvedHistory',
                      label: 'Histórico desconhecido — bloquear ativação',
                    },
                  ]}
                  onChange={(exclusionDisposition) => update({ exclusionDisposition })}
                />
              )}
              <FormField label="Motivo e evidência">
                <Textarea
                  value={resolution.reason}
                  onChange={(e) => update({ reason: e.target.value })}
                />
              </FormField>
              {resolution.action !== 'Exclude' && (
                <>
                  <Picker
                    label="Conta de destino"
                    value={resolution.accountId}
                    options={entities.accountOptions}
                    onChange={(accountId) => update({ accountId: accountId || null })}
                  />
                  <Picker
                    label="Cliente identificado"
                    value={resolution.clientId}
                    options={entities.clientOptions}
                    onChange={(clientId) => update({ clientId: clientId || null })}
                  />
                  <Picker
                    label="Operador identificado"
                    value={resolution.operatorId}
                    options={entities.operatorOptions}
                    onChange={(operatorId) => update({ operatorId: operatorId || null })}
                  />
                  <Picker
                    label="Tipo de destino"
                    value={resolution.transactionTypeId}
                    options={entities.typeOptions}
                    onChange={(transactionTypeId) =>
                      update({ transactionTypeId: transactionTypeId || null })
                    }
                  />
                  <FormField
                    label="Valor validado de destino"
                    helper="Deixe vazio para preservar o valor exato da origem. Justifique qualquer normalização."
                  >
                    <MoneyInput
                      value={
                        typeof resolution.targetValue === 'number' &&
                        isWireMoney(resolution.targetValue)
                          ? {
                              kind: 'amount',
                              digits: moneyDigitsFromCents(
                                centsFromWire(Math.abs(resolution.targetValue)),
                              ),
                            }
                          : blankMoney
                      }
                      onValueChange={(value) =>
                        update({
                          targetValue:
                            value.kind === 'blank'
                              ? null
                              : (negative ? -1 : 1) *
                                wireFromCents(centsFromMoneyDigits(value.digits)),
                        })
                      }
                    />
                  </FormField>
                  <Picker
                    label="Sinal do valor validado"
                    value={negative ? 'negative' : 'positive'}
                    options={[
                      { value: 'positive', label: 'Positivo' },
                      { value: 'negative', label: 'Negativo (relatório assinado)' },
                    ]}
                    onChange={(sign) => {
                      const selected = sign === 'negative';
                      setSignEdits({ ...signEdits, [key]: selected });
                      if (typeof resolution.targetValue === 'number')
                        update({
                          targetValue: (selected ? -1 : 1) * Math.abs(resolution.targetValue),
                        });
                    }}
                  />
                  <Picker
                    label="Produto físico de destino"
                    value={resolution.productId}
                    options={entities.productOptions}
                    onChange={(productId) => update({ productId: productId || null })}
                  />
                  <Picker
                    label="Terminal físico"
                    value={resolution.terminalId}
                    options={entities.terminalOptions}
                    onChange={(terminalId) => update({ terminalId: terminalId || null })}
                  />
                  {table === 'TblContas' && (
                    <Picker
                      label="Terminal de origem associado ao fiado"
                      value={resolution.sourceTerminalId}
                      options={(imported.tables.TblContas ?? [])
                        .filter((x) => x.con_tipo === 'Terminal')
                        .map((x) => ({
                          value: x.con_id!,
                          label: x.con_identificacao || x.con_id!,
                        }))}
                      onChange={(sourceTerminalId) =>
                        update({ sourceTerminalId: sourceTerminalId || null })
                      }
                    />
                  )}
                  <Picker
                    label="Campo de fechamento"
                    value={resolution.field}
                    options={Object.entries(sessionCloseFieldLabels).map(([value, label]) => ({
                      value,
                      label,
                    }))}
                    onChange={(field) =>
                      update({ field: (field as RequestGoLiveResolutionJson['field']) || null })
                    }
                  />
                  <Picker
                    label="Categoria de remuneração"
                    value={resolution.payCategoryId}
                    options={entities.payCategoryOptions}
                    onChange={(payCategoryId) => update({ payCategoryId: payCategoryId || null })}
                  />
                  <Picker
                    label="Natureza do item mensal"
                    value={resolution.payKind}
                    options={Object.entries(payItemKindLabels).map(([value, label]) => ({
                      value,
                      label,
                    }))}
                    onChange={(payKind) =>
                      update({
                        payKind: (payKind as RequestGoLiveResolutionJson['payKind']) || null,
                      })
                    }
                  />
                  <Picker
                    label="Situação do ponto"
                    value={resolution.attendanceStatus}
                    options={Object.entries(timeEntryStatusLabels).map(([value, label]) => ({
                      value,
                      label,
                    }))}
                    onChange={(attendanceStatus) =>
                      update({
                        attendanceStatus:
                          (attendanceStatus as RequestGoLiveResolutionJson['attendanceStatus']) ||
                          null,
                      })
                    }
                  />
                  <FormField label="Regra comprovada do desconto" optional>
                    <Textarea
                      value={resolution.deductionPolicy ?? ''}
                      onChange={(e) => update({ deductionPolicy: e.target.value || null })}
                    />
                  </FormField>
                  <FormField
                    label="Horas de destino validadas"
                    helper="Número decimal com ponto, até quatro casas. Preserve a evidência original e justifique a alteração."
                    optional
                  >
                    <Input
                      inputMode="decimal"
                      value={resolution.targetHours ?? ''}
                      onChange={(e) => update({ targetHours: e.target.value || null })}
                    />
                  </FormField>
                  <FormField label="Nome de destino" optional>
                    <Input
                      value={resolution.targetName ?? ''}
                      onChange={(e) => update({ targetName: e.target.value || null })}
                    />
                  </FormField>
                  <FormField label="Sequência comprovada do turno" optional>
                    <Input
                      inputMode="numeric"
                      value={resolution.sequence ?? ''}
                      onChange={(e) => update({ sequence: e.target.value || null })}
                    />
                  </FormField>
                  {(['sourceDate', 'postedOn'] as const).map((field) => (
                    <FormField
                      key={field}
                      label={
                        field === 'sourceDate'
                          ? 'Data de origem comprovada / bloqueio'
                          : 'Data de registro'
                      }
                      optional
                    >
                      <DateField
                        onInvalidChange={(invalid) =>
                          setInvalidDates({ ...invalidDates, [`${key}:${field}`]: invalid })
                        }
                        value={asCalendarDate(resolution[field]?.slice(0, 10) ?? '')}
                        today={asCalendarDate(today)!}
                        onValueChange={(value) => update({ [field]: value || null })}
                      />
                    </FormField>
                  ))}
                  {(['startsAt', 'endsAt'] as const).map((field) => (
                    <div key={field} className="flex flex-col gap-stack-sm">
                      <FormField
                        label={
                          field === 'startsAt'
                            ? 'Data de início comprovada'
                            : 'Data de fim comprovada'
                        }
                        optional
                      >
                        <DateField
                          onInvalidChange={(invalid) =>
                            setInvalidDates({ ...invalidDates, [`${key}:${field}`]: invalid })
                          }
                          value={asCalendarDate(resolution[field]?.slice(0, 10) ?? '')}
                          today={asCalendarDate(today)!}
                          onValueChange={(value) =>
                            update({
                              [field]: value
                                ? `${value}T${resolution[field]?.slice(11, 16) || '00:00'}:00`
                                : null,
                            })
                          }
                        />
                      </FormField>
                      <FormField
                        label={
                          field === 'startsAt'
                            ? 'Hora de início da filial'
                            : 'Hora de fim da filial'
                        }
                        optional
                      >
                        <TimeField
                          value={resolution[field]?.slice(11, 16) ?? ''}
                          onValueChange={(value) =>
                            update({
                              [field]: resolution[field]
                                ? `${resolution[field]!.slice(0, 10)}T${value}:00`
                                : null,
                            })
                          }
                        />
                      </FormField>
                    </div>
                  ))}
                  <label>
                    <input
                      type="checkbox"
                      checked={resolution.historyComplete ?? false}
                      onChange={(e) => update({ historyComplete: e.target.checked })}
                    />{' '}
                    Confirmei a integridade do histórico, a custódia e os limites deste registro.
                  </label>
                  <FormField label="Ano comprovado" optional>
                    <Input
                      inputMode="numeric"
                      value={resolution.year ?? ''}
                      onChange={(e) => update({ year: e.target.value || null })}
                    />
                  </FormField>
                </>
              )}
            </details>
          );
        })}
        <div className="flex flex-wrap gap-2">
          <Button disabled={page === 0} onClick={() => setPage(page - 1)}>
            Anterior
          </Button>
          <Button disabled={(page + 1) * 20 >= shown.length} onClick={() => setPage(page + 1)}>
            Próximos
          </Button>
        </div>
        <Button
          disabled={datesInvalid || Object.keys(edits).length === 0}
          onClick={() =>
            void command.run({
              kind: 'mapping',
              id,
              request: {
                expectedRevision: imported.mappingRevision,
                resolutions: Object.values(combined),
              },
            })
          }
        >
          Salvar mapeamento
        </Button>
        <Button
          disabled={datesInvalid || Object.keys(edits).length > 0}
          onClick={() =>
            void command.run({
              kind: 'preview',
              id,
              request: { expectedRevision: imported.mappingRevision, resolutions: [] },
            })
          }
        >
          Gerar prévia
        </Button>
      </fieldset>
      {imported.preview && (
        <section className="flex flex-col gap-stack-md" aria-label="Resultado da prévia">
          <p>
            {imported.preview.supportedRows} registros suportados · {imported.preview.excludedRows}{' '}
            excluídos · {imported.preview.issues.length} pendências.
          </p>
          <details>
            <summary>
              Exceções explicadas e exclusões revisadas ({imported.preview.explainedIssues.length})
            </summary>
            {imported.preview.explainedIssues.map((issue, index) => (
              <p key={index}>
                {issue.table} · {issue.sourceId}: {issue.message}
                {issue.sourceValue && ` · origem: ${issue.sourceValue}`}
              </p>
            ))}
          </details>
          {imported.preview.issues.map((issue, index) => (
            <Notice
              key={`${issue.table}:${issue.sourceId}:${index}`}
              kind="warning"
              title={`${issue.table} · ${issue.sourceId}: ${issue.message}`}
            >
              {issue.sourceValue && <p>Valor original: {issue.sourceValue}</p>}
            </Notice>
          ))}
          <details>
            <summary>
              Conferir ponto e política vigente ({imported.preview.attendanceComparisons.length})
            </summary>
            {imported.preview.attendanceComparisons.map((item) => (
              <div key={item.sourceId}>
                <p>
                  Ponto de origem {item.sourceId} · total original:{' '}
                  {item.sourceTotalHours || 'Não informado'} · saldo original:{' '}
                  {item.sourceBalanceHours || 'Não informado'}
                </p>
                <p>
                  Cálculo do servidor: {formatDuration(item.calculatedTotalHours)} · saldo{' '}
                  {formatSignedDuration(item.calculatedBalanceHours)} · meta{' '}
                  {formatDuration(item.dailyTargetHours)} · desconto acima de 6h:{' '}
                  {formatDuration(item.lunchDeductionOver6H)} · acima de 4h:{' '}
                  {formatDuration(item.lunchDeductionOver4H)}
                </p>
                <p className="break-all">Política: {item.policyId}</p>
              </div>
            ))}
          </details>
          <details>
            <summary>Comparação por caixa e dia</summary>
            {imported.preview.days.map((day) => (
              <p key={`${day.sourceAccountId}:${day.date}`}>
                Caixa de origem {day.sourceAccountId} · {formatDate(asCalendarDate(day.date)!)} ·
                Access:{' '}
                {day.accessDifference === null || day.accessDifference === undefined
                  ? 'Indisponível'
                  : formatSignedBrlWire(day.accessDifference)}{' '}
                · Aplicativo:{' '}
                {day.appDifference === null || day.appDifference === undefined
                  ? 'Indisponível'
                  : formatSignedBrlWire(day.appDifference)}{' '}
                · {day.completeness}
              </p>
            ))}
          </details>
          <details>
            <summary>Obrigações e destinos desta prévia</summary>
            {imported.preview.openingPlan.map((plan, index) => (
              <div key={index}>
                <p>
                  {plan.isCheque ? 'Cheque inicial' : 'Saldo fiado inicial'} ·{' '}
                  {entities.accountOptions.find((x) => x.value === plan.accountId)?.label ??
                    'Conta indisponível'}{' '}
                  ·{' '}
                  {plan.clientId
                    ? (entities.clientOptions.find((x) => x.value === plan.clientId)?.label ??
                      'Cliente indisponível')
                    : 'Cliente não identificado'}
                </p>
                <p>
                  {entities.operatorOptions.find((x) => x.value === plan.recordedByOperatorId)
                    ?.label ?? 'Operador indisponível'}{' '}
                  ·{' '}
                  {entities.typeOptions.find((x) => x.value === plan.transactionTypeId)?.label ??
                    'Tipo indisponível'}
                </p>
                <p>
                  Origem: {plan.sourceReference} · {plan.sourceEvidence}
                </p>
                {plan.items.map((item, i) => (
                  <p key={i}>
                    Parcela {i + 1}: {formatSignedBrlWire(item.value)} · vencimento{' '}
                    {formatDate(asCalendarDate(item.dueDate.slice(0, 10))!)}
                  </p>
                ))}
              </div>
            ))}
            {imported.preview.historicalPlan.map((plan) => (
              <p key={`${plan.table}:${plan.sourceId}`}>
                {plan.table} · {plan.sourceId} · {plan.action} ·{' '}
                {entities.accountOptions.find((x) => x.value === plan.accountId)?.label ?? ''} ·{' '}
                {entities.operatorOptions.find((x) => x.value === plan.operatorId)?.label ?? ''} ·{' '}
                {entities.productOptions.find((x) => x.value === plan.productId)?.label ?? ''} ·{' '}
                {entities.payCategoryOptions.find((x) => x.value === plan.payCategoryId)?.label ??
                  ''}{' '}
                · {plan.reason}
              </p>
            ))}
          </details>
          <details>
            <summary>Versão exata e destinos revisados</summary>
            <p className="break-all">
              Origem: {imported.sourceDigest} · esquema {imported.schemaVersion} · ferramenta{' '}
              {imported.preview.toolVersion} · revisão {imported.mappingRevision} · prévia{' '}
              {imported.preview.id} · estado {imported.preview.targetFingerprint}
            </p>
            <pre className="whitespace-pre-wrap break-all">
              {JSON.stringify(
                {
                  obrigacoes: imported.preview.openingPlan,
                  historico: imported.preview.historicalPlan,
                  exclusoes: imported.preview.explainedIssues,
                },
                null,
                2,
              )}
            </pre>
          </details>
          <label>
            <input
              type="checkbox"
              checked={confirmed}
              onChange={(e) => setConfirmed(e.target.checked)}
              disabled={!imported.preview.canActivate || Object.keys(edits).length > 0}
            />{' '}
            Revisei esta versão, os destinos, as exclusões e as evidências.
          </label>
          <Button
            disabled={
              !confirmed ||
              entities.pending ||
              entities.failed ||
              datesInvalid ||
              command.busy ||
              !imported.preview.canActivate ||
              imported.status === 'Activated'
            }
            onClick={() =>
              void command.run({
                kind: 'activate',
                id,
                request: {
                  dryRunVersion: imported.preview!.toolVersion,
                  mappingRevision: imported.mappingRevision,
                  previewId: imported.previewId!,
                  sourceDigest: imported.sourceDigest,
                  targetFingerprint: imported.targetFingerprint!,
                },
              })
            }
          >
            Confirmar ativação desta versão
          </Button>
        </section>
      )}
      {imported.status === 'Activated' && (
        <Notice kind="success" title="Ativação confirmada">
          <p>{imported.targetIds.length} registros de destino persistidos.</p>
          <details>
            <summary>Rastrear origens e destinos persistidos</summary>
            {imported.targets.map((target) => (
              <p className="break-all" key={`${target.sourceTable}:${target.sourceId}`}>
                {target.sourceTable} · {target.sourceId} → {target.targetKind} · {target.targetId}
              </p>
            ))}
          </details>
        </Notice>
      )}
      {command.feedback}
    </Card>
  );
}
