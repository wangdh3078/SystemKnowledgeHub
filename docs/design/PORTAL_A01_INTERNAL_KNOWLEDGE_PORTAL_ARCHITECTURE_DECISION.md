# PORTAL-A01 — Internal Knowledge Portal Architecture and Contract Decision

> Product: 系统知识中心 / System Knowledge Hub
>
> Decision date: 2026-09-03
>
> Scope: Portal reader plus Admin Knowledge Composition architecture only

## 1. Decision Status

```text
PORTAL-A01 APPROVED
Blocking product or architecture decisions: NONE
Implementation status: NOT STARTED
PORTAL-B01 READY: YES
```

This document is the capability-specific post-MVP decision for the internal knowledge portal. It does not edit or silently reinterpret the frozen MVP specifications. It authorizes later PORTAL-Bxx implementation slices, but creates no schema, migration, controller, Vue page, route, package, or deployment change itself.

The supplemental PORTAL-A01 requirement for **Admin Knowledge Composition / 知识门户管理** is part of this same decision. It is not a second A01 design.

## 2. Product Goal

Portal is the anonymous, read-only reading surface of System Knowledge Hub. Its purpose is to help an internal reader find and understand a complete knowledge context without navigating the high-density Admin Console one entity at a time.

The Admin Console gains a future `知识门户管理` capability that lets an Administrator arrange existing knowledge into a page tree and compose several existing knowledge objects into one reading page. The composition stores presentation organization, references, and order only. Canonical facts continue to live exclusively in `System`, `BusinessFunction`, `DatabaseObject`, `KnowledgeDocument`, `Integration`, `KnowledgeRelation`, Evidence, HumanConfirmation, and their current lifecycle/status models.

## 3. Authority and Current Baseline

This decision was checked against the current implementation and the applicable frozen Knowledge Content, Revision, Traceability, Attachment, Soft Delete, Security, API, domain, database, and UI sources.

Current constraints that directly shape the design are:

- the Vue application currently sends every unauthenticated route through `SecurityGate`;
- all current product routes use the existing Admin `app-shell` layout;
- the ASP.NET Core default and fallback authorization policies require Viewer access;
- the shared frontend API client sends credentials and forwards authentication failures to the global security handler;
- KnowledgeDocument current heads, immutable revisions, current-head FTS, explicit KnowledgeRelation rows, bounded Trace projections, protected attachment routes, opaque concurrency tokens, and soft-delete filtering already exist;
- current Admin read/write APIs are authenticated and must remain so.

PORTAL implementation must therefore add explicit route and API boundaries rather than weakening any existing gate.

## 4. Portal vs Admin Boundary

| Capability | Admin Console | Portal |
| --- | --- | --- |
| Authentication | Required | No Portal login flow |
| Create/edit/delete knowledge | Yes, existing rules | Never |
| Evidence/HumanConfirmation/status authoring | Yes | Never |
| Relationship authoring | Yes | Never |
| Discovery/Manual Sync | Yes | Never |
| User/attachment administration | Yes | Never |
| Page tree and composition management | Future `知识门户管理` | Never |
| Preview/publish/unpublish composition | Future Administrator capability | Never |
| Read published safe knowledge | Possible through existing authenticated views | Primary responsibility |

Portal contains no login button, current-user display, avatar, user menu, user management, write control, management shortcut, `进入管理后台`, or `管理员入口`.

## 5. Anonymous Internal-Read Decision

Portal is an **internal-network anonymous knowledge portal**, not an Internet Public Knowledge Site.

The application boundary is:

```text
anonymous + GET only + published only + sanitized projection
```

The deployment boundary remains the operator's trusted internal network, VPN, reverse proxy, firewall, TLS, and other environment controls. Anonymous Portal access does not close, waive, or weaken SEC-04.

## 6. Same-Application / Same-Deployment Decision

Portal uses:

- the same `SystemKnowledgeHub.Web` Vue application;
- the same `SystemKnowledgeHub.Api` ASP.NET Core application;
- the same `KnowledgeHubDbContext` and SQLite database;
- the same build and deployment package;
- the same domain/port unless deployment chooses an ordinary reverse-proxy mapping.

It does not create a second SPA, API, DbContext, database, Docker service, certificate, server, or deployment system.

Example routing may be:

```text
https://knowledge.internal/portal          → Portal reader
https://knowledge.internal/dashboard       → existing Admin Console
https://knowledge.internal/admin/portal    → future authenticated composition management
```

## 7. Chosen Architecture

```text
Canonical knowledge facts and explicit KnowledgeRelation rows
                         │
             safe, current projections
                         │
       PortalPage + PortalPageSection composition
                         │
          PortalPageNode published page tree
                         │
     dedicated anonymous GET /api/portal/** boundary
                         │
          PortalLayout / reading-oriented pages
```

Composition is a locator and presentation plan. It is not a knowledge store, graph, revision system, or content cache.

## 8. Minimal Portal Model

The minimum model is exactly three concepts:

1. `PortalPage` — one addressable reading page with one Primary Target;
2. `PortalPageNode` — one folder or page placement in the navigation tree;
3. `PortalPageSection` — one ordered safe projection in a page.

