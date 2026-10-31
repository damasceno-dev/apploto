import { useRef, useState } from 'react';
import { ApiError, isOutcomeUncertain } from '@loto/api';
import { useSession } from '@/session/provider';
import { Button, Notice, Skeleton } from '@/ui';

export function useTimeReadsAllowed() {
  const { snapshot } = useSession();
  return (
    snapshot.status === 'branch' &&
    snapshot.online &&
    snapshot.foreground &&
    !['checking', 'idle'].includes(snapshot.selfStatus)
  );
}

export function ReadState({
  pending,
  error,
  retry,
}: {
  pending: boolean;
  error: unknown;
  retry: () => void;
}) {
  if (pending) return <Skeleton label="Carregando registros…" />;
  return error ? (
    <Notice
      kind="danger"
      title="Não foi possível carregar os registros."
      messages={errorMessages(error)}
      actions={<Button label="Tentar novamente" onPress={retry} />}
    />
  ) : null;
}
export function errorMessages(error: unknown): readonly string[] {
  return error instanceof ApiError
    ? error.errorMessages
    : error instanceof Error && error.name !== 'AbortError'
      ? [error.message]
      : [];
}
/** Time writes are never replayed. Uncertainty blocks all later commands until explicit reconciliation. */
export function useTimeCommand() {
  const { engine, snapshot } = useSession();
  const flight = useRef(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [uncertain, setUncertain] = useState(false);
  const [postWriteError, setPostWriteError] = useState<unknown>(null);
  const scope = snapshot.scope;
  return {
    pending,
    error,
    uncertain,
    postWriteError,
    blocked: pending || uncertain || !!postWriteError || snapshot.writesPaused,
    clear: () => {
      setError(null);
      setUncertain(false);
      setPostWriteError(null);
    },
    run: async <T,>(write: () => Promise<T>, accept: (value: T) => Promise<void> | void) => {
      if (
        flight.current ||
        uncertain ||
        postWriteError ||
        engine.getSnapshot().scope !== scope ||
        engine.getSnapshot().writesPaused
      )
        return;
      flight.current = true;
      setPending(true);
      setError(null);
      setPostWriteError(null);
      let confirmed = false;
      try {
        const result = await write();
        confirmed = true;
        if (engine.getSnapshot().scope === scope) await accept(result);
      } catch (failure) {
        if (engine.getSnapshot().scope === scope) {
          if (confirmed) setPostWriteError(failure);
          else {
            setError(failure);
            setUncertain(isOutcomeUncertain(failure));
          }
        }
      } finally {
        flight.current = false;
        if (engine.getSnapshot().scope === scope) setPending(false);
      }
    },
  };
}
