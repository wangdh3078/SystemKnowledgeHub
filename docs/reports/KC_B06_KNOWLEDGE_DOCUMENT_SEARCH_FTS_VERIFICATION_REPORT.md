# KC-B06 — KnowledgeDocument Search / FTS Integration Verification Report

## Result

`KC-B06 PASS`

## Scope

KC-B06 extends the existing `GET /api/search` global-search contract with the additive `KnowledgeDocument` group. No second search endpoint or page was introduced.

Changed KC-B06 paths:

- `src/SystemKnowledgeHub.Api/Features/Search/Application/KnowledgeDocumentSearchText.cs`
- `src/SystemKnowledgeHub.Api/Features/Search/Application/KnowledgeDocumentSearchIndex.cs`
- `src/SystemKnowledgeHub.Api/Features/Search/Application/SearchQueries.cs`
- `src/SystemKnowledgeHub.Api/Features/Search/Application/Models/SearchModels.cs`
- `src/SystemKnowledgeHub.Api/Features/KnowledgeDocuments/Application/KnowledgeDocumentService.cs`
- `src/SystemKnowledgeHub.Api/Persistence/Migrations/20260822223000_AddKnowledgeDocumentSearchFts.cs`
- `src/SystemKnowledgeHub.Api/Program.cs`
- `src/SystemKnowledgeHub.Web/src/features/search/api/searchContracts.ts`
- `src/SystemKnowledgeHub.Web/src/features/search/api/searchContracts.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/search/composables/useGlobalSearch.ts`
- `src/SystemKnowledgeHub.Web/src/features/search/components/GlobalSearchOverlay.vue`
- `src/SystemKnowledgeHub.Web/src/features/search/search.css`
- `tests/SystemKnowledgeHub.Api.Tests/Api/KnowledgeDocumentSearchApiTests.cs`
- `tests/SystemKnowledgeHub.Api.Tests/Api/KnowledgeDocumentSearchPerformanceTests.cs`
- `tests/SystemKnowledgeHub.Api.Tests/Persistence/KnowledgeDocumentSearchMigrationTests.cs`
- `tests/SystemKnowledgeHub.Api.Tests/Persistence/SqliteFtsCapabilityTests.cs`

## Baseline and Capability Decision

Before the change, global search used the existing seven grouped `LIKE` query paths and did not include KnowledgeDocument or an FTS index. The worktree already contained substantial unrelated tracked and untracked work for documentation relocation, authentication, UI polish, Evidence, Relationships, KnowledgeDocument KC-B01–B05, migrations, and frontend features. Those changes were retained and not reset, reformatted, or overwritten.

Runtime capability verification used the repository SQLite provider (`3.53.3`): `ENABLE_FTS5` is available, `unicode61` successfully matches transformed Chinese input such as `数据库` and `监听`, and `trigram` is also available. KC-B06 deliberately uses FTS5 with `unicode61`, not a `LIKE` fallback. Chinese text is normalized into character-separated derived index text so one- and two-character Chinese searches work consistently with `unicode61`.

## Search Architecture

`knowledge_documents` remains the only canonical source for document title, summary, Markdown body, lifecycle, knowledge status, detail rendering, and API response content.

`knowledge_documents_fts` is an SQLite FTS5 virtual table containing only derived searchable text (`title`, `summary`, `body_text`). It is not mapped as an EF entity and is never used to return canonical document bodies. Search joins the FTS rowid back to the canonical table, obtains result metadata and the plain-text snippet from canonical values, and uses parameterized FTS queries.

The additive migration creates the virtual table and backfills existing rows without altering canonical documents. The migration normalizes CJK characters during that backfill; the migration regression test proves an existing Chinese body remains unchanged and is searchable after upgrade.

Create and content-update operations update the derived FTS row inside the same service transaction as the canonical save. Lifecycle changes do not duplicate lifecycle truth in FTS: search dynamically excludes canonical `Archived` rows, so archive and restore take effect immediately without stale indexed lifecycle state. `KnowledgeStatus` is intentionally not a searchability filter; `Unknown`, `Inferred`, and `Confirmed` all remain searchable when the lifecycle is Draft or Published.

## Search Semantics and UI