`Composite Page` is a capability of `PortalPage`, not a fourth entity or a separate page type. A simple page can have one PrimaryTarget section. A composite page adds ExplicitReference and/or Derived sections.

The original direct `PortalPageNode.TargetType/TargetId` suggestion is deliberately replaced by this three-model shape. A direct node-to-target model cannot express a stable composite page, page-level publication, or ordered sections without duplicating those concerns in the tree.

## 9. PortalPage Domain

| Property | Rule |
| --- | --- |
| `Id` | Required JavaScript-safe positive integer. Canonical v1 route ID. |
| `Title` | Required curated presentation title, trimmed, 1–200 characters. It is navigation metadata, not copied target content. |
| `PrimaryTargetType` | Required closed `PortalTargetType`. |
| `PrimaryTargetId` | Required JavaScript-safe positive integer, resolved by the controlled target resolver. |
| `IsPublished` | Required, default `false`. |
| `PublishedAt` / `PublishedByUserId` / display snapshot | Nullable; server-owned, all set together on publish. |
| `UnpublishedAt` / `UnpublishedByUserId` / display snapshot | Nullable; server-owned latest unpublish audit. |
| `CreatedAt` / `CreatedByUserId` / display snapshot | Required server-owned audit. |
| `UpdatedAt` / `UpdatedByUserId` / display snapshot | Required server-owned audit. |
| `Version` | Required positive application version, exposed only as opaque `concurrencyToken`. |
| `IsDeleted` / deletion audit | Soft-delete state for stable IDs and fail-closed current reads. Restore UI/API is deferred. |

`PortalPage` stores no summary, body Markdown, rendered HTML, Evidence copy, status copy, relationship copy, attachment copy, search text, or target display snapshot.

Every page, including a composite page, has exactly one Primary Target. This anchor gives the page an unambiguous subject, publication eligibility root, default breadcrumb identity, and Derived-section context. A page with no canonical target is outside v1; folders cover pure navigation grouping.

Multiple PortalPages may use the same Primary Target when the business needs distinct reading compositions. They remain multiple presentations over one fact source.

## 10. Primary Target

The v1 closed `PortalTargetType` is:

```text
System
BusinessFunction
DatabaseObject
KnowledgeDocument
Integration
```

These map to existing canonical identities. `Requirement`, `Specification`, `TestCase`, `Sop`, `Troubleshooting`, `KnowledgeArticle`, and `DesignNote` remain `KnowledgeDocument.DocumentType` values and never become duplicate target types.

`BusinessRule` is intentionally deferred from the first target enum until its Portal-safe reader projection and primary reading workflow are separately validated. `DatabaseSource` and `DatabaseColumn` are not v1 page targets; safe source context and columns may be projected through a DatabaseObject page. The enum is closed and expanded only by an explicit contract amendment.

## 11. PortalPageNode Domain

| Property | Rule |
| --- | --- |
| `Id` | Required JavaScript-safe positive integer. |
| `ParentId` | Nullable self-FK with `RESTRICT`; null means root. |
| `Title` | Required curated navigation label, 1–200 characters. |
| `NodeKind` | Closed `Folder | Page`. |
| `PortalPageId` | Null for Folder; required for Page; FK with `RESTRICT`. |
| `SortOrder` | Required non-negative integer within the sibling set. |
| `IsPublished` | Required, default `false`. |
| `PublishedAt` / `PublishedByUserId` / display snapshot | Nullable; server-owned and set together on publish. |
| `UnpublishedAt` / `UnpublishedByUserId` / display snapshot | Nullable; server-owned latest unpublish audit. |
| `CreatedAt` / `CreatedByUserId` / display snapshot | Required server-owned audit. |
| `UpdatedAt` / `UpdatedByUserId` / display snapshot | Required server-owned audit. |
| `Version` | Required positive version, returned only as opaque token to Admin. |
| `IsDeleted` / deletion audit | Soft-delete state; current reads exclude it. |

A Folder has no target and no knowledge facts. A Page node references a `PortalPage`, not a canonical knowledge object directly. The same `PortalPage` may appear in more than one Page node, and the same canonical target may be used by more than one page or node. These are multiple navigation references, never copied knowledge.

When a page has several effective published paths, its canonical breadcrumb is the first path in deterministic tree order (ancestor `SortOrder`, then ancestor `Id`, repeated by level). Alternate Page nodes remain valid navigation entries but do not create alternate content URLs or ambiguous page identities.

## 12. PortalPageSection Domain

| Property | Rule |
| --- | --- |
| `Id` | Required safe positive integer. |
| `PortalPageId` | Required FK to owning page with `RESTRICT`. |
| `Heading` | Required curated section label, 1–200 characters. |
| `SourceKind` | Closed `PrimaryTarget | ExplicitReference | Derived`. |
| `ReferenceTargetType` / `ReferenceTargetId` | Required only for ExplicitReference; otherwise null. |
| `ProjectionKind` | Required closed projection recipe valid for the chosen source/target type. |
| `SortOrder` | Required non-negative integer, unique within the page. |

Sections are part of the `PortalPage` aggregate and have no independent route, publication state, authoring body, Evidence, status, or concurrency token. A whole-page Admin PUT carries the complete ordered section set and the page's opaque token. Removing a section deletes only composition metadata within that authenticated transaction; it never deletes or changes a referenced target.

