import type { AuthTier, Role } from '@loto/api';
import type { SessionSnapshot } from './types';
export type SessionGateDecision =
  'allow' | 'login' | 'branch' | 'loading' | 'recovery' | 'forbidden';
/** Navigation mode is deliberately absent: only verified identity/branch/role can open a gate. */
export function sessionGate(
  snapshot: SessionSnapshot,
  tier: AuthTier,
  roles?: readonly Role[],
): SessionGateDecision {
  if (tier === 'anonymous') return 'allow';
  if (
    snapshot.status === 'starting' ||
    snapshot.status === 'recovering' ||
    snapshot.status === 'closing'
  )
    return 'loading';
  if (snapshot.status === 'anonymous' || !snapshot.user)
    return snapshot.status === 'storage-error' ? 'recovery' : 'login';
  if (
    !snapshot.online ||
    !snapshot.foreground ||
    ['offline', 'storage-error', 'renewal-unconfirmed'].includes(snapshot.status)
  )
    return 'recovery';
  if (tier === 'identity') return 'allow';
  if (snapshot.status !== 'branch' || !snapshot.branch || !snapshot.clock) return 'branch';
  if (roles && !roles.includes(snapshot.branch.role)) return 'forbidden';
  return 'allow';
}

/** Platform routes use the freshly verified profile flag, independently of branch role/mode. */
export function platformGate(snapshot: SessionSnapshot): SessionGateDecision {
  const identity = sessionGate(snapshot, 'identity');
  if (identity !== 'allow') return identity;
  return snapshot.user?.isPlatformOperator === true ? 'allow' : 'forbidden';
}
