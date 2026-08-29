# UI-UX-R03-FIX-R01 Button + Simplified Chinese UI Consistency Verification Report

## Result

```text
UI-UX-R03-FIX-R01 PASS
```

Page-level primary actions and Evidence/HumanConfirmation actions now share an explicit computed-style contract. The feature selectors that visibly recolored the Unknown Item and Database Object button labels were corrected, the main user-facing mixed-language labels were translated to Simplified Chinese, and the HumanConfirmation Drawer no longer reinitializes authentication or remounts the current detail page when opened.

No backend API, wire enum, persisted value, Drawer close policy, authorization rule, Evidence/HumanConfirmation contract, Attachment domain, or Database Discovery behavior changed.

## Primary Action Fix

- `skh-page-primary-action` now owns the exact 36 px height, 7 px radius, 16 px horizontal padding, 13 px font size, 650 weight, 16 px icon, 6 px icon/label gap, primary colors, and disabled state.
- Primary action label and icon colors explicitly inherit the semantic button color. This prevents a feature-level descendant selector from silently recoloring Element Plus child nodes.
- The Unknown Item rule was narrowed from `.unknown-list-header span` to the descriptive text under the header copy container. It no longer matches the `新增待确认事项` button label.
- User Management, Unknown Items, Knowledge Content, Systems, Business Functions, and Database Objects use the same shared page action class.

Real-browser computed values for `新增待确认事项` and `新增知识内容` matched:

```text
height          36 px
border-radius    7 px
padding          0 16 px
font-size       13 px
font-weight     650
background      rgb(79, 70, 229)
label / icon    rgb(255, 255, 255)
```

## Evidence / HumanConfirmation Fix

- `skh-section-action` owns the exact 32 px compact action dimensions, 7 px radius, 12 px horizontal padding, 12 px font size, 620 weight, 16 px icon, and 6 px gap.
- `skh-evidence-action` remains the solid primary action; `skh-human-confirmation-action` remains the outline secondary action.
- The Database Object heading selector was narrowed from all descendant `span` elements to the evidence-count node only. It no longer recolors the button labels.
- Database Object and KnowledgeDocument keep Evidence before HumanConfirmation in the right-aligned action group. BusinessFunction and the shared KnowledgeStatus progression surface use the same semantic classes where applicable.
- Database Column, BusinessRule, Integration, Evidence detail, and the other audited evidence surfaces were aligned to the same shared class contract.

Real-browser Database Object values:

| Action | Height | Background | Border | Label / icon |
| --- | ---: | --- | --- | --- |
| 添加证据 | 32 px | `rgb(79, 70, 229)` | `rgb(79, 70, 229)` | white |
| 添加人工确认 | 32 px | white | `rgb(140, 134, 234)` | `rgb(79, 70, 229)` |

### HumanConfirmation open-only regression

The follow-up browser report exposed a separate remount bug:

```text
open HumanConfirmation Drawer
→ AddHumanConfirmationDrawer called actorStore.initialize()
→ authStatus changed to loading
→ App.vue unmounted AppShell and RouterView
→ Current User/token and all current detail requests ran again
```

The frontend Actor store now treats completed initialization as idempotent, and `AddHumanConfirmationDrawer` no longer performs a redundant initialization on mount. Opening the Drawer uses the already bootstrapped actor and performs no API request.

Isolated browser evidence:

- Database Object `/database/45`: Drawer opened, URL/detail remained stable, API log length stayed `236475` before and after the click.
- KnowledgeDocument `/knowledge-documents/1`: Drawer opened, title and URL remained stable, API log length stayed `263732` before and after the click.
- Both surfaces displayed `保存人工确认` immediately; no current-user, token, detail, relationship, evidence, system-list, database-object-list, or attachment-content reload occurred.

This is a frontend initialization lifecycle correction only. Authentication modes, login flows, refresh APIs, authorization decisions, and server contracts are unchanged.

## Focus-visible

- Evidence focus retained white label/icon text on `rgb(67, 56, 202)` with a visible primary outline.
- HumanConfirmation focus retained dark primary label/icon text on `rgb(238, 242, 255)` with a visible primary border and outline.
- Disabled primary and outline variants remain visually distinct and readable.
- Keyboard focus does not rely on color alone.

## Simplified Chinese UI Audit

The audit translated user-facing labels, headings, helpers, and presentation values while preserving technical identifiers and wire values.

Key corrections include:

- User Management: removed decorative `ADMIN · USER PROFILE`; translated Knowledge Role and LoginIdentity presentation to `知识身份` and `登录身份`.
- Attachment Administration: removed `ADMIN · ATTACHMENT GOVERNANCE`; translated kind, metadata, storage state, preview mode, reference state, and owner labels; presentation values now use `图片 / 文件`, `可用`, and `等待删除重试`.
- Unknown Items: translated remaining evidence/status presentation labels and removed mixed technical display copy.
- System unified view: maps Integration, relationship, evidence, document, lifecycle, priority, and Unknown Item wire values through Chinese presentation labels.
- Database, BusinessFunction, BusinessRule, Integration, Evidence, Revision Compare, Foundation, Security Gate, and sidebar user-facing labels were normalized where the static audit found mixed presentation text.

