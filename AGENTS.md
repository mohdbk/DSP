# AGENTS.md — how we build DSP

Rules for everyone changing this repository, people and coding agents alike. If a change breaks one of these
rules, change the rule here first (in the same pull request) and say why.

DSP (Digital Services Platform) is the internal name of the Ministry of Economy and Tourism's e-services platform;
the public name is **MoET E-Services**. It replaces OutSystems. The unit name on documents is **Digital Transformation**.

## Repository map

| Path | What it is |
|---|---|
| `src/BuildingBlocks/Dsp.SharedKernel` | Entity base, domain exceptions, `FieldError`, `PagedList` |
| `src/BuildingBlocks/Dsp.Contracts` | Integration events shared between services (the only shared types) |
| `src/BuildingBlocks/Dsp.BuildingBlocks` | Hosting defaults, auth, API conventions, persistence, messaging (outbox/inbox) |
| `src/Services/<Service>/Dsp.<Service>.{Domain,Application,Infrastructure,Api}` | One microservice, clean architecture |
| `src/Gateway/Dsp.Gateway` | YARP gateway: the only public entry to the APIs |
| `web/packages/core` | `@dsp/core`: API client, session, types, language (English/Arabic), formatting — no UI |
| `web/packages/ui` | `@dsp/ui`: the component library used by both portals |
| `web/apps/portal`, `web/apps/backoffice` | The citizen portal and the staff back office: pages only |
| `tests/` | Unit tests and architecture tests |
| `deploy/` | Database provisioning and scripts, observability |
| `tools/migrations` | EF migration generator |

## Backend architecture

### Clean architecture (enforced by `tests/Dsp.ArchitectureTests`)

| Layer | Contains | May depend on |
|---|---|---|
| Domain | Entities, value objects, invariants, domain rules | Shared kernel only. No EF, ASP.NET, HTTP, messaging |
| Application | Use cases, ports (interfaces), DTOs | Domain, contracts. No infrastructure or frameworks (logging abstractions are fine) |
| Infrastructure | EF Core, Elsa, RabbitMQ, HTTP clients, storage, external providers | Application, building blocks |
| Api | Endpoints and composition root only | Infrastructure (to wire it up) |

- Business rules live in the domain (state changes go through entity methods that guard their invariants).
  Use cases orchestrate; endpoints only translate HTTP to a use case call and back.
- Ports are interfaces in Application, implemented in Infrastructure. Use cases take every dependency as a
  required constructor parameter: no optional or nullable dependencies, no service locator.
- Repositories and HTTP clients live in Infrastructure only.
- A service never references another service's projects. Services share only `Dsp.Contracts` and the building blocks.

### Services and data

- Each service owns its database and its own database login. No service reads another's tables.
- Services talk through REST (public `/api/v1/...` or service-to-service `/internal/...` with `X-Internal-Key`)
  and RabbitMQ integration events.
- Events are written to the **outbox** in the same transaction as the change (`IEventBus.PublishAsync` before
  `SaveChangesAsync`) and published with publisher confirms. Consumers use the **inbox** to skip duplicates and
  must be idempotent. Poison messages go to `dsp.<service>.dead-letter`.
- Production database is SQL Server (FedNet); the Railway POC uses PostgreSQL. Code must work on both
  (`Database:Provider`).

### Migrations

- Never use `EnsureCreated`. Every model change needs an EF Core migration for **both** providers.
- On a feature branch, put the migration name in `tools/migrations/REQUEST` and push: the *Generate migrations*
  workflow adds the migrations and idempotent scripts (`deploy/database/scripts`) and commits them.
- CI fails when a model changes without a migration. Migrations apply on startup in the POC
  (`Database__MigrateOnStartup=true`); in FedNet the pipeline applies the reviewed scripts.

### Workflows and the AI

- Elsa workflows are code. Each activity calls exactly one `ProcessSteps` method; the workflow only decides order.
- Process versions run side by side: v1 standard, v2 AI-assisted. A service changes process by publishing a new
  service version; applications in flight finish on the version they started.
