# REPO-CLEAN-A01 — Repository Workspace Audit Report

## Result

```text
REPO-CLEAN-A01 READY FOR HUMAN REVIEW
```

Audit date: 2026-08-23  
Audit mode: read-only repository/content/reference audit; no cleanup executed

## 1. Scope and safety result

This audit read the repository governance, solution/build configuration, source and test trees, documentation, EF migration chain/model snapshot, ignored runtime data, Git index/worktree, duplicate hashes, and large files. It added only this report and the companion cleanup plan.

No source, test, migration, database, document, asset, directory, or existing Git entry was deleted, moved, renamed, reset, restored, cleaned, or reformatted. No `.gitignore` change was made. No build/test was run because production code was not changed by REPO-CLEAN-A01; only read-only `dotnet ef ... --no-build` checks were used for migration/model inspection.

## 2. Worktree baseline

The required baseline commands were executed: `git status --short`, `git status`, `git diff --stat`, the full `git diff`, `git ls-files`, and `git ls-files --others --exclude-standard`.

| Baseline fact | Observation |
|---|---|
| Branch | `main`, synchronized with `origin/main` at `e6fbeeb` |
| Tracked index entries | 556 |
| Staged entries | 13, all R100 documentation moves |
| Unstaged content diffs | 78 files, 3,575 insertions / 426 deletions |
| Full unstaged diff | 6,173 lines; SHA-256 `a6e6633b1463f15924a157b2e06524c1ef4fe4fc523fee27ba198ad2702f4b00` |
| Untracked files | 74 |
| Deleted paths | 0 staged, 0 unstaged |
| Extra status-only path | `src/SystemKnowledgeHub.Api/appsettings.json` is reported ` M`, but HEAD/index/worktree hashes are all `aa66688325885d4d5947bff733f7032d9c33ffcc` and `git diff` is empty; this is a filesystem/index-stat false positive, not a content change |
| Diff whitespace | Existing baseline had line-ending conversion notices; no whitespace error was established |

The worktree is not garbage-heavy; it is mainly an **uncommitted formal delivery set** spanning AUTH/UI/KC/REV. Cleanup must not begin until those deliverables are reviewed and protected in Git.

## 3. Repository structure

```text
.
├─ .github/workflows/                 empty local placeholder
├─ .vs/                               ignored Visual Studio state
├─ artifacts/                         ignored local QA/runtime images
├─ docs/
│  ├─ design/
│  ├─ planning/
│  ├─ product-design/
│  ├─ reports/
│  ├─ specifications/
│  ├─ standards/
│  └─ PROJECT_FILE_MAP.md
├─ product-design/                    tracked Golden and design-history images
├─ src/
│  ├─ SystemKnowledgeHub.Api/
│  └─ SystemKnowledgeHub.Web/
├─ tests/SystemKnowledgeHub.Api.Tests/
├─ AGENTS.md
├─ README.md
├─ SystemKnowledgeHub.sln
├─ global.json
├─ NuGet.Config
└─ design-qa.md
```

No `scripts/` or `tools/` tree exists. No root `src/features` tree exists. The only `src` children are the two frozen projects. No filesystem reparse point/symlink was found.

| Area | Formal role / build reference | Audit result |
|---|---|---|
| `src/SystemKnowledgeHub.Api` | .NET 8 SDK Web project in the solution; default SDK compile includes Feature `.cs` files | ACTIVE_PRODUCTION_SOURCE |
| `src/SystemKnowledgeHub.Web` | Vite/Vue project; `tsconfig.app.json` includes `src/**/*.ts`, `src/**/*.tsx`, `src/**/*.vue`; `index.html` loads `/src/main.ts` | ACTIVE_PRODUCTION_SOURCE plus colocated frontend tests |
| `tests/SystemKnowledgeHub.Api.Tests` | .NET test project in the solution with an API project reference | ACTIVE_TEST_SOURCE |
| `docs` | specifications, design/ADR, planning, reports, standards, and product-design history | KEEP_TRACKED / KEEP_HISTORY |
| `product-design/final-ui` | frozen Final UI Inventory target | DO_NOT_TOUCH |
| other `product-design` children | generation history, review boards, QA, source variants, and archive | KEEP_HISTORY |
| `artifacts` | ignored local verification evidence; multiple reports reference it | HUMAN_REVIEW, not bulk-delete-safe |

## 4. Source classification

### ACTIVE_PRODUCTION_SOURCE

