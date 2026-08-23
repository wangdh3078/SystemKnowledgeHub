# REV-B03 — Revision Compare UX Verification Report

**Result: REV-B03 PASS**

**Verification date:** 2026-08-23  
**Scope:** frontend-only immutable KnowledgeDocument revision comparison UX

## 1. Worktree Baseline

The normative authority was read before implementation: `AGENTS.md`, the frozen REV-A01 architecture decision and freeze report, the REV-B01 and REV-B02 verification reports, and the next-product-capability plan. No authority conflict was found.

The first worktree gate detected uncommitted REV-B02 work and stopped without editing it. After that work was committed, the gate was repeated and passed on clean `main`, synchronized with `origin/main`, at `933ac44 🦄 refactor: 调整代码结构`. Frozen specifications, Golden assets, task definitions, backend code, migrations, and repository `App_Data` were not modified.

## 2. Architecture Compliance

Comparison is implemented entirely inside the existing KnowledgeDocument frontend feature. History Mode enters a local Compare Mode in the existing detail route and Main Content; it adds no top-level route, drawer manager, workspace, global store, generic diff service, backend compare endpoint, or persisted diff.

The component obtains immutable snapshots only through the existing typed `getKnowledgeDocumentRevision(documentId, revisionNumber, signal)` API. The algorithm and field model are isolated in typed, DOM-independent modules. A static search found no Compare implementation or route in `SystemKnowledgeHub.Api` or its tests.

## 3. Compare Pair Semantics

Entering Compare from selected revision N defaults to N-1 → N. The already-loaded selected snapshot seeds a component-local cache, so only the missing previous snapshot is fetched. Revision 1 performs no fetch and no diff and displays `这是最早的修订，没有更早版本可比较`.

Both selectors are explicitly labelled `从` and `到`. Any valid manual pair is supported. A reverse selection is deterministically normalized to older → newer, both selectors are updated, and a status message explains the change. A same-revision pair performs no fetch and no Myers work and displays `两个修订相同，没有可比较的变化`. Returning to History preserves the existing list, page, selection, and loaded detail state.

## 4. Myers Algorithm

`myersLineDiff.ts` implements a deterministic, linear-space Myers bisect for LF-separated lines. It has no DOM dependency, global state, hidden mutation, external viewer, or new library. Stable tie-breaking and replacement normalization produce a repeatable delete-before-add representation.

Focused tests cover empty/add/remove/unchanged/replace, middle insertion/deletion, duplicate lines, repeated headings, repeated code lines, Chinese text, blank lines, and final-newline behavior. An exhaustive check across all small duplicate-line inputs reconstructs the target and verifies that the edit count is shortest against an independent LCS calculation.

## 5. Determinism

The same duplicate/Markdown/code/Chinese fixture was compared repeatedly and returned byte-for-byte equivalent segment structures. Stable tie-breaking is encoded in the algorithm rather than depending on object order, DOM state, or timing.

## 6. Title/Summary Comparison

Title uses only unchanged or old/new changed states; it never runs Myers. Summary separately and correctly represents null/null unchanged, null/text added, text/null removed, different changed, and equal unchanged. Changed fields render explicit `旧版本` and `新版本` values, including `（空）` for null.

## 7. Body Diff

BodyMarkdown is split strictly on `\n`; empty lines are retained. Empty content has zero line tokens, while a non-empty trailing LF creates a final empty line token, so adding or removing a final newline is localized instead of producing a broad false diff.

The UI renders unchanged, removed, and added lines with a visible blank/`-`/`+` prefix and text legend. Chinese content, fenced SQL, Markdown tables, long technical lines, and literal Markdown/HTML are compared as immutable plain text. No line numbers, word diff, Markdown AST diff, or unchanged-context collapsing were introduced.

## 8. Size Limits

The preflight guard computes the exact combined JavaScript string-unit count across Title, nullable Summary, and BodyMarkdown, plus the exact combined body-line token count. The frozen boundaries are inclusive:

- combined content units <= 2,005,000
- combined body lines <= 10,000

Tests prove exact-boundary acceptance and one-over rejection for both limits. The guard returns before Myers is called.

## 9. Oversized UX

If either limit is exceeded, the comparison has only an `oversized` result and no body field or partial diff. The UI renders `该版本组合超出比较限制，未生成差异结果`, explains that both revisions remain separately viewable in History, and does not mount the normal comparison result.

## 10. XSS/Safe Rendering

Compare renders lines and field values only through Vue escaped text interpolation. It does not use `v-html`, Markdown rendering, `innerHTML`, syntax-highlighting HTML, or a second parser.

Automated and real-browser fixtures included `<script>alert(1)</script>`, `<img src=x onerror=alert(1)>`, and `[j](javascript:alert(1))`. The literal text remained visible while the DOM contained zero script elements, zero image elements/event handlers, and zero JavaScript-protocol anchors.

## 11. Compare UX

The view contains a clear `返回修订历史` action, pair selectors, explicit direction summary, paired immutable metadata, Title, Summary, and Body sections. Both sides truthfully show revision number, origin, immutable author snapshot, creation/capture time, lifecycle-at-generation, and independent current/latest-published markers.

MigrationBaseline renders `迁移基线`, `历史作者未知`, and `捕获于`. Restore-origin metadata remains read-only and renders `历史恢复`, its source revision, and reason. Compare contains no Restore, edit, delete, mutation, or historical-write action.

