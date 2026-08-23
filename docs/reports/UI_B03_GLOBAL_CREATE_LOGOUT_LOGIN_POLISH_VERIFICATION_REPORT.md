# UI-B03 — Global Create / Logout / Login Experience Polish Verification Report

## Result

UI-B03 PASS

## Scope

Only frontend UI/UX integration, styles, focused tests, and this report were changed for UI-B03.

- `src/SystemKnowledgeHub.Web/src/features/systems/components/CreateKnowledgeObjectChooser.vue`
- `src/SystemKnowledgeHub.Web/src/layouts/AppShell.vue`
- `src/SystemKnowledgeHub.Web/src/layouts/AppTopBar.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/editor/documentEditState.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.vue`
- `src/SystemKnowledgeHub.Web/src/app/security/SecurityGate.vue`
- `src/SystemKnowledgeHub.Web/src/styles/app.css`
- Focused Vue/Vitest specifications for the chooser, top bar, SecurityGate, and existing document edit state.

## Global Create

- The global `新增` chooser now presents `知识内容` immediately after `集成关系`, with the agreed description.
- Selecting it closes the object chooser and opens the existing `CreateKnowledgeDocumentDialog`; the seven document types, Markdown templates, validation, and create API remain owned by the KC-B02 flow and were not copied.
- The global create control remains hidden for a Viewer through the existing `actorStore.canEdit` gate. The chooser also only enables kinds explicitly supplied by its caller.
- Browser verification confirmed: local Administrator login → dashboard → `新增` → `知识内容` → existing `新建知识内容` dialog.

## Logout Confirmation and Dirty Edit Protection

- Selecting `退出登录` first opens the Element Plus confirmation dialog (`退出登录？` / `退出后需要重新登录才能继续访问系统。`).
- Cancelling does not call the logout endpoint and preserves the active session and Current User panel.
- Confirming calls the existing frontend logout API before `actorStore.clearCurrentUser`, preserving the AUTH-B02 cookie-clearing path and Login Gate reload flow.
- When an active KnowledgeDocument editor is dirty, logout confirmation is followed by the existing discard-confirmation abstraction. Cancelling that second confirmation stops logout, so edits are not silently discarded.

## Login Gate Redesign

- The unauthenticated gate now uses a centered, token-based two-column desktop layout: lightweight System Knowledge Hub brand context on the left and a 420–460px login card on the right.
- The implementation uses the existing purple design token, Element Plus controls, a subtle radial background, and responsive CSS that collapses to one column at `900px` and below.
- Local-only retains only account/password/login. Both mode retains the local form, divider, and enterprise button. OIDC-only retains the enterprise action without empty fields. Existing loading, configuration failure, no-provider, and login error semantics remain intact.
- In the browser-controlled 1280×720 desktop surface (narrower than the required 1366px desktop target), the two-column grid measured `544.8px 460px` with no horizontal overflow. The same desktop rule therefore has additional horizontal space at 1366px, 1714px, and 1920px.

## Focused Verification

Executed from `src/SystemKnowledgeHub.Web`:

```text
npm run type-check
npm run test -- --run src/layouts/AppTopBar.spec.ts src/features/systems/components/CreateKnowledgeObjectChooser.spec.ts src/app/security/SecurityGate.spec.ts src/features/knowledge-documents/editor/documentEditState.spec.ts src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.spec.ts
npx eslint --no-warn-ignored src/layouts/AppTopBar.vue src/layouts/AppTopBar.spec.ts src/layouts/AppShell.vue src/features/systems/components/CreateKnowledgeObjectChooser.vue src/features/systems/components/CreateKnowledgeObjectChooser.spec.ts src/features/knowledge-documents/editor/documentEditState.ts src/features/knowledge-documents/editor/documentEditState.spec.ts src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.vue src/app/security/SecurityGate.vue
npm run build
```

Results:

- Type check: PASS.
- Focused tests: PASS — 5 files, 19 tests.
- Scoped ESLint for the UI-B03 TypeScript/Vue files: PASS with no errors.
- Frontend production build: PASS. Vite reported its pre-existing large-chunk advisory only; it is not a UI-B03 failure.

Executed from the repository root:

```text
dotnet build SystemKnowledgeHub.sln --no-restore
```

Result: PASS — 0 warnings, 0 errors.

## Browser Verification

An isolated temporary SQLite database and local Administrator were created only for this verification, then removed.

Verified in a real browser:

```text
Local login
→ dashboard
→ global create shows 知识内容
→ existing KnowledgeDocument create dialog
→ logout confirmation
→ cancel preserves session
→ logout confirmation
→ confirm returns to Login Gate
```

The desktop login visual was captured and inspected at 1280×720; no horizontal overflow was present and the card, fields, and primary button aligned correctly.

## Security and Knowledge-State Boundaries

- Password handling, antiforgery, ApplicationCookie behavior, authentication options, Current User resolution, AccessLevel, and KnowledgeRole were not changed.
- No backend authentication/authorization code, API route/DTO, database, migration, or persistence code was changed by UI-B03.
- No KnowledgeStatus behavior was changed. `Published + Unknown` remains a valid KnowledgeDocument state.
- KC-B04 relationship behavior was not changed.

## Temporary Process Cleanup

- UI-B03 temporary ASP.NET Core process PID `28072` and its children were stopped.
- UI-B03 temporary Vite launcher PID `34880` and its children were stopped.
- The temporary runtime directory, SQLite database, Data Protection keys, and logs under `artifacts/ui-b03-runtime-20260822` were deleted.
- An existing Vite listener on port `5173` (PID `20384`, started at `2026-08-22 21:25:43`, before UI-B03 verification) remained. Its command line identifies a repository Vite process, but its ownership by this task cannot be proven; it was intentionally not stopped and is not a UI-B03 cleanup failure.
- Port `5099` was released. The remaining `5173` listener is the unrelated existing process above, not a process created by UI-B03.

## Dirty Worktree and Diff Verification

The repository was already dirty before UI-B03, including documentation reorganization, AUTH-B02 work, KnowledgeDocument/KC-B01–B04 work, relationship changes, migrations, and untracked verification reports. Those changes were preserved.

UI-B03 did not change production backend behavior, authentication/authorization rules, API contract shape, database schema, migrations, KnowledgeStatus, document lifecycle, relationship behavior, Evidence/HumanConfirmation, or frontend routes.
