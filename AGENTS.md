# AGENTS.md — System Knowledge Hub

## 1. Purpose
This repository implements **系统知识中心 / System Knowledge Hub**.

The MVP records and connects knowledge about systems, business functions, database objects and columns, business rules, integrations, evidence, relationships, and unknown items. Knowledge is progressively captured and explicitly confirmed.

This file defines mandatory rules for coding agents working in this repository.

## 2. Source of Truth
Before implementing a task, read the relevant frozen specifications under `docs/specifications/` in this order:

1. `docs/specifications/System_Knowledge_Hub_MVP_Final_UI_Inventory.md`
2. `docs/specifications/System_Knowledge_Hub_MVP_Design_Baseline.md`
3. `docs/specifications/System_Knowledge_Hub_MVP_Domain_Model.md`
4. `docs/specifications/System_Knowledge_Hub_MVP_Database_Model.md`
5. `docs/specifications/System_Knowledge_Hub_MVP_Application_Use_Case_Model.md`
6. `docs/specifications/System_Knowledge_Hub_MVP_API_Contract.md`
7. `docs/specifications/System_Knowledge_Hub_MVP_Solution_Structure.md`
8. Current Vertical Slice / task specification

Prefer the latest explicitly frozen/confirmed document. Never silently resolve a material conflict by inventing requirements. Record conflicts in the task conflict/verification report and stop the conflicting work.

**Never modify frozen specifications, Golden UI assets, or task definitions merely to make implementation easier.**

## 3. Product Naming and Language
Formal product name:
- Chinese: **系统知识中心**
- English: **System Knowledge Hub**

Do not display historical names such as `遗留系统知识中心` or `Legacy Knowledge Hub`.

Formal UI text is Simplified Chinese. Technical identifiers remain original, e.g. `MES.TABLE_EQP`, `STATE_FLAG`, `RabbitMQ`, `HTTP API`. API enum values remain frozen English wire values; Chinese labels belong in the frontend.

## 4. MVP Architecture
Keep the MVP intentionally small.

```text
SystemKnowledgeHub/
├─ src/
│  ├─ SystemKnowledgeHub.Api/
│  └─ SystemKnowledgeHub.Web/
└─ tests/
   └─ SystemKnowledgeHub.Api.Tests/
```

Backend:
- .NET 8
- ASP.NET Core Controllers
- EF Core + SQLite
- Feature-first
- direct `DbContext`
- page-oriented query projections
- explicit use-case methods

Frontend:
- Vue 3
- strict TypeScript
- Element Plus
- Pinia only for genuine shared/global state
- native `fetch` through the shared API client
- Feature-first

Do not create extra assemblies or abstraction layers merely to mirror architectural terminology.

## 5. Backend Rules
Organize business code by feature. Create only directories actually needed:

```text
Features/<FeatureName>/
├─ Domain/
├─ Application/
├─ Persistence/
└─ Api/
   └─ Contracts/
```

- Application methods map to frozen use cases, not table CRUD.
- Query services may use `KnowledgeHubDbContext` directly.
- Prefer EF Core `Select(...)` projections for page/detail reads.
- API response contracts are separate from EF entities.
- Never return EF entities directly from Controllers.
- Do not create empty placeholder architecture.

## 6. Frontend Rules
Use feature-first organization:

```text
src/features/<feature-name>/
├─ api/
├─ components/
├─ composables/
├─ pages/
└─ types/
```

Normal data flow:

```text
Page / Drawer
    ↓
Feature composable
    ↓
Feature API
    ↓
Shared apiClient
    ↓
ASP.NET Core API
```

Do not call `fetch` directly from pages/components when a Feature API boundary exists.

Application code uses TypeScript. Avoid `any`, `as any`, `@ts-ignore`, and `@ts-nocheck`. Treat genuinely unknown external input as `unknown` and narrow it at the boundary.

## 7. API Rules
Base path: `/api`. Do not add `/v1` during MVP.

- Use Case First.
- Reads may return page-oriented composite models.
- Writes use explicit resource/section/business-action endpoints.
- No generic CRUD endpoints.
- No generic command endpoint.
- Keep concrete routes for System, BusinessFunction, DatabaseObject/Column, BusinessRule, Integration, UnknownItem, etc.
- Success returns frozen business JSON directly unless the contract says otherwise.
- Errors use HTTP status + frozen Error Contract.
- `concurrencyToken` is opaque to clients.
- Frontend must not parse concurrency tokens.
- IDs exposed to JavaScript must remain safe integers.
- Never change a frozen route/response shape to simplify code.

## 8. Domain and Workflow Rules
Canonical Knowledge Status progression:

```text
Unknown → Inferred → Confirmed
```

This is knowledge progression, not navigation tabs.

Status changes are explicit user operations. Never automatically change KnowledgeStatus because an object, Evidence, Human Confirmation, Relationship, or UnknownItem was saved.

