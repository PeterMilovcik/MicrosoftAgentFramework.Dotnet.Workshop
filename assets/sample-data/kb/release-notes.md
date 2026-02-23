# Release Notes – Platform v3.4.0

**Release Date**: 2026-02-20
**Release Manager**: Alex Johnson
**Affected Services**: AuthService, PaymentGateway, NotificationHub

---

## Summary

This release includes security hardening for authentication tokens, a retry policy fix in PaymentGateway, and performance improvements to NotificationHub. It also deprecates several legacy APIs scheduled for removal in v4.0.

---

## Breaking Changes

None in this release. All APIs remain backward-compatible.

---

## New Features

### AuthService
- **Modern Token Provider (v2)**: Introduced `ModernTokenProvider` to replace `LegacyTokenProvider`.
  - Supports RS256 and ES256 signing algorithms.
  - Token lifetime configurable via `appsettings.json` (`Auth:TokenLifetimeMinutes`, default: 60).
  - Validates audience and issuer claims by default.
- **Token Introspection Endpoint**: Added `POST /api/auth/introspect` for external services to validate tokens without shared secret.

### PaymentGateway
- **Retry Policy v2**: Rewrote `RetryPolicy` to use exponential back-off with jitter.
  - Maximum retry attempts: 3 (configurable).
  - Initial delay: 500ms; multiplier: 2x; max delay: 4s.
  - Logs each retry attempt at `Warning` level with attempt number and delay.
- **Idempotency Key Support**: All `POST /api/v2/charges` requests now accept `X-Idempotency-Key` header to prevent duplicate charges.

### NotificationHub
- **Batching**: Email and push notifications are now batched (max 250 per batch) to reduce external API calls by ~60%.
- **Dead Letter Queue**: Failed notifications (after 3 retries) are written to a dead letter queue for manual review.

---

## Bug Fixes

| ID | Service | Description |
|----|---------|-------------|
| GH-1042 | AuthService | Fixed race condition in token refresh that could issue two valid tokens simultaneously |
| GH-1087 | PaymentGateway | Fixed timeout threshold not being applied to sandbox endpoint during integration tests |
| GH-1101 | NotificationHub | Fixed memory leak when notification sender is disposed prematurely |

---

## Deprecations (Removal Target: v4.0.0)

| Item | Replacement |
|------|-------------|
| `LegacyTokenProvider` class | `ModernTokenProvider` |
| `GET /api/auth/validate` endpoint | `POST /api/auth/introspect` |
| `RetryPolicy v1` (`RetryOptions.MaxAttempts` only) | `RetryPolicy v2` with exponential back-off |

---

## Action Items for Development Teams

1. **Migrate to `ModernTokenProvider`**: Update all services referencing `LegacyTokenProvider`. Deadline: v3.6.0.
2. **Update integration tests**: `ChargeCardTest.ShouldDeclineExpiredCard` must be updated to use the new sandbox endpoint URL and increase timeout to 35s.
3. **Review coverage thresholds**: PaymentGateway coverage dropped to 74.3%. Teams must add missing tests before next release.
4. **Enable idempotency keys**: All clients issuing charge requests should add `X-Idempotency-Key` headers in their next sprint.

---

## Known Issues

- `NotificationHub` batch processor may emit duplicate `Warning` log entries under high load. Investigation in progress (GH-1119).
- `AuthService` introspection endpoint does not yet support opaque tokens (planned for v3.5).
