'use client';

import { useEffect, useState, type FormEvent } from 'react';
import { useGetBranchMyBranches } from '@loto/api/queries';
import { useSession } from '@/client/session/session-provider';
import type {
  LogoutResult,
  SessionCommandFailure,
  SessionCommandResult,
} from '@/client/session/session-client';
import { landingFor } from '@/lib/route-policy';

type Outcome = SessionCommandResult | LogoutResult | SessionCommandFailure | null;

// A session command changes the scope, which remounts this subtree; the last result survives here.
let lastOutcome: Outcome = null;

/**
 * Technical session controls for Playwright (identity level: this page is outside any branch).
 * Holds the password only for the single request. Opening a branch reports where this tab would
 * land (`landingFor`); the test navigates there, as the M4 picker will.
 */
export function SessionFixture() {
  const session = useSession();
  const [outcome, setOutcomeState] = useState<Outcome>(() => lastOutcome);
  const setOutcome = (next: Outcome) => {
    lastOutcome = next;
    setOutcomeState(next);
  };
  const [readBranches, setReadBranches] = useState(false);
  const branches = useGetBranchMyBranches({ query: { enabled: readBranches } });
  const { readBranchChoice } = session;
  const needsList =
    (session.snapshot.state === 'choose-branch' ||
      session.snapshot.state === 'branch-unavailable') &&
    session.snapshot.branches === null;
  // The server render knows only what the cookies say; a choice needs the branch list (as the M4
  // Branch picker will read it), so the fixture reads it when it lacks one.
  useEffect(() => {
    if (needsList) void readBranchChoice();
  }, [needsList, readBranchChoice]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    form.reset();
    setOutcome(
      await session.login(String(data.get('email') ?? ''), String(data.get('password') ?? '')),
    );
  }

  const landing =
    outcome !== null && 'snapshot' in outcome && outcome.snapshot !== null
      ? landingFor(outcome.snapshot)
      : null;

  return (
    <div>
      <pre data-testid="session">{JSON.stringify(session.snapshot)}</pre>
      <form onSubmit={submit}>
        <label>
          E-mail <input name="email" type="email" autoComplete="off" />
        </label>
        <label>
          Senha <input name="password" type="password" autoComplete="off" />
        </label>
        <button type="submit">Entrar</button>
      </form>
      <ul>
        {(session.snapshot.branches ?? []).map((branch) => (
          <li key={branch.id}>
            <button
              type="button"
              onClick={async () => setOutcome(await session.selectBranch(branch.id))}
            >
              Abrir {branch.name}
            </button>
          </li>
        ))}
      </ul>
      <button type="button" onClick={async () => setOutcome(await session.logout())}>
        Sair
      </button>
      <button type="button" onClick={() => setReadBranches(true)}>
        Ler filiais
      </button>
      <pre data-testid="outcome">{JSON.stringify(outcome)}</pre>
      <p data-testid="landing">{landing ?? '-'}</p>
      <p data-testid="identity-read">
        {branches.data
          ? `filiais:${branches.data.data.branches.length}`
          : branches.isError
            ? 'erro'
            : 'ocioso'}
      </p>
    </div>
  );
}
