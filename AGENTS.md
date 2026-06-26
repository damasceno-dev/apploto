# LotoApp Agent Notes

This file is the canonical bootstrap document for a new Codex project for LotoApp.

## Current target structure

```text
web/
mobile/
server/
design/
infra/
shared/
```

## Global rules

- Keep the repo split as `web`, `mobile`, `server`, `design`, `infra`, and `shared`.
- Keep `pnpm workspaces` so `web` and `mobile` can consume shared runtime TS code cleanly.
- Treat `design/` as non-runtime: tokens, specs, assets, references.
- Treat `shared/` as cross-platform runtime TS code only.

## Core architecture decisions

- `.NET` in `server/` is the source of truth for API contracts.
- Frontend and mobile sharing happens through OpenAPI and generated TypeScript, not shared C# assemblies.
- `shared/api` is the first shared runtime package.
- `shared/core` is a **day-one** package (decided 2026-06-26): web and mobile need BRL/CPF/date formatters and pt-BR enum-label maps from the start.
- `design/` is not a runtime UI package at this stage.
- `web` and `mobile` own their own UI components.
- Shared runtime reuse is limited to API and pure TS helpers.

## Area guides

Read the relevant file before implementing work in that area:

- `shared/AGENTS.md`
- `web/AGENTS.md`
- `mobile/AGENTS.md`
- `server/AGENTS.md`
- `design/AGENTS.md`
- `infra/AGENTS.md`
