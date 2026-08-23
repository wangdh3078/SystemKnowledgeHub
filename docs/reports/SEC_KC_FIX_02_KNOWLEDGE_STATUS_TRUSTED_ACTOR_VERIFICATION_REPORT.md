# SEC-KC-FIX-02 — KnowledgeStatus Trusted Actor Verification Report

## Result

```text
SEC-KC-FIX-02 PASS
```

## Original Vulnerability

`PUT /api/knowledge-status` previously accepted an `actor` request object containing display name, role/identity, occurrence time and additional identity metadata. The controller converted it into the application command and the service persisted it into each target's KnowledgeStatus audit snapshot.

## Trust Boundary

New transitions now follow:

```text
Authenticated Principal → ICurrentUserContext → canonical User
→ server actor snapshot → KnowledgeStatusService → persisted audit fields
```

`KnowledgeStatusController` resolves `ICurrentUserContext`, uses canonical `DisplayName`, current `AccessLevel` as the existing audit-role snapshot, and generates the execution time with `DateTimeOffset.UtcNow`. It returns existing Current User security errors rather than falling back to a client actor.

## Request Contract and Field Inventory

| Previous field | Classification | New handling |
| --- | --- | --- |
| `actor.displayName`, identity metadata | Identity attribution / display snapshot | Removed from contract; server-derived. |
| `actor.roleOrIdentity` | Audit snapshot, not authorization | Removed from contract; server-derived from current AccessLevel. |
| `actor.occurredAt` | Audit execution timestamp | Removed from contract; server-generated. |
| `actor.team`, external key, source, note | Client identity/display metadata | Removed; not required by KnowledgeStatus transition semantics. |
| `target`, `targetStatus`, `reason`, `concurrencyToken` | Transition intent/business policy | Remain client-supplied and validated. |

No KnowledgeRole is required by the KnowledgeStatus transition itself. HumanConfirmation retains its independent, principal-backed KnowledgeRole snapshot path.

## Forgery and Regression Verification

- `KnowledgeStatusApiTests` now sends an extra legacy JSON `actor` object with `FORGED ADMIN`, `Administrator`, and future timestamp `2099-01-01`.
- ASP.NET ignores the unknown JSON field after contract removal; the persisted BusinessFunction audit snapshot is `SEC-01 Test Principal` / `Administrator`, and the persisted audit timestamp is not the forged future timestamp.
- The same generic controller/service boundary covers System, BusinessFunction, DatabaseObject, DatabaseColumn, BusinessRule, Integration and KnowledgeDocument targets.
- Existing policy/evidence rules remain: Evidence does not auto-advance; Unknown→Inferred requires evidence; Inferred→Confirmed requires HumanConfirmation; lifecycle remains independent.
- Viewer/Editor/Administrator regression is included through `AccessControlApiTests`; HumanConfirmation regression is included through `KnowledgeDocumentEvidenceStatusApiTests`.

## Verification

| Command | Result |
| --- | --- |
| `dotnet build SystemKnowledgeHub.sln --no-restore` | PASS — 0 warnings, 0 errors |
| Focused KnowledgeStatus, KnowledgeDocument Evidence/Status, CurrentUser and AccessControl tests | PASS — 7 passed, 0 failed |
| `npm run type-check` | PASS |
| KnowledgeDocument detail focused frontend test | PASS — 6 passed |
| `npm run build` | PASS; existing Vite chunk advisory only |
| Scoped ESLint for changed frontend KnowledgeStatus files | PASS |
| `git diff --check` | PASS; existing line-ending/global-ignore advisories only |

The automated integration host executes the real Controller → CurrentUser → service → EF/SQLite path and validates the forged actor payload. No migration or schema change was required; historical records are untouched.

## Scope

Only KnowledgeStatus contracts/controller/application actor construction, the corresponding frontend payload construction, focused tests, and this report changed. Authentication architecture, login, OIDC, local credential, AccessLevel semantics, Evidence/HumanConfirmation semantics, lifecycle, relationships, search, FTS, Unified View, editor, database schema and KC-GAP-003 were not changed.

## Stop Point

KC-GAP-002 is closed by this correction. No new Knowledge Content slice and no KC-GAP-003 work was started.
