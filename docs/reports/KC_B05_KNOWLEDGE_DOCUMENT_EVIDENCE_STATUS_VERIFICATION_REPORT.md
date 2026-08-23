# KC-B05 — KnowledgeDocument Evidence / HumanConfirmation / KnowledgeStatus Integration Verification Report

## Result

```text
KC-B05 PASS
```

## Scope Delivered

`KnowledgeDocument` is now a first-class target of the existing generic Evidence and KnowledgeStatus mechanisms. No `DocumentEvidence`, `DocumentConfirmation`, document-specific status engine, duplicate drawer, or resource-specific Evidence table was introduced.

- `EvidenceSubjectType.KnowledgeDocument` resolves the existing document and its current `KnowledgeStatus` through the shared resolver.
- `KnowledgeStatusTargetType.KnowledgeDocument` uses the existing explicit transition policy and updates the document's existing status/audit/version fields.
- The shared `evidence` table's controlled subject-type constraint was extended through migration `20260822213000_AddKnowledgeDocumentEvidenceSubject`. The SQLite migration rebuilds only that existing table, copies every row and restores the existing constraints, foreign keys and indexes; it creates no new table in the final schema.
- `GET /api/evidence?subjectType=KnowledgeDocument&subjectId={id}` is the generic, Viewer-readable Evidence summary projection used by detail pages. It validates the target through the existing resolver and returns `400` for invalid parameters and `404` when the target does not exist.
- KnowledgeDocument detail now shows ordinary Evidence and HumanConfirmation entries, including HumanConfirmation method and server-stored provider snapshot. Existing Evidence/HumanConfirmation drawers receive a fixed `KnowledgeDocument` target; users cannot choose a different target.
- The existing `KnowledgeStatusProgressionPanel` and shared status dialog now accept `KnowledgeDocument`. Evidence and confirmation merely enable the existing explicit actions; neither save operation changes status automatically.

## Semantics Verified

```text
Authenticated Principal
  -> LoginIdentity
  -> canonical User / Current User
  -> HumanConfirmation provider Snapshot
```

- HumanConfirmation remains a server-side Current User operation. The client submits confirmation facts and an optional KnowledgeRole selection only.
- Existing role semantics remain unchanged: zero active roles uses the fallback Snapshot, one active role is selected automatically, and multiple active roles require an explicit assigned active role.
- `Unknown -> Inferred` still requires valid, locatable Evidence; `Inferred -> Confirmed` still requires HumanConfirmation; rollback policy remains owned by the existing service.
- A document's lifecycle and KnowledgeStatus remain independent. Browser verification proved `Published + Confirmed` and then `Archived + Confirmed`; archiving preserved historical Evidence/confirmation display and hid Evidence, confirmation, and status-write actions.

## Files Changed for KC-B05

### Backend / persistence

- `src/SystemKnowledgeHub.Api/Features/Evidence/Domain/Evidence.cs`
- `src/SystemKnowledgeHub.Api/Features/Evidence/Application/EvidenceSubjectResolver.cs`
- `src/SystemKnowledgeHub.Api/Features/Evidence/Application/EvidenceQueries.cs`
- `src/SystemKnowledgeHub.Api/Features/Evidence/Application/Models/EvidenceModels.cs`
- `src/SystemKnowledgeHub.Api/Features/Evidence/Api/EvidenceController.cs`
- `src/SystemKnowledgeHub.Api/Features/Evidence/Persistence/EvidenceConfiguration.cs`
- `src/SystemKnowledgeHub.Api/Features/KnowledgeStatus/Application/KnowledgeStatusService.cs`
- `src/SystemKnowledgeHub.Api/Persistence/Migrations/20260822213000_AddKnowledgeDocumentEvidenceSubject.cs`
- `src/SystemKnowledgeHub.Api/Persistence/Migrations/KnowledgeHubDbContextModelSnapshot.cs`

### Frontend

