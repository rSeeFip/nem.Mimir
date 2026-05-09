# Mimir Phase 6 — Push, Harden, Polish

## TL;DR

> **Quick Summary**: Push feat/typed-id-adoption to origin, create PR to main, then harden CI/CD (integration tests, frontend CI, Docker build+scan, Helm lint), apply security fixes (Dockerfile non-root, Swagger env-gate, CORS audit), run frontend a11y audit, and add documentation (ADRs, README sections).
>
> **Deliverables**:
> - PR from feat/typed-id-adoption → main with squashed logical commits
> - 4 new CI jobs: integration tests, frontend lint+build, Docker build+scan, Helm lint
> - Hardened Dockerfile (non-root user, ASPNETCORE_URLS)
> - Frontend a11y fixes (critical+serious only)
> - Security audit (CORS, Swagger gating, secret scan in CI)
> - 2 ADRs + updated README sections
>
> **Estimated Effort**: Medium
> **Parallel Execution**: YES - 4 waves
> **Critical Path**: T1 (secret scan + push + PR) → {T2-T8 parallel} → T9 (docs) → F1-F4

---

## Context

### Original Request
Phase 6 of the Mimir typed-id adoption: push the branch, create PR, harden everything, polish, document.

### Interview Summary
**Key Discussions**:
- PR target: `main` (confirmed)
- 72 commits ahead, 1214 files changed, 81k insertions / 24k deletions
- All 1,287 unit tests pass, build clean (0 errors, 4 pre-existing warnings)
- Commits should be squashed into logical groups

**Research Findings**:
- CI has 3 jobs: build-and-test, security-scan, lint. Missing: integration tests, frontend, Docker, Helm
- Integration tests use Testcontainers (PostgreSQL 16-alpine, RabbitMQ, WireMock)
- Frontend has tests (`__tests__/dependency-versions.test.ts`, `__tests__/utils/`)
- Dockerfile runs as root, uses ASPNETCORE_HTTP_PORTS, copies 5 sibling repos
- Helm chart has no dependencies, `helm lint` already passes
- Rate limiting configured (100/min), FluentValidation via Wolverine, HTTPS outside dev

### Metis Review
**Identified Gaps** (addressed):
- Secret scan MUST run before any push (gate for T1)
- Integration tests in CI should start as `continue-on-error: true` (not blocking until stable)
- Dockerfile changes scoped to exactly 3: non-root user, ASPNETCORE_URLS, narrow scope discussion deferred (architectural)
- a11y limited to axe-core critical+serious only
- Max 2 ADRs (typed-id adoption, integration test strategy)
- Frontend tests exist — CI job is meaningful

---

## Work Objectives

### Core Objective
Ship the typed-id-adoption branch to main via PR, with hardened CI/CD pipeline, security fixes, a11y improvements, and documentation.

### Concrete Deliverables
- PR #N from feat/typed-id-adoption → main (squashed commits)
- `.github/workflows/ci.yml` with 4 new jobs
- Hardened `docker/api/Dockerfile`
- Frontend a11y fixes in `src/mimir-chat/`
- `docs/adr/` with 2 ADRs
- Updated README sections

### Definition of Done
- [ ] PR is open and CI passes (all 7 jobs green or continue-on-error for integration)
- [ ] `docker build` produces image that runs as non-root
- [ ] `npx axe` reports 0 critical/serious violations
- [ ] Secret scan finds 0 secrets in git history

### Must Have
- Secret scan passes before push
- PR uses existing template
- Integration tests run in CI (non-blocking)
- Dockerfile runs as non-root

### Must NOT Have (Guardrails)
- NO force-push after PR is created (force-with-lease allowed in T1 for initial squashed push before PR creation)
- NO new tests written in T2/T3 — run existing only
- NO UI redesign in a11y task — only fix violations
- NO new security features (WAF, OAuth changes) — audit + config fixes only
- NO more than 2 ADRs
- NO changes to multi-repo COPY pattern in Dockerfile (architectural decision, out of scope)
- NO new features or schema migrations

---

## Verification Strategy

> **ZERO HUMAN INTERVENTION** - ALL verification is agent-executed.

### Test Decision
- **Infrastructure exists**: YES (dotnet test, npm test, helm lint)
- **Automated tests**: Existing tests — run, don't write new
- **Framework**: dotnet test (backend), vitest/jest (frontend), helm lint (charts)

### QA Policy
Every task includes agent-executed QA scenarios.
Evidence saved to `.sisyphus/evidence/task-{N}-{scenario-slug}.{ext}`.

