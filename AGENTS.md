# AGENTS.md --- System Knowledge Hub

## 1. Purpose

This repository implements **系统知识中心 / System Knowledge Hub**.

This file contains repository-wide mandatory agent workflow and
guardrails. Product, domain, API, database, UI, security, and solution
details belong to the applicable frozen specifications and task
documents under `docs/`.

## 2. Source of Truth

Before changing repository content:

1.  Identify the current task / Vertical Slice.
2.  Read only the frozen specifications and implementation documents
    relevant to that task.
3.  Read `docs/DOCUMENT_INDEX.md` when documentation is involved.
4.  Prefer the latest explicitly frozen/confirmed source.
5.  Never invent requirements to resolve a material conflict.
6.  Never modify frozen specifications, Golden UI assets, or frozen task
    definitions merely to make implementation easier.
7.  If a requested change materially conflicts with a frozen source,
    stop only the conflicting work and record the conflict.

Core frozen sources, when applicable:

-   `docs/specifications/System_Knowledge_Hub_MVP_Final_UI_Inventory.md`
-   `docs/specifications/System_Knowledge_Hub_MVP_Design_Baseline.md`
-   `docs/specifications/System_Knowledge_Hub_MVP_Domain_Model.md`
-   `docs/specifications/System_Knowledge_Hub_MVP_Database_Model.md`
-   `docs/specifications/System_Knowledge_Hub_MVP_Application_Use_Case_Model.md`
-   `docs/specifications/System_Knowledge_Hub_MVP_API_Contract.md`
-   `docs/specifications/System_Knowledge_Hub_MVP_Solution_Structure.md`
-   Current task / Vertical Slice specification

## 3. Product and Architecture Boundaries

Formal product name:

-   Chinese: **系统知识中心**
-   English: **System Knowledge Hub**

Formal UI text is Simplified Chinese. Technical identifiers and frozen
API wire values remain unchanged.

MVP stack:

-   Backend: .NET 8, ASP.NET Core Controllers, EF Core, SQLite
-   Frontend: Vue 3, strict TypeScript, Element Plus
-   Architecture: feature-first, direct `DbContext`, explicit use cases,
    typed frontend API boundaries

Unless a frozen specification explicitly requires it, do not introduce
or replace established patterns with:

-   Generic Repository / UnitOfWork
-   CQRS / MediatR / Command or Query buses
-   AutoMapper / Mapster
-   FluentValidation framework
-   Dapper
-   generic CRUD/detail/table/drawer frameworks
-   BaseController / BaseService hierarchies
-   event-bus frameworks
-   Axios
-   a second data-grid library
-   placeholder layers or speculative abstractions

Reuse established project conventions. Do not replace project libraries
or architecture without a current requirement.

## 4. Scope Discipline

Implement the **smallest coherent change** that satisfies the current
requirement.

-   Follow YAGNI.
-   Do not implement adjacent features unless requested.
-   Do not silently broaden scope.
-   Do not refactor unrelated working code for style or architectural
    purity.
-   Prefer concrete local code until real reuse is proven.
-   Do not create boilerplate merely to look "enterprise".
-   Do not rename frozen business concepts.
-   Do not derive schema, routes, workflows, or UI behavior from
    screenshots when a frozen source exists.
-   Do not perform unrelated package upgrades, schema changes, cleanup,
    or infrastructure changes.
-   After completing the requested task, stop. Do not automatically
    begin the next task.

## 5. Security, Secrets, and Runtime Safety

These rules apply to every task:

-   Preserve existing fail-closed security boundaries unless an approved
    specification explicitly changes them.
-   Never weaken authentication/authorization merely to make a test or
    startup succeed.
-   Never commit passwords, tokens, client secrets, credentials, private
    environment values, Data Protection key material, or other secrets.
-   Use placeholders in documentation/examples for secret values.
-   Do not silently change Production into Development or enable unsafe
    Production defaults.
-   Known configuration failures may be handled with actionable
    diagnostics, but unexpected runtime failures must not be silently
    swallowed.

## 6. Database and Persistent Data Safety

Repository-owned or user-owned runtime data must be treated as protected
state.

Unless the current task explicitly requires a real data
migration/change:

-   Do not use the repository's real SQLite database for tests or
    verification.
-   Do not delete, rebuild, seed, migrate, checkpoint, or otherwise
    mutate the real database as a side effect of verification.
-   Do not delete or alter user-owned `*.db-wal` or `*.db-shm` files.
-   Use task-owned temporary databases for tests/runtime verification.
-   Use task-owned temporary Data Protection directories and other
    runtime state when practical.
-   Never commit generated runtime databases, WAL/SHM files, Data
    Protection keys, logs, or temporary verification artifacts.