- All backend Feature, Persistence, Shared, and composition-root `.cs` files under the API project are included by the SDK project. The six untracked non-migration backend files are referenced by `Program`, `KnowledgeHubDbContext`, controllers/services, or focused tests.
- All non-`*.spec.ts` frontend source under `src/SystemKnowledgeHub.Web/src` participates in the Vite/TypeScript graph. The untracked KnowledgeDocument, Unified View, and favicon files are imported by router/page/component entry points or `index.html`.
- The Bootstrap diagnostic is still an active `/foundation` route and `/api/bootstrap/status` endpoint with tests. It is not dead source, although its long-term production role needs a security/product decision.

### ACTIVE_TEST_SOURCE

- All backend files under `tests/SystemKnowledgeHub.Api.Tests` are included by the SDK test project.
- All frontend `*.spec.ts` files are discoverable by Vitest; the TypeScript application include also type-checks files under `src`.

### DEAD_OR_UNREFERENCED_SOURCE

No non-empty source file met the evidence threshold for this classification.

### DUPLICATE_OR_MISPLACED_SOURCE

No non-empty source file had a proven canonical replacement. Four empty directories are cleanup candidates, but they contain no source.

### HUMAN_REVIEW

`BootstrapController.cs`, `bootstrapApi.ts`, and `FoundationView.vue` are genuinely referenced and tested. `SEC_A01_SECURITY_ACCESS_CONTROL_DESIGN_REVIEW.md` says the diagnostic should not be exposed as a Production surface, while current reports record it as authenticated and intentionally retained. Removal is therefore a future product/security decision, not repository dead-code cleanup.

## 5. Tracked changes

The 78 real unstaged content diffs are classified as expected active implementation/history:

| Group | Files | Classification | Evidence |
|---|---:|---|---|
| Backend production/config | 35 | expected active implementation | AUTH/KC/REV changes under Evidence, KnowledgeDocuments, KnowledgeStatus, Relationships, Search, Systems, Users, DbContext, and Program; referenced by DI/controllers/services/tests |
| EF ModelSnapshot | 1 | generated update, DO_NOT_TOUCH | matches the latest migration/model; no pending model change |
| Frontend production/config | 31 | expected active implementation | package/API/router/security/evidence/status/relationship/search/system/shell/style updates in the current Vite graph |
| Backend tests | 7 | ACTIVE_TEST_SOURCE | current SDK test project |
| Frontend tests | 2 | ACTIVE_TEST_SOURCE | Vitest contract tests |
| Repository documentation | 2 | expected documentation update | README and Project File Map changes from the completed documentation structure work |

The additional status-only `appsettings.json` path has byte-identical HEAD/index/worktree content and belongs in HUMAN_REVIEW only for index refresh; it must not be edited to make status appear clean.

## 6. Untracked files

All 74 baseline untracked files are formal delivery candidates; none is classified as garbage.

### Documentation — 24, KEEP_TRACKED / KEEP_HISTORY

```text
docs/design/AUTH_A01_LOCAL_PASSWORD_POLICY_AMENDMENT.md
docs/design/KC_C01_RELATIONSHIP_VOCABULARY_ARCHITECTURE_DECISION.md
docs/design/REV_A01_REVISION_ARCHITECTURE_AND_CONTRACT_DECISION.md
docs/planning/PHASE_NEXT_PRODUCT_CAPABILITY_PLAN.md
docs/reports/AUTH_B02_LOGIN_UI_AUTH_OPTIONS_VERIFICATION_REPORT.md
docs/reports/DOC_STRUCTURE_B01_DOCUMENTATION_REPOSITORY_CLEANUP_VERIFICATION_REPORT.md
docs/reports/KC_B02_KNOWLEDGE_DOCUMENT_READ_LIST_UX_VERIFICATION_REPORT.md
docs/reports/KC_B03_KNOWLEDGE_DOCUMENT_MARKDOWN_EDITOR_VERIFICATION_REPORT.md
docs/reports/KC_B04_KNOWLEDGE_DOCUMENT_RELATIONSHIPS_VERIFICATION_REPORT.md
docs/reports/KC_B05_KNOWLEDGE_DOCUMENT_EVIDENCE_STATUS_VERIFICATION_REPORT.md
docs/reports/KC_B06_KNOWLEDGE_DOCUMENT_SEARCH_FTS_VERIFICATION_REPORT.md
docs/reports/KC_B07_UNIFIED_KNOWLEDGE_VIEW_VERIFICATION_REPORT.md
docs/reports/KC_C01_RELATIONSHIP_VOCABULARY_DECISION_REPORT.md
docs/reports/KC_C02_RELATIONSHIP_VOCABULARY_CONTRACT_CORRECTION_VERIFICATION_REPORT.md
docs/reports/KC_FIX_01_FRONTEND_BUILD_GATE_REPAIR_VERIFICATION_REPORT.md
docs/reports/PHASE_KC_END_TO_END_REVERIFICATION_R01_REPORT.md
docs/reports/PHASE_KC_END_TO_END_VERIFICATION_REPORT.md
docs/reports/PHASE_KC_GAP_REGISTER.md
docs/reports/PHASE_NEXT_A01_PRODUCT_CAPABILITY_PLANNING_REPORT.md
docs/reports/REV_A01_REVISION_ARCHITECTURE_AND_CONTRACT_FREEZE_REPORT.md
docs/reports/REV_B01_IMMUTABLE_REVISION_FOUNDATION_VERIFICATION_REPORT.md
docs/reports/SEC_KC_FIX_02_KNOWLEDGE_STATUS_TRUSTED_ACTOR_VERIFICATION_REPORT.md
docs/reports/UI_B03_GLOBAL_CREATE_LOGOUT_LOGIN_POLISH_VERIFICATION_REPORT.md
docs/reports/UI_B04_LOGIN_EDITOR_VISUAL_MODERNIZATION_VERIFICATION_REPORT.md
```