- **CI/CD**: Verify workflow YAML syntax + job definitions via `act` dry-run or manual inspection
- **Docker**: Build image, run container, verify non-root
- **Frontend**: Run axe-core, capture violation report
- **Security**: Run gitleaks, check CORS config, verify Swagger gating

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Gate — must complete first):
└── Task 1: Secret scan + commit squash + push + PR creation [deep]

Wave 2 (After T1 — CI jobs, all parallel):
├── Task 2: Integration test CI job [quick]
├── Task 3: Frontend CI job (lint + build + test) [quick]
├── Task 4: Dockerfile hardening (non-root + ASPNETCORE_URLS) [quick]
├── Task 5: Docker build + Trivy scan CI job [quick]
└── Task 6: Helm lint CI job [quick]

Wave 3 (After T1 — independent work, parallel with Wave 2):
├── Task 7: Frontend a11y audit + fixes [unspecified-high]
└── Task 8: Security audit + config fixes [unspecified-high]

Wave 4 (After all — documentation):
└── Task 9: Documentation (2 ADRs + README sections) [writing]

Wave FINAL (After ALL tasks — 4 parallel reviews, then user okay):
├── Task F1: Plan compliance audit (oracle)
├── Task F2: Code quality review (unspecified-high)
├── Task F3: Real manual QA (unspecified-high)
└── Task F4: Scope fidelity check (deep)
-> Present results -> Get explicit user okay
```

### Dependency Matrix

| Task | Depends On | Blocks |
|------|-----------|--------|
| T1 | — | T2-T9 |
| T2 | T1 | T9 |
| T3 | T1 | T9 |
| T4 | T1 | T5, T9 |
| T5 | T4 | T9 |
| T6 | T1 | T9 |
| T7 | T1 | T9 |
| T8 | T1 | T9 |
| T9 | T2-T8 | F1-F4 |

### Agent Dispatch Summary

- **Wave 1**: **1** — T1 → `deep`
- **Wave 2**: **5** — T2-T6 → `quick`
- **Wave 3**: **2** — T7 → `unspecified-high`, T8 → `unspecified-high`
- **Wave 4**: **1** — T9 → `writing`
- **FINAL**: **4** — F1 → `oracle`, F2 → `unspecified-high`, F3 → `unspecified-high`, F4 → `deep`

---

## TODOs

- [ ] 1. Secret Scan + Commit Squash + Push + PR Creation

  **What to do**:
  - Run `gitleaks detect --source . --no-banner` on the repo. If ANY secrets found, STOP and report — do not push.
  - Review `git log --oneline origin/main..HEAD` (72 commits). Group into logical squash targets:
    - Group 1: Core typed-id adoption (domain models, value objects, DB migrations)
    - Group 2: API layer changes (controllers, DTOs, middleware)
    - Group 3: Infrastructure (DI, configuration, Wolverine handlers)
    - Group 4: Test updates (all test projects)
    - Group 5: Frontend changes (mimir-chat)
    - Group 6: DevOps (Dockerfile, Helm, CI, docker-compose, OTEL)
  - Use `git rebase -i` (non-interactive via GIT_SEQUENCE_EDITOR) to squash into ~6 logical commits
  - Verify each squashed commit builds: `dotnet build nem.Mimir.slnx --warnaserrors`
  - Force-push the squashed branch: `git push --force-with-lease origin feat/typed-id-adoption` (this is the INITIAL push of the squashed history — force-with-lease is safe here since no one else is working on this branch)
  - Create PR: `gh pr create --base main --head feat/typed-id-adoption --title "feat: adopt typed IDs across Mimir" --body-file .github/PULL_REQUEST_TEMPLATE.md` (fill template sections)

  **Must NOT do**:
  - Do NOT force-push AFTER the PR is created and reviewed
  - Do NOT push if gitleaks finds secrets
  - Do NOT modify any source code — squash only

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: Complex git operations requiring careful rebasing of 72 commits
  - **Skills**: [`git-master`]
    - `git-master`: Squash/rebase operations, PR creation

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Wave 1 (solo)
  - **Blocks**: T2, T3, T4, T5, T6, T7, T8, T9
  - **Blocked By**: None

  **References**:

  **Pattern References**:
  - `.github/PULL_REQUEST_TEMPLATE.md` — PR body template to fill in
  - `.github/workflows/ci.yml` — CI triggers on PR to main (confirms CI will run)

  **External References**:
  - `gitleaks`: https://github.com/gitleaks/gitleaks — secret scanner

  **WHY Each Reference Matters**:
  - PR template ensures consistent PR format the team expects
  - CI workflow confirms PR will trigger checks automatically

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Secret scan passes
    Tool: Bash
    Preconditions: In repo root /workspace/wmreflect/nem.Mimir-typed-ids
    Steps:
      1. Run: gitleaks detect --source . --no-banner
      2. Assert exit code 0
    Expected Result: No secrets detected, exit code 0
    Failure Indicators: Exit code non-zero, any "Secret" or "Finding" in output
    Evidence: .sisyphus/evidence/task-1-secret-scan.txt

  Scenario: Commits squashed to ~6 logical groups
    Tool: Bash
    Steps:
      1. Run: git log --oneline origin/main..HEAD | wc -l
      2. Assert count is between 4 and 8
      3. Run: git log --oneline origin/main..HEAD
      4. Verify each message is descriptive (not "fix" or "wip")
    Expected Result: 4-8 well-named commits
    Evidence: .sisyphus/evidence/task-1-squash-log.txt

  Scenario: PR created successfully
    Tool: Bash
    Steps:
      1. Run: gh pr list --head feat/typed-id-adoption --state open --json number,title,url
      2. Assert exactly 1 PR exists
      3. Assert title contains "typed"
    Expected Result: One open PR targeting main
    Evidence: .sisyphus/evidence/task-1-pr-created.txt

  Scenario: Build passes at HEAD after squash
    Tool: Bash
    Steps:
      1. Run: dotnet build nem.Mimir.slnx --warnaserrors 2>&1 | tail -5
      2. Assert "Build succeeded" in output
    Expected Result: Build succeeded, 0 errors
    Evidence: .sisyphus/evidence/task-1-build-verify.txt
  ```

  **Commit**: NO (this task IS the push/PR)

