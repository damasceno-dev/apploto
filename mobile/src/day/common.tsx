import { useCallback, useRef, useState } from 'react';
import { ApiError, isOutcomeUncertain } from '@loto/api';
import { formatDate, formatInstantTime, formatInstant, parseCalendarDate } from '@loto/core';
import { useSession } from '@/session/provider';
import { secureUuid } from '@/storage/native';
import { Button, Notice, UncertainNotice } from '@/ui';
import { errorMessages } from '@/time/common';
import { deviceZone } from './contracts';

export { errorMessages };
export { useTimeReadsAllowed as useDayReads } from '@/time/common';

export const businessDate = (date: string) =>
  parseCalendarDate(date.slice(0, 10)) ? formatDate(date.slice(0, 10) as never) : date;
export const auditTime = (instant: string | null | undefined) =>
  instant ? formatInstantTime(instant, deviceZone()) : null;
export const auditInstant = (instant: string | null | undefined) =>
  instant ? formatInstant(instant, deviceZone()) : null;

/**
 * One session command (Start, Conferir, Recontar, Submit, Passar o caixa, …). Never replayed on its
 * own. Idempotent endpoints get one key per logical intent, held in memory only: an explicit retry
 * after an unconfirmed outcome resends the identical request and key; any different request while
 * that outcome is unknown is refused until a fresh read reconciles it. A confirmed result is kept
 * even if the follow-up read fails, and the same command is not sendable again.
 */
export function useDayCommand() {
  const { engine, snapshot } = useSession();
  const flight = useRef(false);
  const held = useRef<{
    readonly fingerprint: string;
    readonly key: string;
    readonly send: (key: string) => Promise<unknown>;
    readonly accept: (value: unknown) => Promise<void> | void;
  } | null>(null);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [uncertain, setUncertain] = useState(false);
  const [postWriteError, setPostWriteError] = useState<unknown>(null);
  const scope = snapshot.scope;
  const dispatch = useCallback(
    async (
      fingerprint: string,
      key: string,
      send: (key: string) => Promise<unknown>,
      accept: (value: unknown) => Promise<void> | void,
    ) => {
      flight.current = true;
      setPending(true);
      setError(null);
      setPostWriteError(null);
      held.current = { fingerprint, key, send, accept };
      let confirmed = false;
      try {
        const value = await send(key);
        confirmed = true;
        held.current = null;
        setUncertain(false);
        if (engine.getSnapshot().scope === scope) await accept(value);
      } catch (failure) {
        if (engine.getSnapshot().scope !== scope) return;
        if (confirmed) setPostWriteError(failure);
        else {
          setError(failure);
          const unknown = isOutcomeUncertain(failure);
          setUncertain(unknown);
          // A definite refusal releases the intent: a corrected request is a new operation.
          if (!unknown) held.current = null;
        }
      } finally {
        flight.current = false;
        if (engine.getSnapshot().scope === scope) setPending(false);
      }
    },
    [engine, scope],
  );
  const allowed = () =>
    !flight.current && engine.getSnapshot().scope === scope && !engine.getSnapshot().writesPaused;
  return {
    pending,
    error,
    uncertain,
    postWriteError,
    blocked: pending || uncertain || !!postWriteError || snapshot.writesPaused,
    /** Clear after an explicit fresh read reconciled the outcome. */
    clear: () => {
      held.current = null;
      setError(null);
      setUncertain(false);
      setPostWriteError(null);
    },
    /** Start a new intent: refused while another intent's outcome is unknown. */
    run: async <T,>(
      fingerprint: string,
      send: (key: string) => Promise<T>,
      accept: (value: T) => Promise<void> | void,
    ) => {
      if (!allowed() || postWriteError) return;
      if (held.current && uncertain) return;
      await dispatch(
        fingerprint,
        secureUuid(),
        send as (key: string) => Promise<unknown>,
        accept as (value: unknown) => void,
      );
    },
    /** Explicit verification of an unconfirmed outcome: the identical request and key, once. */
    retry: async () => {
      const intent = held.current;
      if (!intent || !uncertain || !allowed()) return;
      await dispatch(intent.fingerprint, intent.key, intent.send, intent.accept);
    },
    /** Whether a request with this fingerprint is the unresolved one (a changed input is not). */
    holds: (fingerprint: string) => held.current?.fingerprint === fingerprint,
  };
}
export type DayCommand = ReturnType<typeof useDayCommand>;

/** Verbatim refusal, unknown outcome and confirmed-receipt notices for one session command. */
export function CommandState({
  command,
  receipt,
  reread,
  rereadLabel = 'Reler e conferir',
  testID,
}: {
  command: DayCommand;
  receipt: string;
  reread: () => void;
  rereadLabel?: string;
  testID?: string;
}) {
  if (command.postWriteError)
    return (
      <Notice
        kind="warning"
        testID={testID ? `${testID}-post-write` : undefined}
        title={`${receipt} A conferência local não foi concluída.`}
        body="Nada será enviado de novo. Releia os dados para continuar."
        messages={errorMessages(command.postWriteError)}
        actions={<Button label={rereadLabel} onPress={reread} />}
      />
    );
  if (command.uncertain)
    return (
      <UncertainNotice
        testID={testID ? `${testID}-uncertain` : undefined}
        messages={errorMessages(command.error)}
        recovery={
          <>
            <Button
              label="Verificar e tentar novamente"
              testID={testID ? `${testID}-retry` : undefined}
              onPress={() => void command.retry()}
            />
            <Button label={rereadLabel} variant="secondary" onPress={reread} />
          </>
        }
      />
    );
  if (command.error)
    return (
      <Notice
        kind="danger"
        testID={testID ? `${testID}-error` : undefined}
        title="O servidor não aceitou a ação."
        body="O que você digitou continua na tela."
        messages={errorMessages(command.error)}
        actions={<Button label={rereadLabel} variant="secondary" onPress={reread} />}
      />
    );
  return null;
}

export const httpStatus = (error: unknown) => (error instanceof ApiError ? error.status : null);
