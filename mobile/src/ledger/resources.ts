import {
  getClientClientId,
  getTransactionInstallmentOriginTransactionId,
  getTransactionTransactionId,
} from '@loto/api';
import type { SessionEngine } from '@/session/engine';
import { matchDestination } from '@/shell/policy';
import { statementAllowed } from '@/admin/clients';

/**
 * Fresh validation of an M6 resource origin/intent: the exact transaction, plan or client must be
 * readable now, in this branch, by the current caller (server scope), and the statement/payment
 * routes additionally need the caller's current statement access. A stored id grants nothing.
 */
export async function validateLedgerResource(
  engine: SessionEngine,
  path: string,
): Promise<boolean> {
  const snapshot = engine.getSnapshot();
  if (snapshot.status !== 'branch' || !snapshot.branch || !snapshot.clock) return false;
  const url = new URL(path, 'https://navigation.invalid');
  const destination = matchDestination(url.pathname);
  const parts = url.pathname.split('/');
  const options = { client: engine.client() };
  try {
    switch (destination?.key) {
      case 'edit': {
        const row = await getTransactionTransactionId(parts[2]!, options);
        return (
          row.data.branchId === snapshot.branch.id && engine.getSnapshot().scope === snapshot.scope
        );
      }
      case 'plan': {
        const plan = await getTransactionInstallmentOriginTransactionId(parts[3]!, options);
        return (
          plan.data.originTransactionId === parts[3] &&
          engine.getSnapshot().scope === snapshot.scope
        );
      }
      case 'statement':
      case 'payment': {
        if (!statementAllowed(snapshot)) return false;
        const client = await getClientClientId(parts[2]!, options);
        return (
          client.data.branchId === snapshot.branch.id &&
          engine.getSnapshot().scope === snapshot.scope
        );
      }
      default:
        return false;
    }
  } catch {
    return false;
  }
}
