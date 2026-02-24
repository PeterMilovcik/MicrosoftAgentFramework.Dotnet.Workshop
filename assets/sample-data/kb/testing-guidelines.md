# Testing Guidelines

## Overview

These guidelines define the testing standards for all services in the platform. All engineers are expected to follow them when writing, reviewing, and maintaining tests.

## Test Categories

### Unit Tests
- Must be **deterministic**: same input always produces same output.
- Must run in **under 100ms** each; if a test consistently exceeds this, it is a performance red flag.
- Must **not** depend on external systems (databases, HTTP endpoints, file system outside the project).
- Use **dependency injection** and **mocks** (e.g., Moq, NSubstitute) for external dependencies.

### Integration Tests
- May depend on **local service replicas** (in-process, test containers, or mocked servers).
- Must use **test-scoped databases** (never production or shared staging databases).
- Should clean up created data after each test (use `IAsyncLifetime` or `[TearDown]`).
- Timeout threshold: **30 seconds** per test. Tests exceeding this are flagged for review.

### End-to-End (E2E) Tests
- Run in a **dedicated staging environment** only, never in local CI pipelines.
- Must be **idempotent**: re-running a test suite must produce the same result.
- Maintained by the QA team; developers should not modify without QA sign-off.

## Handling Flaky Tests

A **flaky test** is one that passes and fails non-deterministically without code changes.

**Policy**:
1. If a test fails 2 or more times in 5 consecutive runs, it is classified as flaky.
2. Flaky tests must be **quarantined within 48 hours** (moved to a separate test suite that is not gating).
3. A GitHub issue must be opened with label `flaky-test` and assigned to the test's original author.
4. Flaky tests must be fixed or removed within **2 sprint cycles** (4 weeks).
5. Flaky tests that remain unfixed after 4 weeks are **automatically deleted** from the suite.

## Code Coverage Requirements

| Layer | Minimum Coverage |
|-------|-----------------|
| Domain / Business Logic | 90% |
| Application Services | 80% |
| Infrastructure Adapters | 70% |
| Overall Solution | 75% |

Coverage is measured on every CI run. Dropping below thresholds blocks the pipeline.

## Naming Conventions

Tests must follow the pattern: `MethodName_StateUnderTest_ExpectedBehavior`

Examples:
- `Login_WithValidCredentials_ReturnsAuthToken`
- `RefreshToken_WhenExpired_Returns401Unauthorized`
- `ProcessPayment_WhenGatewayTimesOut_RetriesThreeTimes`

## Test Review Checklist

Before merging a PR, reviewers should verify:
- [ ] Each new public method has at least one unit test.
- [ ] Integration tests use test-scoped infrastructure.
- [ ] No `Thread.Sleep` or arbitrary waits; use proper async patterns.
- [ ] Test names follow the naming convention.
- [ ] Flaky test quarantine policy is respected.
