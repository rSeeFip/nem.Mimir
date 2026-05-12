# ADR-006: Integration Test Strategy

**Status**: Accepted
**Date**: 2026-05-09
**Deciders**: nem.Mimir team

## Context
nem.Mimir relies on several external infrastructure components including PostgreSQL (with pgvector), RabbitMQ, and external LLM APIs via LiteLLM. Unit tests with mocks are insufficient to verify the complex interactions between Wolverine handlers, database persistence, and asynchronous messaging. We need a testing strategy that provides high fidelity while maintaining developer productivity.

## Decision
We have adopted a container-based integration testing strategy using `Testcontainers`. This allows us to spin up real instances of PostgreSQL and RabbitMQ during the test lifecycle. For external HTTP dependencies (like LiteLLM or Keycloak), we use `WireMock.Net` to simulate API responses with high precision.

## Consequences
### Positive
- **High Fidelity**: Tests run against real database and message broker implementations, catching issues that mocks would miss (e.g., SQL syntax errors, Wolverine transport issues).
- **Isolation**: Each test suite can run in its own isolated container environment, preventing state leakage between runs.
- **CI Consistency**: The same container definitions used locally are used in GitHub Actions, ensuring "works on my machine" translates to "works in CI".

### Negative
- **Resource Usage**: Running multiple Docker containers increases CPU and memory consumption during test execution.
- **Execution Time**: Startup and teardown of containers adds overhead to the test suite duration compared to pure unit tests.

### Neutral
- **CI Configuration**: Requires Docker availability in the CI environment (provided by `ubuntu-latest` runners).
- **Non-blocking CI**: Due to the resource intensity, the integration test job in CI is currently configured as non-blocking (`continue-on-error: true`) to avoid stalling PRs during infrastructure flakes.
