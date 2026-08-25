# AGENTS.md — System Knowledge Hub

## 1. Purpose
This repository implements **系统知识中心 / System Knowledge Hub**.

This file contains only mandatory agent workflow and guardrails. Detailed product, domain, API, database, UI, and solution rules belong to the frozen specifications under `docs/specifications/`.

## 2. Source of Truth
Before changing repository content, identify the current task / Vertical Slice and read only the sources relevant to that change.

Frozen sources, when applicable:
1. `docs/specifications/System_Knowledge_Hub_MVP_Final_UI_Inventory.md`
2. `docs/specifications/System_Knowledge_Hub_MVP_Design_Baseline.md`
3. `docs/specifications/System_Knowledge_Hub_MVP_Domain_Model.md`
4. `docs/specifications/System_Knowledge_Hub_MVP_Database_Model.md`
5. `docs/specifications/System_Knowledge_Hub_MVP_Application_Use_Case_Model.md`
6. `docs/specifications/System_Knowledge_Hub_MVP_API_Contract.md`
7. `docs/specifications/System_Knowledge_Hub_MVP_Solution_Structure.md`
8. Current Vertical Slice / task specification

Also read `docs/DOCUMENT_INDEX.md` and the existing implementation documentation for the changed area when present.

Rules:
- Prefer the latest explicitly frozen/confirmed source.
- Never invent requirements to resolve a material conflict.
- Record material conflicts in the task/verification report and stop only the conflicting work.
- Never modify frozen specifications, Golden UI assets, or frozen task definitions merely to make implementation easier.

## 3. Product and Technical Boundaries
Formal product name:
- Chinese: **系统知识中心**
- English: **System Knowledge Hub**

Formal UI text is Simplified Chinese. Technical identifiers and frozen API wire values remain unchanged.

MVP stack:
- Backend: .NET 8, ASP.NET Core Controllers, EF Core, SQLite.
- Frontend: Vue 3, strict TypeScript, Element Plus.
- Architecture: feature-first, direct `DbContext`, explicit use cases, typed frontend API boundaries.

Unless a frozen specification explicitly changes the architecture, do not introduce Generic Repository/UnitOfWork, CQRS/MediatR, Command/Query buses, AutoMapper/Mapster, FluentValidation framework, Dapper, generic CRUD/detail/table/drawer frameworks, BaseController/BaseService hierarchies, event-bus frameworks, Axios, a second data-grid library, placeholder layers, or speculative abstractions.

Do not replace established project libraries/patterns without a current requirement.

## 4. Mandatory Task Lifecycle
For every repository-changing task:

1. Identify the current task / Vertical Slice.
2. Read relevant frozen sources, documentation index, and adjacent implementation/documentation.
3. Determine and implement the smallest coherent change.
4. Synchronize the corresponding non-frozen documentation.
5. Update `docs/DOCUMENT_INDEX.md` when documentation inventory/location/purpose/status changes.
6. Run only verification applicable to the change.
7. **Immediately after each verification cycle, stop every verification-only process/thread started by the agent and release its verification ports when practical.**
8. If verification fails, fix the issue, resynchronize affected documentation, and repeat verification/cleanup.
9. When verification passes, update the required verification report.
10. Review `git status` / relevant `git diff`, then stage only current-task files.
11. Create one task-specific local commit.
12. Attempt to push the current branch to the configured GitHub remote.
13. Report verification, cleanup, documentation, branch, commit SHA, and push result.
14. Stop. Do not begin the next slice automatically.

## 5. Scope and Implementation Discipline
Use the **smallest implementation that satisfies the frozen requirement and current Vertical Slice**.

- YAGNI: do not build for hypothetical future needs.
- Do not implement adjacent features/workflows unless requested.
- Do not refactor unrelated working code for style or architectural purity.
- Reuse existing project conventions before creating a new pattern.
- Prefer concrete local code until real reuse is proven.
- Do not create boilerplate merely to look “enterprise”.
- Do not rename frozen business concepts.
- Do not silently broaden scope.
- Do not derive schema, routes, workflows, or UI behavior from screenshots when a frozen source exists.

Application-specific behavior must follow the relevant frozen specification rather than duplicated summaries in this file.

## 6. Documentation Synchronization — Mandatory
Repository changes and maintainable documentation must stay synchronized in the **same task and same commit**.

When code, configuration, schema/migrations, API behavior, UI behavior/layout, workflow, tests, development behavior, or repository structure changes:
- update the closest existing non-frozen document that owns the subject;
- describe the final verified state, not an intermediate attempt;
- keep documentation changes focused;
- do not create duplicate documents when an existing document already owns the topic.

