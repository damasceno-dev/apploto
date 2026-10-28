export function validateApiBaseUrl(value: unknown): string {
  if (typeof value !== 'string' || !value.trim())
    throw new Error('A API do aplicativo não está configurada.');
  let url: URL;
  try {
    url = new URL(value);
  } catch {
    throw new Error('A URL da API é inválida.');
  }
  if (
    !['https:', 'http:'].includes(url.protocol) ||
    url.username ||
    url.password ||
    url.search ||
    url.hash
  )
    throw new Error('A URL da API é inválida.');
  if (url.protocol === 'http:' && !__DEV__) throw new Error('A API deve usar HTTPS.');
  return url.toString().replace(/\/$/, '');
}
// The app shell alone reads configuration. Shared never reads Expo environment variables.
export function readApiConfiguration(): string {
  return validateApiBaseUrl(process.env.EXPO_PUBLIC_API_BASE_URL);
}
