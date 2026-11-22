# DSP — Digital Services Platform

Proof of concept for replacing OutSystems at the Ministry of Economy and Tourism.
Publicly the platform is **MoET E-Services**; DSP is the internal name.

## What it shows

- E-services defined as **data** (steps, fields, validation) — the shape the OutSystems extraction pipeline produces —
  and built without code in the back-office **service builder**, published as numbered versions.
- Two **process versions** side by side (Elsa): v1 standard (submit → staff review → payment → certificate) and
  v2 **agentic** (an AI review first, which can ask the applicant to fix things and approves clear cases; everything else goes to staff with the AI's findings).
- **Multilingual** everywhere, managed at run time: add, enable, disable or delete languages and edit every text (including the ministry and product names) in the Back Office. English and Arabic ship; only one language is required.
- Two **portals**: the citizen E-Service Portal and the staff Back Office, built from one component library
  (`@dsp/ui`) on the UAE Design System (AEGOV), with React Aria and TanStack Table where AEGOV has no component.
- **Versioned APIs** (`/api/v1/...`) with OpenAPI contracts, RFC 9457 errors and paged lists.
- Two **views** of every service, from day one: a form view and a chat view. Both save to the same application through the same API.
- **Common services** used by every e-service: identity, payments, documents, notifications, workflow.

## Architecture

```
web/apps/portal (citizens)      web/apps/backoffice (staff)
          │  /api                         │  /api
          └──────────────┬────────────────┘
                   src/Gateway (YARP)
   ┌──────────┬─────────┼──────────┬───────────┬──────────────┐
Identity  E-Services  Payments  Documents  Notifications  AI Orchestrator  Platform
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
| E-Services | Service catalog and builder (versioned definitions), applications, validation, business rules, review tasks, AI review records, Elsa workflows v1 and v2 | `DspEServices`, `DspWorkflows` |
| Payments | Payment requests and (simulated) processing; publishes `PaymentCompleted` | `DspPayments` |
| Documents | Uploads and generated certificates in SeaweedFS, SHA-256, owner-scoped access | `DspDocuments` |
| Notifications | In-app notifications from `ApplicationStatusChanged` events | `DspNotifications` |
| AI Orchestrator | Chat view (an LLM with tools over the E-Services API using the user's own token, or a guided mode) and the AI review for the agentic process (model review when configured, built-in checks always) | — |
| Platform | Languages, every text of the portals and services (with import and export), AI model settings | `DspPlatform` |
| Gateway | YARP routing (`/api/v1/{service}/**`, deprecated unversioned aliases) and rate limiting; `/internal/**` is never exposed | — |

## Adding an e-service

**Without code (service builder).** Back Office → Service builder → New service. Define steps, fields, options and
validation in every enabled language, choose the process (standard or AI-assisted) and the AI review checklist, preview the
form in both languages, and publish. Every save is a draft; publishing makes it the next version. Applications keep the
version they started on, so changing a live service never affects applications in flight.

**From the extraction pipeline.** Shipped definitions in
`src/Services/EServices/Dsp.EServices.Infrastructure/Catalog/Definitions/` seed the catalog on first start. Rules beyond
field validation go in an `IServiceRules` class in `Dsp.EServices.Domain/Rules`.

Both portals, the chat view, validation and the AI review pick a service up automatically.

## Agentic process (workflow v2)

```
submit → AI review ─────┬─ fixable problem → ask the applicant (at most twice) → applicant edits and resends ─┐
                        │                                                                                  │
                        └──────────────────────────────── ok ─────────────────────────────────────────────┴→ staff review → payment → certificate
```

- The AI **never rejects** and **approves only clear cases**: when the model recommends approval with no issues,
  warnings or questions, the application goes straight to payment. Anything else, every rejection, a random audit
  sample (Settings → AI, 10% by default) and every case while the AI is paused or unavailable go to a case officer,
  with the AI's findings. When the applicant can fix something, it asks short questions in every enabled language.
- With a model configured (Back Office → Settings → AI, Net0 in production) the model reviews against the service's
  checklist; the built-in checks (missing fields, Arabic fields not in Arabic, English fields in Arabic, placeholder
  values, similar existing names, old start dates) always run as guardrails. Without a model, the built-in checks alone run.
- If the AI service is unavailable, the application goes to staff as usual. Every round is stored (`AiAssessments`).
- v1 and v2 run side by side: a service moves to v2 by publishing a new version with the AI-assisted process.

## Languages

Back Office → Settings has three pages:

- **Languages**: add a language (code, direction, its name in every language), enable, disable, reorder, make
  default or delete it, and choose whether each portal offers it (a new language starts in the back office only,
  so staff can translate it before citizens see it). One language is the default; it can't be disabled, hidden
  or deleted.
- **Translations**: every key with the default text and the chosen language's text, and where it is used (portal,
  back office, both, or service messages); search, filter, edit, add keys, import and export JSON. Changes reach both portals and every service within 30 seconds.
- **Currencies**: ISO code, name in every language, symbol and its position, decimals, enabled and default. The
  builder offers enabled currencies only and checks the fee's decimals; every amount is written with these settings.
- **AI**: provider, address, model, key (write-only, encrypted), temperature, timeout, limits, chat and review
  an emergency **Pause AI** (the model is otherwise always used once set up), the chat and AI review prompts (with reset to default; `{ministry}` and `{portal}` come from
  Translations), extra instructions, and a connection test. The platform's safety rules are always added and
  can't be edited.

The portals send `Accept-Language` with every call, so validation messages, chat, notifications and certificates
come back in the reader's language. Service definitions hold a text per language; only the default is required.

## Configuration

| Variable | Used by | Notes |
|---|---|---|
| `Jwt__Key` | all APIs | Shared signing key for platform tokens (≥ 32 bytes) |
| `Internal__ApiKey` | E-Services, Payments, Documents, Notifications, AI Orchestrator, Platform | Service-to-service calls under `/internal` |
| `Services__AiOrchestrator` | E-Services | AI review endpoint (private network) |
| `RabbitMq__Url` | E-Services, Payments, Notifications | `amqp://user:pass@host:5672` |
| `Database__Provider` | services with a database | `SqlServer` (default, FedNet) or `Postgres` (Railway POC) |
| `ConnectionStrings__<Service>` | each service | One database and one login per service ([deploy/database](deploy/database/README.md)) |
| `Database__MigrateOnStartup` | services with a database | `true` in the POC; `false` in FedNet, where the pipeline applies reviewed scripts |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | all | Sends logs, traces and metrics to the Grafana stack ([deploy/observability](deploy/observability/README.md)) |
| `UaePass__ClientId`, `UaePass__ClientSecret`, `UaePass__BaseUrl` | Identity | Staging: `sandbox_stage` / `https://stg-id.uaepass.ae/idshub` |
| `Identity__PublicBaseUrl` | Identity | Citizen portal URL (UAE PASS redirect) |
| `Staff__InitialPassword` | Identity | Seeds the first staff accounts on an empty database |
| `Storage__ServiceUrl` | Documents | SeaweedFS S3 endpoint |
| `Llm__BaseUrl`, `Llm__ApiKey`, `Llm__Model` | AI Orchestrator | Fallback only, until AI settings are saved in the Back Office. Empty = guided chat and built-in review checks |
| `Platform__SecretKey` | Platform | Encrypts the stored AI key (falls back to `Jwt__Key`) |
| `Services__Platform` | services that render texts | Platform's private address (default `http://platform.railway.internal:8080`) |

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

## Database migrations

Each service owns its EF Core migrations (SQL Server and PostgreSQL). After changing a model on a feature branch, put a
migration name in `tools/migrations/REQUEST` and push: the **Generate migrations** workflow adds migrations for every
database whose model changed, refreshes the idempotent scripts in `deploy/database/scripts` and commits them to the branch.
CI fails if a model changes without a migration.

## Working on the code

Read [AGENTS.md](AGENTS.md) first: architecture rules, API conventions, the component library, AEGOV, languages,
testing and delivery.

## Frontend

| Package | Contains |
|---|---|
| `web/packages/core` (`@dsp/core`) | API client, session, types, languages and texts loaded from Platform, formatting |
| `web/packages/ui` (`@dsp/ui`) | Every visual component: AEGOV-based shell, fields, dynamic service form, data table, reorderable list, application and AI views |
| `web/apps/portal`, `web/apps/backoffice` | Pages and TanStack Router routes only |

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
