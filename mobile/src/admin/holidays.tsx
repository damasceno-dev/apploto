import { useEffect, useRef, useState, type RefObject } from 'react';
import { useLocalSearchParams, useRouter } from 'expo-router';
import type { View } from 'react-native';
import {
  BrazilianHolidayCalendarSource,
  getHolidayImportBrYearPreview,
  type ResponseBrazilianHolidayImportJson,
  type ResponseBrazilianHolidayPreviewJson,
  type ResponseHolidayJson,
} from '@loto/api';
import {
  useDeleteHolidayHolidayId,
  useGetHoliday,
  usePostHoliday,
  usePostHolidayImportBrYear,
  usePutHolidayHolidayId,
} from '@loto/api/queries';
import {
  brazilianHolidayCalendarSourceLabels,
  brazilianHolidayImportStatusLabels,
  brazilianHolidayTypeLabels,
  formatCount,
  formatDate,
  formatDateWithWeekday,
  holidaySourceLabels,
  parseCalendarDate,
} from '@loto/core';
import { useSession, useScopedApiClient } from '@/session/provider';
import {
  AppText,
  Button,
  Card,
  Checkbox,
  ConfirmDialog,
  DateField,
  EmptyState,
  FormField,
  Notice,
  Overlay,
  Pagination,
  Radio,
  RadioGroup,
  ReadOnlyValue,
  TextField,
  useToast,
} from '@/ui';
import {
  datePart,
  holidayPayload,
  holidayYear,
  holidayYearRange,
  isHolidayBatch,
  type HolidayBatch,
} from './contracts';
import {
  CommandNotices,
  ReadState,
  RetainedNotice,
  errorMessages,
  useAdminCommand,
  useAdminDraft,
  useAdminReads,
} from './common';

export const CALENDAR_EFFECT =
  'O calendário da filial entra nos próximos cálculos de vencimento de cheques e cartões e na situação dos registros de ponto. Esta ação não altera transações nem registros já gravados.';
export const IMPORT_REFETCH =
  'Ao importar, a fonte é consultada de novo: o resultado pode ser diferente da prévia. Datas que já têm feriado ativo são ignoradas.';
const sources = Object.values(BrazilianHolidayCalendarSource);
const pageSize = 20;
interface ImportOptions {
  readonly year: number;
  readonly source: BrazilianHolidayCalendarSource;
  readonly includeOptionalFederal: boolean;
}
const sameOptions = (a: ImportOptions, b: ImportOptions) =>
  a.year === b.year &&
  a.source === b.source &&
  a.includeOptionalFederal === b.includeOptionalFederal;
const optionsText = (o: ImportOptions) =>
  `${o.year} · Fonte: ${brazilianHolidayCalendarSourceLabels[o.source]} · ${o.includeOptionalFederal ? 'com pontos facultativos federais' : 'sem pontos facultativos'}`;
const shortDate = (value: string) => {
  const date = datePart(value);
  return date ? formatDate(date) : value;
};