- `src/SystemKnowledgeHub.Web/src/features/evidence/api/evidenceApi.ts`
- `src/SystemKnowledgeHub.Web/src/features/evidence/api/evidenceContracts.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-status/api/knowledgeStatusContracts.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-status/components/KnowledgeStatusProgressionPanel.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/knowledge-documents.css`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.spec.ts`

### Tests

- `tests/SystemKnowledgeHub.Api.Tests/Api/KnowledgeDocumentEvidenceStatusApiTests.cs`
- `tests/SystemKnowledgeHub.Api.Tests/Persistence/HumanConfirmationSnapshotMigrationTests.cs`

## Focused Verification

| Command / check | Result |
| --- | --- |
| `dotnet build SystemKnowledgeHub.sln --no-restore` | Passed: 0 warnings, 0 errors. |
| `dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj --no-build --filter "FullyQualifiedName~KnowledgeDocumentEvidenceStatusApiTests|FullyQualifiedName~KnowledgeDocumentsApiTests|FullyQualifiedName~KnowledgeStatusApiTests|FullyQualifiedName~EvidenceApiTests|FullyQualifiedName~HumanConfirmationSnapshotMigrationTests"` | Passed: 16; failed: 0; skipped: 0. |
| `npm run type-check` in `src/SystemKnowledgeHub.Web` | Passed. |
| `npm run test -- --run src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.spec.ts` | Passed: 5 tests. |
| `npm run build` in `src/SystemKnowledgeHub.Web` | Passed. Vite emitted only its existing large-chunk advisory. |
| `git diff --check` | Passed; no whitespace errors. |

An initial combined command attempted `npm` from the repository root, where no `package.json` exists, and returned `ENOENT`; the required frontend commands were immediately rerun from `src/SystemKnowledgeHub.Web` and passed. This was a command-directory error, not a product regression.

## Browser Runtime Verification

An isolated temporary SQLite database, Data Protection key directory, API on `5099`, and Vite server on `5174` were used. A temporary local Administrator was created only in that disposable database.

Verified in a real browser:

1. Created a Draft/Unknown KnowledgeDocument.
2. Added an ordinary Evidence record through the existing drawer. The document stayed `Unknown` and the UI then enabled, but did not execute, the explicit `Unknown -> Inferred` action.
3. Explicitly progressed to `Inferred`.
4. Added HumanConfirmation through the existing drawer. The confirmation target was fixed to the document; the UI showed the authenticated canonical user's fallback KnowledgeRole Snapshot and the status remained `Inferred`.
5. Explicitly progressed to `Confirmed`.
6. Published, then archived the document. `KnowledgeStatus` remained `Confirmed`; Evidence and HumanConfirmation remained visible, while write actions were hidden.

The exact API/Vite process trees created for this verification were stopped, both verification ports were released, the temporary browser tab was closed, and `artifacts/kc-b05-runtime-20260822` (database, logs and keys) was deleted. The pre-existing Vite listener on `5173` was not touched.

## Scope and Safety Verification

No Authentication implementation, authorization policy, Current User resolution, HumanConfirmation role policy, existing Evidence write semantics, document DTO shape, lifecycle transition behavior, route replacement, frontend router, or unrelated business feature was changed.

This batch adds the necessary generic target integration and one generic Evidence-list read projection. It does not alter the existing status rules or make Evidence/confirmation implicitly advance status.

## Dirty Worktree Safety

Before KC-B05, the worktree already contained substantial unrelated/preceding changes: DOC-STRUCTURE-B01 documentation moves, AUTH-B02/local credential work, KC-B01–B04 KnowledgeDocument work, UI-B03/fav icon work, relationship migration work, and untracked reports/frontend feature files. They were recorded with `git status`, `git diff --stat`, and `git diff` before implementation.

KC-B05 did not reset, revert, format, overwrite, or delete any pre-existing change. The known Git global-ignore permission warning and CRLF advisory remain environment/worktree conditions, not KC-B05 failures.

## Deferred

No KC-B06 search/FTS, unified view, version history, attachment, tag/category, or other KnowledgeDocument work was started.
