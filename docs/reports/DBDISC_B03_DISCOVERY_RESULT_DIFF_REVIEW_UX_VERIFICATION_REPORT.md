# DBDISC-B03 — Database Connection + Discovery Result + Diff Review UX Verification Report

## Result

```text
DBDISC-B03 FAIL
```

The requested web-operable Database Discovery slice is implemented and all functional, security, migration, frontend, backend, and isolated browser checks passed. Delivery is nevertheless marked **FAIL** because the strict repository data-protection gate did not remain byte-for-byte identical from the original baseline: a pre-existing repository runtime disappeared during the task, its WAL/SHM files were removed, and the repository database reflects a checkpointed state. Every verification runtime started by this task used an explicit task-owned SQLite path, but the frozen acceptance rule requires the original DB/WAL/SHM baseline and final state to be completely identical.

No task commit or push was created after this failed mandatory gate.

## Scope

Implemented the four focused surfaces without adding Manual Sync, Apply, automatic knowledge mutation, rename inference, SQL Server, a new provider, or a new EF migration:

- Database Connection Profiles
- Discovery Runs
- immutable Snapshot review
- Difference review

Backend work is limited to sanitized, bounded read projections and the existing B01/B02 command boundaries required by those pages. Frontend work remains feature-first under `features/database-discovery` and uses the existing API client, strict decoders, actor capabilities, router, dialog host, and drawer host.

## Connection Profile UI

- Administrator-only list and write actions cover create, edit, explicit enable/disable, independent secret management, test connection, trigger discovery, and run-history navigation.
- The list displays safe Profile/source/provider/host/port/locator/username/schema/status/timestamps and `HasSecret` information.
- DatabaseSource uses an authorized searchable option endpoint instead of requiring an internal ID.
- Oracle shows `ServiceName` with port 1521; PostgreSQL shows `DatabaseName` with port 5432. No raw connection-string input exists.
- IncludedSchemas is trimmed, de-duplicated, and visibly represented; backend validation remains authoritative.

## Secret Management UI

- Password inputs use `type=password` and are never populated from server state.
- Create Profile and Set Secret remain two explicit commands, including a partial-success message if Profile creation succeeds but secret creation fails.
- Set/replace/clear call the existing independent Secret APIs. Password is not included in Profile update payloads.
- The API and UI expose only `已设置` / `未设置`; no password hint, payload, reference, connection string, or secret version is exposed.

## Test Connection UI

- Test buttons are per-Profile loading guarded and unavailable when the Profile is disabled or has no secret.
- Success renders provider-neutral safe summary, provider version, and the applicable database/service locator.
- Failure renders only normalized error code and safe summary from the existing redaction boundary.

## Trigger Discovery UI

- Enabled Profiles with a secret can trigger discovery through the existing durable Run API.
- Successful trigger navigates directly to the Runs page with the Run selected; no RunId copying is required.
- Existing active-Run conflict remains a backend-authoritative conflict and receives specific UI feedback.

## Discovery Runs UX

- Runs are server-side paged and filterable by Profile and DatabaseSource.
- Queued/Running/Succeeded/Failed/Cancelled states use Chinese presentation and real timestamps; no fabricated percentage is shown.
- Active Runs poll every 2.5 seconds. Terminal state stops polling, and unmount clears the timer and aborts the active request.
- Administrator can cancel cancellable Runs; Viewer/Editor do not receive that write action. Succeeded Runs link to actual Snapshot/Difference artifacts only when IDs exist.

## Snapshot Review

- Summary shows provider, locator, capture time, format/identity versions, content SHA-256, scope generation/fingerprint, included schemas, counts, and capabilities.
- Schema, object, sequence, column, constraint, and index data is fetched through bounded server-side pages and lazy object review.
- Table/View and exact Schema/name filters are handled on the server.
- Snapshot pages are read-only and provide no edit, delete, Apply, or database-mutation action.
- Raw Canonical payload and the earlier raw object endpoint are not exposed. Object/header/column projections use an explicit safe allowlist.

