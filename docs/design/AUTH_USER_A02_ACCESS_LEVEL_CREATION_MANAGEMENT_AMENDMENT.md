# AUTH-USER-A02 — AccessLevel Creation and Management Amendment

## Status

Approved design amendment for `AUTH-USER-ACCESS-LEVEL-R01`.

This document amends only the Create User AccessLevel decision in
`AUTH_USER_A01_LOGIN_CREDENTIAL_PASSWORD_LIFECYCLE_ARCHITECTURE_DECISION.md`.
The historical frozen A01 remains unchanged and continues to govern every authentication,
credential, login-identity, password, concurrency, and usable-Administrator boundary not
explicitly amended here.

## Decision

An authenticated Administrator may explicitly select the initial `User.AccessLevel` while
creating a User. The controlled values remain:

```text
Viewer
Editor
Administrator
```

The Create User UI defaults to `Viewer`, but the request must explicitly carry the selected
`accessLevel`. The API validates that the value is one of the existing enum values. User,
KnowledgeRole assignments, LoginSetup, and AccessLevel are persisted by the existing atomic
Create User transaction.

For an existing User, AccessLevel remains an independent security operation:

```text
PUT /api/users/{id}/access-level
```

It is not added to the ordinary `PUT /api/users/{id}` profile update. The operation continues
to use the User concurrency token, returns the next token after success, rejects stale writes,
and preserves the `last_usable_administrator` protection.

## Independent Concepts

`User.AccessLevel` controls system authorization. `KnowledgeRole` describes knowledge identity
and ownership context. Neither grants, derives, or upgrades the other.

The following states also remain independent:

```text
User.IsActive
User.AccessLevel
LocalLoginCredential.IsActive
LoginIdentity.IsActive
KnowledgeRole
```

Changing AccessLevel does not change User Active state or any login-method state.

## Current-user Refresh

When an Administrator changes their own AccessLevel, the client immediately reloads the
authoritative Current User projection. If the new level no longer permits the current
Administrator-only route, the client navigates safely to the dashboard without requiring a
new login or a page reload. Backend authorization continues to resolve the latest persisted
AccessLevel for every request.

## Non-goals

This amendment does not introduce a new role model, RBAC framework, permission entity,
role-permission mapping, or generic permission service. It does not change KnowledgeRole
semantics, authentication methods, password behavior, User Active behavior, or the existing
three-level ordering:

```text
Viewer < Editor < Administrator
```
