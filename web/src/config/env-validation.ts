export function validateApiBaseUrl(value: string | undefined): string {
  if (!value)
    throw new Error('LOTO_API_BASE_URL é obrigatória no servidor. Consulte .env.example.');
  let url: URL;
  try {
    url = new URL(value);
  } catch {
    throw new Error('LOTO_API_BASE_URL deve ser uma URL absoluta HTTP(S).');
  }
  if (
    !['http:', 'https:'].includes(url.protocol) ||
    url.username ||
    url.password ||
    url.search ||
    url.hash
  ) {
    throw new Error('LOTO_API_BASE_URL deve usar HTTP(S), sem credenciais, query ou fragmento.');
  }
  return value;
}

/** Optional public origin of the web app (behind a proxy); unset means the request's own origin. */
export function validateWebOrigin(value: string | undefined): string | null {
  if (value === undefined || value === '') return null;
  let url: URL;
  try {
    url = new URL(value);
  } catch {
    throw new Error('LOTO_WEB_ORIGIN deve ser uma origem absoluta HTTP(S).');
  }
  if (!['http:', 'https:'].includes(url.protocol) || url.origin !== value) {
    throw new Error('LOTO_WEB_ORIGIN deve ser somente a origem, por exemplo https://app.example.');
  }
  return value;
}