## Difference Review

- Added, Changed, MissingFromSource, and Unchanged counts and paged filters are available.
- Changed entries use an allowlisted field-level projection. The browser smoke confirmed `数据类型 integer → bigint`; `nativeDataType`, `pg_catalog`, internal namespace/origin, and diagnostic identity were absent.
- Before/after values accept only scalar/null JSON. Object/array/internal-only values fail closed or render as hidden; full raw Canonical before/after payloads are not returned.
- Missing uses the exact `来源中未发现` wording and explains that it is not deletion and will not automatically delete/archive knowledge data.
- No Rename inference, SyncPlan creation, Preview, Confirm, or Apply endpoint/action is present.

## Pagination / Lazy Loading

- Run list, Schema list, object list, sequences, object columns/constraints/indexes, and Difference entries use bounded server-side pagination.
- Snapshot object structures are loaded only when the user opens the drawer.
- Full Snapshot Canonical JSON and full Difference entry collections are never sent to the browser.

## Scope / Visibility Warning

Snapshot and Difference pages retain the DBDISC-GAP-004 warning: results represent the current Profile, IncludedSchemas, and metadata visible to the current connection account. Missing objects do not prove physical absence. Compatible scope generation/fingerprint/version semantics remain backend-owned.

## Capability Presentation

Supported, NotSupported, Unavailable, and NotApplicable use one provider-neutral capability presentation with Chinese labels. NotApplicable is not rendered as an error.

## Authorization

- Connection Profile management, Secret management, Test Connection, Trigger, and Cancel remain Administrator-only at both UI and controller policy boundaries.
- Viewer and Editor can use the approved discovery read boundary but do not see management actions.
- Existing direct-request 403 tests passed; frontend capability checks do not replace backend authorization.

## Secret / Redaction

- Explicit response models are used instead of EF entity serialization.
- Tests inject secret and provider-native canaries and assert that password, protected payload, secret/configuration revision, lease owner/token, host, username, raw catalog namespace/origin, diagnostic identity, and raw connection details do not enter discovery read responses.
- Strict frontend decoders reject unexpected enum, nullable, paging, capability, and non-scalar Difference shapes.

## Provider-neutral UX

Oracle and PostgreSQL share the same Connection, Run, Snapshot, capability, and Difference pages. Only the appropriate connection locator label differs. No provider-specific Snapshot or Difference page was introduced.

## Manual End-to-End Path

Using task-owned runtime state and a task-owned PostgreSQL 18 fixture, the browser completed:

```text
Administrator login
→ Connection Profile create
→ independent Secret set
→ Test Connection success
→ Trigger Discovery
→ Run Queued / Running / Succeeded
→ Snapshot summary and lazy Schema/Object/Column/PK/FK/Index/Sequence review
→ second discovery after fixture mutation
→ Difference Added / Changed / MissingFromSource / Unchanged review
```

The second Difference contained Added 5, Changed 1, MissingFromSource 5, and Unchanged 20. The corrected Changed drawer was reverified after the final build. Common 1366/1440 desktop widths were exercised without document-level horizontal overflow.

## Runtime Smoke

- API SQLite: `.tmp/dbdisc-b03-verification/runtime/dbdisc-b03.db`
- Data Protection, attachments, Serilog logs, runsettings, API/Vite ports, PostgreSQL container, and PostgreSQL volume were all task-owned.
- API and browser runtime used isolated ports 26131/26132; PostgreSQL used 25433.
- The task-owned directory, exact labeled Docker container, exact labeled Docker volume, browser tab, and task-started API/Vite processes were removed. The three task ports have no listening process.

## Backend Verification

| Check | Result |
| --- | --- |
| Release solution build | PASS — 0 warnings, 0 errors |
| Focused B03 safe read tests | PASS |
| B01/B02 + migration + Oracle/PostgreSQL provider regression | PASS — 71/71 |
| Approved serial full backend gate | PASS — 373/373 |
| EF pending model changes | PASS — no pending model changes |

