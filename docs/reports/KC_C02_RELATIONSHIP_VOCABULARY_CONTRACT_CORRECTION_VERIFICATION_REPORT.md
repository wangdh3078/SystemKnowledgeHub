# KC-C02 — Relationship Vocabulary Contract Correction Verification Report

## Result

KC-C02 PASS

## Scope

- `src/SystemKnowledgeHub.Api/Features/Relationships/Domain/KnowledgeRelation.cs`
- `src/SystemKnowledgeHub.Api/Features/Relationships/Application/RelationshipEndpointPolicy.cs`
- `src/SystemKnowledgeHub.Api/Features/Relationships/Application/RelationshipQueries.cs`
- `src/SystemKnowledgeHub.Api/Features/Relationships/Application/RelationshipTargetResolver.cs`
- `src/SystemKnowledgeHub.Api/Features/Relationships/Application/Models/RelationshipModels.cs`
- `src/SystemKnowledgeHub.Api/Features/Relationships/Persistence/KnowledgeRelationConfiguration.cs`
- `src/SystemKnowledgeHub.Api/Persistence/Migrations/20260823022046_TightenRelationshipVocabulary.cs`
- `src/SystemKnowledgeHub.Api/Persistence/Migrations/20260823022046_TightenRelationshipVocabulary.Designer.cs`
- `src/SystemKnowledgeHub.Api/Persistence/Migrations/KnowledgeHubDbContextModelSnapshot.cs`
- `src/SystemKnowledgeHub.Web/src/features/relationships/api/relationshipContracts.ts`
- `src/SystemKnowledgeHub.Web/src/features/relationships/api/relationshipContracts.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/relationships/components/AddRelationshipDrawer.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.spec.ts`
- focused relationship API and SQLite migration tests.

No route redesign, new Relationship endpoint, KnowledgeDocument storage/lifecycle change, Evidence change, authorization change, UnknownItem endpoint, or generic relationship feature was introduced.

## Contract Correction

The active wire vocabulary is now exactly:

```text
Calls / Reads / Writes / UsesField / AppliesRule / PublishesVia / ConsumesVia /
UsesIntegration / DependsOn / Documents / References / AppliesTo / SpecifiedBy /
VerifiedBy / Supersedes
```

`RelatedTo`, `Implements`, and `Resolves` were removed from the backend enum, persistence CHECK, frontend union, decoder, labels, and picker. Forged requests using each removed value return the existing `400 Bad Request` validation family; they do not produce a server error.

`RelationshipEndpointPolicy` now enforces the approved layered matrix. In particular:

- `Documents` targets only structured knowledge; it cannot target another KnowledgeDocument.
- `References` remains a directed citation relation.
- `Requirement → SpecifiedBy → Specification` and `Requirement|Specification → VerifiedBy → TestCase` are validated by DocumentType.
- `AppliesTo` is restricted by Requirement, Sop, and Troubleshooting target matrices.
- `Supersedes` requires distinct KnowledgeDocuments of the same DocumentType.
- `DesignNote → References → Specification` is filtered and validated.

Target searches receive the same DocumentType restriction used by write validation, so the picker cannot surface an incompatible target document as an option.

## Data Preflight and Migration

Read-only preflight against `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` found:

```text
Documents | KnowledgeArticle | 222 | MES
RelatedTo / Implements / Resolves | 0
```

The existing `KnowledgeArticle → Documents → System (MES)` row is retained. The migration was applied to the repository application database before this report and the resulting `knowledge_relations` CHECK contains only the retained fifteen values.

The first generated migration form used SQLite-incompatible Check Constraint operations for `Down`. Focused migration testing identified this before acceptance. The final migration rebuilds only `knowledge_relations`, preserves legitimate rows and all four relationship indexes, rejects removed values after `Up`, accepts retained values, and restores the legacy CHECK only on `Down`.

## Frontend Behavior

- The relationship picker opens with no RelationType, TargetType, or target selection.
- Document relation choices are narrowed by source DocumentType.
- Recommended type-specific relations appear before other legal relations.
- Selecting a relation filters target object types; DocumentType-specific document searches are filtered server-side.
- `Documents`, `References`, and `AppliesTo` have concise semantic helper text.
- Incoming relationship displays use contextual labels such as `由文档说明`, `被引用`, `定义需求`, `验证需求/规格`, and `被新文档替代`.

## Verification

Executed successfully:

```text
dotnet build SystemKnowledgeHub.sln --no-restore
dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj --no-build --filter "FullyQualifiedName~RelationshipsApiTests|FullyQualifiedName~RelationshipVocabularyMigrationTests|FullyQualifiedName~KnowledgeDocumentSearchApiTests|FullyQualifiedName~SystemKnowledgeViewApiTests"
```

- Build: 0 warnings, 0 errors.
- Focused backend tests: 17 passed, 0 failed.

Executed successfully from `src/SystemKnowledgeHub.Web`:

```text
npm run type-check
npm test -- relationshipContracts.spec.ts KnowledgeDocumentDetailView.spec.ts KnowledgeDocumentEditor.spec.ts documentEditState.spec.ts milkdownRoundTrip.spec.ts renderMarkdown.spec.ts
npx eslint src/features/relationships/api/relationshipContracts.ts src/features/relationships/api/relationshipContracts.spec.ts src/features/relationships/components/AddRelationshipDrawer.vue src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.vue src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.spec.ts
npm run build
```

- Type check: passed.
- Focused frontend tests: 15 passed, 0 failed.
- Scoped ESLint: 0 errors; one pre-existing attribute-order warning in `KnowledgeDocumentDetailView.vue:466` remains outside this correction.
- Frontend build: passed. Vite retained its existing large-chunk advisory only.

## Runtime Verification

An isolated copy of the repository database was used with a temporary Local-only Administrator; the production application database and its users were not changed.

```text
Browser login
→ Dashboard
→ KnowledgeDocument 222 (KnowledgeArticle)
→ existing Documents → MES relation rendered as “说明”
→ Add Relationship
→ no default relation selected
→ “说明” shown as recommended
→ target list limited to System / BusinessFunction / DatabaseObject / BusinessRule / Integration
```

No relation was saved during browser verification. Browser console inspection reported no errors. The ASP.NET Core process and Vite process were stopped; ports 5099 and 5175 had no listening process after cleanup.

The execution environment blocked deletion of the isolated temporary directory after the servers stopped. It is outside the repository and contains only the copied verification database, its temporary Administrator credential, and Data Protection keys; its exact path is communicated directly to the operator for removal.

## Gap Closure

`KC-GAP-003` is closed. The closed vocabulary, endpoint matrix, data preflight, SQLite migration behavior, contract decoder, picker behavior, focused tests, and authenticated runtime path now conform to the approved KC-C01 decision.

## Dirty Worktree and Scope Check

The repository had extensive pre-existing modified and untracked work from AUTH, KC-B01–B07, UI, documentation restructuring, and prior verification phases. Those changes were preserved. `git diff --check` reported no whitespace errors; Git emitted the pre-existing global-ignore permission and CRLF notices.

No production behavior outside the Relationship vocabulary correction was intentionally changed.
