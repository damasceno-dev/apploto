# LottoGest — Database Environments

## Environment mapping

- `Development` -> local developer machine -> `loto_dev_local`
- `Staging` -> published non-production environment -> `loto_staging`
- `Production` -> live environment -> `loto_prod`

## Appsettings policy

- Tracked in git:
  - `server/server.API/appsettings.json`
  - `server/server.API/appsettings.Development.json`
- Generated outside git:
  - `server/server.API/appsettings.Staging.json`
  - `server/server.API/appsettings.Production.json`

`appsettings.json` should contain only shared non-secret defaults.

`appsettings.Development.json` may contain fixed local-only bootstrap settings for `loto_dev_local`.

`appsettings.Staging.json` and `appsettings.Production.json` must be generated from a non-tracked server-side `.env` on the host before app restart.

Do not commit hosted secrets and do not bake hosted secrets into build artifacts or container images.

## Local workflow

Start PostgreSQL from `infra/`:

```bash
cd infra
docker compose up -d
```

Apply migrations and run the API from `server/`:

```bash
cd server
dotnet ef database update --project server.Infrastructure --startup-project server.API
dotnet run --project server.API
```

## Hosted workflow

1. Keep a non-tracked `.env` on the target host.
2. Set `ASPNETCORE_ENVIRONMENT` to `Staging` or `Production`.
3. Fill the required `SERVER_*` variables from `infra/.env.example`.
4. Generate the hosted appsettings file on the host:

```bash
./infra/scripts/render-server-appsettings.sh /path/to/.env
```

5. Restart the API after the correct `appsettings.{Environment}.json` is generated.

No local workflow may point to the online `loto_prod`.
