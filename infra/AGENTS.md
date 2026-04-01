# Infrastructure Agent Notes

## Direction

- Keep infrastructure co-located in the repo
- Infrastructure should stay outside the JS workspace boundaries
- Docker Compose should cover local services when needed

## Current note

- Hosting and IaC direction still needs a final decision
- Existing notes mention checking Hostinger and IaC options before locking deployment setup

## Important decisions

- `infra` stays outside the workspace-linked JS runtime code.
- Keep local developer setup practical first, then refine hosting and IaC once the product shape is clearer.
