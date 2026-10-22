import 'server-only';
import { validateApiBaseUrl, validateWebOrigin } from './env-validation.ts';

export const serverEnv = Object.freeze({
  apiBaseUrl: validateApiBaseUrl(process.env.LOTO_API_BASE_URL),
  webOrigin: validateWebOrigin(process.env.LOTO_WEB_ORIGIN),
  /** Technical end-to-end fixtures; only meaningful in a build made with the same flag. */
  e2eFixtures: process.env.LOTO_WEB_E2E_FIXTURES === '1',
});