- **The AI never approves or rejects.** It recommends, lists findings for the officer and may ask the applicant to
  fix something (at most twice). If the AI is unavailable, the application goes to staff as usual.
- The AI Orchestrator acts with the user's own token over the public API (chat) or through `/internal/ai/review`
  (pre-review). Built-in checks always run as guardrails next to any model. Models are reached through an
  OpenAI-compatible endpoint (Net0 in production) — never hard-code a vendor.

### Service definitions

- E-services are data (`ServiceDefinition`), edited in the back-office service builder and published as numbered
  versions (draft → published → retired). Never edit a published version.
- Every label, help text, option and pattern message has English **and** Arabic; the builder refuses to publish
  without them (`ServiceDefinitionRules`).

## API conventions

- **Versioning.** Public endpoints live under `/api/v{n}/{service}` (`app.MapApi("service")`); responses carry
  `api-version`. A breaking change means a new version next to the old one; never change a published version's
  contract. Removing a field, renaming, changing a type or meaning, or tightening validation is breaking; adding an
  optional field or a new endpoint is not.
- **Deprecation.** Old paths keep working through the gateway with `Deprecation`, `Sunset` and
  `Link: rel="successor-version"` headers until the sunset date. The unversioned first-release paths sunset on
  30 June 2027 (the UAE PASS redirect URI uses one; re-register it before then).
- **Contracts.** Each service publishes OpenAPI at `/api/v{n}/{service}/openapi.json` (`app.MapApiDocuments`).
  Keep endpoint parameter and return types explicit so the document stays accurate.
- **JSON.** Plain JSON, camelCase, ISO 8601 dates in UTC. We don't use JSON:API envelopes.
- **Errors.** RFC 9457 problem details (`application/problem+json`): `title`, `status`, `detail`, plus `errors`
  (`[{ field, message }]`) for validation and `correlationId`. Validation 422, domain conflicts 409, not found or not
  yours 404, forbidden 403. Never leak stack traces.
- **Lists.** Every list endpoint returns `{ items, page, pageSize, total }` (`PagedList`), 1-based `page`,
  `pageSize` up to 500. Search, sort and filter parameters, when added, are named `q`, `sort` (`field` or `-field`)
  and the field name.
- **Language.** Read `Accept-Language` (`en` or `ar`); messages for people come back in that language.
- **Idempotency.** Commands are guarded by state (submitting or paying twice is refused with 409), and event
  handlers are idempotent. Add `Idempotency-Key` support before exposing commands to partner systems.
- **Security.** Every public endpoint requires a token unless it is explicitly anonymous (catalog, sign-in, OpenAPI).
  Staff-only endpoints use the `staff` policy. `/internal/**` is never routed by the gateway.

## Frontend

### Packages: write a component once

- `@dsp/core` holds everything that isn't visual. `@dsp/ui` holds **every** visual component. Apps contain pages and
  their routes only.
- Before writing markup in an app, look in `@dsp/ui`. If two pages (in either app) need the same thing, it goes into
  `@dsp/ui` — no component is defined twice, and none is copied between apps.
- Pages import visual building blocks only from `@dsp/ui` (which re-exports the AEGOV primitives pages use directly)
  and data helpers only from `@dsp/core`.

### UAE Design System (AEGOV) first

- Use the official components (`@aegov/designsystem-react`) for everything they cover: header and footer
  (mandatory), buttons, links, inputs, selects, radios, checkboxes, date picker, file upload, tabs, timeline,
  callouts, badges, cards, pagination, progress. Never hand-roll a control AEGOV has, and never invent a prop
  (check the component API).
- Where AEGOV has no component, use **React Aria Components** (behaviour and accessibility) and **TanStack Table**
  (data organisation), styled with AEGOV tokens: today that is `DataTable` and `ReorderList`. The stepper is
  `aegov-timeline`; loading is an indeterminate `aegov-progress`; there is no custom spinner.
