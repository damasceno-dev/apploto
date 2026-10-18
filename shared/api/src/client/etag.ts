// Canonical §6.11 optimistic-concurrency wire contract: a strong HTTP entity tag holding one quoted
// positive decimal PostgreSQL `xmin` (an unsigned 32-bit value), for example `"123"`. Weak tags
// (`W/`), unquoted values, zero, `*` and comma-separated lists are invalid. These helpers mirror
// the server's parser; they never cache or persist a version (it is a short-lived UI token).

const maxVersion = 4_294_967_295;
const quotedDecimal = /^"([0-9]+)"$/;

/** Formats a response `version` as the `If-Match` header value the server accepts. */
export function toIfMatch(version: number): string {
  if (!Number.isSafeInteger(version) || version < 1 || version > maxVersion) {
    throw new RangeError(
      `An xmin version must be an integer from 1 to ${maxVersion}; got ${version}.`,
    );
  }
  return `"${version}"`;
}

/**
 * Reads a response `ETag` header (`result.headers.get('ETag')`) as its version. Returns `null`
 * when the header is absent; throws for any value outside the §6.11 representation.
 */
export function parseETag(header: string | null | undefined): number | null {
  if (header === null || header === undefined) return null;
  const match = quotedDecimal.exec(header);
  const version = match ? Number(match[1]) : Number.NaN;
  if (!Number.isSafeInteger(version) || version < 1 || version > maxVersion) {
    throw new TypeError(
      `Not a strong quoted positive decimal xmin ETag: ${JSON.stringify(header)}.`,
    );
  }
  return version;
}
