# KC-B02 — KnowledgeDocument Read/List UX Verification Report

## Result

KC-B02 PASS

## Scope Delivered

- Added the Viewer-readable `/knowledge-documents` list and `/knowledge-documents/:id` detail routes, plus the primary sidebar entry **知识内容**.
- Added typed KnowledgeDocument API adapters, query filters with URL state, pagination, list empty/error states, and title-to-detail navigation.
- Added the Editor/Administrator-only draft creation dialog and the seven controlled Markdown templates in `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/documentTemplates.ts`.
- Added a read-only Markdown renderer using `markdown-it` with raw HTML disabled, protocol validation retained, and external links marked `noopener noreferrer`.
- Added the minimal deferred lifecycle API: `PUT /api/knowledge-documents/{id}/lifecycle`. It uses the authenticated current user, opaque `concurrencyToken`, and supports only Draft → Published, Published → Draft/Archived, and Archived → Draft. Lifecycle changes do not change `KnowledgeStatus`.
- Added lifecycle confirmations and Editor-only controls in document detail. No editor, content update UI, attachment, Evidence, relationship, status-progression, or category/tag feature was added.

## Verification

| Check                                                                                                                                                    | Result                                                                                                                                                                                                                                                  |
| -------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `dotnet build SystemKnowledgeHub.sln --no-restore`                                                                                                       | Passed: 0 warnings, 0 errors.                                                                                                                                                                                                                           |
| `dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj --no-restore --filter FullyQualifiedName~KnowledgeDocumentsApiTests` | Passed: 4 passed, 0 failed, 0 skipped. Includes Viewer read/write boundary, authenticated author attribution, lifecycle graph, stale concurrency conflict, and KnowledgeStatus preservation.                                                            |
| `npm run type-check`                                                                                                                                     | Passed.                                                                                                                                                                                                                                                 |
| `npm run test -- --run src/features/knowledge-documents/markdown/renderMarkdown.spec.ts`                                                                 | Passed: 2 tests. Confirms raw HTML is inert, `javascript:` links are rejected, and external links receive safe rel attributes.                                                                                                                          |
| `npx eslint src/features/knowledge-documents src/app/router/navigation.ts src/app/router/routes.ts`                                                      | Passed. Full-project lint remains blocked by pre-existing errors in `features/integrations/components/CreateIntegrationDialog.vue` and `features/unknown-items/api/unknownItemContracts.ts`; neither was changed by KC-B02.                             |
| `npm run build`                                                                                                                                          | Passed. Vite emitted its existing large-chunk advisory only; no build failure.                                                                                                                                                                          |
| Browser runtime                                                                                                                                          | Passed in an isolated temporary SQLite/local-login environment: local Administrator login, sidebar entry, empty state, template-backed creation, Markdown detail rendering, publish confirmation, published status, and returned list row all verified. |

## Runtime Cleanup

- Closed the verification browser tab.
- Stopped the exact API and Vite process trees started for this task.
- Confirmed both temporary endpoints returned `000` after cleanup.
- Removed the isolated temporary runtime directory, database, and Data Protection keys.

## Scope and Safety Verification

- No production changes were made outside KnowledgeDocument lifecycle support, KnowledgeDocument frontend read/list/detail/create UX, routing/navigation, the renderer dependency, and focused tests.
- No schema or migration was created. No Authentication, Authorization policy, Current User resolution, KnowledgeStatus progression, Evidence, HumanConfirmation, relationship, API route shape outside the allowed lifecycle endpoint, or content-editor behavior was changed.
- Existing DOC-STRUCTURE-B01 and AUTH-B02 dirty worktree changes were preserved. Git inspection requires `-c safe.directory` in this environment because the workspace owner differs from the executing identity; the repository-local diff and whitespace check completed successfully.

## Deferred

- KC-B03 remains deferred: Markdown editing, preview mode, content updates, title/summary editing, attachments, Evidence/relationship panels, KnowledgeStatus actions, categories, tags, version history, and import/export.