### 12.1 SourceKind rules

| SourceKind | Reference fields | Meaning |
| --- | --- | --- |
| `PrimaryTarget` | Must be null | Project a safe facet of the page's Primary Target. |
| `ExplicitReference` | Type and ID required | Project a specifically selected existing target. It asserts no semantic relationship. |
| `Derived` | Must be null | Execute one approved bounded read recipe rooted at the Primary Target. No arbitrary query or path is stored. |

### 12.2 ProjectionKind rules

The first contract uses a closed compatibility matrix rather than a generic component name or query language:

| ProjectionKind | Allowed source | Target constraint |
| --- | --- | --- |
| `Summary` | PrimaryTarget / ExplicitReference | Any v1 target |
| `KnowledgeDocumentBody` | PrimaryTarget / ExplicitReference | KnowledgeDocument only |
| `StructuredOverview` | PrimaryTarget / ExplicitReference | System, BusinessFunction, DatabaseObject, Integration |
| `DatabaseStructure` | PrimaryTarget / ExplicitReference | DatabaseObject only |
| `AttachmentList` | PrimaryTarget / ExplicitReference | KnowledgeDocument only |
| `TrustSummary` | PrimaryTarget / ExplicitReference | One explicit v1 PortalTarget only; `Derived` is not supported in Portal v1 (see PORTAL-A01-AMEND-01) |
| `RelatedKnowledge` | Derived only | Uses approved direct KnowledgeRelation groups |
| `Traceability` | Derived only | KnowledgeDocument Requirement/Specification/TestCase only |

Invalid combinations return `400 validation_error` while authoring and block publication. A section stores none of the rendered result.

## 13. Portal Composition Reference vs KnowledgeRelation

These concepts are strictly separate.

| Dimension | Portal composition reference | KnowledgeRelation |
| --- | --- | --- |
| Purpose | Page organization and presentation | Canonical semantic relationship fact |
| Stored in | PortalPage Primary Target or PortalPageSection ExplicitReference | `KnowledgeRelation` |
| Meaning | “Show this target here” | Controlled RelationType such as Documents, References, AppliesTo |
| KnowledgeStatus / Evidence | Never | Existing independent trust model |
| Appears in relationship authoring/detail | No | Yes |
| Blocks target soft delete | No | Existing dependency rules apply |
| Deleting reference | Removes placement only | Existing relationship remove semantics |
| Automatic conversion | Forbidden | Forbidden |

Creating an ExplicitReference does not create `Documents`, `References`, or any other KnowledgeRelation. Creating a KnowledgeRelation does not add a section or tree node. A Derived section may read eligible KnowledgeRelation rows, but it never persists, reverses, infers, or repairs them.

## 14. No Duplicate Knowledge Truth

Allowed Portal persistence is limited to:

```text
page identity and presentation title
primary target identity
tree parent/page placement and order
section heading, source recipe, reference identity, and order
publication/concurrency/audit metadata
```

Forbidden persistence includes target names/descriptions/bodies, Markdown, rendered HTML/SVG, database structure, relationship lists, trace results, Evidence/HumanConfirmation details, KnowledgeStatus, attachment metadata/binaries, search documents, or lifecycle copies.

Portal reads current canonical data at request time. A canonical fact change is reflected without rewriting the page. This is deliberate: publication approves the page and its projection recipes, not a copied fact snapshot.

## 15. Admin Knowledge Composition / 知识门户管理

The existing authenticated Admin Console adds one future `知识门户管理` navigation capability. It uses the current Admin shell and never appears inside Portal.

The primary workflow is:

1. create a PortalPage and choose one existing Primary Target through a bounded picker;
2. add/reorder sections using `PrimaryTarget`, `ExplicitReference`, or `Derived`;
3. for ExplicitReference, search and select existing System, BusinessFunction, DatabaseObject, KnowledgeDocument, or Integration records;
4. create folders and Page nodes, move/reorder them within the Page Tree, and reuse a page at more than one location where helpful;
5. preview through the exact sanitized Portal projection and fix any eligibility errors;
6. publish the page, then publish an eligible node path;
7. unpublish a page or node to withdraw it immediately while retaining its composition for later correction.

Published page composition is read-only in v1. An Administrator must unpublish before changing title, Primary Target, or sections. Published nodes must be unpublished before move, rename, or reorder. This gives v1 a simple Preview → Publish safety boundary without inventing a second draft revision or approval workflow.

All target pickers are server-paged and display business-readable identity and context. They never ask an Administrator to type a raw polymorphic ID.

## 16. How Admin Builds a Complete Knowledge System

The complete-system answer is the combination of three layers:

```text
Page Tree
  defines where readers find knowledge
        ↓
PortalPage + Primary Target
  defines what each reading page is about
        ↓
Ordered Sections
  combine the primary fact, explicitly selected supporting facts,
  and bounded derived relation/trace/trust context
```

For example, an MES folder may contain one `Lot Track In` page anchored to a BusinessFunction. Its sections can show the function summary, explicitly selected SOP and DatabaseObject facts, and derived related integrations and traceability. Admin controls organization and reading sequence; authors continue to edit each fact through its existing Admin feature. This is how existing knowledge is “strung together” without a second truth model.