- Style only with Tailwind utilities on AEGOV tokens (`text-aeblack-800`, `bg-primary-50`, `text-h3`…). No hex
  colours, no Tailwind default palettes, no arbitrary text sizes. Utilities style the host of an AEGOV element only.
- AEGOV elements move their slotted children into their own layout. React must never add, remove or reorder the
  direct children of an AEGOV element in place (that throws `removeChild`/`insertBefore` errors): put changing
  content inside one stable wrapper element, keep optional slots rendered (hide them), or give the AEGOV element a
  `key` that changes with its structure so React replaces it whole.
- Use AEGOV events (`onAegovButtonClick`, `onAegovInputValue`…) rather than React DOM events on AEGOV elements.
  Content inside `aegov-dropdown`/`aegov-popover` moves to `<body>`: React handlers there don't fire.
- One solid primary button per view; secondary actions are `appearance="outline"`, tertiary `appearance="link"`.
- Forms: visible label on every field, "(optional)" on optional fields (no asterisks), hint for formats, validation
  on blur and submit, errors next to the field. Five or fewer choices are radio buttons, more are a select.
  Use the shared fields (`TextField`, `ChoiceField`, `DateField`, `FileField`…) and `StepFields` for e-service forms.
- Tables: every list uses `DataTable` — search, column filters (`meta.filter`), sorting and paging — with its state
  in the URL (`validateListSearch` + `listStateProps`).
- Citizens sign in with UAE PASS only.

### Routing

TanStack Router, code-based, in each app's `router.tsx`. Protected routes use `requireSignedIn`. List pages keep
search, filters, sort and page in validated search params. AEGOV anchors are handled by `useInternalLinks`
(inside `PortalFrame`), so in-app links don't reload the page.

### Language, Arabic and right-to-left

- Every visible string goes through `t('key')` with English and Arabic in `@dsp/core/i18n.ts`; data with both
  languages uses `loc(en, ar)`. No hard-coded user-facing text in components or pages.
- The language switch sets `lang` and `dir` on `<html>`; AEGOV components follow. Use logical utilities only
  (`ms-`/`me-`, `ps-`/`pe-`, `start-`/`end-`, `text-start`/`text-end`), never left/right.
- Wrap values that may be the other direction in `<bdi>` (numbers, references, names). Amounts read `AED 1,500`
  in a left-to-right span (`Money`). Dates read `11 November 2026`; Arabic keeps Latin digits.

### Accessibility

WCAG 2.2 AA in both languages: keyboard access for everything, one `h1` per page and ordered headings, no meaning
by colour alone, live regions for changing content, icon-only buttons with an accessible name.

### Content

British English, sentence case, plain words. Buttons start with a verb and have four words or fewer. Formal
navigation labels. Say what happens next.

## Testing and CI

- Add or update tests with every behaviour change: domain rules, use cases (with in-memory fakes), validation in both
  languages, and the AI review's built-in checks. Keep architecture tests green.
- CI (GitHub Actions) runs build, tests, the vulnerable-package check, the missing-migration check, the portals'
  type check and build, and `npm audit`. Security runs CodeQL, Trivy, Gitleaks and dependency review. All must pass
  before merging.
- There is no local .NET or package registry in the agent workspace: CI is the build. Push a feature branch and read
  the run.

## Security and operations

- No secrets in the repository (Gitleaks enforces it). Configuration comes from environment variables.
- Containers run as non-root. Dependencies are pinned by range and updated by Dependabot.
- Logs, traces and metrics go through OpenTelemetry (`OTEL_EXPORTER_OTLP_ENDPOINT`). Every request carries a
  correlation ID. Never log personal data (Emirates ID, mobile, email, application answers).

## Delivery

- Work on a `feat/...` branch; merge to `main` only when CI and Security pass. `main` deploys to Railway (POC);
  FedNet is the production target.
- Commit messages: a short imperative summary line, then what changed and why.
- Update `README.md` and this file when you change how something works.
