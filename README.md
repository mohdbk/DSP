# DSP — Digital Services Platform

Proof of concept for replacing OutSystems at the Ministry of Economy and Tourism.
Publicly the platform is **MoET E-Services**; DSP is the internal name.

## What it shows

- E-services defined as **data** (steps, fields, validation) — the shape the OutSystems extraction pipeline produces.
- One **workflow** (Elsa) for the core process: submit → staff review → payment → certificate, or rejection.
- Two **portals**: the citizen E-Service Portal and the staff Back Office, sharing one form renderer.
- Two **views** of every service, from day one: a form view and a chat view. Both save to the same application through the same API.
- **Common services** used by every e-service: identity, payments, documents, notifications, workflow.

## Architecture

```
web/apps/portal (citizens)      web/apps/backoffice (staff)
          │  /api                         │  /api
          └──────────────┬────────────────┘
                   src/Gateway (YARP)
   ┌──────────┬─────────┼──────────┬───────────┬──────────────┐
Identity  E-Services  Payments  Documents  Notifications  AI Orchestrator
 (UAE PASS, (Elsa       │          │            │          (chat view; calls
  staff)    workflow)   │          │            │           E-Services API)
   │          │         │          │            │
 SQL Server or PostgreSQL (one database per service) · RabbitMQ (events) · SeaweedFS (files)
```

Each service follows clean architecture:

| Layer | Contains | Depends on |
|---|---|---|
| `*.Domain` | Entities, business rules, invariants | Shared kernel only |
| `*.Application` | Use cases, ports (interfaces), DTOs | Domain, contracts |
| `*.Infrastructure` | EF Core, Elsa, RabbitMQ, S3, HTTP clients | Application, building blocks |
| `*.Api` | Minimal API endpoints, composition root | Infrastructure |

Services never share databases. They talk through REST (with OpenAPI-style contracts) and RabbitMQ events (`src/BuildingBlocks/Dsp.Contracts`).

## Services

| Service | Responsibility | Database |
|---|---|---|
| Identity | Citizen sign-in through pluggable providers (UAE PASS now), staff sign-in (local accounts now, AD later), platform tokens | `DspIdentity` |
| E-Services | Service catalog, applications, validation, business rules, review tasks, Elsa workflow | `DspEServices`, `DspWorkflows` |
| Payments | Payment requests and (simulated) processing; publishes `PaymentCompleted` | `DspPayments` |
| Documents | Uploads and generated certificates in SeaweedFS, SHA-256, owner-scoped access | `DspDocuments` |
| Notifications | In-app notifications from `ApplicationStatusChanged` events | `DspNotifications` |
| AI Orchestrator | Chat view: an LLM with tools over the E-Services API using the user's own token, or a guided mode when no model is configured | — |
| Gateway | YARP routing and rate limiting; `/internal/**` is never exposed | — |

## Adding an e-service

1. Add a definition to `src/Services/EServices/Dsp.EServices.Infrastructure/Catalog/Definitions/` (steps, fields, fee).
2. If it has rules beyond field validation, add an `IServiceRules` class in `Dsp.EServices.Domain/Rules` and register it.
3. Both portals, the chat view and validation pick it up automatically.

## Configuration

| Variable | Used by | Notes |
|---|---|---|
| `Jwt__Key` | all APIs | Shared signing key for platform tokens (≥ 32 bytes) |
| `Internal__ApiKey` | E-Services, Payments, Documents | Service-to-service calls under `/internal` |
| `RabbitMq__Url` | E-Services, Payments, Notifications | `amqp://user:pass@host:5672` |
| `Database__Provider` | services with a database | `SqlServer` (default, FedNet) or `Postgres` (Railway POC) |
| `ConnectionStrings__<Service>` | each service | One database and one login per service ([deploy/database](deploy/database/README.md)) |
| `Database__MigrateOnStartup` | services with a database | `true` in the POC; `false` in FedNet, where the pipeline applies reviewed scripts |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | all | Sends logs, traces and metrics to the Grafana stack ([deploy/observability](deploy/observability/README.md)) |
| `UaePass__ClientId`, `UaePass__ClientSecret`, `UaePass__BaseUrl` | Identity | Staging: `sandbox_stage` / `https://stg-id.uaepass.ae/idshub` |
| `Identity__PublicBaseUrl` | Identity | Citizen portal URL (UAE PASS redirect) |
| `Staff__InitialPassword` | Identity | Seeds the first staff accounts on an empty database |
| `Storage__ServiceUrl` | Documents | SeaweedFS S3 endpoint |
| `Llm__BaseUrl`, `Llm__ApiKey`, `Llm__Model` | AI Orchestrator | Any OpenAI-compatible endpoint (Net0 in FedNet). Empty = guided mode |

## Running locally

Requires .NET 10, Node 22, SQL Server, RabbitMQ and SeaweedFS (or leave `Storage__ServiceUrl` empty to store files on disk).

```bash
dotnet build Dsp.sln
cd web && npm install && npm run dev:portal   # http://localhost:5173, proxies /api to the gateway on :8080
```

## Reliability

- **Transactional outbox.** Services never publish to RabbitMQ directly. Events are written to an `OutboxMessages`
  table in the same transaction as the business change, and a background dispatcher sends them after commit on a
  publisher-confirm channel, retrying in order until the broker accepts them.
- **Idempotent consumers.** Each handled event ID is recorded in `InboxMessages`; redeliveries are skipped.
  Handlers also check current state, so a retry after a partial failure is safe.
- **Dead-letter queues.** A message that fails twice goes to `dsp.<service>.dead-letter` instead of being dropped.

## Tests and CI

```bash
dotnet test Dsp.sln
```

| Project | Covers |
|---|---|
| `tests/Dsp.UnitTests` | Validation, business rules, application state machine, use cases (with fakes), UAE PASS client, staff sign-in, tokens, payments, outbox/inbox, guided chat, every service definition in the catalog |
| `tests/Dsp.ArchitectureTests` | Clean architecture rules: Domain and Application stay free of frameworks and infrastructure; services never reference each other |

GitHub Actions (`.github/workflows`):

- **CI:** build, tests with coverage, vulnerable NuGet packages, model changes without a migration, portal type check and build, `npm audit`.
- **Security:** CodeQL (C#, TypeScript), Trivy (dependencies, secrets, Dockerfile misconfiguration), Gitleaks, dependency review on pull requests; weekly schedule.
- **Dependabot:** NuGet, npm, Docker base images and Actions.

## POC shortcuts (not for production)

- Payments are simulated; the government payment gateway plugs into `IPaymentProcessor`.
- Staff sign in with local accounts; Active Directory plugs into `IStaffAuthenticator`.
- The Railway POC runs on PostgreSQL because SQL Server crashes on Railway hosts; FedNet uses SQL Server with the same code.
- Services apply their own migrations at startup on Railway; FedNet applies the reviewed scripts with a separate migrator login.
- Grafana runs as a single all-in-one container without persistent storage.
- The outbox dispatcher assumes one replica per service; add row locking before scaling out.