## 17. Preview, Publish, and Unpublish

### Preview

- Administrator-only.
- Uses the same Portal sanitizer, target resolver, lifecycle filters, projection recipes, ordering, and limits as anonymous read.
- Can resolve an unpublished page/node and returns exact actionable blockers to the Administrator.
- Displays a visible `预览` marker and is never cacheable as anonymous content.
- Does not change publication state or canonical knowledge.

### Publish

Publication is explicit and defaults to false. Page publish validates the Primary Target, every ExplicitReference, all source/projection combinations, section limits, and current lifecycle/deletion eligibility. Node publish validates its ancestors and referenced page. Server-owned actor/time are recorded and the opaque token advances.

### Unpublish

Unpublish is immediate, advances the token, records server actor/time, and does not delete composition or knowledge. Unpublishing a Folder makes descendants effectively invisible without rewriting their individual flags. Unpublishing a page hides every node placement of that page.

## 18. Publication and Lifecycle Eligibility

An anonymous page is readable only when all are true:

1. `PortalPage.IsPublished` and not deleted;
2. at least one non-deleted published Page node references it;
3. every ancestor on that path is non-deleted and published;
4. Primary Target exists and is Portal-eligible;
5. every ExplicitReference target exists and is Portal-eligible;
6. composition remains within all hard limits.

Target eligibility is:

- KnowledgeDocument: `LifecycleStatus = Published` and `IsDeleted = false`;
- structured target: current non-deleted row that passes its existing current-read integrity rules;
- soft-deleted target: never eligible;
- Draft or Archived KnowledgeDocument: never eligible.

If the Primary Target or any ExplicitReference becomes ineligible after publication, the whole page and all its search results return `404 not_found` until Admin repairs or removes the reference and republishes. Derived groups simply filter out ineligible related results and expose accurate truncation/empty metadata; they never reveal the missing target.

Direct numeric URLs use the same eligibility. They never return `403`, a login prompt, a tombstone, draft metadata, or evidence that an unpublished/deleted target exists.

## 19. Tree Integrity

The frozen tree rules are:

- maximum depth is **10**, counting a root node as depth 1;
- ParentId cannot equal Id;
- cycle creation is rejected before persistence;
- a move validates the moved subtree's resulting maximum depth;
- a Folder has no `PortalPageId`; a Page node must have one;
- a Folder with current children cannot be removed;
- a PortalPage with current Page-node references cannot be deleted;
- sibling order is deterministic by `SortOrder ASC, Id ASC`;
- active sibling SortOrder values must be unique;
- identical titles and repeated page/target placements are allowed;
- root and non-root sibling uniqueness are both enforced by appropriate filtered indexes plus transactional validation;
- no recursive query accepts client-selected depth beyond the fixed contract.

PortalPage and PortalPageNode use soft delete. A canonical target soft delete is not blocked by Portal composition: the Portal page becomes fail-closed and Admin preview reports a broken reference. This preserves the rule that navigation is not fact ownership. Existing KnowledgeRelation deletion blockers remain unchanged.

## 20. Portal Layout

Portal uses a new `PortalLayout` selected by route metadata, visually independent from `AppShell`.

```text
┌────────────────────────────────────────────────────────────┐
│ 系统知识中心                         [搜索知识……]           │
├──────────────┬─────────────────────────────────────────────┤
│ 可折叠知识树 │ breadcrumb                                  │
│ 240–280 px   │                                             │
│              │ 主阅读内容                                  │
│              │                                             │
│              │                         可选相关知识侧栏     │
└──────────────┴─────────────────────────────────────────────┘
```

The header contains only brand/Portal name, search, breadcrumb, and ordinary navigation. The related rail is optional and collapsible. Main reading width is materially larger than Admin detail pages. Admin tables and drawers are not the primary Portal reading pattern.

## 21. Unified Knowledge Page

A Portal page is a server-composed, page-oriented aggregate. It may include:

- summary and current body from a published KnowledgeDocument;
- safe structured overview of the Primary Target;
- selected supporting targets;
- safe DatabaseObject structure and manually maintained knowledge;
- bounded related knowledge;
- bounded Traceability;
- sanitized trust summary;
- eligible current-revision attachments.

The backend returns final section order and discriminated DTOs. The frontend does not fetch each target in a loop and does not reconstruct relation semantics.

## 22. KnowledgeDocument Projection

Portal uses the current Published KnowledgeDocument head only. It never exposes Draft, Archived, deleted, or historical revision content.

- Markdown renders through the existing shared HTML-disabled safe renderer.
- Mermaid uses the existing strict view-only renderer; generated SVG/HTML is never stored.
- raw HTML, script, event handlers, dangerous protocols, and arbitrary CSS remain disabled.
- Portal provides no source editor, save, lifecycle action, history, compare, restore, revision selector, or current-user attribution.
- revision history is deferred from Portal v1.

Author/uploader identity snapshots and administrative audit fields are omitted from anonymous DTOs.

## 23. Structured Knowledge Projection

Every supported structured target has a concrete allowlisted DTO; there is no generic reflection-based object response.