export function HolidaysScreen() {
  const params = useLocalSearchParams<{ year?: string; page?: string }>();
  const router = useRouter();
  const { snapshot } = useSession();
  const client = useScopedApiClient();
  const reads = useAdminReads();
  const today = parseCalendarDate(snapshot.clock!.branchLocalDate.slice(0, 10))!;
  const year = holidayYear(params.year, today);
  const page = /^\d+$/.test(params.page ?? '') && Number(params.page) > 0 ? Number(params.page) : 1;
  const list = useGetHoliday(
    { Year: year, Page: page, PageSize: pageSize },
    { request: { client }, query: { staleTime: 0, enabled: reads } },
  );
  const [adding, setAdding] = useState(false);
  const [importing, setImporting] = useState(false);
  const [editing, setEditing] = useState<ResponseHolidayJson | null>(null);
  const [deactivating, setDeactivating] = useState<ResponseHolidayJson | null>(null);
  const addTrigger = useRef<View>(null);
  const importTrigger = useRef<View>(null);
  const refetch = async () => {
    const result = await list.refetch();
    if (result.error) throw result.error;
  };
  const setYear = (next: number) => router.setParams({ year: String(next), page: '1' });
  return (
    <>
      <Card title={`Feriados de ${year}`}>
        <Button
          label="Ano anterior"
          testID="holiday-year-previous"
          variant="secondary"
          disabled={year <= holidayYearRange.min}
          onPress={() => setYear(year - 1)}
        />
        <AppText testID="holiday-year" variant="body-strong">{`Ano: ${year}`}</AppText>
        <Button
          label="Próximo ano"
          testID="holiday-year-next"
          variant="secondary"
          disabled={year >= holidayYearRange.max}
          onPress={() => setYear(year + 1)}
        />
        {year !== Number(today.slice(0, 4)) ? (
          <Button
            label="Voltar para o ano atual"
            variant="link"
            onPress={() => setYear(Number(today.slice(0, 4)))}
          />
        ) : null}
      </Card>
      <Button
        ref={importTrigger}
        label="Importar feriados nacionais"
        testID="holiday-import"
        disabled={!reads}
        onPress={() => setImporting(true)}
      />
      <Button
        ref={addTrigger}
        label="Adicionar feriado"
        testID="holiday-add"
        variant="secondary"
        disabled={!reads}
        onPress={() => setAdding(true)}
      />
      <ReadState pending={list.isPending} error={list.error} retry={() => void list.refetch()} />
      {list.data && !list.error ? (
        <>
          {list.data.data.totalCount === 0 ? (
            <EmptyState
              kind="no-records"
              statement={`Nenhum feriado em ${year}.`}
              primary={
                <Button
                  label="Importar feriados nacionais"
                  variant="secondary"
                  onPress={() => setImporting(true)}
                />
              }
            />
          ) : (
            <AppText>{`${formatCount(list.data.data.totalCount)} ${list.data.data.totalCount === 1 ? 'feriado' : 'feriados'} em ${year}`}</AppText>
          )}
          {list.data.data.items.map((holiday) => {
            const date = datePart(holiday.date);
            return (
              <Card
                key={holiday.id}
                title={date ? formatDateWithWeekday(date) : holiday.date}
                testID={`holiday-row-${holiday.id}`}
              >
                <AppText>
                  {holiday.description?.trim() ? holiday.description : 'Sem descrição'}
                </AppText>
                <Button
                  label={`Editar descrição de ${shortDate(holiday.date)}`}
                  testID={`holiday-edit-${holiday.id}`}
                  disabled={!reads}
                  onPress={() => setEditing(holiday)}
                />
                <Button
                  label={`Desativar feriado de ${shortDate(holiday.date)}`}
                  testID={`holiday-deactivate-${holiday.id}`}
                  variant="danger"
                  disabled={!reads}
                  onPress={() => setDeactivating(holiday)}
                />
              </Card>
            );
          })}
          <Pagination
            envelope={list.data.data}
            listName="feriados"
            loading={list.isFetching}
            onPageChange={(next) => router.setParams({ page: String(next) })}
          />
        </>
      ) : null}
      {adding ? (
        <HolidayBatchEditor
          visible={reads}
          year={year}
          today={today}
          returnFocus={addTrigger}
          onClose={() => setAdding(false)}
          refetch={refetch}
        />
      ) : null}
      {importing ? (
        <HolidayImport
          visible={reads}
          year={year}
          returnFocus={importTrigger}
          onClose={() => setImporting(false)}
          refetch={refetch}
        />
      ) : null}
      {editing ? (
        <HolidayDescription
          key={editing.id}
          holiday={editing}
          visible={reads}
          onClose={() => setEditing(null)}
          refetch={refetch}
        />
      ) : null}
      {deactivating ? (
        <HolidayDeactivation
          holiday={deactivating}
          visible={reads}
          onClose={() => setDeactivating(null)}
          refetch={refetch}
        />
      ) : null}
    </>
  );
}

