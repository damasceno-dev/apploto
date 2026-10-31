import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { useFocusEffect, useLocalSearchParams, useRouter } from 'expo-router';
import {
  ApiError,
  TimeEntryStatus,
  getTimeentry,
  getTimeentryTimeEntryId,
  type ResponseTimeEntryJson,
} from '@loto/api';
import {
  useGetOperator,
  useGetSetting,
  useGetTimeentry,
  useGetTimeentryTimeEntryId,
  usePutTimeentry,
  usePutTimeentrySegmentSegmentId,
  useDeleteTimeentrySegmentSegmentId,
  useDeleteTimeentryTimeEntryId,
} from '@loto/api/queries';
import {
  formatDate,
  formatInstant,
  formatDuration,
  formatSignedDuration,
  formatWallDateTime,
  parseCalendarDate,
  timeEntryStatusLabels,
  type CalendarDate,
} from '@loto/core';
import { useSession, useScopedApiClient } from '@/session/provider';
import { useShell } from '@/shell/provider';
import {
  AppText,
  Badge,
  Button,
  Card,
  Checkbox,
  ConfirmDialog,
  DateField,
  FormField,
  Notice,
  Pagination,
  ReadOnlyValue,
  Select,
  TextField,
  UncertainNotice,
  useToast,
  moveFocusTo,
} from '@/ui';
import type { View } from 'react-native';
import type { DeviceDraft } from '@/storage/scoped';
import {
  datePart,
  dayInput,
  isDayInput,
  periodFor,
  resourceStamp,
  snapshotPayload,
  validWall,
  wallInput,
  wallFromInput,
  type DayInput,
  type SegmentInput,
} from './contracts';
import { errorMessages, ReadState, useTimeCommand, useTimeReadsAllowed } from './common';
import { PeriodControls } from './reports';