If the corresponding source is frozen, do not edit it merely to match implementation. Update/create the appropriate non-frozen implementation/task/verification document that references the frozen source. If the requested change truly conflicts with a frozen requirement, report the conflict.

### Documentation index
Maintain:

`docs/DOCUMENT_INDEX.md`

If it does not exist when documentation work is required, create it.

For each maintained project document, record at least:
- **Path** — exact repository-relative path
- **Role / Purpose** — what the file defines, records, verifies, or guides
- **Status / Authority** — Frozen Source, Task Spec, Implementation Note, Verification Report, Guide, etc.
- **Related Area** — feature, Vertical Slice, architecture/workflow concern
- **Update Trigger** — what kind of change requires review/update

Update the index in the same task when a document is created, renamed, moved, deleted, superseded, or changes purpose/status. The index is navigation metadata; do not duplicate full document contents inside it.

## 7. Verification — Minimal and Risk-Based
Verification proves the **current change works**; it is not a coverage-maximization exercise.

Default checks when relevant:

Backend:
```bash
dotnet build
```
Run focused `dotnet test` only when affected tests exist or meaningful business/persistence behavior justifies it. Prefer a small number of high-value tests and real SQLite for relational behavior.

Frontend:
```bash
npm run type-check
npm run build
```
Run lint/tests only when relevant. Simple UI/copy/layout/style/type-only changes may require zero new automated tests.

For an end-to-end UI/API slice, perform one focused runtime check of the changed path when practical. Do not exhaustively test unrelated screens.

### Verification cleanup — mandatory after every cycle
Stop any process/thread started only for verification immediately when that verification cycle ends, including `dotnet run`, ASP.NET Core dev servers, `npm run dev`, Vite, test/watch processes, temporary servers, and background verification scripts.

- Prefer one-shot, non-watch commands.
- Track agent-started verification processes and stop only those processes.
- Do not kill pre-existing/user-started development processes.
- Confirm agent-used verification ports are released when practical.
- Never leave API/Web development ports occupied by an agent-started verification process.
- If cleanup cannot be completed safely, report it explicitly.

A verification cycle is not finished while its verification-only processes are still running.

## 8. Verification Reports
Verification reports describe implementation/verification results, applicable checks, deviations, and limitations.

Never report `PASS` when an applicable required implementation/verification check failed or was skipped.

GitHub delivery status is separate:
- GitHub push failure does **not** change an otherwise valid verification `PASS`.
- Do not mark the report incomplete/failed solely because authentication, permission, branch protection, remote configuration, or network conditions prevented push.
- Report push failures to the user in the final task status instead.

## 9. Git and GitHub — Mandatory
After successful verification and cleanup:
- review `git status --short` and relevant diffs;
- stage only files belonging to the current task;
- keep implementation and corresponding documentation in the same task-specific commit;
- use a concise commit message;
- attempt to push the current branch to the configured GitHub remote;
- report branch name, commit SHA, and push result.

Git safety:
- Do not blindly use `git add .` when unrelated changes may exist.
- Do not discard/reset/stash/rewrite unrelated user changes.
- Do not use `git reset --hard`, destructive checkout/restore, force push, history rewrite, rebase, or amend unless explicitly requested.
- Do not change branches merely to make pushing easier unless the task requires it.
- Never commit secrets, credentials, tokens, private environment data, generated runtime databases, or temporary verification artifacts.
- Do not create a success commit when required verification failed.

If push fails, keep the verified local commit when safe and report the exact blocker, branch, and local commit SHA. **Do not rewrite an otherwise PASS verification report as failed/incomplete solely because push failed.**

## 10. Completion Standard
Repository-changing work is complete only when all applicable items are satisfied:
- frozen requirements/contracts respected;
- intended change complete;
- corresponding non-frozen documentation synchronized;
- `docs/DOCUMENT_INDEX.md` updated when required;
- risk-based verification passed;
- every verification cycle cleaned up agent-started verification processes/threads;
- verification ports released when practical;
- required verification report updated;
- final diff reviewed for task scope;
- task-specific local commit created with implementation + documentation;
- GitHub push attempted;
- branch, commit SHA, and push result reported.

A GitHub push failure is a separately reported delivery issue and does not invalidate an otherwise successful implementation/verification result.

After completing the requested task, **stop**. Do not start the next Vertical Slice or unrelated cleanup automatically.