function HolidayBatchEditor({
  visible,
  year,
  today,
  onClose,
  refetch,
  returnFocus,
}: {
  visible: boolean;
  year: number;
  today: NonNullable<ReturnType<typeof parseCalendarDate>>;
  onClose: () => void;
  refetch: () => Promise<void>;
  returnFocus: RefObject<View | null>;
}) {
  const { engine } = useSession();
  const client = useScopedApiClient();
  const toast = useToast();
  const base: HolidayBatch = { rows: [{ date: '', description: '' }] };
  const [batch, setBatch] = useState<HolidayBatch>(base);
  const batchRef = useRef(batch);
  const [invalid, setInvalid] = useState<readonly number[]>([]);
  const command = useAdminCommand();
  const create = usePostHoliday({ request: { client } });
  const [created, setCreated] = useState<readonly ResponseHolidayJson[] | null>(null);
  const draft = useAdminDraft(
    `admin/holidays/new`,
    isHolidayBatch,
    async () => engine.getSnapshot().branch?.role !== 'Member',
  );
  // Explicit cancel abandons unsent input; an unresolved outcome keeps it for reconciliation.
  const cancel = () => {
    if (!command.uncertain && !command.postWriteError) void draft.discard();
    onClose();
  };
  const change = (next: HolidayBatch) => {
    batchRef.current = next;
    setBatch(next);
    setInvalid([]);
    void draft.keep(next, null);
  };
  const submit = () => {
    const candidate = batchRef.current;
    const prepared = holidayPayload(candidate);
    if (!prepared.ok) {
      setInvalid(prepared.rows);
      return;
    }
    void command.run(
      async () => {
        const kept = await draft.keep(candidate, null);
        return { result: await create.mutateAsync({ data: prepared.payload }), kept };
      },
      async ({ result, kept }) => {
        setCreated(result.data.items);
        await draft.acknowledge(kept);
        toast.show('Feriados salvos no servidor.');
        await refetch();
      },
    );
  };
  return (
    <Overlay
      visible={visible}
      title="Adicionar feriados"
      busy={command.pending}
      returnFocus={returnFocus}
      onDismiss={cancel}
      testID="holiday-batch"
      footer={
        created ? (
          <Button label="Fechar" onPress={onClose} fullWidth />
        ) : (
          <>
            <Button
              label="Salvar feriados"
              testID="holiday-batch-save"
              loading={command.pending}
              loadingLabel="Salvando…"
              disabled={command.blocked}
              onPress={submit}
              fullWidth
            />
            <Button
              label="Cancelar"
              variant="secondary"
              disabled={command.pending}
              onPress={cancel}
              fullWidth
            />
          </>
        )
      }
    >
      <RetainedNotice
        visible={!!draft.retained}
        onRestore={() => {
          const value = draft.retained!.input;
          batchRef.current = value;
          setBatch(value);
          draft.dismissRetained();
        }}
      />
      {draft.problem ? <Notice kind="warning" title={draft.problem} /> : null}
      {created ? (
        <Notice
          kind="success"
          title={`${formatCount(created.length)} ${created.length === 1 ? 'feriado salvo' : 'feriados salvos'} no servidor.`}
          messages={created.map(
            (h) => `${shortDate(h.date)}${h.description ? ` · ${h.description}` : ''}`,
          )}
        />
      ) : null}
      <CommandNotices
        command={command}
        receipt="Feriados salvos no servidor."
        reconcile={() =>
          void refetch()
            .then(() => command.clear())
            .catch(() => undefined)
        }
      />
      <AppText variant="helper">{CALENDAR_EFFECT}</AppText>
      {created
        ? null
        : batch.rows.map((row, index) => (
            <Card key={index} title={`Feriado ${index + 1}`}>
              <FormField
                label="Data"
                required
                error={invalid.includes(index) ? 'Informe uma data válida.' : undefined}
              >
                <DateField
                  testID={index === 0 ? 'holiday-date' : `holiday-date-${index + 1}`}
                  today={today}
                  value={parseCalendarDate(row.date)}
                  onValueChange={(value) =>
                    change({
                      rows: batch.rows.map((r, i) =>
                        i === index ? { ...r, date: value ?? '' } : r,
                      ),
                    })
                  }
                />
              </FormField>
              <FormField label="Descrição" helper="Opcional.">
                <TextField
                  testID={index === 0 ? 'holiday-description' : `holiday-description-${index + 1}`}
                  value={row.description}
                  maxLength={500}
                  onChangeText={(description) =>
                    change({
                      rows: batch.rows.map((r, i) => (i === index ? { ...r, description } : r)),
                    })
                  }
                />
              </FormField>
              {batch.rows.length > 1 ? (
                <Button
                  label={`Remover feriado ${index + 1} da lista`}
                  variant="secondary"
                  disabled={command.pending}
                  onPress={() => change({ rows: batch.rows.filter((_, i) => i !== index) })}
                />
              ) : null}
            </Card>
          ))}
      {created ? null : (
        <Button
          label="Adicionar outra data"
          variant="secondary"
          disabled={command.pending || batch.rows.length >= 50}
          onPress={() => change({ rows: [...batch.rows, { date: '', description: '' }] })}
        />
      )}
      {created ? null : (
        <AppText variant="helper">{`As datas podem ser de qualquer ano; a lista mostra ${year}.`}</AppText>
      )}
    </Overlay>
  );
}