- [ ] 2. Add Integration Test CI Job

  **What to do**:
  - Add a new job `integration-tests` to `.github/workflows/ci.yml`
  - Job runs on `ubuntu-latest`, needs `build-and-test` (to avoid running if unit tests fail)
  - Steps: checkout, setup .NET 10, restore, build, `dotnet test nem.Mimir.slnx --no-build --filter "Category=Integration"` with `continue-on-error: true`
  - Integration tests use Testcontainers which auto-starts Docker containers — no `services` block needed on GitHub Actions (Docker is available on ubuntu-latest)

  **Must NOT do**:
  - Do NOT write new integration tests
  - Do NOT make this job blocking (use `continue-on-error: true`)

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Single file YAML addition, straightforward CI job
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with T3, T4, T5, T6)
  - **Blocks**: T9
  - **Blocked By**: T1

  **References**:

  **Pattern References**:
  - `.github/workflows/ci.yml:build-and-test` — existing job pattern to follow (checkout, setup .NET, restore, build, test)
  - `tests/nem.Mimir.E2E.Tests/E2EWebApplicationFactory.cs` — uses Testcontainers (confirms Docker-in-CI compatibility)

  **API/Type References**:
  - `nem.Mimir.slnx` — solution file for dotnet test command

  **WHY Each Reference Matters**:
  - Follow exact pattern of existing `build-and-test` job for consistency
  - E2E factory confirms Testcontainers usage — no external services needed

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Integration test job defined in CI
    Tool: Bash
    Steps:
      1. Run: grep -A 20 "integration-tests:" .github/workflows/ci.yml
      2. Assert "Category=Integration" filter present
      3. Assert "continue-on-error: true" present
      4. Assert "needs:" references build-and-test
    Expected Result: Job block with correct filter, continue-on-error, and dependency
    Evidence: .sisyphus/evidence/task-2-ci-job.txt

  Scenario: YAML syntax valid
    Tool: Bash
    Steps:
      1. Run: python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))"
      2. Assert exit code 0
    Expected Result: Valid YAML, no parse errors
    Evidence: .sisyphus/evidence/task-2-yaml-valid.txt
  ```

  **Commit**: YES
  - Message: `ci: add integration test job (non-blocking)`
  - Files: `.github/workflows/ci.yml`

- [ ] 4. Harden Dockerfile

  **What to do**:
  - In `docker/api/Dockerfile`, add a non-root user in the runtime stage:
    ```dockerfile
    RUN addgroup -S appgroup && adduser -S appuser -G appgroup
    USER appuser
    ```
  - Change `ENV ASPNETCORE_HTTP_PORTS=5000` to `ENV ASPNETCORE_URLS=http://+:5000` (standard variable)
  - Keep the multi-repo COPY pattern unchanged (architectural, out of scope)

  **Must NOT do**:
  - Do NOT change the COPY pattern for sibling repos
  - Do NOT add health checks (already handled at app level)
  - Do NOT optimize build layers

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: 3-line change in a single file
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with T2, T3, T5, T6)
  - **Blocks**: T5, T9
  - **Blocked By**: T1

  **References**:

  **Pattern References**:
  - `docker/api/Dockerfile` — current Dockerfile (runs as root, uses ASPNETCORE_HTTP_PORTS)

  **WHY Each Reference Matters**:
  - Need exact current state to make precise edits

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Dockerfile has non-root user
    Tool: Bash
    Steps:
      1. Run: grep -n "USER\|adduser\|addgroup" docker/api/Dockerfile
      2. Assert USER directive present after COPY --from=builder
      3. Assert adduser/addgroup commands present
    Expected Result: Non-root user configured in runtime stage
    Evidence: .sisyphus/evidence/task-4-dockerfile-user.txt

  Scenario: ASPNETCORE_URLS replaces ASPNETCORE_HTTP_PORTS
    Tool: Bash
    Steps:
      1. Run: grep "ASPNETCORE" docker/api/Dockerfile
      2. Assert "ASPNETCORE_URLS=http://+:5000" present
      3. Assert "ASPNETCORE_HTTP_PORTS" NOT present
    Expected Result: ASPNETCORE_URLS set, HTTP_PORTS removed
    Evidence: .sisyphus/evidence/task-4-env-var.txt

  Scenario: Docker build succeeds (if Docker available)
    Tool: Bash
    Preconditions: Docker daemon running, working directory is /workspace/wmreflect (parent of sibling repos)
    Steps:
      1. Run: cd /workspace/wmreflect && docker build -f nem.Mimir-typed-ids/docker/api/Dockerfile -t mimir-hardened-test . 2>&1 | tail -10
      2. If build succeeds: docker run --rm mimir-hardened-test whoami
      3. Assert output is NOT "root"
    Expected Result: Build succeeds, container runs as non-root user
    Failure Indicators: Build failure, whoami returns "root"
    Evidence: .sisyphus/evidence/task-4-docker-build.txt
  ```

  **Commit**: YES
  - Message: `build: harden Dockerfile with non-root user and ASPNETCORE_URLS`
  - Files: `docker/api/Dockerfile`

- [ ] 5. Add Docker Build + Trivy Scan CI Job

  **What to do**:
  - Add a new job `docker-build` to `.github/workflows/ci.yml`
  - Job runs on `ubuntu-latest`, needs `build-and-test`
  - Steps: checkout, build Docker image (`docker build -f docker/api/Dockerfile -t mimir-api:ci .` from repo root context — note Dockerfile expects sibling repos, so this may need a mock/skip approach), run Trivy scan (`aquasecurity/trivy-action@master` with severity `CRITICAL` exit-code 1, `HIGH` as warning)
  - NOTE: Docker build in CI requires sibling repos. Options: (a) skip build, only lint Dockerfile, (b) add checkout steps for sibling repos. Document the limitation and use option (a) with `hadolint` for now + Trivy on the Dockerfile itself.
  - Actually: use `hadolint/hadolint-action@v3` to lint Dockerfile + `aquasecurity/trivy-action` in `fs` mode to scan for vulnerabilities in the repo

  **Must NOT do**:
  - Do NOT attempt full Docker build in CI (requires sibling repos)
  - Do NOT add Docker Hub push steps

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: YAML job addition
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with T2, T3, T4, T6)
  - **Blocks**: T9
  - **Blocked By**: T1, T4

  **References**:

  **Pattern References**:
  - `.github/workflows/ci.yml` — existing workflow
  - `docker/api/Dockerfile` — file to lint

  **External References**:
  - hadolint: https://github.com/hadolint/hadolint-action — Dockerfile linter
  - Trivy: https://github.com/aquasecurity/trivy-action — vulnerability scanner (fs mode)

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Docker lint + scan job defined
    Tool: Bash
    Steps:
      1. Run: grep -A 20 "docker-build:" .github/workflows/ci.yml
      2. Assert hadolint or trivy step present
    Expected Result: Job with Dockerfile linting and/or vulnerability scan
    Evidence: .sisyphus/evidence/task-5-ci-job.txt

  Scenario: YAML valid after changes
    Tool: Bash
    Steps:
      1. Run: python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))"
    Expected Result: Valid YAML
    Evidence: .sisyphus/evidence/task-5-yaml-valid.txt
  ```

  **Commit**: YES (group with T2, T3)
  - Message: `ci: add Dockerfile lint and security scan job`
  - Files: `.github/workflows/ci.yml`

