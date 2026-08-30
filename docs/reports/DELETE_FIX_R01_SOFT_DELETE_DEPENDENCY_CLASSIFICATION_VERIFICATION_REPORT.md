# DELETE-FIX-R01 — Soft Delete Dependency Classification Correction Verification Report

## Result

**DELETE-FIX-R01 PASS.** `SystemTechnologyTags` is no longer classified as a `DeleteDependencyBlocker` for System soft delete. A System with only technology-tag associations can be soft-deleted, while all seven true System dependency categories continue to block. No authorization, concurrency, tombstone, historical-read, recovery, hard-delete, cascade-delete, AUTH-USER, or DB Discovery behavior was changed.

DELETE-FIX-R01 is the current corrective authority for this single dependency-classification issue. The older DELETE-A01/B02/B04 evidence that classified technology tags as blockers was reviewed but not edited because those files are frozen or historical evidence.

## Root Cause

`SystemDeleteService` explicitly counted `SystemTechnologyTags` and returned the group `technologyTags` / `技术标签` alongside business dependencies. The shared frontend confirmation/error content renders backend blocker groups generically, so the incorrect backend classification also caused the delete dialog to show `技术标签` and prevented otherwise valid deletion.

Technology tags are a parent-owned metadata association. Soft deletion updates the System tombstone only; it does not physically remove the System or its tag rows. Keeping the association therefore preserves history without creating an active orphan or breaking current projections.

## Removed Non-blocking Dependencies

| Root | Removed blocker | Classification after correction | Persistence behavior |
| --- | --- | --- | --- |
| System | `technologyTags` / `技术标签` | Attached metadata association; not a delete blocker | Tag rows remain unchanged; no physical or cascade delete was introduced |

No other blocker was removed.

## Retained Blocking Dependencies

System soft delete still returns the following bounded active blockers, in the existing order:

1. `businessFunctions` — 业务功能
2. `databaseSources` — 数据库来源
3. `businessRules` — 业务规则
4. `integrations` — 集成关系
5. `unknownItems` — 未关闭待确认事项
6. `knowledgeRelations` — 知识关系
7. `proposedKnowledgeUpdates` — 待应用知识更新

These dependencies can break active business meaning, leave active child/reference state, or invalidate an unfinished workflow if the System disappears. Their checks and counts were not weakened.

## Static Audit

All current concrete soft-delete services and their blocker queries were reviewed. `SoftDeleteCapabilityResolver` and the generic `DeleteDependencyBlocker` model contain no dependency classification logic.

| Delete root | Audited categories | Decision |
| --- | --- | --- |
| System | technology tags; functions; sources; rules; integrations; open UnknownItems; relations; proposed updates | Technology tags removed; seven business/workflow dependencies retained |
| BusinessFunction | process steps; relations; open UnknownItems; proposed updates | Retained: steps are ordered business-process content, not presentation metadata |
| DatabaseSource | database objects; integrations; enabled connection profiles; relations; open UnknownItems; proposed updates | Retained: child data, active operational profiles, references, and workflows are substantive dependencies |
| DatabaseObject | columns; integrations; relations; open UnknownItems; proposed updates | Retained: child schema and active references would otherwise become invalid |
| DatabaseColumn | known values; relations; open UnknownItems; proposed updates | Retained: known-value knowledge and active references are substantive governed content |
| BusinessRule | relations; open UnknownItems; proposed updates | Retained: active references/workflows would lose their target |
| Integration | contract fields; relations; open UnknownItems; proposed updates | Retained: contract fields are integration contract structure, not display metadata |
| KnowledgeDocument | knowledge relations | Retained: active knowledge relationships require a current target |

No second clear tag/classification/pure-display metadata misclassification was found. No ambiguous relationship required a speculative semantic change, so no new classification gap was opened.

## Tests

The existing complete blocker-matrix assertion was updated to retain a technology-tag row while expecting only the seven true System blockers. Three focused API regression tests add the requested A–D evidence:

| Case | Setup | Verified result |
| --- | --- | --- |
| A | System + four technology tags + no other blocker | `DELETE /api/systems/{id}` returns 204 |
| B | System + tags + one business function | 422 contains only `businessFunctions`; neither `technologyTags` nor `技术标签` is returned |
| C | System + tags + one database source + one knowledge relation | 422 contains only `databaseSources` and `knowledgeRelations` |
| D | Successful Case A deletion | Tombstone/audit/version are correct; all four tag associations remain; current detail is 404 and current list excludes the System |

Verification results:

- `dotnet build SystemKnowledgeHub.sln --no-restore`: **PASS**, 0 warnings, 0 errors.
- `dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~CoreSoftDeleteApiTests`: **PASS**, 9/9, 0 skipped.
- Complete backend suite through the approved deterministic serial gate: **PASS**, 232/232, 0 skipped, 54 seconds.
- `git diff --check`: **PASS**.

No frontend source changed. `DeleteConfirmationDialogContent.vue` consumes the backend blocker collection generically; the exact API assertions prove `技术标签` can no longer reach that dialog. A focused frontend run was therefore not applicable under this task's risk-based verification rule.

## Data Safety

The tests used SQLite `Data Source=:memory:` through `BootstrapWebApplicationFactory`, ephemeral Data Protection, and a test-owned temporary attachment directory that the factory removed on disposal. No runtime/browser process or network port was started for this backend-only classification correction. The temporary xUnit serialization configuration was removed after the full suite.

Repository SQLite before and after verification is identical:

- Path: `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db`
- Size: `950272` bytes
- LastWriteTimeUtc: `2026-08-30T01:12:08.4424362Z`
- SHA-256: `144A455510D9CA162041F3093B644E319A8291C390580B4D7EBA5883930698A1`
- WAL: absent before and after
- SHM: absent before and after

No repository database migration, seed, checkpoint, or write occurred.

## Existing/New Gaps

- Existing Low `REV-GAP-011` remains OPEN / Deferred: default collection-parallel SQLite/WebApplicationFactory execution can stall. The approved deterministic serial gate passed 232/232; its temporary runner configuration was deleted.
- The older frozen/historical DELETE documents retain their original technology-tag classification. DELETE-FIX-R01 and this indexed report are the later narrow correction; frozen sources were not rewritten.
- New gaps: none.
- Blocker / High findings: none.

## Scope and Delivery Decision

The change is limited to dependency classification, focused regression coverage, and this verification evidence. There is no new cascade, recovery, hard delete, permission path, concurrency behavior, historical mutation, or adjacent DELETE/AUTH/DBDISC feature.

**DELETE-FIX-R01 PASS.**
