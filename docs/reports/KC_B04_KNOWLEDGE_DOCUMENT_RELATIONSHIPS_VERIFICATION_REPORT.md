# KC-B04 — KnowledgeDocument Relationships Verification Report

## Result

`KC-B04 PASS`

## Scope

KnowledgeDocument was added to the existing Relationship Feature only. The implementation reuses the canonical `knowledge_relations` table and the existing global Relationship API/UI patterns; it does not introduce a document-specific relationship table, graph store, or resource-scoped relationship API.

Changed KC-B04 surfaces:

- `src/SystemKnowledgeHub.Api/Features/Relationships/Domain/KnowledgeRelation.cs`
- `src/SystemKnowledgeHub.Api/Features/Relationships/Application/RelationshipEndpointPolicy.cs`
- `src/SystemKnowledgeHub.Api/Features/Relationships/Application/RelationshipQueries.cs`
- `src/SystemKnowledgeHub.Api/Features/Relationships/Application/RelationshipTargetResolver.cs`
- `src/SystemKnowledgeHub.Api/Features/Relationships/Application/RelationshipService.cs`
- `src/SystemKnowledgeHub.Api/Features/Relationships/Api/RelationshipsController.cs` and contracts/models
- `src/SystemKnowledgeHub.Api/Features/Relationships/Persistence/KnowledgeRelationConfiguration.cs`
- `src/SystemKnowledgeHub.Api/Persistence/Migrations/20260822124136_AddKnowledgeDocumentRelationships.cs` and the EF model snapshot
- `src/SystemKnowledgeHub.Web/src/features/relationships/`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.vue` and its feature CSS/test
- `tests/SystemKnowledgeHub.Api.Tests/Api/RelationshipsApiTests.cs`

## Relationship Semantics

- `KnowledgeDocument` is a first-class `KnowledgeTargetType` for both source and target resolution.
- A document can explicitly relate to System, BusinessFunction, DatabaseObject, BusinessRule, Integration, and another KnowledgeDocument.
- The controlled document relationship vocabulary is `Documents`, `References`, `AppliesTo`, `Implements`, `SpecifiedBy`, `VerifiedBy`, `Resolves`, `RelatedTo`, and `Supersedes`.
- The existing unique edge definition remains `(source type, source id, target type, target id, relation type)`. Duplicate edges return `409`; self-relations return `422`; general graph-cycle detection was not added.
- The global `GET /api/relationships?objectType=KnowledgeDocument&objectId={id}` projection returns outgoing/incoming direction, relation ID/type, related type/ID, and server-resolved title/type label. The document detail page does not issue per-row object lookups.
- Document target search uses the existing `GET /api/knowledge-targets` resolver path. Archived KnowledgeDocuments are excluded from newly searchable targets; existing historical edges remain resolvable.

## UI and Access Verification

- KnowledgeDocument detail has an independent “关联对象” loading/error/empty section below the body.
- Editor/Administrator can open the existing add-relationship drawer, select the controlled target types, search before selecting, save, and remove a single edge after confirmation.
- Viewer can read relationship projections and navigate the related object, but the add/remove controls are not rendered and backend write policies return `403`.
- Outgoing and incoming relations are explicitly labelled “指向” and “来自”. Supported related targets route to their existing detail pages; KnowledgeDocument routes to its own detail route.
- A browser-discovered initialization defect was corrected: a KnowledgeDocument source now defaults to `Documents` rather than the legacy `Reads`, so the endpoint policy is valid when the drawer first opens.

## Current User Boundary

Relationship creation, description updates, and relationship knowledge-status writes no longer accept a client-supplied actor payload. The controller resolves the already authenticated canonical Current User and uses that trusted display name/access level for the existing relationship audit fields. This does not alter authentication, authorization policy, identity mapping, or KnowledgeStatus progression rules.

## Migration Verification

`20260822124136_AddKnowledgeDocumentRelationships` updates the existing SQLite CHECK constraints for `knowledge_relations` to include `KnowledgeDocument` and the controlled relation types. It does not add a relationship table or alter document persistence.

## Focused Tests

Executed successfully:

```text
dotnet build SystemKnowledgeHub.sln --no-restore
0 warnings, 0 errors

dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj --no-build --filter "FullyQualifiedName~RelationshipsApiTests|FullyQualifiedName~KnowledgeDocumentMigrationTests|FullyQualifiedName~KnowledgeDocumentsApiTests"
10 passed, 0 failed, 0 skipped

cd src/SystemKnowledgeHub.Web
npm run type-check
npm run test -- --run src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.spec.ts
4 passed, 0 failed
npm run build
passed

npx eslint src/features/relationships/api/relationshipContracts.ts src/features/relationships/api/relationshipApi.ts src/features/relationships/components/AddRelationshipDrawer.vue src/features/relationships/components/RelationshipDetailDrawer.vue src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.vue src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.spec.ts
passed
```

The Vite build retains its existing large-chunk advisory; it is not a KC-B04 build failure and no new lint/type/XML warning was introduced.

The relationship API tests cover all six document target categories, duplicate/self rejection, incoming projection, explicit relation deletion, unchanged document `KnowledgeStatus`, forged client actor being ignored in favour of the authenticated principal, and Viewer read/write boundaries.

## Browser Runtime Verification

An isolated temporary SQLite database and temporary local Administrator were used on ports `5099` and `5173`.

1. Signed in through the local login page.
2. Created a temporary KnowledgeDocument and System through the UI.
3. Opened “添加关联”; confirmed the Document source defaulted to `文档说明`, searched and selected the System, and saved the edge.
4. Confirmed the detail section showed `指向 / 文档说明 / 系统`, the relationship detail recorded the authenticated temporary administrator, and the document status remained `未知`.
5. Followed the related System navigation to `/systems/1`.
6. Confirmed and removed the edge; the document then showed “暂无关联对象”.

The temporary API/Vite process trees were stopped. Ports `5099` and `5173` had no listener afterwards, and the isolated runtime directory/database/logs were removed.

## Dirty Worktree Safety

The pre-existing worktree contained documentation relocation changes, AUTH-B02/security work, KC-B01–KC-B03 KnowledgeDocument work, package/auth frontend changes, and untracked prior reports/KnowledgeDocument feature files. These were recorded before KC-B04 and were neither reverted nor overwritten. The previously observed empty root `src/features` directory was not moved or deleted.

## Diff and Scope Verification

`git diff --check` completed without whitespace errors. KC-B04 changes are limited to the Relationship feature, the KnowledgeDocument relation detail UI, its CHECK-constraint migration, focused tests, and this report.

No Evidence or HumanConfirmation capability, KnowledgeStatus transition rule, search/FTS, unified view, revision history, attachment, authentication flow, authorization matrix, database schema outside the existing relationship checks, or unrelated business behavior was implemented. No follow-on KC-B05 work was started.