- Indexed content: title, summary, and BodyMarkdown-derived plain text. Markdown links retain link text; formatting/fence markers are removed for result snippets while code content remains searchable.
- Query input is tokenized as plain input and passed as a parameter; FTS operators and punctuation are not exposed as query syntax. `"`, `'`, `+`, `MES/EAP`, and `(` return normal non-500 responses.
- Ranking uses `bm25(fts, 10.0, 4.0, 1.0)` for title, summary, and body respectively. Document rows preserve this FTS ordering within their group, so the existing generic alphabetical grouping does not overwrite document relevance.
- The response adds only nullable metadata needed by the existing overlay: `contentType`, `lifecycleStatus`, and `updatedAt`. `KnowledgeRole`, authorization, and existing non-document groups are unchanged.
- The existing overlay now labels document types in Chinese, displays KnowledgeDocument as `知识内容`, shows lifecycle state, and navigates directly to `knowledge-document-detail`. It uses no `v-html`/`innerHTML` rendering path.
- Viewer read access remains supported; no new access-control rule or frontend type filter was introduced.

## Tests and Validation

Executed successfully:

```text
dotnet build SystemKnowledgeHub.sln --no-restore
Result: PASS — 0 warnings, 0 errors.

dotnet test tests\SystemKnowledgeHub.Api.Tests\SystemKnowledgeHub.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~KnowledgeDocumentSearchApiTests|FullyQualifiedName~KnowledgeDocumentSearchMigrationTests|FullyQualifiedName~KnowledgeDocumentSearchPerformanceTests|FullyQualifiedName~SqliteFtsCapabilityTests|FullyQualifiedName~GlobalSearchApiTests|FullyQualifiedName~KnowledgeDocumentsApiTests" --logger "console;verbosity=detailed"
Result: PASS — 18 passed, 0 failed, 0 skipped.

npm run type-check
Result: PASS.

npm run test -- --run src/features/search/api/searchContracts.spec.ts
Result: PASS — 1 passed.

npx eslint src/features/search/api/searchContracts.ts src/features/search/api/searchApi.ts src/features/search/api/searchContracts.spec.ts src/features/search/composables/useGlobalSearch.ts src/features/search/components/GlobalSearchOverlay.vue
Result: PASS.

npm run build
Result: PASS. Vite emitted its existing large-chunk advisory only; it is not a build failure.
```

Focused backend coverage verifies title, summary, Chinese body, English/error-code, and code-text search; plain-text snippets; index replacement after content update; Draft/Published visibility; archive exclusion and restore; title-over-body ranking; Viewer search; special-character safety; FTS5 runtime capability; and migration backfill/data preservation.

The 1,000-document representative FTS regression test used 1,620-character bodies, query `监听`, and a per-group limit of five. It completed in 26 ms in the final focused run and returned the bounded document group.

## Browser Runtime Verification

An isolated temporary SQLite database and local administrator were created solely for this check, then removed.

The following browser path passed:

```text
login
→ create SOP "Oracle 数据库连接异常处理 SOP" (title does not contain “监听”)
→ save body containing “检查 Oracle 数据库监听服务”
→ global search “监听” returns the SOP with a body-derived snippet
→ open result navigates to /knowledge-documents/1
→ publish, archive, then search returns no result
→ restore to Draft, then the same search returns the SOP again
```

The temporary API listener on `5099` and Vite listener on `5175` were stopped. Their proven KC-B06 process chain was cleaned up; the unrelated `SogouCloud.exe` child observed under the npm process was not stopped. Final listener verification found no listener on either port. The temporary runtime directory, database, logs, Data Protection keys, and local test account were removed.

## Migration and Data Safety

The migration is additive and does not change `KnowledgeHubDbContextModelSnapshot`, canonical table shape, or existing KnowledgeDocument data. `Down` drops only the derived FTS virtual table. No destructive migration, data reset, or production database operation was performed.

## Diff and Scope Verification

`git diff --check` completed with no whitespace errors (Git emitted pre-existing global-ignore/line-ending access advisories only). Scoped source review confirms KnowledgeDocument body search uses `knowledge_documents_fts MATCH` and `bm25`, with no `LIKE` body fallback and no second search API.

KC-B06 did not modify frozen specifications, authentication behavior, authorization policy, user/current-user behavior, Evidence or HumanConfirmation semantics, KnowledgeDocument lifecycle rules, KnowledgeStatus rules, existing routes, existing non-document search-group behavior, database schema outside the additive FTS virtual table, or unrelated frontend features. The global-search response changes are additive KnowledgeDocument metadata only. It did not begin KC-B07.

## Intentionally Not Implemented

- Search result type-filter UI controls; the established global overlay has no such control, so the existing request contract remains the compatibility surface.
- Separate document-search page, endpoint, repository, search provider abstraction, external search service, synonym/highlight system, background indexing worker, or popularity ranking.
- Deletion-oriented index synchronization, because the current KnowledgeDocument lifecycle is archive/restore rather than document deletion.
