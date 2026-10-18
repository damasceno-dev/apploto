import { operations } from '../generated/operations/operations';
import { ApiConfigurationError } from './errors';

/**
 * The token an operation requires, generated from its published `security` (server H4):
 * `anonymous` (`security: []`), `identity` (`IdentityBearer`, the global token from login, register
 * or renew-token) or `branch` (`BranchBearer`, the token from `POST /branch/session`). Both tokens
 * travel as `Authorization: Bearer`. The roles the server lists on `BranchBearer` are contract
 * documentation, not app-side permissions, so they are not part of the tier.
 */
export type AuthTier = (typeof operations)[number]['authTier'];

/** What the contract says about one operation: its token tier and its declared header parameters. */
export interface OperationContract {
  readonly authTier: AuthTier;
  /** Declared header parameter names, lower-cased (for example `idempotency-key`, `if-match`). */
  readonly headers: ReadonlySet<string>;
}

interface CompiledOperation extends OperationContract {
  readonly method: string;
  readonly segments: readonly (string | null)[];
  readonly literals: number;
}

let compiled: readonly CompiledOperation[] | undefined;

function compile(): readonly CompiledOperation[] {
  compiled ??= operations.map((operation) => {
    const segments = operation.path
      .split('/')
      .slice(1)
      .map((segment) => (segment.startsWith('{') && segment.endsWith('}') ? null : segment));
    return {
      method: operation.method,
      segments,
      literals: segments.filter((segment) => segment !== null).length,
      authTier: operation.authTier,
      headers: new Set(operation.headers.map((name) => name.toLowerCase())),
    };
  });
  return compiled;
}

/**
 * Resolves a concrete request path against the generated operation templates. The template with
 * the most literal segments wins (`/transaction/installment` over `/transaction/{transactionId}`);
 * generation rejects any pair that could tie.
 */
export function resolveOperation(method: string, path: string): OperationContract {
  const segments = path.split('/').slice(1);
  let best: CompiledOperation | undefined;
  for (const operation of compile()) {
    if (operation.method !== method || operation.segments.length !== segments.length) continue;
    const matches = operation.segments.every(
      (segment, index) => segment === null || segment === segments[index],
    );
    if (matches && (best === undefined || operation.literals > best.literals)) best = operation;
  }
  if (best === undefined) {
    throw new ApiConfigurationError(`${method} ${path} is not an operation in the API contract.`);
  }
  return best;
}