- [ ] 6. Add Helm Lint CI Job

  **What to do**:
  - Add a new job `helm-lint` to `.github/workflows/ci.yml`
  - Job runs on `ubuntu-latest`, independent
  - Steps: checkout, install Helm (`azure/setup-helm@v4`), `helm lint deploy/helm/nem-mimir`
  - No `helm dependency build` needed (chart has no dependencies)

  **Must NOT do**:
  - Do NOT add helm template or helm install dry-run (no cluster config available)

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Simple YAML job addition
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with T2, T3, T4, T5)
  - **Blocks**: T9
  - **Blocked By**: T1

  **References**:

  **Pattern References**:
  - `deploy/helm/nem-mimir/Chart.yaml` — chart metadata (no dependencies confirmed)
  - `deploy/helm/nem-mimir/values.yaml` — default values for lint

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Helm lint job defined
    Tool: Bash
    Steps:
      1. Run: grep -A 15 "helm-lint:" .github/workflows/ci.yml
      2. Assert "helm lint" present
      3. Assert "deploy/helm/nem-mimir" in the lint command
    Expected Result: Job with helm lint step
    Evidence: .sisyphus/evidence/task-6-ci-job.txt

  Scenario: Helm lint passes locally
    Tool: Bash
    Steps:
      1. Run: helm lint deploy/helm/nem-mimir 2>&1
      2. Assert "0 chart(s) failed" or no errors
    Expected Result: Lint passes
    Evidence: .sisyphus/evidence/task-6-helm-lint.txt
  ```

  **Commit**: YES (group with T2, T3, T5)
  - Message: `ci: add Helm lint job`
  - Files: `.github/workflows/ci.yml`

- [ ] 3. Add Frontend CI Job

  **What to do**:
  - Add a new job `frontend` to `.github/workflows/ci.yml`
  - Job runs on `ubuntu-latest`, independent (no `needs`)
  - Steps: checkout, setup Node 22, `cd src/mimir-chat`, `npm ci`, `npm run lint`, `npm test`, `npm run build`
  - Each step is separate for clear failure identification

  **Must NOT do**:
  - Do NOT write new frontend tests
  - Do NOT add new lint rules

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: Single file YAML addition
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with T2, T4, T5, T6)
  - **Blocks**: T9
  - **Blocked By**: T1

  **References**:

  **Pattern References**:
  - `.github/workflows/ci.yml` — existing workflow structure
  - `src/mimir-chat/package.json` — npm scripts: `lint`, `test`, `build`
  - `src/mimir-chat/__tests__/dependency-versions.test.ts` — confirms tests exist

  **WHY Each Reference Matters**:
  - package.json confirms exact script names to use
  - Existing test files confirm `npm test` won't be a no-op

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Frontend job defined in CI
    Tool: Bash
    Steps:
      1. Run: grep -A 25 "frontend:" .github/workflows/ci.yml
      2. Assert "npm ci" present
      3. Assert "npm run lint" present
      4. Assert "npm test" present
      5. Assert "npm run build" present
    Expected Result: Job with all 4 npm steps
    Evidence: .sisyphus/evidence/task-3-ci-job.txt

  Scenario: Frontend CI steps pass locally
    Tool: Bash
    Steps:
      1. cd src/mimir-chat
      2. Run: npm ci 2>&1 | tail -3
      3. Run: npm run lint 2>&1 | tail -3
      4. Run: npm test 2>&1 | tail -5
      5. Run: npm run build 2>&1 | tail -5
    Expected Result: All 4 commands exit 0
    Evidence: .sisyphus/evidence/task-3-local-verify.txt
  ```

  **Commit**: YES (group with T2)
  - Message: `ci: add frontend lint, test, and build job`
  - Files: `.github/workflows/ci.yml`

