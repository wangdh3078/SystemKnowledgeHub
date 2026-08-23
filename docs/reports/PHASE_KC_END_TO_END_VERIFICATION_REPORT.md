# PHASE-KC-VERIFY — Knowledge Content End-to-End Verification Report

## Result

```text
PHASE-KC-VERIFY FAIL
```

The verification found two blocking gaps: the frontend production build is currently red, and KnowledgeStatus audit attribution still trusts request-body actor data. No production code was changed in this verification-only phase.

## Baseline

Before verification, `git status`, `git diff --stat`, and `git diff` recorded a substantially dirty worktree containing earlier documentation moves, AUTH work, KC-B01–B07, UI work, migrations, tests, and untracked reports/features. This phase did not reset, clean, revert, format, or overwrite those changes.

## Capability Matrix

| Capability | Expected | Actual | Result | Evidence |
| --- | --- | --- | --- | --- |
| Authentication / Current User | Local login resolves canonical Administrator; client header cannot select identity. | Temporary Local Administrator login returned 204 and `/api/current-user` returned canonical Administrator. Existing CurrentUser/LocalLogin tests passed. | PASS | Runtime API; 35 focused backend tests. |
| Login UX | Local-only gate is usable and 502 is not treated as a login success. | Local-only form, brand area and capability chips rendered; 1280px `scrollWidth == width`. A PowerShell proxy produced 502 until `curl --noproxy localhost` was used; direct local API returned 200, so it was environment proxy behavior, not Login Gate behavior. | PASS | Browser DOM/runtime; direct HTTP. |
| Document foundation/list/detail | Seven controlled types, Draft/Unknown create, canonical Markdown and access controls. | Existing focused API tests passed; runtime SOP created as Draft/Unknown. | PASS | `KnowledgeDocumentsApiTests`; runtime document id 1. |
| Editor / Markdown safety | Milkdown round-trip, safe preview, dirty/conflict behavior, production frontend build. | Targeted editor/renderer/detail tests passed; but type-check and build fail in a test file. Browser authenticated edit chain was not claimed after the build gate failed. | FAIL | 31 focused Vitest passed; TS2349 at `KnowledgeDocumentDetailView.spec.ts:174`. |
| Lifecycle / status separation | Explicit lifecycle transitions; Evidence/confirmation do not auto-advance status. | Runtime Evidence kept Unknown; explicit Inferred then HumanConfirmation kept Inferred; explicit Confirmed; Published/Archived preserved Confirmed; archive removed FTS result. | PASS | Isolated SQLite/API runtime. |
| HumanConfirmation identity | Principal-backed canonical User and KnowledgeRole snapshot. | Runtime confirmation recorded `Phase 验证管理员` and fallback knowledge-identity snapshot; no status auto-change. | PASS | `/api/evidence/human-confirmations` and evidence list. |
| KnowledgeStatus audit identity | Actor attribution must be trusted server-side. | Controller accepts client `actor` payload and forwards it to service. | FAIL | Source audit; KC-GAP-002. |
| Relationships | Explicit document→System edges, duplicates/self guarded, no implicit status change. | Runtime `AppliesTo` relationship returned 201; tests cover duplicate/self/read-write boundaries. Architecture vocabulary drift remains. | PASS WITH FOLLOW-UP | Runtime; `RelationshipsApiTests`; KC-GAP-003. |
| Search / FTS | Canonical Markdown, derived FTS, body search, archive exclusion and bounded performance. | Runtime body search produced KnowledgeDocument group; archived document disappeared. 1,000-document performance test passed. | PASS | Runtime; search tests. |
| Unified System View | Bounded read projection, both-direction document relation, no writes/N+1 API storm. | Runtime System 12 showed the related document; focused endpoint tests passed. | PASS | Runtime; `SystemKnowledgeViewApiTests`. |

## Commands Executed

| Command | Result |
| --- | --- |
| `dotnet build SystemKnowledgeHub.sln --no-restore` | PASS — 0 warnings, 0 errors. |
| Focused `dotnet test` filter for LocalLogin, CurrentUser, AccessControl, KnowledgeDocuments, Relationships, Evidence/Status, Search/FTS and SystemKnowledgeView | PASS — 35 passed, 0 failed. |
| `npm run test -- --run` targeted SecurityGate, editor, dirty state, renderer, detail, search, unified view, global-create and logout specs | PASS — 31 passed, 0 failed. |
| Scoped `npx eslint --quiet` for verified frontend surfaces | PASS. |
| `npm run type-check` | FAIL — TS2349 at `KnowledgeDocumentDetailView.spec.ts:174`. |
| `npm run build` | FAIL — stopped by the same TypeScript error before Vite build. |

## Runtime Data Chain

An isolated SQLite/Data Protection runtime was created under `artifacts/phase-kc-verify-runtime-20260823` with one temporary administrator.

```text
local login → canonical Current User → create SOP Draft/Unknown
→ document AppliesTo System MES → FTS body search → System Unified View
→ ordinary Evidence (still Unknown) → explicit Inferred
→ HumanConfirmation snapshot (still Inferred) → explicit Confirmed
→ Publish → Archive (still Confirmed; excluded from search)
```

The browser verified the unauthenticated Local-only Login Gate. The authenticated browser document-edit chain is intentionally recorded as not completed because the frontend production build gate failed first.

## Architecture / Scope Review

- `body_markdown` remains canonical; FTS is derived and joins back to `knowledge_documents`.
- HumanConfirmation is principal-backed; `X-Current-User-Id` was not reintroduced in Current User/document paths.
- No current document version history, attachments, comments, autosave, spaces/tree, tags, ACLs, AI/RAG, TestRun, or incident-management scope was found; these remain future-scope exclusions.
- Relationship vocabulary deviates from the approved plan; see KC-GAP-003.

## Required Follow-up

See [PHASE_KC_GAP_REGISTER.md](PHASE_KC_GAP_REGISTER.md). The next work must be a focused corrective gate, not a new Knowledge Content capability slice. After the two blocking gaps are corrected, rerun this phase’s type-check/build and complete the authenticated browser chain before considering Internal Pilot readiness.

## Cleanup

Stopped only the inspected verification process roots and their direct children (API PowerShell `18236` / `dotnet` `38796`; Vite PowerShell `22332` / Node `12144`, `39216`). Ports 5099 and 5175 had no listener afterwards. The temporary browser tab and the exact `artifacts/phase-kc-verify-runtime-20260823` directory, including cookie jars, SQLite database, Data Protection keys and logs, were removed. No persistent test data is retained.