| Target | Safe examples | Explicit exclusions |
| --- | --- | --- |
| System | name, business description, lifecycle label, approved technology tags | creator/update identity, admin actions |
| BusinessFunction | name, description, function type, bounded process steps, owning System context | write metadata, internal audit |
| DatabaseObject | schema/name/type, business description, manual EstimatedRows, access mode, business keys, bounded columns/native types/nullability/comments | connection profile, host, username, secret, connection string, provider errors, raw SQL, Discovery identity/run/sync plan |
| Integration | business-readable parties, direction/type, description, bounded contract fields | credentials, secrets, raw transport diagnostics, admin actions |

Structured data has no separate Portal entity and is read from current canonical rows.

## 24. Traceability and Related Knowledge

Portal does not anonymously expose the existing authenticated Trace endpoints or their Admin DTOs. A Portal `Traceability` section uses a Portal-specific sanitized projection over the same canonical KnowledgeRelation rows and the same frozen fixed path semantics and hard limits.

- only Published, non-deleted KnowledgeDocument nodes are eligible;
- no actor IDs/names, free-form relationship description, raw Evidence, or admin route is returned;
- missing links, bounded paths, status/trust signals, truncation, and cycle warnings remain derived;
- no graph, coverage, inverse edge, or status is persisted;
- no arbitrary depth/path parameter is accepted;
- relationship authoring remains authenticated and unchanged.

The outstanding PHASE-TRACE real-domain Product acceptance gate remains independent. Portal design does not falsely close it; the future trace integration slice must respect its outcome.

## 25. Evidence, HumanConfirmation, and KnowledgeStatus

Portal may show only an allowlisted trust summary such as KnowledgeStatus, Evidence count, HumanConfirmation count, and current-revision confirmation coverage state. It does not expose provider/user identities, source locators that may be sensitive, actor snapshots, Evidence edit/detail payloads, or management actions.

KnowledgeStatus, lifecycle, Evidence, and HumanConfirmation remain independent canonical facts. Portal composition never creates or changes them.

## 26. Attachment Read Boundary

Existing authenticated attachment endpoints remain authenticated. Portal adds separate GET-only routes scoped by an already readable Portal page:

```text
GET /api/portal/pages/{pageId}/attachments/{attachmentId}/content
GET /api/portal/pages/{pageId}/attachments/{attachmentId}/download
GET /api/portal/pages/{pageId}/attachments/{attachmentId}/preview
```

The server first resolves the full page publication path, then proves that the Attachment belongs to a Published KnowledgeDocument contributing an effective PrimaryTarget or ExplicitReference section and is referenced by that document's current revision. Attachment ID alone is insufficient. Derived sections do not grant attachment access in v1.

The existing extension/MIME/signature, size, storage-key, path, integrity, `nosniff`, safe disposition, preview limit, and missing-storage policies remain. Portal returns no storage key/path and supports no upload, replace, remove, or delete. Historical revision attachment routes are not exposed to Portal.

## 27. Anonymous API Boundary

