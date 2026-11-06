import { useCallback, useEffect, useState } from 'react';
import { useSession } from '@/session/provider';
import type { StorageScope } from '@/storage/scoped';
import { isWorkstationChoice, type WorkstationChoice } from './contracts';

/**
 * "Em qual caixa você vai operar hoje?" — kept per verified identity (UserId + canonical-email
 * hash), branch, device and branch-local day. It is presentation only: it never grants a cashbox,
 * and a choice from another day or for a cashbox no longer granted is ignored and removed.
 */
const keyOf = (scope: StorageScope) =>
  `loto.workstation.v1:${scope.userId}:${scope.emailKey}:${scope.branchId}`;
export function useWorkstationChoice(today: string) {
  const { store, snapshot } = useSession();
  const [state, setState] = useState<{
    readonly status: 'loading' | 'ready';
    readonly choice: WorkstationChoice | null;
    readonly problem: string | null;
    readonly scope: number;
    readonly today: string;
  }>({ status: 'loading', choice: null, problem: null, scope: snapshot.scope, today });
  useEffect(() => {
    let active = true;
    void (async () => {
      const scope = await store.scope();
      const raw = await store.storage.getItem(keyOf(scope));
      store.assert(scope);
      let choice: WorkstationChoice | null = null;
      try {
        const parsed: unknown = raw === null ? null : JSON.parse(raw);
        choice = isWorkstationChoice(parsed) && parsed.date === today ? parsed : null;
      } catch {
        choice = null;
      }
      // The next branch-local day (server clock) clears the choice.
      if (raw !== null && !choice) await store.storage.removeItem(keyOf(scope));
      if (active)
        setState({ status: 'ready', choice, problem: null, scope: scope.generation, today });
    })().catch(() => {
      if (active)
        setState({
          status: 'ready',
          choice: null,
          problem: 'Não foi possível ler a escolha de caixa guardada neste dispositivo.',
          scope: snapshot.scope,
          today,
        });
    });
    return () => {
      active = false;
    };
  }, [store, today, snapshot.scope]);
  const choose = useCallback(
    async (accountId: string) => {
      const choice: WorkstationChoice = { v: 1, date: today, accountId };
      setState((s) => ({ ...s, choice, problem: null }));
      try {
        const scope = await store.scope();
        await store.storage.setItem(keyOf(scope), JSON.stringify(choice));
        store.assert(scope);
      } catch {
        setState((s) => ({
          ...s,
          problem: 'Não foi possível guardar a escolha de caixa neste dispositivo.',
        }));
      }
    },
    [store, today],
  );
  const clear = useCallback(async () => {
    setState((s) => ({ ...s, choice: null }));
    try {
      const scope = await store.scope();
      await store.storage.removeItem(keyOf(scope));
    } catch {
      /* The in-memory choice is already cleared; a stale device copy is ignored by date. */
    }
  }, [store]);
  const current = state.scope === snapshot.scope && state.today === today;
  return {
    status: current ? state.status : ('loading' as const),
    choice: current ? state.choice : null,
    problem: current ? state.problem : null,
    choose,
    clear,
  };
}