- [ ] 7. Frontend Accessibility Audit + Fixes

  **What to do**:
  - Install `@axe-core/cli` or use `npx @axe-core/cli` to run a11y audit against the built Next.js app
  - Alternatively, add `axe-core` + `jest-axe` for component-level a11y testing
  - Run audit on key pages: home/chat page, login (if exists), settings
  - Fix ONLY critical and serious violations (WCAG 2.1 AA):
    - Missing alt text on images
    - Missing form labels
    - Insufficient color contrast
    - Missing landmark regions
    - Missing ARIA attributes on interactive elements
  - Do NOT fix moderate/minor violations
  - Do NOT redesign any UI

  **Must NOT do**:
  - Do NOT fix moderate or minor a11y violations
  - Do NOT change visual design or layout
  - Do NOT write new tests

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: Requires running app, analyzing violations, making targeted fixes across multiple components
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with T8)
  - **Blocks**: T9
  - **Blocked By**: T1

  **References**:

  **Pattern References**:
  - `src/mimir-chat/` — Next.js frontend root
  - `src/mimir-chat/app/` — App Router pages
  - `src/mimir-chat/components/` — shared components (if exists)

  **External References**:
  - axe-core: https://github.com/dequelabs/axe-core — a11y testing engine

  **WHY Each Reference Matters**:
  - Need to know page structure to target audit correctly
  - axe-core is the industry standard for automated a11y testing

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: axe-core reports 0 critical violations
    Tool: Bash
    Preconditions: mimir-chat built (npm run build)
    Steps:
      1. Start app: cd src/mimir-chat && npm start &
      2. Wait for port 3000 to be available
      3. Run: npx @axe-core/cli http://localhost:3000 --tags wcag2a,wcag2aa 2>&1
      4. Count critical and serious violations
      5. Kill the app server
    Expected Result: 0 critical violations, 0 serious violations
    Failure Indicators: Any critical/serious violation listed in output
    Evidence: .sisyphus/evidence/task-7-axe-report.txt

  Scenario: No visual regressions (build still works)
    Tool: Bash
    Steps:
      1. cd src/mimir-chat
      2. Run: npm run build 2>&1 | tail -10
      3. Assert "Build succeeded" or similar success message
    Expected Result: Build succeeds with 0 errors
    Evidence: .sisyphus/evidence/task-7-build-verify.txt
  ```

  **Commit**: YES
  - Message: `fix(a11y): resolve critical accessibility violations in mimir-chat`
  - Files: `src/mimir-chat/...` (affected components)

- [ ] 8. Security Audit + Config Fixes

  **What to do**:
  - **CORS audit**: Read `AddNemCors` implementation in `../nem.Contracts/src/nem.Contracts.AspNetCore/Cors/NemCorsExtensions.cs` (sibling repo). Verify allowed origins are environment-specific (not unconditional `AllowAnyOrigin()`). Document findings. Note: CORS is implemented in nem.Contracts shared library, not in this repo — fixes to CORS itself are out of scope, but audit findings should be documented.
  - **Swagger/API docs gating**: The API uses `app.MapNemApiDocs()` from nem.Contracts (not `UseSwagger`). Check `../nem.Contracts/src/nem.Contracts.AspNetCore/Api/NemApiExtensions.cs` to verify docs are gated by environment. Tests in nem.Contracts confirm: non-dev without opt-in = no docs, dev = docs mapped, non-dev with opt-in = docs mapped. Verify Program.cs does NOT pass an opt-in flag for production.
  - **Secret scan CI**: Add `gitleaks` step to CI security-scan job (or as a separate job). Use `gitleaks/gitleaks-action@v2`.
  - Do NOT implement new security features

  **Must NOT do**:
  - Do NOT add WAF, OAuth changes, or new auth middleware
  - Do NOT change rate limiting config (already correct at 100/min)
  - Do NOT modify health endpoint auth (correctly anonymous)

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: Requires reading multiple files, understanding security implications, making targeted fixes
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 3 (with T7)
  - **Blocks**: T9
  - **Blocked By**: T1

  **References**:

  **Pattern References**:
  - `src/nem.Mimir.Api/Program.cs:152` — `AddNemCors(configuration, NemCorsProfile.NemTrusted)` call
  - `src/nem.Mimir.Api/Program.cs:289` — `app.MapNemApiDocs()` call (docs endpoint)
  - `../nem.Contracts/src/nem.Contracts.AspNetCore/Cors/NemCorsExtensions.cs` — actual CORS implementation (sibling repo)
  - `../nem.Contracts/src/nem.Contracts.AspNetCore/Api/NemApiExtensions.cs:68` — `MapNemApiDocs` implementation (env-gated)
  - `../nem.Contracts/tests/nem.Contracts.Tests/Api/NemApiExtensionsTests.cs` — tests confirming env gating behavior
  - `.github/workflows/ci.yml:security-scan` — existing security scan job to extend

  **API/Type References**:
  - `NemCorsProfile.NemTrusted` — the CORS profile used in Program.cs (defined in nem.Contracts)
  - `MapNemApiDocs()` — extension method that gates API docs (Scalar/Swagger) by environment

  **External References**:
  - gitleaks-action: https://github.com/gitleaks/gitleaks-action — CI secret scanning

  **WHY Each Reference Matters**:
  - Program.cs shows exact calls (`AddNemCors`, `MapNemApiDocs`) — need to verify no production opt-in flags
  - NemCorsExtensions.cs is the real CORS implementation — must audit there, not in this repo
  - NemApiExtensions.cs is the real docs gating — verify env check logic
  - Tests confirm expected behavior — use as verification baseline
  - Security-scan job is where gitleaks step belongs (logical grouping)

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: API docs gated by environment
    Tool: Bash
    Steps:
      1. Run: grep -n "MapNemApiDocs" src/nem.Mimir.Api/Program.cs
      2. Verify the call does NOT pass an opt-in flag (no `enableInProduction: true` or similar)
      3. Run: grep -A 20 "MapNemApiDocs" ../nem.Contracts/src/nem.Contracts.AspNetCore/Api/NemApiExtensions.cs
      4. Verify implementation checks `IsDevelopment()` or similar env guard
    Expected Result: API docs only enabled in Development environment (no production opt-in)
    Failure Indicators: Opt-in flag passed in Program.cs, or no env guard in implementation
    Evidence: .sisyphus/evidence/task-8-swagger-gate.txt

  Scenario: CORS not using unconditional wildcard
    Tool: Bash
    Steps:
      1. Run: grep -B5 -A10 "AllowAnyOrigin" ../nem.Contracts/src/nem.Contracts.AspNetCore/Cors/NemCorsExtensions.cs
      2. Verify AllowAnyOrigin is inside a conditional (e.g., specific profile or env check)
      3. Run: grep "NemCorsProfile" src/nem.Mimir.Api/Program.cs
      4. Document which profile is used
    Expected Result: AllowAnyOrigin is conditional, not applied to the NemTrusted profile used by Mimir
    Evidence: .sisyphus/evidence/task-8-cors-audit.txt

  Scenario: Gitleaks step in CI
    Tool: Bash
    Steps:
      1. Run: grep -A5 "gitleaks" .github/workflows/ci.yml
      2. Assert gitleaks action or command present in security-scan job
    Expected Result: Gitleaks step exists in CI pipeline
    Evidence: .sisyphus/evidence/task-8-gitleaks-ci.txt
  ```

  **Commit**: YES
  - Message: `security: add gitleaks CI, audit CORS and API docs gating`
  - Files: `.github/workflows/ci.yml` (gitleaks step), docs or commit message for audit findings

