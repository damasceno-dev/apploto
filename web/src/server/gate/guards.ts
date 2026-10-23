import 'server-only';
import type { Route } from 'next';
import { cookies } from 'next/headers';
import { notFound, redirect } from 'next/navigation';
import type { SessionSnapshot } from '@/lib/bff-protocol';
import { isBranchId } from '@/lib/branch-route';
import { decideGate, gateDestinations } from '@/lib/route-policy';
import { readSessionCookies } from '../session/cookies';
import { snapshotFromCookies } from '../session/service';

/** The identity-level session the request's cookies describe (no network): seeds the client. */
export async function serverSnapshot(): Promise<SessionSnapshot> {
  return snapshotFromCookies(readSessionCookies(await cookies()));
}

/**
 * Layout guard for the `/f/{branchId}` segment, checked before any branch fetch in it. The proxy
 * already resolved this branch for the signed-in identity (renewing or opening its session, or
 * answering 403); this repeats the decision from the cookies so a render can never start without
 * that identity's own session for the route's branch, whatever path reached it.
 */
export async function requireBranchSession(
  branchId: string,
): Promise<SessionSnapshot & { readonly state: 'branch' }> {
  if (!isBranchId(branchId)) notFound();
  const snapshot = snapshotFromCookies(readSessionCookies(await cookies()), branchId);
  const decision = decideGate('branch', snapshot, { branchId });
  if (decision.kind === 'redirect') gateRedirect(decision.to);
  // No slot for this branch although the proxy passed: let the picker reopen it.
  if (snapshot.state !== 'branch') gateRedirect(gateDestinations.branchPicker);
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
