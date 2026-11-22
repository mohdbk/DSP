# Databases

Every service owns its database. No service has credentials for another service's database.

| Service | Databases (FedNet / SQL Server) | Databases (POC / PostgreSQL) | Login(s) |
|---|---|---|---|
| Identity | `DspIdentity` | `identity` | `dsp_identity` |
| E-Services | `DspEServices`, `DspWorkflows` (Elsa) | `eservices`, `workflows` | `dsp_eservices` |
| Payments | `DspPayments` | `payments` | `dsp_payments` |
| Documents | `DspDocuments` | `documents` | `dsp_documents` |
| Notifications | `DspNotifications` | `notifications` | `dsp_notifications` |

## Migrations

Schema changes are EF Core migrations, kept per provider in each service:
`src/Services/<Svc>/Dsp.<Svc>.Infrastructure/Persistence/Migrations/{SqlServer,Postgres}`.

Each service has one model (`<Svc>DbContext`, abstract) and two thin subclasses
(`<Svc>SqlServerDbContext`, `<Svc>PostgresDbContext`), so each provider has its own history.

Add a migration after changing a model:

```bash
MIGRATION_NAME=AddSomething tools/migrations/generate.sh
```

This adds the migration for both providers and refreshes the reviewable idempotent scripts in
`deploy/database/scripts/{SqlServer,Postgres}/<service>.sql`. Commit all of it in the same pull request.

## Applying migrations

| Environment | How |
|---|---|
| POC (Railway) | `Database__MigrateOnStartup=true`: each service applies its pending migrations at startup. |
| FedNet | The pipeline applies `deploy/database/scripts/SqlServer/*.sql` with the `_migrator` login after DBA review. Services run with `Database__MigrateOnStartup=false` and the runtime login (read/write only). A service logs a warning at startup if migrations are pending. |

Elsa manages its own workflow schema (`DspWorkflows`) with its bundled migrations.

## Provisioning logins

- **FedNet (SQL Server):** `sqlserver/provision.sql` creates each database, a runtime login with `db_datareader` + `db_datawriter`, and a `_migrator` login with `db_owner`.
- **POC (PostgreSQL):** `postgres/provision.sh` runs as a one-off job and creates one role per service that owns only its databases; `PUBLIC` cannot connect to them.
