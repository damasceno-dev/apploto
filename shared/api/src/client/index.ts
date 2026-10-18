// Hand-written client core (Shared M2 Phase 3). Pure and injected: no environment, storage,
// cookies or platform APIs, and nothing runs at import time.
export { type AuthTier } from './operations';
export {
  clearApiClientConfiguration,
  configureApiClient,
  createApiClient,
  type AccessTokenProvider,
  type AccessTokenRequest,
  type ApiClient,
  type ApiClientConfig,
} from './config';
export {
  ApiConfigurationError,
  ApiError,
  isApiConfigurationError,
  isApiError,
  isOutcomeUncertain,
  type ApiErrorBody,
  type ApiErrorDetails,
  type ApiErrorKind,
} from './errors';
export { parseETag, toIfMatch } from './etag';
export { type ApiRequestOptions } from './fetch';
