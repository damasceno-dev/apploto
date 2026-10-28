import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useSession } from '../session/provider';
import { isApiError } from '@loto/api';
import { SessionGateError, SessionSupersededError } from '../session/types';
import type { DeviceDraft, StorageScope, Stored } from '../storage/scoped';
const storageCopy =
  'Não foi possível guardar as alterações neste dispositivo. Mantenha esta tela aberta.';
/** In-memory input survives storage failure; a new resource requires fresh authorized restoration. */
export function useDeviceDraft<T>(resource: string) {
  const { store, drafts } = useSession();
  const owner = useMemo(() => ({ resource, store, drafts }), [resource, store, drafts]);
  const active = useRef<object | null>(null);
  const current = useRef<{ owner: object; draft: DeviceDraft<T> } | null>(null);
  const [state, setState] = useState({
    owner,
    memory: null as DeviceDraft<T> | null,
    problem: null as string | null,
  });
  useEffect(() => {
    active.current = owner;
    return () => {
      if (active.current === owner) active.current = null;
    };
  }, [owner]);
  const save = useCallback(
    async (input: T, base: unknown) => {
      const candidate = { revision: 'memory-only', input, base };
      current.current = { owner, draft: candidate };
      setState({ owner, memory: candidate, problem: null });
      try {
        const scope = await store.scope();
        const value = await drafts.save(scope, resource, input, base);
        if (
          active.current === owner &&
          current.current?.owner === owner &&
          current.current.draft === candidate
        ) {
          current.current = { owner, draft: value };
          setState({ owner, memory: value, problem: null });
        }
        return { ok: true as const, revision: value.revision };
      } catch {
        if (
          active.current === owner &&
          current.current?.owner === owner &&
          current.current.draft === candidate
        )
          setState({ owner, memory: candidate, problem: storageCopy });
        return { ok: false as const };
      }
    },
    [store, drafts, resource, owner],
  );
  const restore = useCallback(
    async (
      validate: (value: unknown) => value is T,
      recheck: () => Promise<boolean>,
    ): Promise<Stored<DeviceDraft<T>>> => {
      const before = current.current;
      try {
        const scope = await store.scope();
        const found = await drafts.restore(scope, resource, validate, recheck);
        if (active.current === owner && current.current === before && found.status === 'found') {
          current.current = { owner, draft: found.value };
          setState({ owner, memory: found.value, problem: null });
        }
        return found;
      } catch (error) {
        if (
          isApiError(error) ||
          error instanceof SessionGateError ||
          error instanceof SessionSupersededError
        )
          throw error;
        if (active.current === owner)
          setState({
            owner,
            memory: current.current?.owner === owner ? current.current.draft : null,
            problem: storageCopy,
          });
        return { status: 'none' };
      }
    },
    [store, drafts, resource, owner],
  );
  const acknowledge = useCallback(
    async (scope: StorageScope, revision: string) => {
      await drafts.acknowledge(scope, resource, revision);
      if (
        active.current === owner &&
        current.current?.owner === owner &&
        current.current.draft.revision === revision
      ) {
        current.current = null;
        setState({ owner, memory: null, problem: null });
      }
    },
    [drafts, resource, owner],
  );
  return {
    memory: state.owner === owner ? state.memory : null,
    problem: state.owner === owner ? state.problem : null,
    save,
    restore,
    acknowledge,
  };
}
