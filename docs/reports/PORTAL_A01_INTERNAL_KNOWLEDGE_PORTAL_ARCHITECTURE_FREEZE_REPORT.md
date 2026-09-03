# PORTAL-A01 — Internal Knowledge Portal Architecture Freeze Report

## Result

**PORTAL-A01 PASS.** The architecture and contract for the internal anonymous read-only Portal and the authenticated Admin Knowledge Composition capability are complete and frozen. No Portal implementation, migration, controller, Vue page, package, or deployment change was made.

```text
PORTAL-A01 PASS

SAME APPLICATION: PASS
SAME DEPLOYMENT: PASS
PORTAL WITHOUT LOGIN UI: PASS
READ-ONLY PORTAL: PASS
ADMIN AUTHORIZATION PRESERVED: PASS
EXPLICIT PUBLICATION BOUNDARY: PASS
PAGE TREE MODEL: PASS
NO DUPLICATE KNOWLEDGE TRUTH: PASS
SANITIZED PORTAL API: PASS
PORTAL SEARCH BOUNDARY: PASS
ATTACHMENT READ BOUNDARY: PASS
TRACE INTEGRATION DESIGN: PASS

PORTAL-A01 FROZEN
PORTAL-B01 READY: YES
```

## Prerequisite and Task Boundary

UI-CONSISTENCY-R02 completed before PORTAL-A01 began:

- result: PASS;
- commit: `c5622f7` (`fix(ui): improve sync plan readability and pagination layout`);
- delivery: pushed to `origin/main`.

PORTAL-A01 remained a separate documentation/architecture task. It did not modify the R02 implementation or report and did not start PORTAL-B01.

## Supplemental Scope Integration

The later PORTAL-A01 supplement was reread before design and merged into this single A01 decision. It adds the complete Admin `知识门户管理` responsibility:

- Admin-maintained Page Tree;
- existing-target selection for System, BusinessFunction, DatabaseObject, KnowledgeDocument, and Integration;
- Composite Page composition;
- the minimal PortalPage / PortalPageNode / PortalPageSection model;
- a required Primary Target;
- PrimaryTarget / ExplicitReference / Derived section sources;
- strict Portal composition-reference versus KnowledgeRelation separation;
- organization/reference/order-only persistence;
- Preview, Publish, and Unpublish;
- a Portal that remains anonymous, read-only, and free of management entry points.

No second A01 design was created.

## Sources and Implementation Inspected

The review followed `AGENTS.md` and `docs/DOCUMENT_INDEX.md`, then checked the applicable frozen/current sources, including:

- MVP API/domain/database/design/solution contracts;
- Knowledge Content architecture and current end-to-end evidence;
- KnowledgeDocument Markdown source/rendering contracts;
- REV-A01 and PHASE-REV verification;
- TRACE-A01 and PHASE-TRACE technical verification;
- ATTACH-A01/A02 and ATTACH-VERIFY evidence;
- DELETE-A01 soft-delete/dependency rules;
- current Vue routes, `App.vue`, Admin shell, navigation, SecurityGate, actor store integration, and shared API client;
- current ASP.NET Core default/fallback authorization and explicit policy setup;
- current KnowledgeDocument, KnowledgeRelation, DocumentType, lifecycle, Attachment, and soft-delete implementation boundaries.

The current code confirms that future Portal implementation must branch Portal routes before SecurityGate/current-user bootstrap, use a dedicated anonymous read client, and apply `[AllowAnonymous]` only to dedicated `/api/portal/**` GET controllers. These are frozen as implementation gates rather than performed in A01.

## Product and Deployment Decision

PASS. Portal is the System Knowledge Hub internal reading surface and remains inside the current Web/API/database/deployment. It does not add a second SPA, API, DbContext, database, Docker service, domain, certificate, or deployment system.

Portal Anonymous is explicitly not Internet Public. Deployment retains responsibility for internal network, VPN, reverse proxy, TLS, and environment controls. SEC-04 remains independent and open according to its own evidence.

## Portal vs Admin Decision

PASS. Admin remains the authenticated management console and owns all writes, including future Portal composition. Portal owns published safe reading only and contains no login, user identity, avatar, management link, write action, Discovery, Manual Sync, publishing control, or Admin entry.