Their headings/status markers and cross-references show approved amendments, architecture decisions, the phase plan, gap register, and completed verification reports. Superseded decision text remains audit history rather than a deletion candidate.

### Backend production source — 6, KEEP_TRACKED

```text
src/SystemKnowledgeHub.Api/Features/KnowledgeDocuments/Domain/KnowledgeDocumentRevision.cs
src/SystemKnowledgeHub.Api/Features/KnowledgeDocuments/Persistence/KnowledgeDocumentRevisionConfiguration.cs
src/SystemKnowledgeHub.Api/Features/Search/Application/KnowledgeDocumentSearchIndex.cs
src/SystemKnowledgeHub.Api/Features/Search/Application/KnowledgeDocumentSearchText.cs
src/SystemKnowledgeHub.Api/Features/Systems/Application/Models/SystemKnowledgeViewModels.cs
src/SystemKnowledgeHub.Api/Features/Systems/Application/SystemKnowledgeViewQueries.cs
```

### EF migrations — 8 files / 5 migrations, DO_NOT_TOUCH and KEEP_TRACKED

```text
src/SystemKnowledgeHub.Api/Persistence/Migrations/20260822124136_AddKnowledgeDocumentRelationships.cs
src/SystemKnowledgeHub.Api/Persistence/Migrations/20260822124136_AddKnowledgeDocumentRelationships.Designer.cs
src/SystemKnowledgeHub.Api/Persistence/Migrations/20260822213000_AddKnowledgeDocumentEvidenceSubject.cs
src/SystemKnowledgeHub.Api/Persistence/Migrations/20260822223000_AddKnowledgeDocumentSearchFts.cs
src/SystemKnowledgeHub.Api/Persistence/Migrations/20260823022046_TightenRelationshipVocabulary.cs
src/SystemKnowledgeHub.Api/Persistence/Migrations/20260823022046_TightenRelationshipVocabulary.Designer.cs
src/SystemKnowledgeHub.Api/Persistence/Migrations/20260823092808_AddImmutableKnowledgeDocumentRevisions.cs
src/SystemKnowledgeHub.Api/Persistence/Migrations/20260823092808_AddImmutableKnowledgeDocumentRevisions.Designer.cs
```

### Frontend production/config — 14, KEEP_TRACKED

`public/favicon.svg`; eleven KnowledgeDocument API/component/editor/markdown/page/style files; and three Unified View contract/component/composable files. The favicon is referenced by `index.html`; the feature files are imported by routes/components and included by strict TypeScript.

### Frontend tests — 13, KEEP_TRACKED

SecurityGate/authentication, KnowledgeDocument contract/editor/edit-state/round-trip/renderer/detail, relationship/search contracts, create chooser, Unified View, and top-bar specifications are formal Vitest sources.

### Backend tests — 9, KEEP_TRACKED

KnowledgeDocument Evidence/Status, Revision, Search API/performance, Unified View, Revision/Search/Relationship migration, and SQLite FTS capability tests are formal SDK test sources.

## 7. Renamed and deleted files

