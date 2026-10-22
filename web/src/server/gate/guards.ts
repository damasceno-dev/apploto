import 'server-only';
import type { Route } from 'next';
import { cookies } from 'next/headers';
import { redirect } from 'next/navigation';
import type { SessionSnapshot } from '@/lib/bff-protocol';
import { decideGate } from '@/lib/route-policy';
import { readSessionCookies } from '../session/cookies';
import { snapshotFromCookies } from '../session/service';

/** The session the request's cookies describe (no network): seeds the client providers. */
export async function serverSnapshot(): Promise<SessionSnapshot> {
  return snapshotFromCookies(readSessionCookies(await cookies()));
}

/**
 * Layout guard for branch-scoped segments, checked before any branch fetch in the segment. The
 * proxy already resolved the gate (and stored the intent); this repeats the decision so a render
 * can never start without a branch session, whatever path reached it.
 */
export async function requireBranchSession(): Promise<
  SessionSnapshot & { readonly state: 'branch' }
> {
  const snapshot = await serverSnapshot();
  const decision = decideGate('branch', snapshot);
  // For a branch route the gate allows only the `branch` state.
  if (decision.kind === 'redirect') gateRedirect(decision.to);
  return snapshot as SessionSnapshot & { readonly state: 'branch' };
}

/** Layout guard for identity-tier segments (Branch picker, account screens). */
export async function requireIdentity(): Promise<SessionSnapshot> {
  const snapshot = await serverSnapshot();
  const decision = decideGate('identity', snapshot);
  if (decision.kind === 'redirect') gateRedirect(decision.to);
  return snapshot;
}

/**
 * Redirects to a gate destination from the shell blueprint's route map. Web M4 builds those screens,
 * so typed routes cannot know them yet; this is the single place that asserts them as routes.
 */
export function gateRedirect(to: string): never {
  redirect(to as Route);
}
