# REV-B02 — Revision History Read UX Verification Report

**Result: REV-B02 PASS**

**Verification date:** 2026-08-23  
**Scope:** immutable KnowledgeDocument revision list/detail read UX only

## 1. Normative basis and worktree baseline

The implementation was checked against `AGENTS.md`, the frozen REV-A01 architecture decision and freeze report, the REV-B01 verification report, the next-capability plan, and the repository-cleanup verification report. The existing KnowledgeDocument query/detail, revision persistence, authorization, API error and safe-integer conventions, frontend detail/editor/dirty-guard flow, shared Markdown renderer, layout, and typed API boundary were inspected before editing. No authority conflict was found.

The initial worktree gate was clean on `main`, synchronized with `origin/main`, at `008461e 📃 docs: 修改文档结构`. `git status`, `git status --short`, `git diff --stat`, and `git diff` showed no pre-existing uncommitted source, test, migration, or document work. Frozen specifications, task definitions, Golden assets, repository `App_Data`, and unrelated implementation were not modified.

## 2. Backend list/detail contracts — PASS

Added the two frozen read endpoints:

- `GET /api/knowledge-documents/{id}/revisions?page=1&pageSize=20`
- `GET /api/knowledge-documents/{id}/revisions/{revisionNumber}`

The list defaults to page 1 and 20 rows, caps page size at 100, returns `revisionNumber DESC`, and returns `400 validation_error` for invalid pagination. Its item contract contains only immutable revision metadata and the derived `isCurrent` / `isLatestPublished` flags. It does not return Title, Summary, BodyMarkdown, concurrency tokens, edit capability, or write tokens.

The detail contract adds `knowledgeDocumentId`, historical Title, Summary, and BodyMarkdown to the same metadata. Safe positive JavaScript-range document/revision identifiers are enforced. Missing documents, missing revisions, and cross-document revision lookups return 404. No Revision PUT, PATCH, DELETE, generic CRUD, compare, or restore endpoint was added.

## 3. Authorization — PASS

Revision reads use the existing authenticated Viewer fallback policy on the owning KnowledgeDocument; Revision has no independent ACL. Focused API tests proved Viewer, Editor, and Administrator list access and Viewer detail access. Existing write endpoints retain the Editor policy, and a Viewer content-write attempt remains 403.

## 4. Pagination and query performance — PASS

The list resolves only the owning document's current/latest-published pointers, counts the matching revisions, and performs database-side `ORDER BY revision_number DESC`, `OFFSET`, and `LIMIT` through EF Core. It never loads all revision rows for frontend pagination. Author presentation is projected from immutable `AuthorDisplayNameSnapshot`; the query does not join or issue per-revision User lookups.

Tests covered defaults, page 2, page size 100, invalid values, total count, newest-first ordering, and a page beyond the available range. Runtime verification with 21 revisions produced exactly 20 rows on page 1 and one row on page 2. A page change issued one list request followed by the necessary detail request for the newly selected first row; it did not reload the current document or create a request storm.

## 5. Frontend history mode — PASS

The existing KnowledgeDocument detail route now exposes `修订历史（N）`, where `N` comes directly from the contiguous `currentRevisionNumber`; no extra COUNT endpoint/request was introduced. The entry is visible to Viewer, Editor, and Administrator for Draft, Published, and Archived documents.

Selecting the entry switches the existing route's Main Content into local history mode. It does not add a top-level route, drawer, drawer manager, permanent history section, or browser-history state. The mode contains a clear `返回当前内容` action. List and detail have independent loading, empty, and error states; a detail failure preserves the already loaded list. Page/selection state remains local, and entering a populated page selects its first revision.

The historical surface is immutable and read-only. It contains no edit, delete, compare, diff, or restore action.

## 6. Baseline truthfulness and metadata — PASS

Origin labels are `创建`, `内容保存`, `历史恢复`, and `迁移基线`. Lifecycle labels are Draft/草稿, Published/已发布, and Archived/已归档, explicitly described as `修订生成时生命周期` rather than an independent Revision lifecycle.

MigrationBaseline with a null actor renders `迁移基线`, `历史作者未知`, and `捕获于 <CreatedAt>`; it does not guess an author or call the time a modification time. Non-baseline author text comes from the immutable author display-name snapshot. Restore-origin metadata renders read-only as `历史恢复`, `从修订 K 恢复`, and its reason without exposing a Restore button.

## 7. Current/latest-published markers — PASS

Both markers are explicit text badges and are not color-only. Automated tests cover a revision carrying both markers as well as the legal split state. The browser runtime verified a Draft current revision 21 marked `当前版本` and an older published revision 2 independently marked `最近发布`. Marker values agreed with both API projections and SQLite pointers.

## 8. Historical safe Markdown — PASS

Selecting a revision loads its exact immutable title, summary, body, and metadata into Main Content. Rendering reuses the existing shared `renderMarkdown` boundary with raw HTML disabled; Milkdown and a second renderer were not loaded.