- [ ] 9. Documentation (ADRs + README sections)

  **What to do**:
  - Create `docs/adr/` directory
  - **ADR-005: Typed ID Adoption**: Document the decision to adopt strongly-typed IDs from nem.Contracts. Context, decision, consequences. Reference the migration from nem.Mimir to nem.Mimir-typed-ids.
  - **ADR-006: Integration Test Strategy**: Document the choice of Testcontainers for integration testing. Context (PostgreSQL + RabbitMQ + WireMock), decision, trade-offs (CI resource usage vs fidelity).
  - Update `README.md` with:
    - Docker build instructions (reference `docker/api/Dockerfile`, note sibling repo dependency)
    - Helm deployment section (reference `deploy/helm/nem-mimir/`)
    - CI pipeline overview (7 jobs, what each does)
  - Max 2 ADRs. Document what exists, not aspirational.

  **Must NOT do**:
  - Do NOT create more than 2 ADRs
  - Do NOT write aspirational documentation
  - Do NOT duplicate AGENTS.md content

  **Recommended Agent Profile**:
  - **Category**: `writing`
    - Reason: Pure documentation task
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Wave 4 (after all implementation)
  - **Blocks**: F1-F4
  - **Blocked By**: T2-T8 (needs to document final state)

  **References**:

  **Pattern References**:
  - `README.md` — existing README to extend
  - `AGENTS.md` — project knowledge base (don't duplicate, reference)
  - `CHANGELOG.md` — existing changelog format

  **API/Type References**:
  - `tests/nem.Mimir.E2E.Tests/E2EWebApplicationFactory.cs` — Testcontainers setup for ADR-002
  - `docker/api/Dockerfile` — Docker build for README section
  - `deploy/helm/nem-mimir/` — Helm chart for README section

  **WHY Each Reference Matters**:
  - E2E factory is the primary evidence for integration test strategy ADR
  - Dockerfile and Helm chart are what users need to build/deploy

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: ADRs exist with correct structure
    Tool: Bash
    Steps:
      1. Run: ls docs/adr/ADR-005* docs/adr/ADR-006*
      2. Assert both files exist
      3. Run: head -20 docs/adr/ADR-005-*.md
      4. Assert has: Title, Status, Context, Decision, Consequences sections
      5. Run: head -20 docs/adr/ADR-006-*.md
      6. Assert same structure
    Expected Result: 2 new ADRs (005, 006) with standard structure
    Evidence: .sisyphus/evidence/task-9-adrs.txt

  Scenario: README has new sections
    Tool: Bash
    Steps:
      1. Run: grep -n "Docker\|Helm\|CI Pipeline\|CI/CD" README.md
      2. Assert at least 3 new section headers found
    Expected Result: Docker, Helm, and CI sections present in README
    Evidence: .sisyphus/evidence/task-9-readme.txt
  ```

  **Commit**: YES
  - Message: `docs: add ADRs for typed-IDs and integration tests, update README`
  - Files: `docs/adr/ADR-005-typed-id-adoption.md`, `docs/adr/ADR-006-integration-test-strategy.md`, `README.md`

---

## Final Verification Wave (MANDATORY — after ALL implementation tasks)

> 4 review agents run in PARALLEL. ALL must APPROVE. Present consolidated results to user and get explicit "okay" before completing.

- [ ] F1. **Plan Compliance Audit** — `oracle`
  Read the plan end-to-end. For each "Must Have": verify implementation exists. For each "Must NOT Have": search codebase for forbidden patterns — reject with file:line if found. Check evidence files exist in .sisyphus/evidence/. Compare deliverables against plan.
  Output: `Must Have [N/N] | Must NOT Have [N/N] | Tasks [N/N] | VERDICT: APPROVE/REJECT`

- [ ] F2. **Code Quality Review** — `unspecified-high`
  Run `dotnet build nem.Mimir.slnx --warnaserrors` + `dotnet test nem.Mimir.slnx --filter "Category!=Integration"` + `cd src/mimir-chat && npm run lint && npm run build`. Review changed files for: empty catches, console.log in prod, commented-out code, unused imports. Check YAML syntax on all CI workflow files.
  Output: `Build [PASS/FAIL] | Lint [PASS/FAIL] | Tests [N pass/N fail] | Files [N clean/N issues] | VERDICT`

- [ ] F3. **Real Manual QA** — `unspecified-high`
  Execute EVERY QA scenario from EVERY task — follow exact steps, capture evidence. Test CI workflow YAML validity. Verify Docker build + non-root. Run secret scan. Check a11y report. Save to `.sisyphus/evidence/final-qa/`.
  Output: `Scenarios [N/N pass] | Integration [N/N] | Edge Cases [N tested] | VERDICT`

- [ ] F4. **Scope Fidelity Check** — `deep`
  For each task: read "What to do", read actual diff (`git diff origin/main..HEAD`). Verify 1:1 — everything in spec was built, nothing beyond spec was built. Check "Must NOT do" compliance. Flag unaccounted changes.
  Output: `Tasks [N/N compliant] | Contamination [CLEAN/N issues] | Unaccounted [CLEAN/N files] | VERDICT`

---

## Commit Strategy

All changes committed to `feat/typed-id-adoption` branch, pushed to remote, included in PR.

- **T1**: Squash existing commits + push + PR (no new commit)
- **T2-T6**: `ci: add {job-name} to CI pipeline` — `.github/workflows/ci.yml`
- **T4**: `build: harden Dockerfile with non-root user` — `docker/api/Dockerfile`
- **T7**: `fix(a11y): resolve critical accessibility violations` — `src/mimir-chat/...`
- **T8**: `security: add gitleaks CI, audit CORS and API docs gating` — `.github/workflows/ci.yml`
- **T9**: `docs: add ADRs and update README` — `docs/adr/`, `README.md`

---

## Success Criteria

### Verification Commands
```bash
# PR exists and is open
gh pr list --head feat/typed-id-adoption --state open

# CI jobs visible on PR
gh pr checks <PR-NUMBER>

# Docker image runs as non-root (run from /workspace/wmreflect)
cd /workspace/wmreflect && docker build -f nem.Mimir-typed-ids/docker/api/Dockerfile -t mimir-test . && docker run --rm mimir-test whoami

# Secret scan clean
gitleaks detect --source . --no-banner

# All existing tests pass
dotnet test nem.Mimir.slnx --filter "Category!=Integration"
cd src/mimir-chat && npm test
```

### Final Checklist
- [ ] All "Must Have" present
- [ ] All "Must NOT Have" absent
- [ ] PR open with CI checks
- [ ] All tests pass
- [ ] Evidence files in .sisyphus/evidence/
