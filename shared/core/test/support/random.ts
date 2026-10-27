// A seeded PRNG (mulberry32) for property-style checks: reproducible runs without a new dependency.
export function seeded(seed: number): () => number {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/** An integer in [min, max] from `random`. */
export function integer(random: () => number, min: number, max: number): number {
  return min + Math.floor(random() * (max - min + 1));
}

// Code points spelled out, so a test can never pass with a visually identical ASCII character.
export const NBSP = String.fromCodePoint(0x00a0);
export const MINUS = String.fromCodePoint(0x2212);
export const NNBSP = String.fromCodePoint(0x202f);
export const TIMES = String.fromCodePoint(0x00d7);
export const DIVIDE = String.fromCodePoint(0x00f7);
export const EM_DASH_CHAR = String.fromCodePoint(0x2014);