Only dedicated `/api/portal/**` controllers are marked `[AllowAnonymous]`. Existing controllers and the Viewer default/fallback policies remain unchanged.

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/portal/home` | Bounded top-level categories and at most eight recently published readable pages. |
| GET | `/api/portal/tree` | Complete effective published tree within the hard node cap. |
| GET | `/api/portal/pages/{id}` | One sanitized unified reading page. |
| GET | `/api/portal/search?q=&page=1&pageSize=20` | Published-visible lexical search. |
| GET | `/api/portal/pages/{pageId}/attachments/{attachmentId}/content` | Eligible inline image. |
| GET | `/api/portal/pages/{pageId}/attachments/{attachmentId}/download` | Eligible current attachment download. |
| GET | `/api/portal/pages/{pageId}/attachments/{attachmentId}/preview` | Eligible safe preview. |

There is no POST, PUT, PATCH, or DELETE under `/api/portal/**`. Invalid safe-integer/query input returns `400 validation_error`. Unavailable/unpublished/deleted content returns `404 not_found`. Projection integrity failure is fail-closed and sanitized.

The page response contains an ordered list of explicit discriminated section DTOs. It never returns a generic property bag, storage key, connection secret, raw SQL, provider exception, stack trace, or user object.

Minimum page response shape:

```json
{
  "id": 81,
  "title": "Lot Track In",
  "primaryTarget": {
    "type": "BusinessFunction",
    "id": 42,
    "title": "Lot Track In"
  },
  "breadcrumb": [
    { "nodeId": 10, "title": "MES" },
    { "nodeId": 11, "title": "业务流程" }
  ],
  "sections": [
    {
      "id": 301,
      "heading": "业务说明",
      "sourceKind": "PrimaryTarget",
      "projectionKind": "StructuredOverview",
      "content": { "kind": "BusinessFunctionOverview" }
    }
  ]
}
```

`content.kind` is a closed discriminator with a separately defined allowlisted DTO for each projection kind. Anonymous responses omit concurrency, publication actor, deletion audit, Admin capability, and raw reference-integrity diagnostics.

## 28. Admin API Boundary

Future composition writes use a separate Administrator-only `/api/admin/portal/**` family. This does not violate the GET-only anonymous Portal contract.

| Method | Route | Contract |
| --- | --- | --- |
| GET | `/api/admin/portal/pages?page=1&pageSize=20&search=` | Bounded page inventory, publication/reference health, opaque token. |
| POST | `/api/admin/portal/pages` | Create unpublished page from title and Primary Target. |
| GET | `/api/admin/portal/pages/{id}` | Complete editable composition and diagnostics. |
| PUT | `/api/admin/portal/pages/{id}` | Replace title, Primary Target, and complete ordered section set while unpublished. |
| DELETE | `/api/admin/portal/pages/{id}` | Soft delete an unpublished unreferenced page with token. |
| GET | `/api/admin/portal/pages/{id}/preview` | Exact sanitized preview plus actionable eligibility blockers. |
| POST | `/api/admin/portal/pages/{id}/publish` | Validate and publish with token. |
| POST | `/api/admin/portal/pages/{id}/unpublish` | Unpublish with token. |
| GET | `/api/admin/portal/tree` | Complete Admin tree including drafts and broken-reference diagnostics within the hard cap. |
| POST | `/api/admin/portal/nodes` | Create an unpublished Folder or Page node. |
| PUT | `/api/admin/portal/nodes/{id}` | Rename/move/change page placement of one unpublished node. |
| PUT | `/api/admin/portal/nodes/reorder` | Replace one sibling set's order atomically using IDs and tokens. |
| DELETE | `/api/admin/portal/nodes/{id}` | Soft delete an unpublished eligible node with token. |
| POST | `/api/admin/portal/nodes/{id}/publish` | Publish an eligible node with token. |
| POST | `/api/admin/portal/nodes/{id}/unpublish` | Unpublish a node with token. |
| GET | `/api/admin/portal/targets?type=&search=&page=1&pageSize=20` | Bounded business-readable canonical target picker. |

The whole-page update request has this authoritative shape:

```json
{
  "title": "Lot Track In",
  "primaryTarget": { "type": "BusinessFunction", "id": 42 },
  "sections": [
    {
      "id": 301,
      "heading": "业务说明",
      "sourceKind": "PrimaryTarget",
      "referenceTarget": null,
      "projectionKind": "StructuredOverview",
      "sortOrder": 0
    },
    {
      "id": null,
      "heading": "相关 SOP",
      "sourceKind": "ExplicitReference",
      "referenceTarget": { "type": "KnowledgeDocument", "id": 99 },
      "projectionKind": "KnowledgeDocumentBody",
      "sortOrder": 1
    }
  ],
  "concurrencyToken": "opaque"
}
```

Create omits section IDs and token. Existing section IDs must belong to the addressed page; cross-page IDs return bounded `422 reference_invalid` without target disclosure. Publish/unpublish requests contain only `concurrencyToken`. Node create/update requests contain only title, NodeKind, parent/page reference, SortOrder, and the applicable token; server owns actor/time/publication audit.

All mutations require the existing Administrator policy, antiforgery protection, canonical Current User, server UTC, and opaque concurrency tokens. Stale tokens return `409 conflict`. PUT, not generic PATCH, owns the full page composition section. No client actor/time/publication audit fields are authoritative.

## 29. Search Boundary

Portal search is a dedicated lexical read model over only currently readable PortalPages.

- query length 1–100;
- page defaults to 1;
- pageSize defaults to 20 and has a hard maximum of 100;
- server-side paging only;
- search results are page identities, not raw canonical target routes;
- each result contains page title, Primary Target type/title, and bounded published breadcrumb;
- Draft, Archived, deleted, unreferenced, page-unpublished, node-unpublished, or ancestor-unpublished data is absent from results;
- User, Discovery Run, Snapshot, Difference, Sync Plan, connection profile, and admin-only attachment data are never indexed or returned.

The existing current-head lexical/FTS capability may be joined or reused where safe; any Portal acceleration remains derived and rebuildable, never a source of truth. V1 adds no AI, RAG, embedding, vector database, semantic ranking, or body-copy table.

## 30. Frontend Routing and SecurityGate Integration

The v1 route family is:

```text
/portal
/portal/pages/:id
/portal/search
/portal/:pathMatch(.*)*   (Portal-specific 404)
```

Numeric safe IDs are canonical. Friendly slugs, rename redirects, and slug history are deferred.

`App.vue` must select `PortalLayout` **before** evaluating `SecurityGate` or `ForcedPasswordChangeGate`. Portal route startup must not call `/api/current-user`, require actor-store initialization, or turn an anonymous Portal error into the Admin login screen. Admin routes keep the current gates.

Portal uses a small read-only API client on the same base URL with `credentials: 'omit'`, no antiforgery provider, no unsafe methods, and no global Admin security-error handler. Admin preview and composition continue to use the authenticated client.

## 31. Portal Home and Navigation UX

`/portal` is for finding knowledge, not monitoring operations. It contains:

- `系统知识中心` identity;
- global Portal search;
- top-level published tree categories;
- up to eight recently published readable pages when the bounded query is available;
- no Admin metrics, Discovery status, user state, or management CTA.

Desktop tree width is 240–280px and collapsible. At narrower widths it becomes an accessible disclosure/overlay and does not permanently consume content width. Closing/returning preserves tree expansion and page scroll where practical.

## 32. Empty and Error States

Required copy is plain and login-free:

- 404: `页面未找到`;
- empty tree/home: `暂无已发布知识`;
- empty search: `未找到匹配的已发布知识`;
- retryable failure: `知识暂时无法加载，请稍后重试。`.

No state says “请登录后查看” or reveals whether a hidden object exists.

## 33. Data Model and Migration Impact

Expected future additive tables are:

```text
portal_pages
portal_page_nodes
portal_page_sections
```

Required constraints/indexes include:

- positive safe IDs and versions, nonblank bounded titles/headings, non-negative SortOrder;
- closed CHECK constraints for NodeKind, PortalTargetType, SourceKind, and ProjectionKind;
- real FKs for Node parent, Node page, and Section page, all `RESTRICT`;
- active unique section order `(portal_page_id, sort_order)`;
- active non-root sibling order `(parent_id, sort_order)` plus an active-root SortOrder filtered index;
- indexes for page publication, node parent/publication/order, node page lookup, and section page/order;
- check constraints for Folder/Page and SourceKind nullable-field combinations;
- no polymorphic FK to canonical targets; target existence/type/lifecycle is enforced by one controlled resolver and focused tests;
- no second DbContext or database.

PORTAL-A01 creates **no migration**. PORTAL-B01 owns one reviewed additive migration and backfill behavior. New rows default unpublished; no existing knowledge is automatically exposed.

## 34. Removal and Target Deletion Behavior

- PortalPage and PortalPageNode use soft delete with current query filtering and server audit.
- deletion requires Administrator, current opaque token, and unpublished state;
- Folder deletion is blocked by current children;
- Page deletion is blocked by current Page-node references;
- section removal occurs only inside a whole-page unpublished composition update;
- deleting/removing Portal metadata never changes canonical knowledge or KnowledgeRelation;
- canonical target soft delete is not blocked by Portal composition and makes affected pages fail-closed;
- no automatic rebind by name or target replacement occurs;
- recovery UI/API, composition history, and approval workflow are deferred.

## 35. Concurrency and Atomicity

`PortalPage.Version` protects title, Primary Target, complete section composition, publish/unpublish, and delete. `PortalPageNode.Version` protects title, parent, page placement, order, publish/unpublish, and delete.

A sibling reorder request carries the complete ordered current sibling IDs and their opaque tokens. Validation, cycle/depth checks, order update, actor/time update, and token increments occur in one short transaction. Any stale token returns `409 conflict` with no partial move.

Publish and unpublish perform authoritative revalidation inside the same transaction. Tokens remain opaque; API clients never calculate or persist integer versions directly.

## 36. Performance Limits

| Surface | Frozen limit |
| --- | ---: |
| Tree depth | 10 |
| Effective published nodes | 2,000 |
| Sections per page | 30 |
| Full KnowledgeDocument body sections per page | 5 |
| Related results per derived group | 20 |
| Trace nodes/edges/depth | Reuse existing 200 / 300 / fixed 2 caps |
| Recent pages on home | 8 |
| Search page size | default 20, maximum 100 |

Publication rejects composition that would exceed hard limits. Tree/page reads use `AsNoTracking`, bulk target resolution, and fixed query plans; no node or section causes a separate API request. Stable ordering always ends with ID. No graph database, materialized knowledge copy, background Portal projector, or cache is authorized without measured evidence.

## 37. Accessibility and Responsive Expectations

- one `<main>` landmark per Portal page;
- semantic headings follow the configured section order;
- tree navigation uses semantic lists unless a complete ARIA tree keyboard model is implemented;
- all links, disclosures, tabs, search, and collapse controls have accessible names and visible focus;
- state is never color-only;
- Markdown tables/code blocks own bounded local overflow and never create document-level horizontal overflow;
- keyboard and 200% zoom keep content/action order usable;
- desktop validation targets include 1366×768, 1440×900, and 1920×1080;
- narrow layout collapses navigation/related rail before shrinking readable content below a practical measure.

## 38. Implementation Sequence

The revised sequence is:

```text
PORTAL-B01
PortalPage / PortalPageNode / PortalPageSection persistence,
controlled target resolver, publication rules, anonymous read foundation

PORTAL-B02
Admin Knowledge Composition: Page Tree, target picker, Composite Page,
Preview, Publish, Unpublish

PORTAL-B03
PortalLayout, tree, home, unified page, responsive anonymous route boundary

PORTAL-B04
Portal search, page-scoped attachments, sanitized related/trust/trace integration

PORTAL-VERIFY
End-to-end internal anonymous read, Admin composition, security,
lifecycle, search, attachment, trace, performance, accessibility and cleanup gate
```

Each slice receives its own implementation contract and verification report. A01 does not start B01.

## 39. Explicit DEFER List

The following are outside v1:

- independent Portal project, SPA, API, DbContext, database, deployment, domain, certificate, or Docker service;
- KnowledgeSpace, workspace, tenant, multi-tenant, space RBAC, permission inheritance, or per-page ACL;
- Internet Public publishing, Portal login/account/user management, or Admin entry in Portal;
- comments, collaboration, approval workflow redesign, page revision history, scheduled publication, or publishing channels;
- friendly slug/history/redirect machinery;
- BusinessRule as a primary/explicit PortalTargetType;
- Requirement/Specification/TestCase as duplicate target types;
- historical KnowledgeDocument/revision reading in Portal;
- arbitrary section HTML/component/query/path expressions;
- AI, RAG, embeddings, vector/semantic search, automatic relationship inference, or automatic KnowledgeStatus;
- database live query, SQL console, raw Discovery data, SECS/GEM, mobile app, offline mode, analytics, favorites, or personalization.

## 40. Alternatives Rejected

| Alternative | Decision |
| --- | --- |
| Second Portal application/deployment | Rejected: duplicates delivery and security boundaries. |
| Node directly references canonical target | Rejected: cannot model Composite Page and page publication cleanly. |
| Copy facts into Portal pages | Rejected: creates drift and a second truth. |
| Treat ExplicitReference as KnowledgeRelation | Rejected: presentation placement is not a semantic fact. |
| Reuse Admin detail DTOs anonymously | Rejected: leaks fields and lifecycle states not approved for Portal. |
| Open existing attachment endpoints anonymously | Rejected: attachment ID lacks a published page authorization context. |
| Reuse global/Admin search as-is | Rejected: it contains non-Portal-visible knowledge. |
| Generic graph/query/section framework | Rejected: unbounded semantics, authorization, and complexity. |

## 41. Acceptance Gates

Future implementation cannot claim Portal completion until it proves:

1. same Web/API/DbContext/database/deployment;
2. `/portal/**` renders with no current-user bootstrap or login UI;
3. only dedicated GET routes are anonymous and every existing Admin route remains protected;
4. Page, Node, Section schema/integrity/concurrency and max-depth rules;
5. Admin can select existing targets, compose, preview, publish, unpublish, move, and reorder;
6. composition stores no canonical fact copies and never mutates KnowledgeRelation;
7. Draft/Archived/deleted/unpublished direct URLs and search are fail-closed as 404/absent;
8. safe type-specific projections and secret/identity/raw technical-field denial tests;
9. page-scoped attachment current-reference authorization and all ATTACH safety headers/limits;
10. published-only bounded lexical search with real server paging;
11. bounded related/trace/trust projections with no N+1 or persisted derived truth;
12. responsive/accessibility/browser tests and zero new console errors;
13. one additive reviewed migration in B01, no automatic publication, and repository database protection;
14. cleanup, final diff review, focused regression, and honest gap reporting.

## 42. Final Decision

```text
Portal reader:
same application + same deployment + anonymous internal + read-only

Admin Knowledge Composition:
authenticated Administrator + Page Tree + Primary Target + ordered Sections
+ Preview / Publish / Unpublish

Truth model:
composition references canonical knowledge; it never copies or invents facts
```

PORTAL-A01 is frozen. PORTAL-B01 may begin only under a separate instruction.

## 43. PORTAL-A01-AMEND-01 — TrustSummary Source Compatibility

**Amendment date:** 2026-09-04

**Reason.** The original compatibility row allowed `Derived + TrustSummary` but did not define a unique bounded recipe. `KnowledgeStatus` belongs to each canonical target or relation, Evidence and HumanConfirmation remain independent facts, and revision confirmation coverage has an exact meaning only for one KnowledgeDocument. No approved contract defines a composite status, weakest/strongest/majority status, cross-target count total, inherited confirmation, or heterogeneous revision-coverage aggregation. Defining one during PORTAL-B04 would create new business semantics and a duplicate aggregate trust truth.

**Previous rule:** `TrustSummary` allowed `PrimaryTarget / ExplicitReference / Derived`, with target or relations to be resolved by an unspecified bounded recipe.

**New frozen rule:** Portal v1 `TrustSummary` represents the safe trust summary of exactly one explicit canonical target. It allows only `PrimaryTarget` and `ExplicitReference`. `Derived + TrustSummary` is an invalid combination.

| SourceKind | TrustSummary target | Portal v1 status |
| --- | --- | --- |
| `PrimaryTarget` | `PortalPage.PrimaryTarget` | Allowed |
| `ExplicitReference` | `PortalPageSection.ReferenceTarget` | Allowed |
| `Derived` | None | Not supported; invalid combination |

The safe projection contains the target type, safe target title, that target's `KnowledgeStatus`, direct Evidence count, and direct HumanConfirmation count. For a KnowledgeDocument it also contains the existing derived current-revision confirmation coverage state. For every other v1 PortalTarget, `confirmationCoverage` is `null`; Portal does not invent an equivalent state.

TrustSummary must not traverse `KnowledgeRelation`, select related targets, aggregate or deduplicate multiple targets, sum trust counts across targets, calculate minimum/maximum/majority status, aggregate relation trust, aggregate trace-node trust, or inherit confirmation. Relationship and trace items may expose their own allowlisted per-item trust signals inside `RelatedKnowledge` or `Traceability`; they are not inputs to TrustSummary.

Admin Knowledge Composition must not offer `Derived + TrustSummary`. Backend authoring validation returns `400 validation_error` for that combination. Portal read and Admin Preview fail closed if corrupt or legacy data contains it; they must not ignore or reinterpret the section.

This amendment narrows only the compatibility matrix. It does not change `PortalPageProjectionKind`, `PortalPageSectionSourceKind`, Portal persistence, canonical trust facts, TRACE semantics, or database schema, and it requires no migration. PORTAL-B04 must implement the two allowed single-target forms through the shared Portal projection/sanitization path.
