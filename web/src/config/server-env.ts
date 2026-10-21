import 'server-only';
import { validateApiBaseUrl } from './env-validation.ts';

export const serverEnv = Object.freeze({
  apiBaseUrl: validateApiBaseUrl(process.env.LOTO_API_BASE_URL),
});
