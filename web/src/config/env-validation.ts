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
