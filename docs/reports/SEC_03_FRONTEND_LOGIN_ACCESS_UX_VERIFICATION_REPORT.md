# SEC-03 Frontend Login & Access UX Verification Report

## Result

SEC-03 PASS

## Scope

- `src/SystemKnowledgeHub.Web/src/app/security/` — authenticated-session bootstrap, antiforgery token retrieval, logout call, and login/access gate.
- `src/SystemKnowledgeHub.Web/src/api/client/apiClient.ts` — credentialed API requests, unsafe-method antiforgery header, and security-error propagation.
- `src/SystemKnowledgeHub.Web/src/app/stores/actor.ts` — authenticated canonical User state and `AccessLevel` UX capability projection.
- `src/SystemKnowledgeHub.Web/src/app/router/`, `src/SystemKnowledgeHub.Web/src/layouts/` and `src/SystemKnowledgeHub.Web/src/App.vue` — protected application shell, administrator route guard, read-only current-user presentation, and administrator-only navigation.
- Current User-facing authoring entry points under Systems, Business Functions, Database Knowledge, Business Rules, Integrations, Relationships, Evidence, Knowledge Status, and Unknown Items — Viewer write-action hiding/prevention.
- `src/SystemKnowledgeHub.Web/src/features/users/` — administrator-only User management with LoginIdentity mapping administration.
- `src/SystemKnowledgeHub.Api/Program.cs` and `src/SystemKnowledgeHub.Api/Shared/Api/AntiforgeryController.cs` — standard ASP.NET Core antiforgery validation for unsafe application requests and the authenticated token endpoint.
- `tests/SystemKnowledgeHub.Api.Tests/TestSupport/BootstrapWebApplicationFactory.cs` and `tests/SystemKnowledgeHub.Api.Tests/Api/AntiforgeryApiTests.cs` — antiforgery test support and coverage.

## Security Model

The production frontend now establishes its working identity through:

```text
Browser
  → HttpOnly authentication cookie
  → GET /api/current-user
  → authenticated Principal mapped to canonical User
  → AccessLevel-driven UX
```

`X-Current-User-Id`, selected-user state, and persisted `currentUserId` are not used by the frontend. No `X-Access-*` header is sent. A forged legacy current-user header remains unable to override the authenticated Principal, as covered by the existing Current User and HumanConfirmation regression tests.

## Login and Access UX

- The application waits for Current User bootstrap before rendering protected UI.
- The gate distinguishes loading, unauthenticated, session-expired, unmapped identity, inactive LoginIdentity, inactive account, and recoverable error states.
- Login begins at `/auth/login` with the current safe local path as return target.
- The top bar displays the mapped canonical profile and `AccessLevel` read-only, includes profile details and logout, and offers no local actor or user switcher.
- Viewer is read-only. Editor and Administrator can open write flows; Administrator alone sees and can enter `/admin/users`. Direct Administrator-route access by a non-administrator resolves to the dedicated forbidden view.
- `KnowledgeRole` continues to be presented as knowledge attribution, not a permission or authorization role.
- A returned `403 forbidden` triggers Current User reload so a server-side access-level downgrade is reflected by the UI.

## Antiforgery

- `GET /api/antiforgery/token` is authenticated and stores the ASP.NET Core antiforgery cookie while returning the request token.
- The shared client uses `credentials: include` and sends `X-CSRF-TOKEN` only for `POST`, `PUT`, `PATCH`, and `DELETE` requests, including logout.
- Unsafe `/api/*` and `/auth/logout` requests validate through `IAntiforgery.ValidateRequestAsync` and return the shared `ApiErrorResponse` code `antiforgery_failed` on failure.
- Safe reads do not require the token.

## Administrator Management Note

The administrator User drawer now manages explicit `LoginIdentity` `Provider + Subject` mappings and their active state via the SEC-02 APIs. The existing User list/detail contract does not return each managed User's current `AccessLevel`, although it offers the separate access-level mutation endpoint. To avoid an ambiguous control that cannot display the current value without widening that frozen read contract, this batch does not add a per-user AccessLevel selector. Follow-up: add an approved read projection for managed-user AccessLevel before surfacing that mutation in the UI.

## Verification

Passed:

- `dotnet build SystemKnowledgeHub.sln --no-restore` — 0 warnings, 0 errors.
- `dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj --no-build --filter "FullyQualifiedName~AntiforgeryApiTests|FullyQualifiedName~CurrentUserApiTests|FullyQualifiedName~AccessControlApiTests|FullyQualifiedName~EvidenceApiTests"` — 13 passed, 0 failed.
- `npm run type-check` — passed.
- `npm run test -- --run src/api/client/apiClient.spec.ts src/app/stores/actor.spec.ts src/app/stores/overlays.spec.ts` — 7 passed, 0 failed.
- `npm run build` — passed. The existing Vite large-chunk advisory remains non-failing.

The broad `npm run test -- --run` was also attempted. 25 tests passed; the one pre-existing failing test is `AppShell.spec.ts`, which expects the context rail without `route.meta.hasContextRail`, contrary to the existing `AppContentArea` behavior. The test emitted existing unresolved Element Plus component warnings. This failure is outside SEC-03 changes and was not modified.

`npm run lint` was attempted and has two pre-existing unrelated errors:

- `features/integrations/components/CreateIntegrationDialog.vue`: unused `props`.
- `features/unknown-items/api/unknownItemContracts.ts`: empty interface equivalent to its supertype.

No SEC-03 lint errors were reported.

## Dirty Worktree and Diff Verification

The worktree was already extensively dirty before SEC-03, including modified API features, frontend feature work, persistence files, test files, and untracked Security, Users, migrations, reports, and design documents. Those changes were preserved. This batch did not revert, reformat, overwrite, or clean unrelated work.

SEC-03 changed authentication/session UX, client request security, route and action presentation, and antiforgery enforcement only. It did not change the canonical User resolution logic, OIDC claims, authorization policies, backend access-level decision rules, API route/DTO shape, persistence schema/migrations, or KnowledgeRole semantics.

No verification server or watcher was started; no ports remain occupied by this batch.