Future Portal composition APIs are Administrator-only under `/api/admin/portal/**`; existing Admin API authorization remains unchanged. The anonymous `/api/portal/**` family is GET-only.

## Minimal Model Decision

PASS. The frozen model is:

- `PortalPage`: one addressable composition with a required Primary Target, publication/audit state, soft-delete state, and opaque page concurrency;
- `PortalPageNode`: Folder/Page tree organization, parent/page reference, order, publication/audit state, soft-delete state, and opaque node concurrency;
- `PortalPageSection`: ordered page-owned projection recipe with PrimaryTarget, ExplicitReference, or Derived source.

Composite Page is the normal multi-section capability of PortalPage and does not require a fourth entity or duplicate page type. The direct Node→canonical-target suggestion was analyzed and rejected because it cannot represent composite composition and page-level publication cleanly.

## Primary Target and Target-Type Decision

PASS. Every PortalPage has exactly one Primary Target. The v1 closed target enum is System, BusinessFunction, DatabaseObject, KnowledgeDocument, and Integration.

Requirement, Specification, TestCase, Sop, Troubleshooting, KnowledgeArticle, and DesignNote remain KnowledgeDocument DocumentType values. They are not duplicate Portal target identities. BusinessRule, DatabaseSource, and DatabaseColumn as direct Portal targets are deferred pending an approved reader projection/workflow.

## Composition Reference / KnowledgeRelation Decision

PASS. A composition reference means only “show this existing target here.” It has no RelationType, KnowledgeStatus, Evidence, semantic direction, or relationship-authoring behavior. It does not block target soft delete and never appears as a canonical relationship.

KnowledgeRelation remains an explicit semantic fact with the existing authoring, status, Evidence, validation, dependency, and removal rules. Neither side is automatically created, deleted, or translated from the other. Derived sections may read bounded eligible relations without persisting derived results.

## Admin Composition Workflow

PASS. The decision answers how Admin builds a complete knowledge system:

```text
Page Tree organizes discovery
→ PortalPage anchors a Primary Target
→ ordered sections combine primary, explicit supporting, and derived context
→ preview uses the exact Portal sanitizer
→ explicit publish exposes the page
→ unpublish withdraws it without deleting knowledge
```

Existing target pickers are bounded and business-readable. Published composition is changed only after unpublish in v1. Page and node mutations use opaque tokens and atomic validation.

## Publication and Lifecycle

PASS. All PortalPage and PortalPageNode rows default unpublished. Anonymous page read requires a published page, at least one fully published non-deleted node path, an eligible Primary Target, and eligible ExplicitReferences.

KnowledgeDocument must be Published and not deleted. Structured targets must be current and not deleted. Draft, Archived, deleted, unpublished, broken-reference, or orphaned direct routes fail closed as `404 not_found`, not `403` or a login prompt. Search uses the identical visibility predicate.

If a required explicit target later becomes ineligible, the page becomes unreadable until Admin repairs and republishes it. Derived groups filter ineligible results without disclosure. Publication never changes target lifecycle or KnowledgeStatus.

## Sanitization

PASS. Portal uses concrete target-specific DTO allowlists. It excludes user/current-user data, creator/updater identities, credentials, connection profiles, host/user/password/secret fields, connection strings, raw SQL, provider exceptions, Discovery technical identities, Runs/Snapshots/Differences/Sync Plans, admin audit payloads, storage keys/paths, stack traces, and write metadata.

KnowledgeDocument Markdown reuses the existing HTML-disabled safe renderer and strict Mermaid boundary. Evidence/HumanConfirmation are represented only as safe counts/states; no anonymous Evidence authoring/detail payload is reused.

## Anonymous API

PASS. The design freezes dedicated GET-only routes for home, tree, page, search, and page-scoped attachment delivery. Existing authenticated Admin/detail/trace/attachment APIs are not opened anonymously. There is no anonymous POST, PUT, PATCH, or DELETE.

The Portal frontend uses a read-only client without antiforgery, Admin security error handling, or current-user bootstrap. Admin preview/composition continues to use the authenticated client.

## Search