export function ManagementScreen() {
  const params = useLocalSearchParams<{
    id?: string;
    section?: string;
    date?: string;
    operatorId?: string;
    from?: string;
    to?: string;
    page?: string;
    status?: string;
    mine?: string;
    inProgress?: string;
    inProgressFirst?: string;
  }>();
  const { snapshot } = useSession();
  const client = useScopedApiClient();
  const shell = useShell();
  const router = useRouter();
  const period = periodFor(datePart(snapshot.clock!.branchLocalDate)!, params.from, params.to);
  if (params.id) return <ExistingDay id={params.id} />;
  if (params.section === 'new')
    return <NewDay initialOperator={params.operatorId} initialDate={params.date} />;
  if (!period)
    return <Notice kind="danger" title="Período inválido. Escolha um período de até 366 dias." />;
  return (
    <TimeList
      period={period}
      params={params}
      apply={(values) => router.setParams(values)}
      open={(path, row) => void shell.navigate(path, true, false, row)}
      client={client}
    />
  );
}
function TimeList({
  period,
  params,
  apply,
  open,
  client,
}: {
  period: NonNullable<ReturnType<typeof periodFor>>;
  params: Record<string, string | undefined>;
  apply: (values: Record<string, string>) => void;
  open: (path: string, row?: string) => void;
  client: ReturnType<typeof useScopedApiClient>;
}) {
  const { snapshot } = useSession();
  const readEnabled = useTimeReadsAllowed();
  const operators = useGetOperator({ request: { client }, query: { enabled: readEnabled } });
  const [operator, setOperator] = useState(params.operatorId ?? '');
  const [mine, setMine] = useState(params.mine === 'true');
  const [status, setStatus] = useState(params.status ?? '');
  const [progress, setProgress] = useState(params.inProgress === 'true');
  const [first, setFirst] = useState(params.inProgressFirst !== 'false');
  const page = /^\d+$/.test(params.page ?? '') && Number(params.page) > 0 ? Number(params.page) : 1;
  const validStatus = Object.values(TimeEntryStatus).includes(params.status as TimeEntryStatus);
  const list = useGetTimeentry(
    {
      DateFrom: period.from,
      DateTo: period.to,
      ...(params.operatorId && params.mine !== 'true' ? { OperatorId: params.operatorId } : {}),
      Mine: params.mine === 'true',
      ...(validStatus ? { Status: params.status as TimeEntryStatus } : {}),
      IsInProgress: params.inProgress === 'true' ? true : undefined,
      InProgressFirst: params.inProgressFirst !== 'false',
      Page: page,
      PageSize: 20,
    },
    { request: { client }, query: { staleTime: 0, enabled: readEnabled } },
  );
  const rowRefs = useRef(new Map<string, View>());
  useFocusEffect(
    useCallback(() => {
      if (params.focus && list.data) moveFocusTo(rowRefs.current.get(params.focus));
    }, [params.focus, list.data]),
  );
  return (
    <>
      <Button
        label="Banco de horas da equipe"
        onPress={() =>
          open(
            '/gestao/ponto/banco-de-horas?' +
              new URLSearchParams({ from: period.from, to: period.to }),
          )
        }
      />
      <Button
        label="Novo registro do dia"
        testID="time-new"
        onPress={() =>
          open(
            '/gestao/ponto?' +
              new URLSearchParams({
                section: 'new',
                ...(params.operatorId ? { operatorId: params.operatorId } : {}),
                ...(params.date ? { date: params.date } : {}),
              }),
          )
        }
      />
      {snapshot.selfContext?.operatorId ? (
        <Button
          label="Registrar minhas horas"
          variant="secondary"
          onPress={() =>
            open(
              '/gestao/ponto?' +
                new URLSearchParams({
                  section: 'new',
                  operatorId: snapshot.selfContext!.operatorId!,
                }),
            )
          }
        />
      ) : null}
      <PeriodControls period={period} apply={(p) => apply({ from: p.from, to: p.to, page: '1' })} />
      <Card title="Filtros de ponto">
        <FormField label="Operador" disabled={!readEnabled}>
          <Select
            searchable
            options={[
              { value: '', label: 'Todos os operadores' },
              ...(operators.data?.data.operators ?? []).map((o) => ({
                value: o.id,
                label: o.name,
              })),
            ]}
            value={operator}
            onValueChange={(value) => {
              setOperator(value);
              if (value) setMine(false);
            }}
          />
        </FormField>
        <Checkbox
          label="Somente minhas"
          checked={mine}
          onCheckedChange={(value) => {
            setMine(value);
            if (value) setOperator('');
          }}
        />
        <FormField label="Situação" disabled={!readEnabled}>
          <Select
            value={status}
            onValueChange={setStatus}
            options={[{ value: '', label: 'Todas' }, ...statusOptions]}
          />
        </FormField>
        <Checkbox label="Somente em andamento" checked={progress} onCheckedChange={setProgress} />
        <Checkbox label="Em andamento primeiro" checked={first} onCheckedChange={setFirst} />
        <Button
          label="Aplicar filtros"
          onPress={() =>
            apply({
              operatorId: operator,
              mine: String(mine),
              status,
              inProgress: String(progress),
              inProgressFirst: String(first),
              page: '1',
            })
          }
        />
      </Card>
      <ReadState
        pending={operators.isPending}
        error={operators.error}
        retry={() => void operators.refetch()}
      />
      <ReadState pending={list.isPending} error={list.error} retry={() => void list.refetch()} />
      {list.data && !list.error ? (
        <>
          {list.data.data.items.length === 0 ? (
            <AppText>Nenhum registro neste filtro.</AppText>
          ) : null}
          {list.data.data.items.map((entry) => (
            <Card
              key={entry.id}
              title={`${formatDate(datePart(entry.date)!)} · ${entry.operatorName}`}
              testID={`time-row-${entry.id}`}
            >
              <Badge kind="status" domain="time-entry-status" value={entry.status} />
              {entry.isInProgress ? <AppText>Em andamento</AppText> : null}
              <AppText>{`Horas: ${formatDuration(entry.totalHours)} · Saldo: ${formatSignedDuration(entry.balanceHours)}`}</AppText>
              <Button
                label={`Abrir registro de ${entry.operatorName} em ${formatDate(datePart(entry.date)!)}`}
                testID={`time-open-${entry.id}`}
                ref={(node) => {
                  if (node) rowRefs.current.set(entry.id, node);
                  else rowRefs.current.delete(entry.id);
                }}
                onPress={() => open(`/gestao/ponto/${entry.id}`, entry.id)}
              />
            </Card>
          ))}
          <Pagination
            envelope={list.data.data}
            listName="registros de ponto"
            loading={list.isFetching}
            onPageChange={(next) => apply({ page: String(next) })}
          />
        </>
      ) : null}
    </>
  );
}
const statusOptions = Object.values(TimeEntryStatus).map((value) => ({
  value,
  label: timeEntryStatusLabels[value],
}));
function ExistingDay({ id }: { id: string }) {
  const readEnabled = useTimeReadsAllowed();
  const client = useScopedApiClient();
  const detail = useGetTimeentryTimeEntryId(id, {
    request: { client },
    query: { staleTime: 0, refetchOnMount: 'always', enabled: readEnabled },
  });
  const [initial, setInitial] = useState<ResponseTimeEntryJson | null>(null);
  // Seed once from this mount's successful read. Later read failures must block, not destroy, typed work.
  if (!initial && detail.isFetchedAfterMount && detail.isSuccess) setInitial(detail.data.data);
  return (
    <>
      <ReadState
        pending={!initial && !detail.error}
        error={detail.error}
        retry={() => void detail.refetch()}
      />
      {initial ? (
        <>
          <AppText testID="time-resource-ready">Registro atual carregado.</AppText>
          <DayEditor initial={initial} key={id} readUnavailable={!!detail.error} />
        </>
      ) : null}
    </>
  );
}
function NewDay({
  initialOperator,
  initialDate,
}: {
  initialOperator?: string;
  initialDate?: string;
}) {
  const { snapshot } = useSession();
  const client = useScopedApiClient();
  const readEnabled = useTimeReadsAllowed();
  const operators = useGetOperator({ request: { client }, query: { enabled: readEnabled } });
  const [operatorId, setOperator] = useState(initialOperator ?? '');
  const [date, setDate] = useState<CalendarDate | null>(
    initialDate ? parseCalendarDate(initialDate) : datePart(snapshot.clock!.branchLocalDate),
  );
  const [ready, setReady] = useState(false);
  return ready && date && operatorId ? (
    <DayEditor initial={null} newInput={{ operatorId, date, status: 'Present', segments: [] }} />
  ) : (
    <>
      <ReadState
        pending={operators.isPending}
        error={operators.error}
        retry={() => void operators.refetch()}
      />
      <FormField label="Operador" disabled={!readEnabled}>
        <Select
          searchable
          value={operatorId}
          onValueChange={setOperator}
          options={(operators.data?.data.operators ?? []).map((operator) => ({
            value: operator.id,
            label: operator.name,
          }))}
        />
      </FormField>
      <FormField label="Data" disabled={!readEnabled}>
        <DateField
          today={datePart(snapshot.clock!.branchLocalDate)!}
          value={date}
          onValueChange={setDate}
        />
      </FormField>
      <Button
        testID="time-prepare"
        label="Preparar registro"
        disabled={!date || !operators.data?.data.operators.some((o) => o.id === operatorId)}
        onPress={() => setReady(true)}
      />
    </>
  );
}
function SegmentFields({
  segment,
  change,
  locked,
  existing,
  prefix = 'segment',
}: {
  segment: SegmentInput;
  change: (segment: SegmentInput) => void;
  locked: boolean;
  existing: boolean;
  prefix?: string;
}) {
  // Full branch wall date allows overnight ClockOut without timezone conversion.
  return (
    <>
      <FormField
        label="Entrada (data e hora da filial)"
        helper="dd/mm/aaaa hh:mm"
        readOnly={existing}
        disabled={locked}
      >
        <TextField
          maxLength={40}
          testID={`${prefix}-in`}
          value={wallInput(segment.clockIn)}
          onChangeText={(clockIn) => change({ ...segment, clockIn: wallFromInput(clockIn) })}
        />
      </FormField>
      <FormField
        label="Saída (data e hora da filial)"
        helper="Deixe vazio enquanto estiver em andamento. dd/mm/aaaa hh:mm"
        disabled={locked}
      >
        <TextField
          maxLength={40}
          testID={`${prefix}-out`}
          value={segment.clockOut ? wallInput(segment.clockOut) : ''}
          onChangeText={(clockOut) =>
            change({ ...segment, clockOut: clockOut ? wallFromInput(clockOut) : null })
          }
        />
      </FormField>
    </>
  );
}
function DayEditor({
  initial,
  newInput,
  readUnavailable = false,
}: {
  initial: ResponseTimeEntryJson | null;
  newInput?: DayInput;
  readUnavailable?: boolean;
}) {
  const { engine, store, drafts } = useSession();
  const toast = useToast();
  const [auditZone] = useState(() => Intl.DateTimeFormat().resolvedOptions().timeZone);
  const auditAt = (value: string) => formatInstant(value, auditZone);
  const client = useScopedApiClient();
  const shell = useShell();
  const [base, setBase] = useState(initial);
  const [input, setInput] = useState<DayInput>(() => (initial ? dayInput(initial) : newInput!));
  const inputRef = useRef(input);
  useLayoutEffect(() => {
    inputRef.current = input;
  }, [input]);
  const command = useTimeCommand();
  const readEnabled = useTimeReadsAllowed();
  const settings = useGetSetting({
    request: { client },
    query: { staleTime: 0, enabled: readEnabled },
  });
  const lockDate = settings.data?.data.lockDate.slice(0, 10);
  const locked = !!lockDate && input.date <= lockDate;
  const [reviewRequired, setReviewRequired] = useState(false);
  const handoffRef = useRef<string | null>(null);
  const [handoffId, setHandoffId] = useState<string | null>(null);
  const blocked =
    !!handoffId ||
    readUnavailable ||
    reviewRequired ||
    command.blocked ||
    locked ||
    settings.isPending ||
    !!settings.error;
  const put = usePutTimeentry({ request: { client } });
  const edit = usePutTimeentrySegmentSegmentId({ request: { client } });
  const removeSegment = useDeleteTimeentrySegmentSegmentId({ request: { client } });
  const removeDay = useDeleteTimeentryTimeEntryId({ request: { client } });
  const [segmentEdit, setSegmentEdit] = useState<SegmentInput | null>(null);
  const [confirmation, setConfirmation] = useState<SegmentInput | 'day' | null>(null);
  const [fresh, setFresh] = useState<ResponseTimeEntryJson | null | undefined>();
  const [localError, setLocalError] = useState<unknown>(null);
  const [saved, setSaved] = useState(false);
  const [retained, setRetained] = useState<DeviceDraft<DayInput> | null>(null);
  const [revision, setRevision] = useState<string | null>(null);
  const resource = `time/${input.operatorId}/${input.date}`;
  const alive = useRef(true);
  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
    };
  }, []);
  const readCurrent = async () => {
    if (base) {
      try {
        return (await getTimeentryTimeEntryId(base.id, { client })).data;
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) return null;
        throw error;
      }
    }
    const list = (
      await getTimeentry(
        {
          OperatorId: input.operatorId,
          DateFrom: input.date,
          DateTo: input.date,
          Page: 1,
          PageSize: 1,
        },
        { client },
      )
    ).data;
    return list.items[0]
      ? (await getTimeentryTimeEntryId(list.items[0].id, { client })).data
      : null;
  };
  useEffect(() => {
    let active = true;
    void (async () => {
      const scope = await store.scope();
      const found = await drafts.restore(scope, resource, isDayInput, async () => {
        await engine.verifyEntry();
        await readCurrent();
        return engine.getSnapshot().branch?.role !== 'Member';
      });
      if (active && found.status === 'found') setRetained(found.value);
      if (active && found.status === 'corrupt')
        setLocalError(new Error('O rascunho guardado não pôde ser validado.'));
    })().catch((error) => {
      if (active) setLocalError(error);
    });
    return () => {
      active = false;
    };
    // A fresh ownership check precedes any offered restoration; it never submits.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [resource]);
  const keep = async (candidate = inputRef.current) => {
    if (!isDayInput(candidate))
      throw new Error('O texto precisa respeitar o formato deste formulário para ser guardado.');
    const scope = await store.scope();
    const draft = await drafts.save(scope, resource, candidate, base ? dayInput(base) : null);
    if (alive.current) setRevision(draft.revision);
    return { scope, revision: draft.revision, draft };
  };
  const update = (candidate: DayInput) => {
    setInput(candidate);
    inputRef.current = candidate;
    setSaved(false);
    void keep(candidate).catch(setLocalError);
  };
  const accept = async (
    result: { data: ResponseTimeEntryJson },
    retainedRevision: Awaited<ReturnType<typeof keep>>,
    candidate: DayInput,
    acknowledge: boolean,
  ) => {
    // A successful response is a receipt even if local acknowledgement/reread fails.
    // Publish its identity first so recovery reads this resource, never a second create.
    if (engine.getSnapshot().scope !== retainedRevision.scope.generation || !alive.current) return;
    setBase(result.data);
    setSaved(true);
    setConfirmation(null);
    const current = result.data.active
      ? (await getTimeentryTimeEntryId(result.data.id, { client })).data
      : result.data;
    if (engine.getSnapshot().scope !== retainedRevision.scope.generation || !alive.current) return;
    setBase(current);
    setSegmentEdit(null);
    if (JSON.stringify(inputRef.current) === JSON.stringify(candidate)) {
      setInput(dayInput(current));
      inputRef.current = dayInput(current);
    }
    if (acknowledge) {
      await drafts.acknowledge(retainedRevision.scope, resource, retainedRevision.revision);
      if (alive.current) setRevision(null);
    } else if (alive.current) setRetained(retainedRevision.draft);
  };
  const execute = (operation: 'snapshot' | 'segment' | 'deleteSegment' | 'deleteDay') => {
    if (handoffRef.current) return;
    const candidate = inputRef.current;
    const edited = segmentEdit;
    const confirmed = confirmation;
    setSaved(false);
    setLocalError(null);
    void command
      .run(
        async () => {
          const retention = await keep(candidate);
          const current = await readCurrent();
          if (resourceStamp(current) !== resourceStamp(base)) {
            setFresh(current);
            setReviewRequired(true);
            throw new Error(
              'O registro mudou. Confira a versão atual e seu texto antes de continuar.',
            );
          }
          if (operation === 'snapshot')
            return {
              result: await put.mutateAsync({ data: snapshotPayload(candidate, base) }),
              retention,
            };
          if (operation === 'segment' && edited?.id) {
            if (
              !validWall(edited.clockIn) ||
              (edited.clockOut !== null && !validWall(edited.clockOut))
            )
              throw new Error('Confira a data e os horários do segmento.');
            return {
              result: await edit.mutateAsync({
                segmentId: edited.id,
                data: { clockIn: edited.clockIn, clockOut: edited.clockOut },
              }),
              retention,
            };
          }
          if (operation === 'deleteSegment' && confirmed && confirmed !== 'day' && confirmed.id)
            return {
              result: await removeSegment.mutateAsync({ segmentId: confirmed.id }),
              retention,
            };
          if (operation === 'deleteDay' && confirmed === 'day' && base)
            return { result: await removeDay.mutateAsync({ timeEntryId: base.id }), retention };
          throw new Error('Selecione um registro atual antes de continuar.');
        },
        async ({ result, retention }) => {
          if (!base && operation === 'snapshot') {
            // Router.replace dispatches navigation; its promise does not mean the old screen unmounted.
            // Retire this editor before any asynchronous after-success work or route transition.
            handoffRef.current = result.data.id;
            setHandoffId(result.data.id);
          }
          const sameAsResponse =
            JSON.stringify(dayInput(result.data)) === JSON.stringify(candidate);
          const unchangedRemoval =
            (operation === 'deleteSegment' || operation === 'deleteDay') &&
            base !== null &&
            JSON.stringify(dayInput(base)) === JSON.stringify(candidate);
          await accept(
            result,
            retention,
            candidate,
            operation === 'snapshot' || sameAsResponse || unchangedRemoval,
          );
          if (!base && operation === 'snapshot') {
            toast.show('Registro salvo no servidor.');
            await shell.replacePreservingOrigin('/gestao/ponto/' + result.data.id);
          }
        },
      )
      .then(() => {
        if (alive.current) {
          setConfirmation(null);
          void settings.refetch();
        }
      });
  };
  const reconcile = () => {
    setLocalError(null);
    void (async () => {
      const result = await readCurrent();
      const scope = await store.scope();
      const candidate = await drafts.restore(scope, resource, isDayInput, async () => true);
      if (alive.current) {
        setFresh(result);
        if (candidate.status === 'found') setRetained(candidate.value);
      }
    })().catch((error) => {
      if (alive.current) setLocalError(error);
    });
  };
  const handoffFailure = command.postWriteError ?? localError;
  if (handoffId)
    return (
      <Notice
        kind={handoffFailure ? 'warning' : 'success'}
        title="Registro salvo no servidor."
        body={
          handoffFailure
            ? 'A abertura ou conferência local não foi concluída. Abra o registro confirmado para conferir. Nenhuma gravação será repetida.'
            : 'Abrindo o registro confirmado…'
        }
        messages={errorMessages(handoffFailure)}
        actions={
          <Button
            label="Abrir registro confirmado"
            testID="time-open-confirmed"
            disabled={command.pending}
            onPress={() => {
              void shell.replacePreservingOrigin('/gestao/ponto/' + handoffId).catch(setLocalError);
            }}
          />
        }
      />
    );
  return (
    <>
      <AppText variant="section-title">{`${formatDate(parseCalendarDate(input.date)!)} · ${base?.operatorName ?? 'Novo registro'}`}</AppText>
      <ReadState
        pending={settings.isPending}
        error={settings.error}
        retry={() => void settings.refetch()}
      />
      {locked ? (
        <Notice
          kind="warning"
          title="Dia bloqueado para alterações"
          body={`A filial está bloqueada até ${formatDate(datePart(lockDate!)!)}.${revision || retained ? ' O texto guardado está disponível para revisão.' : ''}`}
        />
      ) : null}
      {retained ? (
        <Notice
          kind="warning"
          title="Há texto guardado neste dispositivo."
          body="Confira os dados atuais antes de restaurar. Restaurar não salva no servidor."
          actions={
            <Button
              label="Restaurar para revisão"
              onPress={() => {
                setReviewRequired(true);
                setInput(retained.input);
                inputRef.current = retained.input;
                setRevision(retained.revision);
                setRetained(null);
                reconcile();
              }}
            />
          }
        />
      ) : null}
      {command.postWriteError ? (
        <Notice
          kind="warning"
          title="Registro salvo no servidor. A conferência local não foi concluída."
          messages={errorMessages(command.postWriteError)}
          actions={<Button label="Reler registro para reconciliar" onPress={reconcile} />}
        />
      ) : null}
      {command.uncertain ? (
        <UncertainNotice
          messages={errorMessages(command.error)}
          recovery={<Button label="Reler registro para reconciliar" onPress={reconcile} />}
        />
      ) : command.error ? (
        <Notice
          kind="danger"
          title="Não foi possível salvar."
          messages={errorMessages(command.error)}
        />
      ) : null}
      {localError ? (
        <Notice
          kind="danger"
          title="Confira os dados antes de continuar."
          messages={errorMessages(localError)}
          actions={<Button label="Reler registro atual" onPress={reconcile} />}
        />
      ) : null}
      {fresh !== undefined ? (
        <Card title="Registro atual para revisão">
          <AppText>
            {fresh
              ? `${fresh.operatorName} · ${timeEntryStatusLabels[fresh.status]} · ${formatDuration(fresh.totalHours)}`
              : 'Sem registro'}
          </AppText>
          {fresh?.segments
            .filter((s) => s.active)
            .map((s) => (
              <AppText
                key={s.id}
              >{`${formatWallDateTime(s.clockIn)} → ${s.clockOut ? formatWallDateTime(s.clockOut) : 'Em andamento'}`}</AppText>
            ))}
          <AppText>
            Seu texto continua guardado. Não há reenvio automático. Confira o que foi persistido
            antes de preparar outra alteração.
          </AppText>
          <Button
            label="Revisei o texto; preparar alteração"
            onPress={() => {
              setBase(fresh);
              setFresh(undefined);
              setReviewRequired(false);
              setConfirmation(null);
              command.clear();
            }}
          />
          <Button
            label="Usar registro atual e manter texto guardado"
            onPress={() => {
              setReviewRequired(false);
              setBase(fresh);
              const next = fresh ? dayInput(fresh) : { ...input, segments: [] };
              setInput(next);
              inputRef.current = next;
              setFresh(undefined);
              setSegmentEdit(null);
              setConfirmation(null);
              command.clear();
            }}
          />
        </Card>
      ) : null}
      {saved ? <Notice kind="success" title="Registro salvo no servidor." /> : null}
      {revision ? (
        <AppText>Texto guardado neste dispositivo; ainda não confirmado no servidor.</AppText>
      ) : null}
      {base && !base.active ? (
        <Notice kind="info" title="Registro removido. O dia passa a contar como sem registro." />
      ) : (
        <>
          {base ? (
            <>
              <Badge kind="status" domain="time-entry-status" value={base.status} />
              <AppText>{`Horas: ${formatDuration(base.totalHours)} · Saldo: ${formatSignedDuration(base.balanceHours)}`}</AppText>
              <ReadOnlyValue
                label="Atualizado em / por"
                value={`${auditAt(base.updatedAt ?? base.createdAt)} / ${base.updatedByUserId ?? '—'}`}
              />
            </>
          ) : null}
          <FormField label="Situação" disabled={blocked}>
            <Select
              testID="time-status"
              value={input.status}
              options={statusOptions}
              onValueChange={(status) => update({ ...input, status: status as TimeEntryStatus })}
            />
          </FormField>
          <AppText>
            A situação mantém o dia no banco de horas. Remover um registro incorreto retira o dia
            inteiro, que passa a contar como Sem registro.
          </AppText>
          {input.segments.map((segment, index) => (
            <Card key={segment.id ?? `new-${index}`} title={`Segmento ${index + 1}`}>
              <SegmentFields
                segment={segment}
                prefix={index === 0 ? 'segment' : `segment-${index + 1}`}
                locked={blocked}
                existing={!!segment.id}
                change={(next) =>
                  update({
                    ...input,
                    segments: input.segments.map((s, i) => (i === index ? next : s)),
                  })
                }
              />
              {segment.id ? (
                <>
                  <ReadOnlyValue
                    label="Atualizado em / por"
                    value={`${auditAt(base?.segments.find((s) => s.id === segment.id)?.updatedAt ?? base?.segments.find((s) => s.id === segment.id)?.createdAt ?? base!.createdAt)} / ${base?.segments.find((s) => s.id === segment.id)?.updatedByUserId ?? '—'}`}
                  />
                  <Button
                    testID={`time-edit-segment-${index + 1}`}
                    label={`Editar segmento ${index + 1}`}
                    disabled={blocked}
                    onPress={() => setSegmentEdit({ ...segment })}
                  />
                  <Button
                    testID={`time-remove-segment-${index + 1}`}
                    label={`Remover segmento ${index + 1}`}
                    variant="danger"
                    disabled={blocked}
                    onPress={() => {
                      const persisted =
                        base && dayInput(base).segments.find((s) => s.id === segment.id);
                      if (persisted) setConfirmation(persisted);
                    }}
                  />
                </>
              ) : (
                <Button
                  label="Descartar novo segmento"
                  variant="secondary"
                  disabled={blocked}
                  onPress={() =>
                    update({ ...input, segments: input.segments.filter((_, i) => i !== index) })
                  }
                />
              )}
            </Card>
          ))}
          <Button
            testID="time-add"
            label="Adicionar segmento"
            disabled={blocked}
            onPress={() =>
              update({
                ...input,
                segments: [
                  ...input.segments,
                  {
                    id: null,
                    clockIn: formatDate(parseCalendarDate(input.date)!) + ' ',
                    clockOut: null,
                  },
                ],
              })
            }
          />
          <Button
            label="Salvar dia"
            testID="time-save-day"
            disabled={blocked}
            loading={command.pending}
            loadingLabel="Salvando…"
            onPress={() => execute('snapshot')}
          />
          {base ? (
            <Button
              label="Remover registro do dia"
              variant="danger"
              disabled={blocked}
              onPress={() => setConfirmation('day')}
            />
          ) : null}
        </>
      )}
      {segmentEdit ? (
        <Card title="Editar segmento">
          <SegmentFields
            segment={segmentEdit}
            existing={false}
            prefix="segment-edit"
            locked={blocked}
            change={(next) => {
              setSegmentEdit(next);
              update({
                ...input,
                segments: input.segments.map((s) => (s.id === next.id ? next : s)),
              });
            }}
          />
          <Button
            testID="time-save-segment"
            label="Salvar segmento"
            disabled={blocked}
            loading={command.pending}
            onPress={() => execute('segment')}
          />
          <Button
            label="Cancelar edição do segmento"
            variant="secondary"
            disabled={command.pending}
            onPress={() => {
              setSegmentEdit(null);
              setReviewRequired(true);
              reconcile();
            }}
          />
        </Card>
      ) : null}
      <Button
        label="Banco de horas deste operador"
        variant="link"
        onPress={() =>
          void shell.navigate(
            '/gestao/ponto/banco-de-horas?' +
              new URLSearchParams({
                operatorId: input.operatorId,
                from: input.date,
                to: input.date,
              }),
            true,
          )
        }
      />
      <ConfirmDialog
        visible={confirmation !== null && readEnabled}
        title={confirmation === 'day' ? 'Remover registro do dia' : 'Remover segmento'}
        destructive
        busy={command.pending}
        confirmLabel="Remover"
        consequence={
          confirmation === 'day'
            ? `O registro de ${formatDate(parseCalendarDate(input.date)!)} de ${base?.operatorName ?? ''} será removido. O dia passa a contar como sem registro e sai do banco de horas. Essa ação não pode ser desfeita.`
            : confirmation
              ? `O segmento de ${formatWallDateTime(confirmation.clockIn)} a ${confirmation.clockOut ? formatWallDateTime(confirmation.clockOut) : 'Em andamento'} será removido. Essa ação não pode ser desfeita.`
              : ''
        }
        onDismiss={() => setConfirmation(null)}
        onConfirm={() => {
          if (!blocked) execute(confirmation === 'day' ? 'deleteDay' : 'deleteSegment');
        }}
      />
    </>
  );
}
