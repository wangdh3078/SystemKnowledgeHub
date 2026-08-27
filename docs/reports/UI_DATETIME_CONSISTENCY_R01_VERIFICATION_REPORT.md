# UI-DATETIME-CONSISTENCY-R01

## Result

```text
UI-DATETIME-CONSISTENCY-R01 PASS
```
All user-visible DateTime/timestamp values found in the real frontend now use `yyyy-MM-dd HH:mm:ss`. True DateOnly values remain governed by `yyyy-MM-dd`; no current audited surface exposes a true DateOnly field. Storage, API wire values, timezone conversion, sorting, filtering, pagination, and domain semantics are unchanged.

## Worktree Baseline

- Branch: `main`
- Starting HEAD: `42dfe54`
- `git status --short`: clean
- `git diff --stat`, `git diff`, and `git diff --check`: no starting changes
- Repository database baseline: length `724992`, `LastWriteTimeUtc` `2026-08-26T14:32:28.8616945Z`, SHA-256 `A122F8D208338A8445DCF19CE27601634B98A0B35AC1575FDBB6ADF7A3422DA9`

No concurrent worktree task was present.

## Normative Authority

The implementation and verification used:

- `AGENTS.md`
- the current `UI-DATETIME-CONSISTENCY-R01` task definition
- `docs/DOCUMENT_INDEX.md`
- `docs/design/SYSTEM_UI_COMPONENT_AND_INTERACTION_BASELINE.md`
- the frozen MVP UI Inventory, Design Baseline, API Contract, Database Model, and Solution Structure
- adjacent frontend implementation, tests, and the prior product-consistency report

Frozen specifications and historical reports were not modified.

## DateTime Inventory

| Area | Field / Surface | Previous formatter / format | Classification | Final |
| --- | --- | --- | --- | --- |
| Dashboard | Recent activity timestamp | shared minute helper / `yyyy-MM-dd HH:mm` | DateTime | shared `formatDateTime` / `yyyy-MM-dd HH:mm:ss` |
| Systems list | `updatedAt` | `slice(0, 10)` / `yyyy-MM-dd` | DateTime | shared canonical format |
| Business Functions list | `updatedAt` | `slice(0, 10)` / `yyyy-MM-dd` | DateTime | shared canonical format |
| Database Objects list/detail/fields | no displayed timestamp | none | No user-visible time | unchanged; no time field invented |
| Knowledge Content list | `updatedAt` | `slice(0, 10)` / `yyyy-MM-dd` | DateTime | shared canonical format |
| KnowledgeDocument detail | created/updated/published/archived | local replace/slice / minute precision | DateTime | shared canonical format; missing values `—` |
| Revision history/compare/restore | revision timestamps | local replace/slice / minute precision | DateTime | shared canonical format |
| Unknown Items list/detail | updated/created/confirmed/apply/activity timestamps | locale-dependent `toLocaleString('zh-CN')` | DateTime | shared canonical format |
| User Management list | `updatedAt` | `slice(0, 10)` / `yyyy-MM-dd` | DateTime | shared canonical format |
| Evidence drawer | evidence/confirmation timestamps | local `Intl.DateTimeFormat` | DateTime | shared canonical format |
| Relationship drawer | created timestamp | local `toLocaleString('zh-CN')` | DateTime | shared canonical format |
| System Unified Knowledge View | related document/unknown timestamps | `Intl` medium date | DateTime | shared canonical format |
| Search results | API contains `updatedAt`, UI does not display it | none | No user-visible time | unchanged |
| Trace / Impact | no displayed datetime metadata | none | No user-visible time | unchanged |
| Evidence editing inputs | recorded/confirmed input values | wire/editing semantics | Out of scope | unchanged |
| True business DateOnly | none found in audited frontend | none | DateOnly | rule retained; no `00:00:00` invented |

System Detail, Business Function Detail, Database Object Detail, Database Fields, Business Rules, Integrations, HumanConfirmation, Relationships, Search, Trace/Impact, and read-only dialog/drawer metadata were included in the source audit. Surfaces without a displayed timestamp were not expanded.

## Shared Formatter Audit

