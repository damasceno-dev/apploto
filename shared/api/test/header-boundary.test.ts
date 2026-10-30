// S06 review round 2: the generation-time header boundary keeps the caller's options headers apart
// from the generated ones in both Orval outputs.
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { separateCallerHeaders } from '../scripts/header-boundary.ts';

const generatedDir = join(import.meta.dirname, '../src/generated');

describe('separateCallerHeaders', () => {
  it.each([
    [
      "headers: { 'Content-Type': 'application/json',...headers, ...getHeaders(options?.headers) }",
      "headers: { 'Content-Type': 'application/json',...headers }, callerHeaders: options?.headers",
    ],
    [
      "headers: { 'Content-Type': 'application/json', ...getHeaders(options?.headers) }",
      "headers: { 'Content-Type': 'application/json' }, callerHeaders: options?.headers",
    ],
    [
      'headers: { ...headers, ...getHeaders(options?.headers) }',
      'headers: { ...headers }, callerHeaders: options?.headers',
    ],
  ])('separates %s', (merge, separated) => {
    expect(
      separateCallerHeaders(`apiFetch(url, { ...options, method: 'POST', ${merge} })`, 'op'),
    ).toBe(`apiFetch(url, { ...options, method: 'POST', ${separated} })`);
  });

  it('fails generation on a merge shape it does not recognize', () => {
    expect(() =>
      separateCallerHeaders('headers: new Headers([...getHeaders(options?.headers)])', 'postThing'),
    ).toThrow(/postThing: Orval emitted a header merge/);
  });
});

describe('committed generated output', () => {
  const files = readdirSync(generatedDir, { recursive: true, withFileTypes: true })
    .filter((entry) => entry.isFile())
    .map((entry) => readFileSync(join(entry.parentPath, entry.name), 'utf8'));
  const count = (needle: string) =>
    files.reduce((total, text) => total + text.split(needle).length - 1, 0);

  it('keeps no lossy merge and separates every one in both outputs', () => {
    expect(count('getHeaders(options?.headers)')).toBe(0);
    expect(count('callerHeaders: options?.headers')).toBe(132);
  });
});
