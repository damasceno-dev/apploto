// `@loto/api` root entry: generated contract types and raw functions, plus the client
// configuration API. Server-safe — it must never import `./queries`, `src/generated/queries`,
// React or `@tanstack/*`, directly or transitively (`scripts/check-boundary.ts` enforces it).
export * from './generated/model';
export * from './generated/endpoints';
export * from './client';
