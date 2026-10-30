import { useEffect, useRef, useState, type ReactNode } from 'react';
import { useGetTimeentry, useGetTimeentryTimeEntryId, usePutTimeentry } from '@loto/api/queries';
import {
  addDays,
  formatDate,
  formatDuration,
  formatSignedDuration,
  formatWallTime,
  formatWallDateTime,
} from '@loto/core';
import { useScopedApiClient, useSession } from '@/session/provider';
import { useShell } from '@/shell/provider';
import { AppText, Badge, Button, Card, Notice, UncertainNotice } from '@/ui';
import { datePart, memberTap } from './contracts';
import { errorMessages, ReadState, useTimeCommand, useTimeReadsAllowed } from './common';
import { useClockAttempt } from './attempt';

function wallMilliseconds(value: string) {
  // Offset-free branch wall clock; UTC is used only as an arithmetic container, never converted.
  const [date, time] = value.split('T');
  if (!date || !time) return null;
  const [year, month, day] = date.split('-').map(Number);
  const [hour, minute, second = 0] = time.split(':').map(Number);
  return Date.UTC(year!, month! - 1, day, hour, minute, second);
}
function Elapsed({ clockIn }: { clockIn: string }) {
  const { snapshot } = useSession();
  const observation = snapshot.clock?.branchLocalDateTime;
  const [observed] = useState(() => ({ value: observation, at: performance.now() }));
  const [elapsed, setElapsed] = useState(0);
  useEffect(() => {
    if (snapshot.writesPaused || !snapshot.foreground || !snapshot.online) return;
    const timer = setInterval(
      () => setElapsed(Math.max(0, performance.now() - observed.at)),
      60000,
    );
    return () => clearInterval(timer);
  }, [snapshot.writesPaused, snapshot.foreground, snapshot.online, observed]);
  const start = wallMilliseconds(clockIn),
    now = observed.value && wallMilliseconds(observed.value);
  if (start === null || !now || snapshot.writesPaused || elapsed > 60000) return null;
  return (
    <AppText>{`Tempo decorrido: ${formatDuration(Math.max(0, now - start + elapsed) / 3600000)}`}</AppText>
  );
}
export function ClockScreen() {
  const { snapshot } = useSession();
  const client = useScopedApiClient();
  const shell = useShell();
  const today = datePart(snapshot.clock!.branchLocalDate)!;
  const yesterday = addDays(today, -1);
  const linked = snapshot.selfContext?.operatorId;
  const readEnabled = useTimeReadsAllowed();
  const allowed = snapshot.branch?.role === 'Member' && readEnabled;
  const list = useGetTimeentry(
    { Mine: true, DateFrom: yesterday, DateTo: today, Page: 1, PageSize: 10 },
    { request: { client }, query: { enabled: allowed, staleTime: 0 } },
  );
  const current = list.data?.data.items.find((row) => datePart(row.date) === today);
  const overnight = list.data?.data.items.find(
    (row) => datePart(row.date) === yesterday && row.isInProgress,
  );
  const detail = useGetTimeentryTimeEntryId(current?.id ?? '', {
    request: { client },
    query: { enabled: allowed && !!current, staleTime: 0 },
  });
  const observation = snapshot.clock!.branchLocalDateTime;
  const previousObservation = useRef(observation);
  useEffect(() => {
    if (previousObservation.current === observation) return;
    previousObservation.current = observation;
    if (allowed) {
      void list.refetch();
      if (current) void detail.refetch();
    }
  }, [observation, allowed, list, detail, current]);
  const mutation = usePutTimeentry({ request: { client } });
  const command = useTimeCommand();
  const attempt = useClockAttempt(linked);
  const segments = detail.data?.data.segments.filter((s) => s.active) ?? [];
  const open = segments.find((s) => !s.clockOut);
  const blocked =
    command.blocked ||
    attempt.blocked ||
    !linked ||
    list.isFetching ||
    !!list.error ||
    (!!current && (detail.isFetching || !!detail.error || !detail.data));
  const tap = (date: typeof today, action: 'Open' | 'Close') =>
    void command.run(
      async () => {
        const holding = await attempt.begin(date, action);
        await mutation.mutateAsync({ data: memberTap(linked!, date, action) });
        return holding;
      },
      async (holding) => {
        await attempt.confirmed(holding);
        await list.refetch();
        if (current) await detail.refetch();
      },
    );
  return (
    <>
      <AppText>{`${formatDate(today)} · ${snapshot.selfContext?.operatorName ?? 'Operador sem vínculo'}`}</AppText>
      <ReadState pending={list.isPending} error={list.error} retry={() => void list.refetch()} />
      {!linked ? (
        <Notice
          kind="info"
          title="Seu login ainda não está vinculado a um operador."
          body="Peça à administração da filial para conferir seu vínculo."
        />
      ) : null}
      {attempt.status === 'checking' ? (
        <ReadState pending error={null} retry={attempt.retryCheck} />
      ) : attempt.status === 'failed' ? (
        <Notice
          kind="danger"
          title="Não foi possível conferir toques guardados."
          messages={errorMessages(attempt.error)}
          actions={<Button label="Conferir novamente" onPress={attempt.retryCheck} />}
        />
      ) : null}
      {attempt.held && !command.pending ? (
        <AttemptReviewNotice
          uncertain={!command.postWriteError && (command.uncertain || !command.error)}
          messages={errorMessages(command.postWriteError ?? command.error)}
          confirmed={!!command.postWriteError}
          recovery={
            <>
              <AppText>{`${attempt.held.draft.input.action === 'Open' ? 'Entrada' : 'Saída'} de ${formatDate(attempt.held.draft.input.date)} precisa de conferência. Nenhum toque será reenviado automaticamente.`}</AppText>
              <Button
                label="Reler registros"
                loading={attempt.reading}
                onPress={() => {
                  void attempt.reread();
                  void list.refetch();
                  if (current) void detail.refetch();
                }}
              />
              {attempt.review !== null ? (
                <>
                  <Card title={`Registro relido de ${formatDate(attempt.held.draft.input.date)}`}>
                    {attempt.review.length === 0 ? (
                      <AppText>Sem registro</AppText>
                    ) : (
                      attempt.review.map((entry) => (
                        <Card key={entry.id}>
                          <Badge kind="status" domain="time-entry-status" value={entry.status} />
                          <AppText>{`Horas: ${formatDuration(entry.totalHours)} · Saldo: ${formatSignedDuration(entry.balanceHours)}`}</AppText>
                          {entry.segments
                            .filter((segment) => segment.active)
                            .map((segment) => (
                              <AppText
                                key={segment.id}
                              >{`${formatWallDateTime(segment.clockIn)} → ${segment.clockOut ? formatWallDateTime(segment.clockOut) : 'Em andamento'}`}</AppText>
                            ))}
                        </Card>
                      ))
                    )}
                  </Card>
                  <AppText>
                    Confira os registros atuais. A releitura não comprova sozinha o resultado do
                    toque anterior.
                  </AppText>
                  <Button
                    label="Revisei os registros; continuar"
                    onPress={() =>
                      void attempt.reviewed().then((accepted) => {
                        if (accepted) command.clear();
                      })
                    }
                  />
                </>
              ) : null}
              {attempt.error ? (
                <Notice
                  kind="danger"
                  title="Não foi possível conferir o registro."
                  messages={errorMessages(attempt.error)}
                />
              ) : null}
            </>
          }
        />
      ) : command.postWriteError ? (
        <Notice
          kind="warning"
          title="Ponto registrado no servidor. Releia os registros para conferir a tela."
          messages={errorMessages(command.postWriteError)}
          actions={
            <Button
              label="Reler registros"
              onPress={() => {
                void list.refetch().then((result) => {
                  if (!result.error) command.clear();
                });
                if (current) void detail.refetch();
              }}
            />
          }
        />
      ) : command.error ? (
        <Notice
          kind="danger"
          title="Não foi possível registrar o ponto."
          messages={errorMessages(command.error)}
        />
      ) : null}
      {!list.isPending && !list.error && linked ? (
        <>
          {overnight ? (
            <Card title="Seu turno de ontem ainda está aberto." accent="warning">
              <Button
                label="Encerrar o turno de ontem"
                testID="time-close-yesterday"
                disabled={blocked}
                loading={command.pending}
                loadingLabel="Registrando…"
                onPress={() => tap(yesterday, 'Close')}
              />
              {!open ? (
                <Button
                  label="Começar o dia de hoje"
                  testID="time-open-today"
                  disabled={blocked}
                  onPress={() => tap(today, 'Open')}
                />
              ) : null}
            </Card>
          ) : null}
          <Card title="Estado de hoje" variant="next-action">
            <AppText variant="next-action">
              {open
                ? `Em andamento desde ${formatWallTime(open.clockIn.slice(11))}`
                : current
                  ? 'Dia encerrado'
                  : 'Fora do ponto'}
            </AppText>
            {open ? (
              <Elapsed key={snapshot.clock!.branchLocalDateTime + open.id} clockIn={open.clockIn} />
            ) : null}
            {!overnight || open ? (
              <Button
                label={open ? 'Saída' : 'Entrada'}
                testID="time-tap"
                fullWidth
                disabled={blocked}
                loading={command.pending}
                loadingLabel="Registrando…"
                onPress={() => tap(today, open ? 'Close' : 'Open')}
              />
            ) : null}
          </Card>
          <ReadState
            pending={!!current && detail.isPending}
            error={detail.error}
            retry={() => void detail.refetch()}
          />
          <Card title="Registros de hoje">
            {current ? (
              <>
                <Badge kind="status" domain="time-entry-status" value={current.status} />
                <AppText>{`Horas: ${formatDuration(current.totalHours)} · Saldo: ${formatSignedDuration(current.balanceHours)}`}</AppText>
              </>
            ) : (
              <AppText>Nenhum registro hoje.</AppText>
            )}
            {segments.map((segment) => (
              <AppText
                key={segment.id}
              >{`Entrada ${formatWallTime(segment.clockIn.slice(11))} · Saída ${segment.clockOut ? formatWallTime(segment.clockOut.slice(11)) : 'Em andamento'}`}</AppText>
            ))}
          </Card>
        </>
      ) : null}
      <Button
        label="Meu banco de horas"
        variant="link"
        onPress={() => void shell.navigate('/turno/banco-de-horas', true)}
      />
    </>
  );
}

function AttemptReviewNotice({
  uncertain,
  messages,
  recovery,
  confirmed,
}: {
  uncertain: boolean;
  confirmed: boolean;
  messages: readonly string[];
  recovery: ReactNode;
}) {
  return uncertain ? (
    <UncertainNotice messages={messages} recovery={recovery} />
  ) : (
    <Notice
      kind="warning"
      title={
        confirmed
          ? 'Ponto registrado no servidor. A conferência local não foi concluída.'
          : 'Confira o registro antes de continuar.'
      }
      messages={messages}
      actions={recovery}
    />
  );
}
