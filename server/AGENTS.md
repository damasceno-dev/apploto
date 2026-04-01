# Server Agent Notes

## Structure

- Keep the DDD-style split: `server.API`, `server.Application`, `server.Communication`, `server.Domain`, `server.Exceptions`, `server.Infrastructure`

## Contract guidance

- `server.Communication` remains the DTO boundary
- `server.Exceptions` stays backend-only
- Emit a stable OpenAPI spec for frontend and mobile generation
- Fix nullability and required metadata so required C# fields stay required in generated TS

## Auth direction

- Manual backend auth remains the preferred direction for now

## Important decisions

- The backend owns the API contract.
- Frontend and mobile consumers depend on generated TS artifacts, not shared C# DTO assemblies.
- Keep the API error contract stable and serializable for both web and mobile.
