# AUTH-A01 Local Password Policy Amendment

## Status

Approved by explicit product direction on 2026-08-22.

## Decision

The minimum length for a Local Login password is **8 characters**. The maximum remains **128 characters**.

This amendment supersedes the `15 characters` / `15–128` minimum-length statements in:

- `docs/design/AUTH_A01_LOCAL_LOGIN_OIDC_COEXISTENCE_DESIGN_REVIEW.md` section 9 and its summary table;
- the historical AUTH-B01 verification report's password-policy summary.

The original documents remain unchanged as approved historical records.

## Unchanged Security Semantics

- Passwords remain Unicode and whitespace-significant; they are never trimmed, normalized, case-folded, or truncated.
- Passwords are never logged, returned, or stored in plaintext.
- The maximum length remains 128 characters.
- PasswordHasher configuration, dummy-hash behavior, lockout, rate limiting, antiforgery, and cookie/session semantics are unchanged.