For tasks that can affect persistence, migrations, startup data
handling, or database paths:

1.  Record the relevant database/WAL/SHM baseline before verification
    when practical.
2.  Verify the repository/user database remained unchanged unless the
    task explicitly required a change.
3.  Report any unexpected persistent-data modification immediately.

## 7. Documentation Synchronization

Code/configuration and maintainable non-frozen documentation must remain
synchronized in the same task when the change affects documented
behavior.

-   Update the closest existing non-frozen document that owns the
    subject.
-   Describe the final verified state, not intermediate attempts.
-   Keep documentation changes focused.
-   Do not create duplicate documents when an existing document owns the
    topic.
-   Do not edit frozen sources merely to match implementation.
-   Update `docs/DOCUMENT_INDEX.md` when a document is created, renamed,
    moved, deleted, superseded, or changes purpose/status.

The document index is navigation metadata. Do not duplicate full
document contents inside it.

## 8. Verification --- Minimal and Risk-Based

Verification must be proportional to the current change. Do not run
broad checks merely for ceremony.

Typical checks when relevant:

Backend:

``` bash
dotnet build
```

Run focused `dotnet test` when affected tests exist or meaningful
business/persistence behavior requires it.

Frontend:

``` bash
npm run type-check
npm run build
```

Run lint/tests only when relevant. Simple UI/copy/layout/style/type-only
changes do not automatically require new tests.

For an end-to-end UI/API slice, perform one focused runtime check of the
changed path when practical.

If the repository documents an approved workaround/gate for a known test
infrastructure issue, use that approved gate rather than inventing a new
workaround.

### Verification cleanup

After every verification cycle:

-   Stop only verification processes/threads started by the agent.
-   Release agent-used verification ports when practical.
-   Prefer one-shot, non-watch commands.
-   Do not kill pre-existing/user-started development processes.
-   Do not leave `dotnet run`, Vite, test/watch processes, temporary
    servers, or verification scripts running.
-   If cleanup cannot be completed safely, report it.

## 9. Verification Reports and Gaps

When the current task requires a verification report:

-   Record the actual implementation and verification result.
-   Never report `PASS` when an applicable required check failed or was
    skipped.
-   Clearly distinguish implementation/test status from delivery/push
    status.
-   Record limitations honestly.
-   Do not claim a local smoke test proves a real Production deployment.
-   Reuse existing Gap IDs for known issues; do not create duplicate
    gaps.
-   New out-of-scope issues should be recorded through the repository's
    existing Gap mechanism rather than fixed opportunistically.

## 10. Git and GitHub Safety

Before delivery, review `git status --short` and relevant diffs.

-   Stage only current-task files.
-   Do not blindly use `git add .` when unrelated changes may exist.
-   Do not discard, reset, stash, rewrite, or overwrite unrelated user
    changes.
-   Do not use `git reset --hard`, destructive checkout/restore, force
    push, history rewrite, rebase, or amend unless explicitly requested.
-   Do not change branches merely to make delivery easier.
-   Do not create a success commit when required verification failed.

For repository-changing implementation tasks, unless the current task
explicitly says otherwise:

1.  Keep implementation and corresponding documentation in one
    task-specific local commit.
2.  Use a concise commit message.
3.  Attempt to push the current branch to the configured GitHub remote.
4.  Report branch, commit SHA, and push result.

A push failure is a delivery issue and does not invalidate an otherwise
valid implementation/verification `PASS`.

## 11. Standard Task Lifecycle

For a repository-changing task:

1.  Identify task scope.
2.  Read applicable sources.
3.  Inspect the current implementation before editing.
4.  Implement the smallest coherent change.
5.  Synchronize affected non-frozen documentation.
6.  Run applicable risk-based verification.
7.  Clean up verification-only processes/resources.
8.  Fix and reverify if necessary.
9.  Update the required verification report/index when applicable.
10. Review final diff and persistent-data safety.
11. Commit/push according to Section 10 unless the task says otherwise.
12. Report result, verification, cleanup, documentation, gaps,
    branch/commit/push status.
13. Stop.

## 12. Completion Standard

A task is complete only when all **applicable** items are satisfied:

-   frozen requirements/contracts respected;
-   requested change completed without scope creep;
-   security and persistent-data safety preserved;
-   corresponding non-frozen documentation synchronized;
-   applicable verification passed;
-   verification-only processes/resources cleaned up;
-   required verification report/index updated;
-   final diff reviewed;
-   no undisclosed new gap or limitation remains;
-   Git delivery performed when required by Section 10.

Do not turn non-applicable checklist items into mandatory work. The
current task should remain focused on the requested change.
