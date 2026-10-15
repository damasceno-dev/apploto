// `@loto/api` root entry: contract types and raw fetchers (filled by Shared M2).
// Server-safe — must never import `./queries` or `@tanstack/react-query`, directly or
// transitively. Hooks are reachable only through `@loto/api/queries`.
export {};
