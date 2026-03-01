# ADR-001: OpenChat UI Pivot from Angular to Next.js

## Status
Accepted

## Date
2025-01

## Context
The original mimir-web was built with Angular 17. The decision was made to pivot to Next.js for the following reasons:
- Faster development velocity with React/Next.js
- Better ecosystem for AI/chat applications
- Easier integration with Server-Sent Events (SSE) for streaming
- Next.js standalone builds for optimized Docker deployments

## Decision
We will use Next.js (React) for the frontend UI instead of Angular.

## Consequences

### Positive
- Faster cold starts with Next.js standalone builds
- Better SSE/streaming support
- Larger developer pool (React > Angular)
- Reduced bundle size

### Negative
- Migration effort required
- Two UI frameworks to maintain (Next.js + TUI)

## Alternatives Considered
- Keep Angular 17 - rejected due to slower development
- Use raw React - rejected due to SSR benefits of Next.js
- Use SvelteKit - rejected due to ecosystem size