Loading clears all prior pair results and exposes one complete `正在加载两个修订快照…` state. A failure on either side prevents diff generation and reuses the existing accessible error/retry UX. AbortController plus a monotonically increasing request sequence prevents an older response from overwriting the latest selector pair; the race regression passed.

## 12. Responsive/Accessibility

Selectors have explicit labels and native keyboard-operable Element Plus controls. Loading, errors, normalization, identical/same-pair states, and the earliest-revision state use readable text/status semantics. Current/latest markers and added/removed states use text and symbols, not color alone. Focus order follows the visual order.

The diff body owns its horizontal/vertical overflow, while the root page remains width-bounded. A final user-paced browser rerun reported no root or compare-surface horizontal overflow:

| Viewport | Root client/scroll width | Compare client/scroll width | Selectors client/scroll width |
|---|---:|---:|---:|
| 1920×1080 | 1920 / 1920 | 1649 / 1649 | 1647 / 1647 |
| 1714×892 | 1714 / 1714 | 1443 / 1443 | 1441 / 1441 |
| 1366×768 | 1366 / 1366 | 1111 / 1111 | 1109 / 1109 |
| 1024×768 | 1024 / 1024 | 769 / 769 | 767 / 767 |

## 13. Network Smoke

The default pair reuses the selected B02 snapshot and fetches only its missing predecessor. Pair changes use `Promise.all` for missing snapshots and a small component-local Map for already-read revisions. Component call-count assertions and the browser flow confirmed no current-document reload, per-user/N+1 lookup, compare API, API storm, or stale-result overwrite.

## 14. B02/B01 Regression

The B02 History component regression verifies entering Compare with the current selected snapshot and returning without reloading or losing History state. The affected frontend selection passed 34/34 tests across Compare, History, detail integration, typed contracts, and shared Markdown safety.

The final affected backend selection passed 22/22 tests across revision list/detail reads, revision foundation/write behavior, Evidence/KnowledgeStatus, KnowledgeDocument search/current-head behavior, authorization, and existing KnowledgeDocument APIs. No backend business logic, schema, migration, or persistence changed.

## 15. Build/Tests/Lint

| Gate | Result |
|---|---|
| `dotnet build SystemKnowledgeHub.sln --no-restore` | PASS — 0 warnings, 0 errors |
| REV-B01/B02 and affected backend selection | PASS — 22/22 |
| `npm run type-check` | PASS |
| Affected frontend Vitest selection | PASS — 7 files, 34/34 tests |
| `npm run build` | PASS — production build completed; only the existing non-blocking chunk-size advisory |
| REV-B03 modified-scope ESLint | PASS — 0 warnings/errors |
| `git diff --check` | PASS — no whitespace errors; only repository CRLF conversion notices |

The optional full ESLint run still reports exactly the two unrelated recorded baselines: unused `props` in `CreateIntegrationDialog.vue` and the empty interface in `unknownItemContracts.ts`. Neither file was changed.

## 16. Browser Runtime

Runtime verification used an isolated temporary SQLite database, disposable Local Administrator, isolated Data Protection keys, API on 127.0.0.1:5116, and Vite on 127.0.0.1:5183. The immutable fixture contained five contiguous revisions: the frozen Oracle Listener baseline, an added list, a Summary change, a fenced SQL addition, and a final Chinese/table/body/Title/Summary change. Current revision was 5, latest-published revision was 4, and lifecycle was Draft.

The real browser completed Login → Document Detail → Revision History → Compare → default 4→5 → manual 1→5 → reverse selection/normalization → Return History. UI values agreed with SQLite snapshots and pointers. Added/removed/unchanged segments, Chinese, fenced SQL, Markdown table text, current/latest markers, baseline metadata, XSS literal text, and absence of Restore/historical edit all passed. The final browser error list was empty.

SQLite verification reported `integrity_check=ok`, zero foreign-key violations, exact revisions 1–5, correct origin progression, stable actor snapshots, exact current-head equality with revision 5, and correct current/latest-published pointers.

## 17. Cleanup

The agent-created browser tabs were closed before final acceptance. All task-owned API/Vite listeners are absent on ports 5116 and 5183; the launch-profile false-start port 5090 is also free. The precisely resolved isolated directory, runtime SQLite/WAL/SHM, disposable account data, logs, and Data Protection key were permanently removed. Repository `App_Data` and pre-existing processes were untouched.

During verification, Codex Desktop restarted and all PTY-owned verification children disappeared. Read-only diagnostics established that this was a Codex host crash, not a repository cleanup command: Crashpad recorded `capture_kind=crash`, `ptype=browser` at 21:03:35, the desktop log showed repeated primary-renderer `ResizeObserver` errors immediately beforehand, no `Stop-Process`, `taskkill`, or name-based process termination had been executed, and Windows Application log contained no separate application-error entry. The likely trigger was the in-app Browser's earlier high-frequency viewport automation; no dump stack exists, so that trigger remains a supported inference rather than a proven exact stack cause. The final responsive run had already been repeated at an 800 ms user-paced interval with no browser/Vite errors. Subsequent gates used only bounded one-shot processes, and current Codex remained responsive.

## 18. Explicitly Not Implemented

REV-B03 does not implement Restore API/UI/use case, revision mutation/delete, branch/merge, semantic Markdown AST or rich-text structural diff, word-level diff, historical FTS, approval/comments/attachments/spaces/AI, a backend compare endpoint, persisted diff, generic diff service, new route, new drawer manager, Monaco/CodeMirror, an external diff viewer, or a new UI library.

REV-B04 was not started. Work stops here for the human Verification Gate.
