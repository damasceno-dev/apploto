import { useCallback, useEffect, useRef, useState } from 'react';
import { useSession } from '@/session/provider';
import { Button, Notice, UncertainNotice } from '@/ui';
import type { DeviceDraft } from '@/storage/scoped';
import { errorMessages, useTimeCommand, useTimeReadsAllowed } from '@/time/common';

export { ReadState, errorMessages } from '@/time/common';
/** Branch reads wait for verified authority, connection and foreground (same gate as M9). */
export const useAdminReads = useTimeReadsAllowed;
/**
 * Single-flight, scope-fenced, never-replayed admin write. An uncertain outcome or a failure after
 * a confirmed write blocks every later command until the screen explicitly reconciles.
 */
export const useAdminCommand = useTimeCommand;
export type AdminCommand = ReturnType<typeof useAdminCommand>;

/** Verbatim refusal, unknown outcome and confirmed-receipt states for one admin command. */
export function CommandNotices({
  command,
  receipt,
  reconcile,
  reconcileLabel = 'Reler dados para conferir',
}: {
  command: AdminCommand;
  receipt: string;
  reconcile: () => void;
  reconcileLabel?: string;
}) {
  if (command.postWriteError)
    return (
      <Notice
        kind="warning"
        title={`${receipt} A conferência local não foi concluída.`}
        body="Nada será enviado de novo. Releia os dados para continuar."
        messages={errorMessages(command.postWriteError)}
        actions={<Button label={reconcileLabel} onPress={reconcile} />}
      />
    );
  if (command.uncertain)
    return (
      <UncertainNotice
        messages={errorMessages(command.error)}
        recovery={<Button label={reconcileLabel} onPress={reconcile} />}
      />
    );
  if (command.error)
    return (
      <Notice
        kind="danger"
        title="Não foi possível salvar."
        body="Seu texto continua no formulário."
        messages={errorMessages(command.error)}
      />
    );
  return null;
}

/**
 * Scoped device retention for one admin editor. Restoration needs a fresh, feature-owned check
 * (role, branch and resource) and never submits; storage failure keeps the in-memory candidate.
 */
export function useAdminDraft<T>(
  resource: string,
  validate: (value: unknown) => value is T,
  recheck: () => Promise<boolean>,
) {
  const { store, drafts, engine } = useSession();
  const [retained, setRetained] = useState<DeviceDraft<T> | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const revision = useRef<string | null>(null);
  const kept = useRef<{ scope: Awaited<ReturnType<typeof store.scope>>; revision: string } | null>(
    null,
  );
  // Saves and an explicit discard run in order, so a late save cannot resurrect a discarded draft.
  const chain = useRef<Promise<unknown>>(Promise.resolve());
  const closed = useRef(false);
  const alive = useRef(true);
  const check = useRef(recheck);
  useEffect(() => {
    check.current = recheck;
  }, [recheck]);
  useEffect(() => {
    alive.current = true;
    let active = true;
    revision.current = null;
    void (async () => {
      const scope = await store.scope();
      const found = await drafts.restore(scope, resource, validate, async () => {
        await engine.verifyEntry();
        return check.current();
      });
      if (!active) return;
      if (found.status === 'found') setRetained(found.value);
      if (found.status === 'corrupt') setProblem('O texto guardado não pôde ser validado.');
    })().catch(() => {
      if (active) setProblem(null);
    });
    return () => {
      active = false;
      alive.current = false;
    };
    // The validator is a stable module function; restoration runs once per resource.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [resource, store, drafts, engine]);
  const keep = useCallback(
    (input: T, base: unknown) => {
      const next = chain.current.then(async () => {
        if (closed.current) return null;
        try {
          const scope = await store.scope();
          const draft = await drafts.save(scope, resource, input, base);
          revision.current = draft.revision;
          kept.current = { scope, revision: draft.revision };
          if (alive.current) setProblem(null);
          return { scope, revision: draft.revision };
        } catch {
          if (alive.current)
            setProblem(
              'Não foi possível guardar o texto neste dispositivo. Mantenha esta tela aberta.',
            );
          return null;
        }
      });
      chain.current = next.catch(() => undefined);
      return next;
    },
    [store, drafts, resource],
  );
  /** Explicit cancel: the person abandons this unsent input, so its device copy is removed. */
  const discard = useCallback(() => {
    closed.current = true;
    chain.current = chain.current.then(async () => {
      const last = kept.current;
      if (last)
        await drafts.acknowledge(last.scope, resource, last.revision).catch(() => undefined);
    });
    return chain.current;
  }, [drafts, resource]);
  const acknowledge = useCallback(
    async (kept: Awaited<ReturnType<typeof keep>>) => {
      if (!kept) return;
      await drafts.acknowledge(kept.scope, resource, kept.revision);
      if (revision.current === kept.revision) revision.current = null;
    },
    [drafts, resource],
  );
  return {
    retained,
    dismissRetained: () => setRetained(null),
    problem,
    keep,
    acknowledge,
    discard,
  };
}

/** Restore offer for a retained candidate: restoring fills the form for review, never submits. */
export function RetainedNotice({
  visible,
  onRestore,
}: {
  visible: boolean;
  onRestore: () => void;
}) {
  return visible ? (
    <Notice
      kind="warning"
      title="Há texto guardado neste dispositivo."
      body="Confira os dados atuais antes de restaurar. Restaurar não salva no servidor."
      actions={<Button label="Restaurar para revisão" onPress={onRestore} />}
    />
  ) : null;
}