The existing authoritative utility was `src/SystemKnowledgeHub.Web/src/app/formatters/dateTime.ts`, previously named `formatLocalDateTimeToMinute`. The audit searched formatter names, `Intl.DateTimeFormat`, locale date methods, `new Date`, string slicing/replacement, and date-library usage.

The utility now exports the explicit `formatDateTime` contract. All audited read-only DateTime surfaces use it; no new date dependency or date-specific component family was added. A static surface test guards against reintroducing page-local formatting in the audited files.

## Canonical DateTime Format

Canonical output:

```text
yyyy-MM-dd HH:mm:ss
```

The formatter uses the real parsed timestamp, local calendar/time components, and zero padding for year, month, day, hour, minute, and second. It never manufactures `:00`. `null`, `undefined`, empty, and invalid inputs fail safely to `—` rather than `Invalid Date` or the Unix epoch.

## DateOnly Exception

The rule remains:

```text
DateTime / timestamp -> yyyy-MM-dd HH:mm:ss
true DateOnly        -> yyyy-MM-dd
```

No true DateOnly presentation was found in the audited frontend, so no DateOnly code was changed and no midnight time was appended.

## Timezone Boundary

The API contract continues to return ISO-8601 UTC DateTime values, and the database model continues to require UTC storage with application-side timezone conversion. The existing frontend behavior uses native `Date` local conversion; the task retained that behavior and standardized only the output shape and precision. The project has no additional formal user-selectable timezone contract, and none was introduced.

## Dashboard

Dashboard recent activity now displays complete timestamps such as `2026-08-12 09:20:00`. The seconds are read from the original API value. The recent-time grid allocation was widened so the complete value remains readable.

## Systems

Systems list `UpdatedAt` now uses the shared formatter and a `156px` DateTime column. Browser data showed six complete timestamps from `2026-08-07 09:20:00` through `2026-08-12 09:20:00`, with no clipped time cells at either required viewport.

## Business Functions

Business Functions list `UpdatedAt` now uses the shared formatter and a `156px` DateTime column. Six seeded rows displayed complete seconds without clipping at both viewports.

## Database Objects

The real Database Objects list, object detail, and field surfaces currently expose no DateTime value. They were audited and left unchanged; the task did not add a timestamp merely for consistency.

## Knowledge Content

Knowledge Content list `UpdatedAt` uses the shared formatter and a `156px` column. Detail metadata for created, updated, published, and archived values uses the same formatter; absent publish/archive values render `—`. An isolated runtime document displayed `2026-08-27 20:19:31` consistently in list, detail, and revision history.

## Unknown Items

Unknown Items list and detail now use the shared formatter for updated, created, confirmed, latest-apply, and activity timestamps. The isolated runtime item displayed `2026-08-27 20:22:52` consistently; locale-dependent slashes and omitted seconds are removed.

## User Management

User Management list `UpdatedAt` now uses the shared formatter and a `156px` column. At 1280x720 the full timestamp and both `编辑` / `停用` actions remained inside the visible viewport.

## Revision History

Revision list, selected revision metadata, compare view, and restore metadata use `formatDateTime`. Revision identity, ordering, storage, restore behavior, and snapshot semantics were not changed. The runtime revision displayed `2026-08-27 20:19:31` in both list and selected detail.

## Other DateTime Surfaces

- Evidence and HumanConfirmation read-only metadata use the shared formatter; subject revision and confirmation semantics are unchanged.
- Relationship detail created time uses the shared formatter; relationship behavior is unchanged.
- System Unified Knowledge View related document/unknown timestamps use the shared formatter.
- Search, Trace, and Impact currently do not render DateTime metadata and remain unchanged.
- Create/edit date inputs retain their existing wire/editing semantics.

## Column Width / Responsive

PASS at exact content viewports 1440x900 and 1280x720.

- Dashboard, Systems, Business Functions, Database Objects, Knowledge Content, Unknown Items, User Management, KnowledgeDocument Detail, and Revision History reported no document/body root horizontal overflow.
- Every populated list DateTime cell reported `scrollWidth <= clientWidth`; complete seconds were visible.
- At 1280x720 User Management `编辑` and `停用` were both within the viewport.
- The existing table/component baseline and internal table behavior were retained; no page-wide horizontal scroll was added.