The 13 staged moves are all R100, so content identity is preserved:

- two root architecture/design documents → `docs/design/`;
- AUTH-B01, KC-B01, SEC-01–04, UI-B02, and XML-DOC-B01–04 reports → `docs/reports/`.

`DOC_STRUCTURE_B01_DOCUMENTATION_REPOSITORY_CLEANUP_VERIFICATION_REPORT.md` records the exact old/new mapping. README and Project File Map use canonical `docs/design` / `docs/reports` references. A targeted search found no non-canonical references to the old root paths. These moves are expected documentation organization and should be retained/committed.

There are no staged or unstaged `D` entries and no suspected accidental deletion.

## 8. Suspicious directories

| Path | State | References | Classification / risk |
|---|---|---|---|
| `.github/workflows` | empty, untracked, not a link | none; no workflow file exists | SAFE_DELETE / Low |
| `src/SystemKnowledgeHub.Api/Controllers` | empty, untracked, not in csproj | no path reference; real controllers are Feature-first | SAFE_DELETE / Low |
| `src/SystemKnowledgeHub.Web/.vscode` | empty and ignored | none | SAFE_DELETE / Low |
| `src/SystemKnowledgeHub.Web/src/assets` | empty, untracked | no `src/assets` or `@/assets` import | SAFE_DELETE / Low |
| `src/features` | absent | n/a | no issue |
| `artifacts` | 54 ignored files, 27.40 MiB | Bootstrap and VS01–VS06 reports/reference documents point into it | HUMAN_REVIEW / Medium |
| `src/SystemKnowledgeHub.Api/App_Data` | runtime DB + SHM/WAL; csproj includes the folder | connection string and design-time factory use it | DO_NOT_TOUCH / High |

## 9. Documentation audit

Baseline `docs/` contains 86 files: 9 design, 1 planning, 7 product-design history, 60 reports, 7 frozen specifications, 1 standard, and Project File Map. Of those, 24 are untracked formal deliverables. This task adds one report and one planning file, raising the post-task total to 88.

- Architecture/ADR documents are correctly under `docs/design`; the Knowledge Content file named `...ARCHITECTURE_PLAN` is substantively cross-layer design, not misplaced planning.
- The phase proposal is correctly under the newly established `docs/planning` directory.
- Verification reports are now under `docs/reports`; no report remains at root except the ambiguous generic `design-qa.md` visual-QA artifact.
- Frozen specifications and Golden assets remain canonical and must not be moved or rewritten for cleanup.
- Historical `Legacy Knowledge Hub` filenames under product-design documentation are historical evidence; AGENTS naming rules do not justify deleting or renaming history.
- No exact duplicate Markdown content and no duplicate H1 title were found. Same-phase verification, re-verification, gap, and decision reports have distinct content/purpose.
- Local Markdown links found by standard `[]()` syntax resolve, but most repository references use backticked paths and are not clickable links.
- `docs/README.md` and `docs/INDEX.md` are absent. `PROJECT_FILE_MAP.md` is a 73,690-byte implementation map, not a concise navigation index, and it currently lists KC-B01 but not the complete KC-B02–REV-B01 delivery set.
- README still says not to add KnowledgeDocument, which conflicts with the implemented KC/REV baseline. This is stale current-documentation text, not history.

### Ambiguous `design-qa.md`

`docs/reports/design-qa.md` is a stable VS-01 report. Root `design-qa.md` currently contains VS-06 Evidence QA, yet VS02–VS05 reports also refer to root `design-qa.md` as their own QA result. The root file was evidently reused over time; moving/renaming it without deciding how to repair those historical references would preserve only VS-06 and could make older reports more misleading. This is HUMAN_REVIEW, not a proven SAFE_MOVE.

## 10. Database and runtime artifacts

| Path | Files / size | Tracked | Ignore evidence | Classification |
|---|---:|---:|---|---|
| `.vs` | 20 / 13.67 MiB | 0 | `.gitignore` `.vs/` | SAFE_DELETE / Low after Visual Studio is closed |
| API `bin` | 118 / 62.64 MiB | 0 | `**/bin/` | SAFE_DELETE / Low |
| API `obj` | 30 / 4.67 MiB | 0 | `**/obj/` | SAFE_DELETE / Low |
| test `bin` | 460 / 58.69 MiB | 0 | `**/bin/` | SAFE_DELETE / Low |
| test `obj` | 22 / 1.58 MiB | 0 | `**/obj/` | SAFE_DELETE / Low |
| frontend `node_modules` | 20,428 / 213.25 MiB | 0 | `**/node_modules/` | SAFE_DELETE / Medium; regenerable but reinstall cost/network applies |
| frontend `dist` | 5 / 1.80 MiB | 0 | `**/dist/` | SAFE_DELETE / Low |
| `artifacts` | 54 / 27.40 MiB | 0 | `artifacts/` | HUMAN_REVIEW; referenced QA evidence |
| `App_Data` | DB 507,904 B; SHM 32,768 B; zero-byte WAL | 0 | `**/App_Data/` | DO_NOT_TOUCH / High |

