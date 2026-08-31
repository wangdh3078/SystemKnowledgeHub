# DBDISC-B03-R01 — Discovery UX Navigation / Action Simplification / Safety Re-verification Report

## Result

```text
DBDISC-B03-R01 PASS
```

This corrective slice closes the historical DBDISC-B03 delivery gate without rewriting the historical report. The original `DBDISC-B03 FAIL` remains valid historical evidence for its repository file-state mismatch. R01 establishes a new stable baseline, uses only task-owned runtime state, and finishes with byte-for-byte identical repository DB/WAL/SHM state.

No Manual Sync, Apply, automatic knowledge mutation, provider change, Worker change, schema change, or EF migration was added.

## Action Simplification

- A Connection Profile row now exposes only `开始发现`, `测试连接`, and `更多` as primary actions.
- `更多` contains edit, run history, set/replace password, and the state-appropriate enable/disable command.
- A disabled Profile promotes `启用连接` to the first menu item while keeping the primary row compact.
- `清除密码` is separated and styled as a danger action. Its confirmation states that clearing prevents connection tests and discovery until a password is set again.
- Viewer and Editor continue to receive no Profile management actions; backend Administrator policies remain the authority.
- Browser verification found that the newly used Element Plus dropdown components were not registered by the application bootstrap. R01 registered `ElDropdown`, `ElDropdownMenu`, and `ElDropdownItem` with their styles, then reverified the real menu, conditional states, and confirmation dialog.

## Direct Navigation and Route State

The four discovery navigation entries are real routes:

| Entry | Route |
| --- | --- |
| 连接配置 | `/database-discovery/connections` |
| 发现运行 | `/database-discovery/runs` |
| 发现快照 | `/database-discovery/snapshots` |
| 差异审查 | `/database-discovery/differences` |

The active entry is derived only from `route.path`, including list routes, detail routes, direct URL entry, refresh, and browser Back/Forward. Run artifact shortcuts remain available but are no longer the only way to reach Snapshot or Difference review.

Snapshot and Difference details now show explicit context:

```text
数据库发现 / 发现快照 / Snapshot #x
数据库发现 / 差异审查 / Difference #x
```

## Snapshot History UX

- Added the provider-neutral `/database-discovery/snapshots` history page.
- The list shows safe Snapshot/Profile/DatabaseSource/Provider/capture/scope/count/baseline/Difference information.
- Profile and DatabaseSource filters and bounded server-side paging are supported.
- Oracle and PostgreSQL use the same list and detail route.
- The empty state explains that a completed discovery produces reviewable structure and offers navigation without automatically triggering discovery.

## Difference History UX

- Added the provider-neutral `/database-discovery/differences` history page.
- The list shows Difference/Profile/DatabaseSource/Provider/base/target/created time and Added/Changed/MissingFromSource/Unchanged counts.
- Profile and DatabaseSource filters and bounded server-side paging are supported.
- The empty state correctly treats the first Snapshot as a baseline rather than an error.
- The page states that results are review-only and that structure synchronization belongs to the later manual-sync flow; no enabled Apply action exists.

## Backend Read Boundary

Two minimal paged read endpoints were added:

```text
GET /api/database-discovery/snapshots
GET /api/database-discovery/differences
```

Both reuse the existing accessible-Run authorization boundary, bounded paging, safe Profile/DatabaseSource filters, explicit response models, and provider-neutral projections. They do not return Canonical JSON, provider catalog rows, secrets, protected payload, connection strings, raw errors, lease state, or internal worker data.

No new table, cache table, DbContext, vendor-specific persistence, or migration was introduced.

## Authorization and Security

- Administrator remains the only role allowed to manage Profiles/Secrets, test connections, trigger discovery, or cancel Runs.
- Viewer and Editor retain the approved Run/Snapshot/Difference read boundary and cannot read Administrator connection details.
- Existing controller policies and direct-request 403 regression remain unchanged.
- Strict frontend decoders fail closed on malformed history projections.
- Existing redaction canaries and B01/B02/Oracle/PostgreSQL regressions passed.

## Browser / Runtime Verification

The R01 browser smoke used:

- task-owned SQLite, Data Protection keys, attachments, and Serilog logs;
- isolated API/web ports `26241` / `26242`;
- a task-only Administrator and task-only Connection Profile/Secret.

Verified in the real Vue runtime:

- Administrator login and four direct navigation routes;
- direct Snapshot URL, route-derived active state, Difference navigation, Back, and Forward;
- Connection Profile creation followed by independent Secret persistence;
- the compact three-action row and real Element Plus `更多` menu;
- set/replace password wording, enable/disable conditional behavior, disabled action state, danger separation, and clear-password consequence confirmation;
- normalized safe `ConnectionFailed` presentation against an intentionally unavailable isolated endpoint;
- Trigger Discovery, durable `Queued` to `Failed` transition, safe error summary, and no successful Snapshot on provider failure;
- Snapshot/Difference empty history states and the B04 boundary copy.

The machine did not expose Docker, PostgreSQL, or Oracle runtime commands. R01 therefore did not repeat provider acceptance. This is allowed by the task because no Provider/shared Worker code changed. The previously recorded task-owned PostgreSQL browser success path and the Oracle/PostgreSQL real-integration reports remain the successful Provider evidence; R01 reverified the changed UI/API boundary and the durable failure path. Together with the complete B01/B02/provider regression, the existing end-to-end manual discovery path remains accepted.

The browser tab, task-started API/Vite processes, temporary database/WAL/SHM, keys, attachments, logs, credentials, and serial runsettings were removed. Neither isolated port has a listening process.

## Verification

### Backend

| Check | Result |
| --- | --- |
| Release solution build | PASS — 0 warnings, 0 errors |
| B03 focused safe history/read regression | PASS |
| B01/B02 + Oracle/PostgreSQL regression | PASS — 73/73 |
| Approved serial full backend gate | PASS — 373/373 |
| EF pending model changes | PASS — no pending model changes |

The unchanged EF global-query-filter relationship warnings did not fail the model gate.

### Frontend

| Check | Result |
| --- | --- |
| Focused discovery/action/navigation/history tests | PASS — 8 files / 46 tests |
| Full frontend tests after runtime correction | PASS — 84 files / 499 tests |
| `npm run type-check` | PASS |
| `npm run build` | PASS; existing chunk-size warnings only |
| `npm run lint` | PASS |
| Scoped Prettier check | PASS |

## Repository Data Protection

The R01 baseline was established only after confirming that no repository API runtime was active. Existing MSBuild/VBCSCompiler build servers did not target the repository database.

Baseline and final state were exactly identical:

| File | Exists | Size | mtime UTC | SHA-256 |
| --- | --- | ---: | --- | --- |
| `system-knowledge-hub.db` | yes | 995328 | 2026-08-30T14:41:08.5965122Z | CAF01CC0C624305AAD040B2A0033581079062613F8DF9E5C5DE593ED6C64EC87 |
| `system-knowledge-hub.db-wal` | yes | 712792 | 2026-08-31T13:45:14.7596508Z | 546CA57DC432102634ECF710F7B2960929A85E217C4AD87F9CCB2A14948C4EE7 |
| `system-knowledge-hub.db-shm` | yes | 32768 | 2026-08-31T13:19:48.8314574Z | 8801BD03733AF262F5A04FC775EAF0A8F0E0E2D7425D486BBC06DF04A12E248A |

No repository DB migration, checkpoint, WAL/SHM deletion, mtime manipulation, or hash restoration was performed.

```text
REPOSITORY DATA PROTECTION: PASS
```

## Existing / New Gaps

- DBDISC-GAP-004 remains: Snapshot/Difference meaning is bounded by Profile scope and the connection account's metadata visibility.
- REV-GAP-011 remains: the approved serial backend gate is used for the known parallel test infrastructure issue.
- SEC-04 production operations/configuration work remains unchanged.
- No new Blocker/High gap remains. The runtime-only dropdown registration defect found during R01 was corrected and reverified.

## Final Status

```text
DBDISC-B03-R01 PASS

ACTION SIMPLIFICATION: PASS
SNAPSHOT DIRECT NAVIGATION: PASS
DIFFERENCE DIRECT NAVIGATION: PASS
ROUTE-DRIVEN ACTIVE TAB: PASS
SNAPSHOT HISTORY UX: PASS
DIFFERENCE HISTORY UX: PASS

DATABASE CONNECTION PROFILE UI: PASS
SECRET MANAGEMENT UI: PASS
TEST CONNECTION UI: PASS
TRIGGER DISCOVERY UI: PASS
DISCOVERY RUN UX: PASS
SNAPSHOT REVIEW: PASS
DIFF REVIEW: PASS

AUTHORIZATION: PASS
PROVIDER-NEUTRAL UX: PASS
END-TO-END MANUAL DISCOVERY PATH: PASS
REPOSITORY DATA PROTECTION: PASS

MANUAL SYNC / APPLY: NOT IMPLEMENTED BY DESIGN

DBDISC-B03 ACCEPTED: YES
DBDISC-B04 READY: YES
```