function HolidayImport({
  visible,
  year,
  onClose,
  refetch,
  returnFocus,
}: {
  visible: boolean;
  year: number;
  onClose: () => void;
  refetch: () => Promise<void>;
  returnFocus: RefObject<View | null>;
}) {
  const client = useScopedApiClient();
  const { engine } = useSession();
  const [options, setOptions] = useState<ImportOptions>({
    year,
    source: 'Composite',
    includeOptionalFederal: false,
  });
  const [preview, setPreview] = useState<{
    options: ImportOptions;
    data: ResponseBrazilianHolidayPreviewJson;
  } | null>(null);
  const [previewPending, setPreviewPending] = useState(false);
  const [previewError, setPreviewError] = useState<unknown>(null);
  const [confirming, setConfirming] = useState(false);
  const [result, setResult] = useState<ResponseBrazilianHolidayImportJson | null>(null);
  const command = useAdminCommand();
  const run = usePostHolidayImportBrYear({ request: { client } });
  const flight = useRef(0);
  const scope = engine.getSnapshot().scope;
  const change = (next: ImportOptions) => {
    // Any option change discards the preview and any late response for the old options.
    flight.current++;
    setOptions(next);
    setPreview(null);
    setPreviewPending(false);
    setPreviewError(null);
    setResult(null);
  };
  const loadPreview = () => {
    if (previewPending) return;
    const requested = options;
    const ticket = ++flight.current;
    setPreviewPending(true);
    setPreviewError(null);
    void getHolidayImportBrYearPreview(
      requested.year,
      { includeOptionalFederal: requested.includeOptionalFederal, source: requested.source },
      { client },
    )
      .then((response) => {
        if (ticket === flight.current && engine.getSnapshot().scope === scope)
          setPreview({ options: requested, data: response.data });
      })
      .catch((error: unknown) => {
        if (ticket === flight.current && engine.getSnapshot().scope === scope)
          setPreviewError(error);
      })
      .finally(() => {
        if (ticket === flight.current) setPreviewPending(false);
      });
  };
  const current = preview && sameOptions(preview.options, options) ? preview : null;
  const execute = () => {
    if (!current) return;
    const requested = current.options;
    void command.run(
      () =>
        run.mutateAsync({
          year: requested.year,
          params: {
            includeOptionalFederal: requested.includeOptionalFederal,
            source: requested.source,
          },
        }),
      async (response) => {
        setConfirming(false);
        setResult(response.data);
        await refetch();
      },
    );
  };
  return (
    <Overlay
      visible={visible}
      title="Importar feriados nacionais"
      busy={command.pending || previewPending}
      returnFocus={returnFocus}
      onDismiss={onClose}
      testID="holiday-import-dialog"
      footer={
        <>
          {current && !result ? (
            <Button
              label="Importar"
              testID="holiday-import-confirm"
              disabled={command.blocked}
              onPress={() => setConfirming(true)}
              fullWidth
            />
          ) : null}
          <Button
            label="Fechar"
            variant="secondary"
            disabled={command.pending}
            onPress={onClose}
            fullWidth
          />
        </>
      }
    >
      {result ? (
        <Card title="Importação concluída" testID="holiday-import-result">
          <AppText variant="body-strong">{`${formatCount(result.importedCount)} importados · ${formatCount(result.skippedCount)} ignorados`}</AppText>
          <AppText>
            {optionsText({
              year: result.year,
              source: result.source,
              includeOptionalFederal: result.includesOptionalFederal,
            })}
          </AppText>
          {result.items.map((item) => (
            <AppText key={`${item.date}-${item.type}`}>
              {`${shortDate(item.date)} · ${item.description} · ${brazilianHolidayTypeLabels[item.type]} · ${holidaySourceLabels[item.source]} · ${brazilianHolidayImportStatusLabels[item.status]}`}
            </AppText>
          ))}
          <Button
            label="Ver nova prévia"
            testID="holiday-new-preview"
            variant="secondary"
            onPress={() => {
              setResult(null);
              setPreview(null);
              command.clear();
            }}
          />
        </Card>
      ) : (
        <>
          <FormField label="Ano">
            <ReadOnlyValue value={String(options.year)} />
          </FormField>
          <FormField label="Fonte">
            <RadioGroup
              value={options.source}
              accessibilityLabel="Fonte"
              disabled={command.pending}
              onValueChange={(source) =>
                change({ ...options, source: source as BrazilianHolidayCalendarSource })
              }
            >
              {sources.map((source) => (
                <Radio
                  key={source}
                  value={source}
                  label={brazilianHolidayCalendarSourceLabels[source]}
                  testID={`holiday-source-${source}`}
                />
              ))}
            </RadioGroup>
          </FormField>
          <Checkbox
            label="Incluir pontos facultativos federais"
            description="Carnaval (terça), Quarta-feira de Cinzas e Corpus Christi."
            testID="holiday-optional"
            checked={options.includeOptionalFederal}
            disabled={command.pending}
            onCheckedChange={(includeOptionalFederal) =>
              change({ ...options, includeOptionalFederal })
            }
          />
          <Button
            label="Ver prévia"
            testID="holiday-preview"
            loading={previewPending}
            loadingLabel="Consultando…"
            disabled={command.pending}
            onPress={loadPreview}
          />
          {previewError ? (
            <Notice
              kind="danger"
              title="Não foi possível carregar a prévia."
              messages={errorMessages(previewError)}
              actions={<Button label="Tentar a prévia de novo" onPress={loadPreview} />}
            />
          ) : null}
          <CommandNotices
            command={command}
            receipt="Importação registrada no servidor."
            reconcile={() =>
              void refetch()
                .then(() => {
                  command.clear();
                  setPreview(null);
                })
                .catch(() => undefined)
            }
            reconcileLabel="Reler feriados e pedir nova prévia"
          />
          {current ? (
            <Card title="Prévia" testID="holiday-preview-list">
              <AppText>{optionsText(current.options)}</AppText>
              {current.data.items.length === 0 ? (
                <AppText>A fonte não retornou feriados.</AppText>
              ) : null}
              {current.data.items.map((item) => (
                <AppText key={`${item.date}-${item.type}`}>
                  {`${shortDate(item.date)} · ${item.description} · ${brazilianHolidayTypeLabels[item.type]} · ${holidaySourceLabels[item.source]}${item.alreadyExists ? ' · Já existe' : ''}`}
                </AppText>
              ))}
              <AppText variant="helper">{IMPORT_REFETCH}</AppText>
            </Card>
          ) : null}
        </>
      )}
      <ConfirmDialog
        visible={visible && confirming && !!current}
        title="Importar feriados?"
        consequence={`${current ? optionsText(current.options) : ''}. ${IMPORT_REFETCH} ${CALENDAR_EFFECT}`}
        confirmLabel="Importar feriados"
        busy={command.pending}
        busyLabel="Importando…"
        onDismiss={() => setConfirming(false)}
        onConfirm={() => {
          if (!command.blocked) execute();
        }}
      />
    </Overlay>
  );
}

