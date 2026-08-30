# AUTH-USER-ACCESS-LEVEL-R01 Verification Report

## Result

`AUTH-USER-ACCESS-LEVEL-R01` is **PASS**.

The approved AccessLevel amendment is recorded in
`docs/design/AUTH_USER_A02_ACCESS_LEVEL_CREATION_MANAGEMENT_AMENDMENT.md`; the historical
AUTH-USER-A01 frozen design was not changed.

## Implemented Behavior

- Create User now explicitly accepts and validates `Viewer`, `Editor`, or `Administrator`.
  The UI defaults to `Viewer`, while an Administrator can deliberately select either higher
  level. The selected value is persisted in the existing atomic User creation transaction.
- User list and detail responses project the canonical AccessLevel. The frontend decoder
  rejects missing, unknown, and non-string AccessLevel values.
- Existing User AccessLevel changes continue to use the independent
  `PUT /api/users/{id}/access-level` security operation. Ordinary profile updates remain
  separate and cannot mutate AccessLevel.
- The User Drawer displays a dedicated system-permission section and uses the current User
  concurrency token. A successful write replaces it with the returned token; stale writes
  return `409` and are neither overwritten nor silently retried.
- The User list presents system permission separately from knowledge identity and User state.
- Changing AccessLevel does not alter `User.IsActive`, a Local credential, a LoginIdentity, or
  KnowledgeRole assignments.

## Security and Current Actor

- User creation and AccessLevel mutation remain Administrator-only and retain antiforgery
  enforcement.
- The existing usable-Administrator guard remains authoritative. Downgrading the last usable
  Administrator returns `422 business_rule_violation` with
  `reason = last_usable_administrator`; the UI shows the corresponding actionable Chinese
  message.
- After an Administrator changes their own AccessLevel, the client reloads the authoritative
  Current User through `actorStore`. If the resulting level no longer authorizes
  `/admin/users`, it closes the Drawer and navigates safely to `/dashboard` without requiring
  a new login.
- Backend authorization continues to resolve the latest persisted AccessLevel for each
  request. No RBAC framework, permission entity, role mapping, or KnowledgeRole permission
  behavior was introduced.

## Verification

| Check | Result |
| --- | --- |
| Release build | PASS — `dotnet build SystemKnowledgeHub.sln -c Release --no-restore`; 0 warnings, 0 errors |
| Focused backend | PASS — 29/29 User, AccessLevel, Current User, and login-method tests |
| Full backend regression | PASS — 277/277, 0 skipped, serial approved gate |
| Focused frontend | PASS — 4 files, 19 tests |
| Frontend type-check | PASS |
| Frontend production build | PASS — existing chunk-size advisory only |
| Affected ESLint | PASS |
| Task-owned runtime smoke | PASS — Administrator login; Editor create/list projection; independent promotion to Administrator; detail projection; Active unchanged; token rotated |
| Browser need assessment | Component interaction and navigation behavior were covered by focused Vue tests; no additional password-bearing interactive browser login was needed for this change |
| Repository SQLite safety | PASS — database, WAL, and SHM size, UTC mtime, and SHA-256 all exactly matched the pre-verification baseline |
| Runtime cleanup | PASS — task API/Vite processes stopped, ports 5688/5699 released, and task-owned SQLite/Data Protection/attachment data removed |

The first ordinary parallel full-backend run reproduced existing `REV-GAP-011` test-host
stall behavior. The documented serial gate was then used. Two direct-process startup checks
initially received loopback proxy `502` responses because the host defines `HTTP_PROXY` and
`HTTPS_PROXY` without `NO_PROXY`; rerunning with process-scoped
`NO_PROXY=127.0.0.1,localhost` passed the focused startup tests (10/10) and the complete
backend suite (277/277). No repository configuration or machine proxy setting was changed.

## Data Safety

Verification used a task-owned SQLite database, Data Protection directory, attachment root,
and isolated ports. The protected repository files remained unchanged:

| File | Size | SHA-256 |
| --- | ---: | --- |
| `system-knowledge-hub.db` | 995328 | `B55F1652FA4CC5F0BC6A12B6EB205CAB2F505C8301AF296BDDF0CBF910A2FCE1` |
| `system-knowledge-hub.db-wal` | 45352 | `FB8A5D292010143E7D423587CA64DFF166B2B97000CD08A8628BD8DA6F061458` |
| `system-knowledge-hub.db-shm` | 32768 | `DE1160806E82759BFC0C049CF82E4095C875717F0CB3AF509F7237598112B1B3` |

No repository migration or runtime write occurred. No task-owned runtime process, port,
database, key, attachment, or log artifact remains.

## Gaps

- No new Blocker or High gap was found.
- Existing `REV-GAP-011` remains unchanged and was handled through its approved serial test
  gate.

```text
AUTH-USER-ACCESS-LEVEL-R01 PASS
CREATE USER ACCESS LEVEL: PASS
EDIT USER ACCESS LEVEL: PASS
USER LIST / DETAIL PROJECTION: PASS
LAST USABLE ADMIN PROTECTION: PASS
CURRENT USER PERMISSION REFRESH: PASS
```