PASS. Search includes only effectively published readable PortalPages, uses bounded server paging (default 20, maximum 100), and returns page-oriented safe results. Draft/Archived/deleted/unpublished/admin-only data is excluded. Existing lexical/FTS capability may be reused as a derived accelerator; AI/RAG/vector/semantic search remains deferred.

## Attachment Read Boundary

PASS. Portal attachment access is scoped by `pageId + attachmentId`, not attachment ID alone. The service must first prove page visibility and then prove that the Attachment is a current-revision reference of a Published KnowledgeDocument contributing an effective PrimaryTarget or ExplicitReference section.

Existing attachment validation, safe content disposition, MIME/signature, preview limits, integrity, `nosniff`, and storage-path secrecy remain. Existing Admin/current/history attachment routes remain authenticated. Portal has no upload/replace/remove/delete and no historical revision attachment access.

## Trace Integration

PASS. Portal Trace is a separate sanitized read projection over the same canonical KnowledgeRelation truth and fixed TRACE-A01 paths/limits. It does not expose the current authenticated Trace DTO directly, include Draft/Archived/deleted documents, persist graph/coverage, or accept arbitrary traversal.

The separate PHASE-TRACE real-domain Product acceptance gate remains pending according to its historical final report. PORTAL-A01 does not claim to close it; the future PORTAL-B04 integration must continue to respect that gate.

## Tree, Integrity, and Concurrency

PASS. The tree maximum depth is 10. Self-parenting, cycles, excessive-depth moves, invalid Folder/Page fields, invalid source/projection combinations, duplicate active sibling/section order, and unsafe IDs are rejected. Ordering is deterministic and ends with ID.

Page/node soft delete and section removal affect composition only. Canonical target soft delete is not blocked by Portal navigation and causes fail-closed Portal reads. Existing KnowledgeRelation blockers remain unchanged. Page and node writes use current opaque tokens; stale state returns 409 with no partial reorder/move/publish.

## Performance and UX

PASS. Tree, page, related knowledge, trace, home, and search are all bounded. Page projections use bulk target resolution and do not trigger frontend or backend per-node N+1 behavior. The hard design limits include depth 10, 2,000 published nodes, 30 sections, five full document bodies, and 20 related results per group.

PortalLayout is independent from Admin AppShell, gives priority to reading width, and uses a collapsible 240–280px tree plus an optional collapsible related rail. Accessibility rules cover one main landmark, semantic headings/lists, visible focus, named controls, text-backed states, bounded local table/code overflow, zoom, and responsive layouts.

## Migration Impact

PASS / NOT EXECUTED BY DESIGN. A future B01 additive migration is expected to add `portal_pages`, `portal_page_nodes`, and `portal_page_sections` with the frozen FK/check/index rules. Existing rows are never automatically published. A01 made no schema/model change and created no migration.

## Validation Performed

This was a Markdown-only architecture task, so build/test/migration commands were not applicable. Validation consisted of:

- source-of-truth and current-code inspection;
- explicit Admin authorization preservation review;
- no-duplicate-truth and CompositionReference/KnowledgeRelation review;
- same-application/same-deployment review;
- publication/lifecycle/404 fail-closed review;
- sanitization and anonymous route allowlist review;
- attachment parent-page authorization-chain review;
- published-visible search review;
- bounded Trace/related projection review;
- final Markdown/index diff and whitespace validation with `git diff --check`.

## Files Owned by This Task

- `docs/design/PORTAL_A01_INTERNAL_KNOWLEDGE_PORTAL_ARCHITECTURE_DECISION.md`
- `docs/reports/PORTAL_A01_INTERNAL_KNOWLEDGE_PORTAL_ARCHITECTURE_FREEZE_REPORT.md`
- the two corresponding navigation entries in `docs/DOCUMENT_INDEX.md`
- the corresponding architecture/report entry in `docs/PROJECT_FILE_MAP.md`

Pre-existing unrelated worktree content was preserved and excluded from the PORTAL-A01 commit.

## Final Freeze

```text
PORTAL-A01 PASS
PORTAL-A01 FROZEN
PORTAL-B01 READY: YES
```

Stop after this documentation task. Do not start PORTAL-B01 without a separate instruction.