function HolidayDescription({
  holiday,
  visible,
  onClose,
  refetch,
}: {
  holiday: ResponseHolidayJson;
  visible: boolean;
  onClose: () => void;
  refetch: () => Promise<void>;
}) {
  const client = useScopedApiClient();
  const toast = useToast();
  const [description, setDescription] = useState(holiday.description ?? '');
  const command = useAdminCommand();
  const update = usePutHolidayHolidayId({ request: { client } });
  const [saved, setSaved] = useState(false);
  const date = datePart(holiday.date);
  return (
    <Overlay
      visible={visible}
      title={`Editar descrição de ${shortDate(holiday.date)}`}
      busy={command.pending}
      onDismiss={onClose}
      testID="holiday-description-editor"
      footer={
        <>
          <Button
            label="Salvar"
            testID="holiday-description-save"
            loading={command.pending}
            loadingLabel="Salvando…"
            disabled={command.blocked}
            onPress={() =>
              void command.run(
                () =>
                  update.mutateAsync({
                    holidayId: holiday.id,
                    data: { description: description.trim() === '' ? null : description },
                  }),
                async () => {
                  setSaved(true);
                  toast.show('Descrição salva no servidor.');
                  await refetch();
                },
              )
            }
            fullWidth
          />
          <Button
            label="Cancelar"
            variant="secondary"
            disabled={command.pending}
            onPress={onClose}
            fullWidth
          />
        </>
      }
    >
      {saved && !command.postWriteError ? (
        <Notice kind="success" title="Descrição salva no servidor." />
      ) : null}
      <CommandNotices
        command={command}
        receipt="Descrição salva no servidor."
        reconcile={() =>
          void refetch()
            .then(() => command.clear())
            .catch(() => undefined)
        }
      />
      <FormField label="Data" readOnly helper="Para mudar a data, desative e crie de novo.">
        <ReadOnlyValue value={date ? formatDateWithWeekday(date) : holiday.date} />
      </FormField>
      <FormField label="Descrição" helper="Opcional.">
        <TextField
          testID="holiday-description-edit"
          value={description}
          maxLength={500}
          onChangeText={(value) => {
            setDescription(value);
            setSaved(false);
          }}
        />
      </FormField>
    </Overlay>
  );
}

