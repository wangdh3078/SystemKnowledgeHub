# Security & Access Control Requirement

## Status

This document records a future business requirement only. It neither approves an implementation design nor authorizes security-related implementation in the current project stage.

## Required future access outcome

- The system must require Authentication for enterprise use. An unauthenticated visitor is denied access; it must not be treated as an Anonymous Viewer.
- An authenticated user receives Viewer access by default unless a future approved access-control design grants a higher access level.
- Future Viewer access is read-only access to knowledge, evidence, SOP, and troubleshooting information.
- Future Editor access includes Viewer access plus approved knowledge creation and editing, evidence recording, HumanConfirmation recording, and other approved maintenance operations.
- Future Administrator access includes Editor access plus User Management, KnowledgeRole management, and other future approved system-administration operations.

## User Management protection

When access control is designed and implemented, User Management must be protected in both places:

- the frontend navigation and routes; and
- the backend User and KnowledgeRole management APIs.

Hiding a navigation item alone is not sufficient protection.

## KnowledgeRole boundary

KnowledgeRole represents a knowledge identity for business attribution. It is not an access role, permission, claim, or authorization grant. Existing KnowledgeRole data must not be reused as the future access-control model.

## Current User boundary

The existing Current User mechanism remains operator context only. `X-Current-User-Id` is not a login identity, authenticated principal, or permission identity.

Any future mapping from an authenticated identity to a canonical User and then to Current User must be decided by the future **SEC-A01 — Security & Access Control Design** review. No such mapping is approved or implemented by this requirement.

## Deferred SEC-A01 design decisions

SEC-A01 must make the actual design decisions for, at minimum:

- the authentication approach and identity lifecycle;
- local versus enterprise/SSO integration choices;
- authenticated identity to canonical User mapping;
- Viewer, Editor, and Administrator access-control rules;
- backend endpoint enforcement and frontend route/navigation behavior; and
- default-deny handling for unauthenticated requests.

## Explicitly not implemented now

This requirement does not introduce Authentication, Authorization, RBAC, permissions, claims, login/logout, passwords, JWT, OAuth, OIDC, SSO, ASP.NET Core Identity, access-control middleware, or any security framework. It also does not change Current User, User, KnowledgeRole, HumanConfirmation, Evidence, or U04 behavior.
