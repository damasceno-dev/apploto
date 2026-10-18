// Type-level assertion helpers for the compile-time contract gates (Shared M2 2.6–2.8, 4.3).
// These files are compiled by `pnpm -r typecheck` but never imported by either package entry.

/** Exact type equality (distinguishes `string` from `string | null`, optional from required). */
export type Equal<A, B> =
  (<T>() => T extends A ? 1 : 2) extends <T>() => T extends B ? 1 : 2 ? true : false;

/** Fails compilation unless `T` is `true`. */
export type Expect<T extends true> = T;

/** `true` when `K` may be omitted from `T`. */
export type IsOptional<T, K extends keyof T> = object extends Pick<T, K> ? true : false;

/** `true` when `null` is assignable to `T`. */
export type IsNullable<T> = null extends T ? true : false;