The existing EF global-query-filter relationship warnings remain unchanged and did not fail the model gate.

## Frontend Verification

| Check | Result |
| --- | --- |
| Focused database-discovery tests | PASS — 6 files / 33 tests |
| Full frontend tests | PASS — 82 files / 486 tests |
| `npm run type-check` | PASS |
| `npm run build` | PASS; existing chunk-size warnings only |
| `npm run lint` | PASS |
| Scoped Prettier check | PASS |

## Repository Safety

Original baseline:

| File | Size | mtime UTC | SHA-256 |
| --- | ---: | --- | --- |
| `system-knowledge-hub.db` | 995328 | 2026-08-30T04:51:50.7995165Z | B55F1652FA4CC5F0BC6A12B6EB205CAB2F505C8301AF296BDDF0CBF910A2FCE1 |
| `system-knowledge-hub.db-wal` | 86552 | 2026-08-30T14:33:49.6417002Z | 93BE0C3A799FB6D6D34C8DD0CA1CC696279B320A6F73A41C6CF373CDAF7E8C16 |
| `system-knowledge-hub.db-shm` | 32768 | 2026-08-30T14:26:46.0176378Z | 959339E947D6BC309458A0231D83FE42C1DEDC5032CFAF7B3668739A2EBF3DE2 |

Final state:

| File | Size | mtime UTC | SHA-256 |
| --- | ---: | --- | --- |
| `system-knowledge-hub.db` | 995328 | 2026-08-30T14:41:08.5965122Z | CAF01CC0C624305AAD040B2A0033581079062613F8DF9E5C5DE593ED6C64EC87 |
| `system-knowledge-hub.db-wal` | absent | — | — |
| `system-knowledge-hub.db-shm` | absent | — | — |

The task recorded a pre-existing repository runtime process before isolated verification and did not stop it. That process was already absent before the final task-owned runtime started. The transition is consistent with that external runtime closing/checkpointing its WAL. All task-started commands used the explicit task-owned SQLite path, and no task command targeted the repository database. Nevertheless, the required original file-state equality is objectively false, so:

```text
REPOSITORY DATA PROTECTION: FAIL
```

No repository DB/WAL/SHM file was deleted, restored, overwritten, or otherwise changed in an attempt to force the gate to pass.

## Existing / New Gaps

- DBDISC-GAP-004 remains: visibility is bounded by Profile scope and the connection account's metadata privileges.
- REV-GAP-011 remains: the approved serial full-backend gate is used instead of the known parallel test execution path.
- Existing production operations/configuration gap SEC-04 is unchanged.
- No new product Blocker/High issue was found. The only failed delivery gate is the repository file-state mismatch recorded above.

## Delivery

- Branch: `main`
- Commit: not created because a mandatory verification gate failed.
- Push: not attempted.
- Unrelated pre-existing TRACE UX work and the unrelated `appsettings.json` worktree status were preserved and not staged or modified by this task.

## Final Status

```text
DBDISC-B03 FAIL

DATABASE CONNECTION PROFILE UI: PASS
SECRET MANAGEMENT UI: PASS
TEST CONNECTION UI: PASS
TRIGGER DISCOVERY UI: PASS

DISCOVERY RUN UX: PASS
SNAPSHOT REVIEW: PASS
DIFF REVIEW: PASS

PAGINATION / LARGE DATA SAFETY: PASS
SCOPE / VISIBILITY WARNING: PASS
CAPABILITY PRESENTATION: PASS
AUTHORIZATION: PASS
SECRET / REDACTION: PASS
PROVIDER-NEUTRAL UX: PASS
END-TO-END MANUAL DISCOVERY PATH: PASS
REPOSITORY DATA PROTECTION: FAIL

MANUAL SYNC / APPLY: NOT IMPLEMENTED BY DESIGN

DBDISC-B03 COMPLETE: NO
DBDISC-B04 READY: NO
```