Focused security regression covers raw HTML/script, JavaScript URLs, dangerous links, and malformed Markdown. In the real browser, the historical body contained a raw `<script>` and a `javascript:` link fixture; the rendered DOM contained zero script elements and zero JavaScript-protocol anchors. No editor or write action appeared in the historical preview.

## 9. Dirty guard — PASS

History entry reuses the existing dirty-discard confirmation. Automated and real-browser checks proved:

- Cancel/`继续编辑` keeps the editor and the exact unsaved title.
- Confirm/`放弃修改` exits editing and only then enters history mode.

No second dialog or competing dirty-state implementation was introduced.

## 10. Responsive UX and accessibility — PASS

The history list/detail layout uses the existing visual system, collapses to one column at narrower widths, wraps/truncates long technical content, and retains text-labelled keyboard-reachable buttons. Runtime browser measurements reported no page-level horizontal overflow:

| Viewport | Root client/scroll width | Main layout client/scroll width |
|---|---:|---:|
| 1920×1080 | 1920 / 1920 | 1664 / 1664 |
| 1714×892 | 1714 / 1714 | 1458 / 1458 |
| 1366×768 | 1366 / 1366 | 1111 / 1111 |
| 1024×768 | 1024 / 1024 | 769 / 769 |

A focused shell media rule removes the prior 1180px minimum at sub-1180 widths and compacts the top bar; no new UI library was introduced.

## 11. REV-B01 regression — PASS

The final affected backend regression selection passed 22/22 tests across revision create/save/no-op/publish behavior, HumanConfirmation revision snapshots/coverage, Evidence/KnowledgeStatus, current-head search/FTS, KnowledgeDocument APIs, and the new revision reads. Existing revision write authorization remains intact. No schema or migration changed in B02.

## 12. Build, tests, and lint — PASS

| Gate | Result |
|---|---|
| `dotnet build SystemKnowledgeHub.sln --no-restore` | PASS — 0 warnings, 0 errors |
| B02 + REV-B01/KC affected backend selection | PASS — 22/22 |
| `npm run type-check` | PASS |
| B02 focused Vitest | PASS — 4 files, 18/18 tests |
| `npm run build` | PASS — production build completed; only the existing non-blocking chunk-size advisory |
| REV-B02 modified-scope ESLint | PASS — no warnings/errors |
| `git diff --check` | PASS — no whitespace errors; only repository CRLF conversion notices |

The optional full ESLint run remains blocked only by the two unrelated REPO-CLEAN-B01 baselines: unused props in `CreateIntegrationDialog.vue` and the empty interface in `unknownItemContracts.ts`; neither was changed. An optional broad Vitest attempt passed 25 files / 71 tests and encountered the already-recorded unrelated stale `AppShell.spec.ts` expectation for `关系与缺口`; all required B02 and affected tests pass.

## 13. Browser → API → SQLite runtime — PASS

Runtime verification used an isolated temporary SQLite database, disposable Local administrator, isolated Data Protection keys, API on 127.0.0.1:5109, and Vite on 127.0.0.1:5176. The real browser logged in, created the KnowledgeDocument through the product, performed content saves and publish/return-to-Draft transitions through the frozen APIs, and exercised the complete detail → history → current/older/baseline preview → return flow. A controlled change to revision 1 in the isolated fixture set `MigrationBaseline` and a null actor solely to exercise truthful legacy-baseline read compatibility; repository data was not touched.

The final runtime document had 21 revisions, current pointer 21, latest-published pointer 2, and Draft lifecycle. Page 1 rendered revisions 21 through 2 newest-first; page 2 rendered only revision 1 and automatically selected it. Historical title, summary, body, origin, lifecycle, actor snapshot, times, and markers agreed across UI, API projections, and immutable SQLite rows.

Final SQLite checks reported `integrity_check=ok`, zero foreign-key violations, 21 strictly descending revision numbers, exact current-head equality with revision 21, a truthful MigrationBaseline/null-actor revision 1, and current-head FTS text equal to the normalized current document. The browser console contained no errors. An existing `el-tooltip` resolution warning occurs when the current editor is mounted; it is outside the new read-only history component and was not changed in this slice.

## 14. Network smoke — PASS

The history-entry count is supplied by the already-loaded current document. Entering history performs the required paged list request and selected-revision detail request. Revision selection performs only its detail request; changing page performs one paged list request and one selected detail request. No per-revision author API/User lookup, redundant current-document reload, infinite-scroll traffic, or failed application request occurred in the verified flow.

## 15. Cleanup — PASS

The agent-created browser tabs were closed and the automation tab list was empty. Only the task-owned API and Vite sessions were stopped; ports 5109 and 5176 had no remaining listeners. The validated task-specific temporary directory, runtime SQLite database, disposable account data, and Data Protection key were permanently removed. Repository `App_Data` and pre-existing processes were untouched.

## 16. Explicitly not implemented

REV-B02 does not implement Compare/Myers diff, Restore API/use case/button, historical edit/delete, historical FTS, revision-scoped Evidence/Relationships, Approval, Attachments, Comments, Spaces, AI/RAG, a new route hierarchy, a drawer manager, or a generic revision framework.

REV-B03 and REV-B04 were not started. Work stops here for the human Verification Gate.