function HolidayDeactivation({
  holiday,
  visible,
  onClose,
  refetch,
}: {
  holiday: ResponseHolidayJson;
  visible: boolean;
  onClose: () => void;
  refetch: () => Promise<void>;
}) {
  const client = useScopedApiClient();
  const toast = useToast();
  const command = useAdminCommand();
  const remove = useDeleteHolidayHolidayId({ request: { client } });
  const [done, setDone] = useState(false);
  useEffect(() => {
    if (done && !command.postWriteError) onClose();
  }, [done, command.postWriteError, onClose]);
  return (
    <ConfirmDialog
      visible={visible}
      title={`Desativar feriado de ${shortDate(holiday.date)}`}
      destructive
      busy={command.pending}
      busyLabel="Desativando…"
      confirmLabel="Desativar feriado"
      consequence={`${shortDate(holiday.date)} deixa de ser feriado da filial. ${CALENDAR_EFFECT} Para mudar a data, desative e crie de novo.`}
      onDismiss={onClose}
      onConfirm={() => {
        if (command.blocked || done) return;
        void command.run(
          () => remove.mutateAsync({ holidayId: holiday.id }),
          async () => {
            toast.show('Feriado desativado.');
            // Close only after the reread: a failure keeps the confirmed receipt and its recovery.
            await refetch();
            setDone(true);
          },
        );
      }}
    >
      <CommandNotices
        command={command}
        receipt="Feriado desativado no servidor."
        reconcile={() =>
          void refetch()
            .then(() => {
              command.clear();
              onClose();
            })
            .catch(() => undefined)
        }
      />
    </ConfirmDialog>
  );
}
