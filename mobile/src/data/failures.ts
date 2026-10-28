import { isApiError, isOutcomeUncertain } from '@loto/api';
export interface ActionFailure {
  readonly kind:
    'not-sent' | 'uncertain' | 'auth-refused' | 'rate-limited' | 'conflict' | 'rejected';
  readonly messages: readonly string[];
  readonly recovery: string;
  readonly retryAfter: string | null;
}
export function actionFailure(error: unknown): ActionFailure {
  const messages = isApiError(error) ? error.errorMessages : [];
  const retryAfter = isApiError(error) ? (error.headers?.get('retry-after') ?? null) : null;
  if (
    isOutcomeUncertain(error) ||
    (isApiError(error) &&
      error.dispatched &&
      error.method !== 'GET' &&
      error.kind === 'invalid-response')
  )
    return {
      kind: 'uncertain',
      messages,
      retryAfter,
      recovery: 'Não foi possível confirmar se a ação foi concluída.',
    };
  const kind =
    !isApiError(error) || !error.dispatched
      ? 'not-sent'
      : error.status === 401 || error.status === 403
        ? 'auth-refused'
        : error.status === 429
          ? 'rate-limited'
          : error.status === 409
            ? 'conflict'
            : 'rejected';
  return {
    kind,
    messages,
    retryAfter,
    recovery:
      kind === 'conflict'
        ? 'Recarregue e confira os dados antes de continuar.'
        : messages.length
          ? ''
          : 'Não foi possível concluir a ação. Verifique sua conexão e tente novamente.',
  };
}