### Progressive authoring
1. Create minimum valid information.
2. Initial knowledge may remain `Unknown`.
3. Add relationships later.
4. Add evidence later.
5. Explicitly mark `Inferred`.
6. Record Human Confirmation when appropriate.
7. Explicitly mark `Confirmed`.

Do not require completeness at initial creation unless a frozen use case requires it.

### Relationships
Relationships are first-class explicit knowledge. Do not persist a relationship merely because free text appears to describe one.

### Unknown Items
Unknowns are data, not validation failures. Investigation, findings, evidence, conclusion, knowledge update, confirmation, close, and reopen remain explicit workflow actions.

## 9. Persistence Rules
`docs/specifications/System_Knowledge_Hub_MVP_Database_Model.md` is the canonical physical schema source.

- Do not derive schema from screenshots.
- Use EF Core SQLite.
- Create schema incrementally for the current Vertical Slice.
- Do not pre-create the entire MVP schema.
- Preserve frozen FK, uniqueness, nullability, and index rules.
- Use the selected app-managed integer version strategy where specified.
- Do not introduce a second concurrency mechanism.
- Keep development/test seed data small and purposeful.

## 10. UI / Golden Rules
`docs/specifications/System_Knowledge_Hub_MVP_Final_UI_Inventory.md` determines valid UI assets. Only its designated Golden Reference is an implementation source.

Do not use SUPERSEDED, DEPRECATED, raw ImageGen `exec-*.png`, or Review Board assets as replacements for Golden screens.

Core desktop model:

```text
Application Shell
├─ Left Navigation
├─ Global Search
├─ Main Content
├─ Object-level Context Rail
└─ Single Detail / Authoring Drawer
```

Detail pages use `Main Content + object-level Context Rail + Detail Drawer`.

Context Rail summarizes object-level relationships and gaps; it must not duplicate detailed Main Content.

Maintain:
- light desktop shell
- high information density
- compact technical tables
- first-class Evidence
- consistent typography/spacing
- Simplified Chinese UI

At 1920px, Main Content + Context Rail + Drawer may coexist. At 1440/1366px, opening a Drawer may hide/replace Context Rail. Drawers never stack; a new Drawer replaces the current one. Do not create a second Drawer manager.

## 11. Frozen Edit Patterns
- System: Overview Inline Edit
- Business Function: Overview Inline Edit
- Database Knowledge: Drawer Edit
- Business Rule: Drawer Edit
- Integration: Drawer Edit

Do not create edit Route pages unless the frozen inventory defines one.

## 12. Evidence
Evidence answers: **Why do we believe this knowledge?**

Frozen concepts include code references, SQL, database samples/comments, existing documents, API/MQ evidence, and Human Confirmation.

Evidence is not a generic attachment center. Evidence may support a status transition, but never performs the transition automatically.

## 13. Simplicity and Token-Efficiency
Use the **smallest implementation that satisfies the frozen requirement and current Vertical Slice**.

Do not optimize for theoretical future needs, architectural completeness, maximum abstraction, maximum test coverage, or impressive code volume. These increase maintenance cost and consume unnecessary implementation/review tokens.

Rules:
- YAGNI: do not build something merely because it may be useful later.
- Prefer concrete code over abstractions until at least two real current use cases prove reuse.
- Do not create interfaces, wrappers, factories, base classes, extension layers, helper frameworks, or configuration systems without a current requirement.
- Do not add optional features, fallback paths, extensibility hooks, caching, background jobs, telemetry, generic infrastructure, or generalized error handling unless the current slice requires them.
- Do not refactor working unrelated code for style or architectural purity.
- Do not generate large amounts of boilerplate to make the project look more "enterprise".
- Reuse the existing project conventions before introducing a new pattern.
- When two implementations both satisfy the requirement, choose the simpler one with fewer files and less code.
- Token efficiency matters: inspect only relevant files, make focused edits, run focused verification, and stop when the requested task is complete.

### Prohibited Premature Architecture
Unless a future frozen design explicitly changes this, do not introduce:
- Generic Repository / Repository per Entity
- UnitOfWork Framework
- Specification Pattern
- CQRS Framework / MediatR
- Command Bus / Query Bus
- AutoMapper / Mapster
- FluentValidation framework
- Dapper
- Generic CRUD / Query / Detail / Table / Drawer frameworks
- BaseController / BaseService hierarchies
- Generic KnowledgeObject service
- Domain Event Framework / Event Bus
- dynamic forms
- second data-grid library
- Axios
- VueUse solely for convenience

Prefer a small local helper until a second real use case proves reuse.

## 14. Vertical Slice Development
Implement narrow end-to-end slices:

```text
SQLite
  ↓
EF Core
  ↓
Application Query / Use Case
  ↓
Controller
  ↓
Frozen HTTP Contract
  ↓
Typed Vue API
  ↓
Composable
  ↓
Golden UI Page / Drawer
```

Rules:
1. Implement only the named slice.
2. Do not opportunistically finish the whole feature.
3. Do not implement adjacent workflows unless requested.
4. Do not refactor unrelated modules.
5. Complete verification before the next slice.
6. Produce the requested verification report.
7. Stop after the requested slice.

Canonical first business slice: **Database Object Detail + Column Drawer**. Do not start the next slice automatically.

## 15. Testing and Verification — Minimal / Risk-Based
Testing exists to prove that the **current change works**, not to maximize test count, coverage percentage, or test architecture.

### Core rule
Use the **minimum verification necessary for the risk of the current change**.

Do not automatically create tests for every class, method, DTO, mapper, Controller, composable, or component.

### Do not over-test
- No coverage target is required.
- Do not create tests merely to increase coverage.
- Do not test trivial getters/setters, DTO shape, simple property assignment, framework behavior, or Element Plus internals.
- Do not create repetitive happy-path tests that prove the same behavior at multiple layers.
- Do not build large mock-heavy unit-test suites when one focused integration test proves the behavior better.
- Do not add snapshot-heavy UI tests.
- Do not introduce new testing frameworks, fixtures, test builders, fake servers, containers, or abstraction layers unless the current slice genuinely requires them.
- Simple UI, copy, layout, styling, or type-only changes may require **zero new automated tests**.
- Existing tests affected by the change should be run; unrelated large suites should not be run repeatedly without reason.

### Backend verification
Default verification for ordinary backend changes:

```bash
dotnet build
```

Run `dotnet test` only when:
- relevant tests already exist and are affected, or
- the current slice contains meaningful business/persistence behavior that benefits from a focused test.

When new backend tests are justified:
- prefer a small number of high-value tests;
- normally **1–3 focused tests per Vertical Slice is enough** unless the task explicitly requires more;
- use real EF Core SQLite for relational behavior;
- do not use EF Core InMemory as a substitute for SQLite behavior.

### Frontend verification
Default verification:

```bash
npm run type-check
npm run build
```

Run lint/test commands when they exist and are relevant to changed code. Add Vitest/Vue Test Utils tests only for meaningful frontend logic or regressions that are difficult to verify safely otherwise.

Do not create component tests solely because a component was added.

### Runtime verification
For an end-to-end UI/API slice, perform one focused runtime check of the changed path when practical:

```text
Browser → API → EF Core → SQLite
```

Do not exhaustively exercise unrelated screens.

### Verification process lifecycle — mandatory cleanup
Any process started only for verification must be stopped before the task is considered complete.

This includes, but is not limited to:
- `dotnet run`
- ASP.NET Core development servers
- `npm run dev`
- Vite dev servers
- test/watch processes
- temporary HTTP/mock servers
- background verification scripts

Rules:
1. Prefer non-watch, one-shot commands for automated verification.
2. If a server is started, record its PID/process and stop it after verification.
3. Do not leave a terminal/process running in the background.
4. After stopping verification processes, confirm the ports used for verification are released when practical.
5. Never leave the API/Web development port occupied for the user.
6. If a process cannot be stopped safely, report it explicitly instead of claiming completion.

**A task is not complete while verification-only processes are still running.**

## 16. Change Discipline
Before editing:
1. Identify the current Vertical Slice.
2. Read relevant frozen docs and Golden reference.
3. Inspect adjacent implementation.
4. Determine the smallest coherent change.
5. Preserve verified behavior.

During implementation:
- do not silently broaden scope
- do not rename frozen business concepts
- do not clean up unrelated code
- do not rewrite frozen docs
- do not invent missing requirements
- do not replace established libraries with alternatives

## 17. Completion Standard
Generated code alone is not completion.

A slice is complete only when the **applicable, risk-based checks** pass:
- frozen contracts respected
- affected code builds/type-checks
- relevant existing tests pass when tests are applicable
- only necessary new tests were added
- migration/schema is verified when persistence changed
- focused runtime path is verified when required
- Golden UI is reviewed when UI changed
- verification-only servers/watchers/processes are stopped
- verification ports are released
- deviations are documented
- requested verification report is generated

Do not turn this checklist into a reason to run every possible test for every task. Apply only checks relevant to the actual change.

Never report `PASS` when an applicable required check failed or was skipped, and never report completion while a verification process is still occupying the user's development ports.

## 18. Scope Expansion
The long-term product may include engineering knowledge such as development processes, deployment, operations, troubleshooting, architecture notes, and technical knowledge records.

**These are not part of the current MVP unless a future frozen specification explicitly adds them.**

Do not introduce KnowledgeDocument/wiki/Markdown article/tag/document-tree modules during current MVP slices.