## Sort Semantics

Formatting occurs only in cell rendering. Existing raw field/backend ordering remains authoritative. Runtime Systems `UpdatedAt` sorting changed the first visible timestamp from `2026-08-12 09:20:00` to `2026-08-07 09:20:00`, confirming the control still sorts chronological raw values rather than formatted display text. Filters and pagination were not changed.

## Files Changed

Frontend:

- shared DateTime formatter and unit tests
- static audited-surface regression test
- Dashboard presentation and time allocation
- Systems, Business Functions, Knowledge Content, Unknown Items, and User Management list presentation
- KnowledgeDocument detail/revision/compare/restore presentation
- Evidence, Relationship, System Unified View, and Unknown Item detail presentation
- focused existing component expectations

Documentation:

- this verification report
- `docs/DOCUMENT_INDEX.md`

No backend source, schema, migration, repository database, generated runtime database, key, log, or build artifact is part of the task diff.

## Frontend Type Check

`npm run type-check`: PASS.

## Frontend Build

`npm run build`: PASS — 3,406 modules transformed. The existing chunk-size advisory remains informational.

## Affected Vitest

PASS — 8 files, 80/80 tests.

Coverage includes shared formatter zero padding, all date/time components, missing/invalid values, 2025/2026 year boundaries, audited surface usage, Dashboard, KnowledgeDocument detail/revision/compare/restore, and System Unified Knowledge View expectations.

## ESLint

All task-modified TypeScript and Vue source/tests: PASS — 0 errors.

## Browser Runtime

PASS. The final scenario used a task-owned API on `127.0.0.1:5292`, Vite on `127.0.0.1:5174`, isolated temporary SQLite, isolated Data Protection keys, a disposable local Administrator, and an isolated in-app browser tab.

The scenario verified Dashboard recent activity, Systems, Business Functions, Database Objects, populated Knowledge Content, populated Unknown Items, User Management, KnowledgeDocument detail, Revision History, and a System detail/unified view. Temporary document and Unknown Item fixtures existed only in the disposable database.

One setup attempt used the wrong connection-string override key and therefore opened the repository database read-only for migration/auth checks. It was stopped immediately; repository length, timestamp, and SHA-256 remained identical before and after. The final scenario was restarted with `ConnectionStrings__KnowledgeHub` and all browser mutations occurred only in the isolated database.

Task-owned browser, API, and Vite were closed. Ports 5174 and 5292 were released, and no task-path/process match remained.

## Browser Console

PASS — final controlled-tab collection returned 0 application warnings, 0 application errors, and no unhandled rejection.

## Temporary SQLite Integrity

After the API stopped:

```text
PRAGMA wal_checkpoint(TRUNCATE) => 0 (success / not busy)
PRAGMA integrity_check          => ok
PRAGMA foreign_key_check        => 0 rows
```

The temporary database, WAL/SHM, Data Protection keys, disposable Administrator, and runtime directory were then deleted.

## Repository DB Protection

Before and after values are identical:

| Measurement | Before | After |
| --- | --- | --- |
| Length | `724992` | `724992` |
| LastWriteTimeUtc | `2026-08-26T14:32:28.8616945Z` | `2026-08-26T14:32:28.8616945Z` |
| SHA-256 | `A122F8D208338A8445DCF19CE27601634B98A0B35AC1575FDBB6ADF7A3422DA9` | `A122F8D208338A8445DCF19CE27601634B98A0B35AC1575FDBB6ADF7A3422DA9` |

Result: `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` is unchanged.

## New Gap Check

No new DateTime presentation gap was found in the audited real frontend. Surfaces with no visible timestamp were deliberately not expanded, and there is no backend precision limitation on the displayed fields. No PRODUCT-CONSISTENCY, TRACE, soft-delete, database progression, UI redesign, or unrelated corrective work was included.

## Final Result

```text
UI-DATETIME-CONSISTENCY-R01 PASS
```