No `.sqlite`, `.sqlite3`, logs, backups, temp files, archives, patches, TestResults, coverage, Playwright report, or Data Protection key directory currently exists outside ignored dependency/build trees.

The SQLite file is not a tracked repository baseline database. It is the configured local development/internal-pilot database, automatically migrated/seeded in Development and containing user-authored local state. Its untracked/ignored status does not make it disposable. It must be backed up and explicitly approved before any future database cleanup. The SHM/WAL companions must not be deleted independently from the database lifecycle.

No task-owned API/Vite/test server was listening on the standard verification ports during the audit.

## 11. Migration audit

EF recognizes all 18 migrations in order with `dotnet ef migrations list --no-build --no-connect`. The actual SQLite `__EFMigrationsHistory` contains all 18 through `20260823092808_AddImmutableKnowledgeDocumentRevisions`. `dotnet ef migrations has-pending-model-changes --no-build` reports: `No changes have been made to the model since the last migration.`

| Range | Classification | State |
|---|---|---|
| `20260813124350_InitialDatabaseKnowledge` through `20260822091626_AddLocalLoginCredentialFoundation` (13) | HISTORICAL_REQUIRED_MIGRATION | tracked, applied, DO_NOT_TOUCH |
| `20260822124136_AddKnowledgeDocumentRelationships` | HISTORICAL_REQUIRED_MIGRATION | untracked files, applied, referenced by KC-B04/C01 tests/reports, DO_NOT_TOUCH |
| `20260822213000_AddKnowledgeDocumentEvidenceSubject` | HISTORICAL_REQUIRED_MIGRATION | untracked manual attributed migration, applied, migration-tested, DO_NOT_TOUCH |
| `20260822223000_AddKnowledgeDocumentSearchFts` | HISTORICAL_REQUIRED_MIGRATION | untracked manual attributed migration, applied, search-tested, DO_NOT_TOUCH |
| `20260823022046_TightenRelationshipVocabulary` | HISTORICAL_REQUIRED_MIGRATION | untracked pair, applied, referenced by migration tests, DO_NOT_TOUCH |
| `20260823092808_AddImmutableKnowledgeDocumentRevisions` | ACTIVE_REQUIRED_MIGRATION | untracked pair, applied, latest matching model snapshot, DO_NOT_TOUCH |

No duplicate/abandoned migration candidate was found. The two manual migrations without Designer files carry explicit `[Migration(...)]` attributes and are recognized by EF; their missing Designer file is intentional implementation shape, not evidence of abandonment.

## 12. Gitignore audit

Existing coverage is correct for `bin`, `obj`, TestResults/TRX, App_Data, `*.db`, DB WAL/SHM, node_modules, dist, coverage, tsbuildinfo, Visual Studio/VS Code/JetBrains/OS state, logs, artifacts, and `*.log`.

Read-only `git check-ignore --no-index` probes found these gaps:

- `*.sqlite`, `*.sqlite3` and their `-wal` / `-shm` companions;
- standalone `.vite` cache directories;
- `playwright-report` and common `test-results` directories;
- conventional Data Protection key directories when configured inside the repository but outside App_Data;
- root/local `tmp` and `temp` verification directories plus `*.tmp`/`*.bak`;
- Vite secret-bearing `.env.local` / `.env.*.local` variants.

Only narrow patterns should be proposed. Source, migrations, official reports, frozen assets, and intentionally tracked `.env`, `.env.development`, `.env.production` files must remain visible.

## 13. Duplicate candidates

- Product-design content hashing found 33 duplicate groups covering 75 files; 42 copies beyond one account for about 51.97 MiB. These are canonical copies plus generation-source aliases and the explicitly named archive. Final UI Inventory and the freeze report require canonical `final-ui` assets and preserve archive/design history. They are KEEP_HISTORY / DO_NOT_TOUCH, not SAFE_DELETE.
- `.env`, `.env.development`, and `.env.production` are exact 23-byte duplicates containing only `VITE_API_BASE_URL=/api`. Vite consumes these filenames implicitly by mode. Their equality is intentional low-cost configuration, so KEEP_TRACKED.
- No exact duplicate Markdown or same H1 document pair was found.

