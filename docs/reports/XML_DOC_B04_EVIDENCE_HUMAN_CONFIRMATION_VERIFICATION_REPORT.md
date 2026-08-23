# XML-DOC-B04 — Evidence / HumanConfirmation Verification Report

## Result

XML-DOC-B04 PASS

## Scope

Only high-value XML documentation was added in the Evidence Feature:

- `src/SystemKnowledgeHub.Api/Features/Evidence/Domain/Evidence.cs`
- `src/SystemKnowledgeHub.Api/Features/Evidence/Api/Contracts/EvidenceContracts.cs`
- `src/SystemKnowledgeHub.Api/Features/Evidence/Application/Models/EvidenceModels.cs`
- `src/SystemKnowledgeHub.Api/Features/Evidence/Application/EvidenceService.cs`
- `src/SystemKnowledgeHub.Api/Features/Evidence/Application/EvidenceQueries.cs`
- `src/SystemKnowledgeHub.Api/Features/Evidence/Application/EvidenceSubjectResolver.cs`
- `src/SystemKnowledgeHub.Api/Features/Evidence/Api/EvidenceController.cs`

## Documentation Added

- Evidence is documented as the historical basis for believing knowledge, rather than an attachment center, audit log, approval, or permission record.
- `EvidenceType.HumanConfirmation` is documented as Evidence, not an independent aggregate or approval.
- Provider reference versus historical Snapshot semantics are documented at the Evidence domain and C25 application boundary. A later User or KnowledgeRole change does not dynamically alter stored facts; C24 is the explicit correction path and can deliberately correct editable Evidence data.
- C25 documents the trusted chain `Authenticated Principal → LoginIdentity → canonical User → Current User → HumanConfirmation Snapshot`. The browser cannot select or override another confirmer.
- `AddHumanConfirmationRequest` and `EvidenceService.AddHumanConfirmation` document client-owned confirmation facts, server-owned identity hydration, and the 0/1/multiple active KnowledgeRole resolution rules. KnowledgeRole remains attribution for this confirmation, not a permission.
- C25 documents its transaction-level re-read of canonical User/role, Subject validation, Snapshot materialization, and Evidence insert; creation does not advance KnowledgeStatus.
- C24 documents explicit correction and opaque `concurrencyToken`, including stale-token conflict semantics, without exposing the internal version encoding.
- The controller documents the actual Viewer-read / Editor-write boundary and HTTP responses. Security failures remain an API boundary, not an Evidence-domain failure model.

## Evidence and Snapshot Semantics

Evidence answers why a knowledge conclusion is believed. HumanConfirmation is one `EvidenceType`.

Provider canonical IDs track the source where present, while Provider Snapshot values preserve the historical fact at Evidence creation. The Snapshot is non-dynamic: later User Profile, active-state, KnowledgeRole rename/deactivation, or assignment changes do not propagate into existing Evidence. This is not described as absolutely immutable because C24 permits an intentional Evidence correction.

Legacy Evidence may validly predate canonical Provider references. No migration, backfill, persistence change, or legacy-data rewrite was introduced.

## HumanConfirmation and Security

The documented production identity path is:

```text
Authenticated Principal
→ LoginIdentity
→ canonical User
→ Current User
→ HumanConfirmation Snapshot
```

The C25 API requires the SEC-02 Editor minimum. Its KnowledgeRole choice is never authorization: zero active roles produces the approved fallback Snapshot, one active role is selected by the server when omitted, and multiple active roles require an explicit assigned active role. A HumanConfirmation can satisfy a later explicit KnowledgeStatus transition condition, but `knowledgeStatusChanged` remains false on create.

`X-Current-User-Id` is not documented as a Production identity source and cannot override the authenticated principal.

## Legacy Compatibility

The existing detail-consumption path was reviewed: new HumanConfirmation records store `confirmationMethod` in `source_locator_json`; the existing frontend read decoder uses that locator value first and falls back to legacy `provider_source` without modifying historical records. This batch did not edit that frontend compatibility code.

## Intentionally Skipped

- Users, Current User B03, LoginIdentity/authentication, and authorization implementation.
- Detailed KnowledgeStatus policy, Persistence configuration, DbContext, migrations, and concurrency codec.
- Frontend, including the existing locator-first confirmation-method decoder.
- Private helpers and obvious scalar properties.

## Pre-existing Dirty Worktree

At B04 start, the worktree already had extensive modified and untracked content, including all tracked Evidence implementation/contract/persistence changes, SEC-01/SEC-02/SEC-03 work, migrations, Users, tests, frontend, and prior XML reports. In particular, the pre-existing HumanConfirmation contract/logic changes in Evidence files were retained as the current source baseline. B04 did not revert, format, overwrite, or alter them.

## Verification

| Check | Result |
| --- | --- |
| `dotnet build SystemKnowledgeHub.sln --no-restore` | PASS — 0 warnings, 0 errors. |
| `dotnet build SystemKnowledgeHub.sln --no-restore -p:GenerateDocumentationFile=true -p:NoWarn=1591` | PASS for B04 — no Evidence XML warnings. Three pre-existing CS1573 warnings remain only in `Features/KnowledgeStatus/Api/Contracts/KnowledgeStatusContracts.cs`. |
| `dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj --no-build --filter "FullyQualifiedName~EvidenceApiTests|FullyQualifiedName~HumanConfirmationSnapshotMigrationTests|FullyQualifiedName~KnowledgeStatusApiTests|FullyQualifiedName~CurrentUserApiTests|FullyQualifiedName~AccessControlApiTests"` | PASS — 15 passed, 0 failed, 0 skipped. Covers HumanConfirmation Snapshot, forged-header Principal attribution, Viewer/Editor boundary, Current User, and KnowledgeStatus regressions. |
| XML / terminology review | PASS — tags, `cref`, and `paramref` compile; no improper approval/permission/selected-user terminology was introduced. |
| `git diff --check` | PASS — no whitespace error. |

## Diff Verification

XML-DOC-B04 did not change Evidence/HumanConfirmation business logic, API contract shape, security behavior, persistence schema, migration, route, validation, or frontend behavior. The scoped B04 source changes are XML documentation only, plus this report.

## Deviations

None. The older Current User header example in the XML standard remains superseded by the SEC-01 model already recorded in B03; B04 directly documents the approved principal-backed Current User semantics.
