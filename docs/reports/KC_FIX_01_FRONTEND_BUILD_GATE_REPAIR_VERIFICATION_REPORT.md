# KC-FIX-01 — Frontend Build Gate Repair Verification Report

## Result

```text
KC-FIX-01 PASS
```

## Original Failure

`npm run type-check` and `npm run build` both failed with:

```text
TS2349: This expression is not callable.
Type 'never' has no call signatures.
```

The failure was at `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.spec.ts:174`, in the deferred-save test.

## Root Cause

The test declared a local resolver variable with an initial `null` value, then assigned it only inside a Promise executor callback. TypeScript does not assume that callback assignment has happened at the later call site, so control-flow narrowing retained the initial null state. The optional call consequently narrowed its callable branch to `never`.

## Fix

The test now owns an explicit deferred-save state object:

```ts
const deferredSave: { complete: ((value: KnowledgeDocumentDetail) => void) | null } = { complete: null }
```

The Promise executor records its resolver in `deferredSave.complete`, and the existing test invokes that same resolver after asserting `正在保存…`. This preserves the asynchronous pending-save, success, and assertion semantics without `any`, suppressions, casts, non-null assertions, tsconfig changes, package-script changes, or production-code changes.

## Scope

Only the existing `KnowledgeDocumentDetailView.spec.ts` test typing was changed. The KnowledgeDocument production component, API, editor, Markdown behavior, lifecycle, KnowledgeStatus, Evidence, HumanConfirmation, relationships, search, unified view, authentication, authorization, database, and migrations were not modified.

The repository was already substantially dirty before this repair. The test file itself is pre-existing untracked KC work; it remains untracked. No pre-existing change was reset, reverted, formatted, or overwritten.

## Verification

| Command | Result |
| --- | --- |
| `npm run type-check` | PASS |
| `npm run test -- --run src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.spec.ts` | PASS — 1 file, 6 tests |
| Editor/detail/dirty/round-trip/renderer focused Vitest selection | PASS — 5 files, 12 tests |
| `npx eslint --quiet src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.spec.ts` | PASS |
| `npm run build` | PASS — Vite emitted only its existing chunk-size advisory |
| `dotnet build SystemKnowledgeHub.sln --no-restore` | PASS — 0 warnings, 0 errors |
| `git diff --check` | PASS — no whitespace errors; existing CRLF/global-ignore advisories remain |

## Diff Review

The KC-FIX-01 production diff is empty. The only code change is the explicit test-only deferred resolver state. No TypeScript compiler option, build script, lint suppression, `as any`, `@ts-ignore`, or `@ts-expect-error` was introduced.

## Stop Point

KC-GAP-001 is repaired and its build gate is restored. KC-GAP-002 and KC-GAP-003 remain out of scope and were not started.