API enum values, DTO fields, TypeScript identifiers, C# identifiers, and database values were not translated.

## Allowed Technical Keywords

The following remain where technically meaningful and are wrapped by Chinese UI labels where applicable:

- Oracle, PostgreSQL, SQL Server;
- OIDC / SSO, Subject / `sub`, Provider;
- URL, HTTP, SQL, JSON, XML, Markdown;
- MIME, SHA-256, file extensions;
- API IDs and revision/attachment numbers;
- Exchange, Topic, and Queue as parenthetical protocol terms.

## Files Changed

### Shared action/session boundaries

- `src/SystemKnowledgeHub.Web/src/styles/ui-foundation.css`
- `src/SystemKnowledgeHub.Web/src/styles/uiConsistency.spec.ts`
- `src/SystemKnowledgeHub.Web/src/app/stores/actor.ts`
- `src/SystemKnowledgeHub.Web/src/app/stores/actor.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/unknown-items/unknown-items.css`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/database-knowledge.css`
- `src/SystemKnowledgeHub.Web/src/features/evidence/components/AddHumanConfirmationDrawer.vue`
- `src/SystemKnowledgeHub.Web/src/features/evidence/components/AddHumanConfirmationDrawer.spec.ts`

### Simplified Chinese presentation audit

- User Management and Security Gate presentation components;
- Attachment Administration list/detail presentation mapper and tests;
- System unified knowledge view and regression tests;
- Unknown Item detail and application sidebar;
- BusinessFunction, BusinessRule, Database, Evidence, Integration, KnowledgeDocument Revision Compare, and Foundation presentation components.

### Documentation

- this report;
- `docs/DOCUMENT_INDEX.md`.

## Focused Tests

| Check | Result |
| --- | --- |
| Actor initialization idempotence | PASS |
| HumanConfirmation Drawer opens without actor reinitialization | PASS |
| Shared computed-style and feature-selector regression | PASS |
| DatabaseObject / KnowledgeDocument / Evidence regressions | PASS |
| Attachment Administration presentation regressions | PASS |
| System unified and Unknown Item Chinese presentation regressions | PASS |
| Focused Vitest aggregate | PASS — 11 files, 54 tests |
| `npm run type-check` | PASS |
| `npm run lint` | PASS — 0 issues |
| `npm run build` | PASS |

The production build retains the existing non-failing Vite chunk-size advisory.

## Browser Verification

Runtime isolation:

- task-owned SQLite database;
- task-owned Data Protection keys;
- task-owned Attachment StorageRoot;
- isolated API/Vite ports `18571` / `18572`;
- task-local Administrator and task-local KnowledgeDocument.

### 1440 × 900

- Exact inner viewport measured `1440 × 900`; document scroll width remained 1440 px.
- KnowledgeDocument Evidence/HumanConfirmation hierarchy, right alignment, dimensions, colors, and focus-visible states passed.
- Database Object Evidence/HumanConfirmation computed styles matched the shared contract.
- KnowledgeDocument HumanConfirmation opened without page remount or API requests.

### 1280 × 720

- Exact inner viewport measured `1280 × 720`; document scroll width remained 1280 px.
- `新增待确认事项` remained visible and matched the Knowledge Content primary action computed style.
- Database Object Evidence/HumanConfirmation remained visible, ordered, and right aligned without horizontal overflow.
- Label and icon colors remained readable in both primary and outline actions.

### User-facing language

User Management, Attachment Administration, Unknown Items, Database Object, KnowledgeDocument, and the audited shared detail surfaces use Simplified Chinese labels except for approved technical keywords.

Final browser console:

```text
new errors   = 0
new warnings = 0
```

## SQLite / Runtime Safety

- Repository SQLite metadata matched the recorded baseline exactly: `950272` bytes and `2026-08-29T11:50:30.0126773Z`.
- A pre-existing user process kept the repository database open, so this task did not stop it or force an exclusive hash read.
- Repository Attachment StorageRoot remained at 2 files with unchanged last-write metadata (`2026-08-29T03:13:46.1231809Z`).
- Task Attachment StorageRoot contained no uploaded files; staging residue was `0`.
- Verification processes were stopped, ports `18571` / `18572` were released, the browser viewport override and tab were cleared, and the entire task-owned runtime tree was removed.

## Existing / New Gaps

- Existing: the non-failing Vite chunk-size advisory remains outside this corrective UI slice.
- Existing: UI-UX-R02's documented HumanConfirmation contract decision remains unchanged.
- New Blocker / High gaps: none.
- New in-scope gaps: none.

## Final Result

All applicable PASS conditions and the follow-up HumanConfirmation open-only regression are satisfied:

```text
UI-UX-R03-FIX-R01 PASS
```

No Drawer close-policy change, DB Discovery, or follow-on feature work was started.
