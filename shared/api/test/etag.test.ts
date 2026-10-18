// Shared M2 3.5: the §6.11 representation — one strong, quoted, positive decimal xmin (uint32).
import { describe, expect, it } from 'vitest';
import { parseETag, toIfMatch } from '../src/index';

describe('toIfMatch', () => {
  it.each([
    [1, '"1"'],
    [123, '"123"'],
    [4294967295, '"4294967295"'],
  ])('formats %i as %s', (version, header) => {
    expect(toIfMatch(version)).toBe(header);
  });

  it.each([0, -1, 1.5, Number.NaN, Number.POSITIVE_INFINITY, 4294967296])(
    'rejects %s',
    (version) => {
      expect(() => toIfMatch(version)).toThrow(RangeError);
    },
  );
});

describe('parseETag', () => {
  it('returns null when the header is absent', () => {
    expect(parseETag(null)).toBeNull();
    expect(parseETag(undefined)).toBeNull();
  });

  it.each([
    ['"1"', 1],
    ['"123"', 123],
    ['"4294967295"', 4294967295],
    ['"007"', 7], // the server's parser accepts leading zeros
  ])('reads %s as %i', (header, version) => {
    expect(parseETag(header)).toBe(version);
  });

  it.each([
    'W/"1"',
    '1',
    '"0"',
    '"4294967296"',
    '*',
    '"1", "2"',
    '" 1"',
    '"+1"',
    '"-1"',
    '"1e3"',
    '""',
    '"1.0"',
  ])('rejects %s', (header) => {
    expect(() => parseETag(header)).toThrow(TypeError);
  });

  it('round-trips with toIfMatch', () => {
    expect(toIfMatch(parseETag('"98765"') as number)).toBe('"98765"');
  });
});