## 14. Large files and Git object store

- `product-design` contains 103 files / 132.37 MiB; largest tracked file is `Authoring_Flows_Review_Board.png` at 3.09 MiB.
- `artifacts` contains 27.40 MiB; its largest local file is the VS06 comparison at 5.95 MiB.
- Dependency/build/IDE output consumes roughly 356 MiB and is already ignored.
- `.git` occupies about 100.63 MiB, entirely loose (2,350 objects / 97.49 MiB, no pack). `git count-objects` identifies four `tmp_obj_*` garbage files totaling 3.03 MiB; connectivity-only fsck succeeds but reports numerous dangling trees/blobs.
- No `.gitattributes` exists; Git LFS is installed but unused.

Dangling objects may be the only recovery path for prior interrupted work in this heavily dirty repository. No object/prune/gc operation is safe before a reviewed commit/backup checkpoint.

## 15. Cleanup candidates summary

| Classification | Candidates |
|---|---|
| SAFE_DELETE | four proven-empty directories; `.vs`; API/test `bin`/`obj`; frontend `dist`; optionally `node_modules` |
| SAFE_MOVE | none currently proven; root `design-qa.md` is ambiguous and therefore HUMAN_REVIEW |
| SAFE_GITIGNORE | narrow SQLite/cache/test-report/key/temp/local-env patterns |
| KEEP_TRACKED | all active source/tests/config, 24 untracked official docs, staged documentation moves, README/map, three Vite mode env files |
| KEEP_HISTORY | architecture decisions/amendments, superseded reports/design history, non-canonical product-design source/archive assets |
| HUMAN_REVIEW | root QA history and its VS02–VS06 references; ignored artifact retention; Bootstrap diagnostic; Git object maintenance; line-ending/LFS policy |
| DO_NOT_TOUCH | all 18 migrations and ModelSnapshot, App_Data database family, frozen specifications, canonical Golden assets |

The actionable CLEAN-ID table is in `docs/planning/REPO_CLEAN_B01_SAFE_CLEANUP_PLAN.md`.

## 16. High-risk items

1. The formal implementation set is not committed: 13 staged moves, 78 real modified files, and 74 untracked formal files.
2. Five already-applied migrations are represented by eight untracked files; losing them would make the database history unreproducible.
3. The ignored App_Data database contains local/internal-pilot state and is not recoverable from Git.
4. Frozen specifications and 34 canonical Golden files must not be treated as ordinary duplicates.
5. The Git object store contains dangling recovery objects and explicit garbage while the worktree is heavily dirty.
6. Ignored artifacts are cited by tracked reports; bulk deletion would remove local evidence and expose non-portable documentation references.

## 17. Human review items

- Approve the exact official-delivery checkpoint strategy before any cleanup.
- Decide whether ignored QA images should be selected into Git/LFS, archived externally, or allowed to remain local-only.
- Decide how to repair the generic root `design-qa.md` and inaccurate VS02–VS05 historical references.
- Decide whether the Bootstrap diagnostic remains supported, becomes Development-only, or is removed in a separate security slice.
- Approve narrow `.gitignore` additions.
- Decide whether to add a concise docs index and refresh stale README/Project File Map statements.
- Defer Git GC/prune, `.gitattributes`, and any LFS migration until the official work is committed/backed up.
- Explicitly retain or separately back up App_Data; do not infer disposal from `.gitignore`.

## 18. Recommended cleanup order

1. Human review and protect the current formal source/tests/migrations/docs in a coherent Git checkpoint.
2. Back up and mark App_Data, migrations, specifications, and Golden assets as excluded from cleanup.
3. Remove only approved empty directories.
4. Remove approved regenerable IDE/build outputs; treat node_modules as optional.
5. Add and verify narrow `.gitignore` protections.
6. Add docs navigation and correct current README/map statements.
7. Resolve root QA/artifact provenance and Bootstrap diagnostic decisions separately.
8. Consider Git object maintenance, line-ending policy, or LFS only after a clean, backed-up worktree.

REPO-CLEAN-B01 must execute only approved CLEAN IDs, one by one. It must not use `git clean -fd` and must not begin REV-B02.
