# nem.Mimir Enterprise Refactoring Master Plan v2

> Generated: 2026-03-01 | Status: APPROVED (Oracle + Momus reviewed) | Est. 10-14 sessions across 8 waves

## Federation Decision

**Stay independent.** Only converge xunit.v3 + Mapperly when touching those areas (low-priority, evaluate ROI).

| Pattern | Federation | Mimir | Converge? | Rationale |
|---------|-----------|-------|-----------|-----------|
| Auth | Azure AD | Keycloak | **No** | High-risk, Keycloak works, isolated |
| Frontend | Angular 20 | Next.js | **No** | Full rewrite, zero user benefit |
| ORM | Marten | EF Core | **No** | Paradigm shift, data migration risk |
| Test | xunit.v3 | xUnit 2.8.1 | **Maybe (W7)** | Low-risk, only if .NET 10 compat needed |
| Mapping | Mapperly | AutoMapper | **Maybe (W7)** | Only 6 mappings, evaluate ROI |
| Messaging | nem.Messaging | Wolverine | **No** | Working system, no integration need |
| Plugin SDK | nem.Plugins.Sdk | Standalone | **Evaluate** | Only if cross-project plugins needed |

## Corrected Execution Order (Momus: tests BEFORE data model refactoring)

```
[Wave 1 ∥ Wave 2] → [Wave 5 (tests)] → [Wave 3] → [Wave 4] → [Wave 6] → [Wave 7] → [Wave 8]
```

**Rationale**: Tests must exist BEFORE high-blast-radius refactoring (typed IDs, CQRS, soft delete).

---

## Wave 1: Critical Security Hardening ⚠️ URGENT
**Risk**: Active exploitability | **Effort**: 1 session

| # | Item | Fix | Files | Acceptance |
|---|------|-----|-------|------------|
| 1.1 | ChatHub sanitization bypass | Inject ISanitizationService into ChatHub, call SanitizeUserInput() before processing in SendMessage | `src/Mimir.Api/Hubs/ChatHub.cs` | All ChatHub messages sanitized. Unit test confirms |
| 1.2 | Plugin path traversal | [Authorize(Policy="RequireAdmin")] on load/unload endpoints + Path.GetFullPath + root directory validation | `src/Mimir.Api/Controllers/PluginsController.cs` | Non-admin→403, paths outside root→400 |
| 1.3 | LoggingBehavior PII exposure | Log only command type name + correlation ID in LoggingBehavior/UnhandledExceptionBehavior/PerformanceBehavior. Remove {@Request}/{@Response} destructuring | `src/Mimir.Application/Common/Behaviours/*.cs` | No request/response objects in logs |
| 1.4 | Hardcoded credentials in source | Remove hardcoded passwords from appsettings.json, add appsettings.Production.json template (empty), document user-secrets for dev | `src/Mimir.Api/appsettings*.json`, new `appsettings.Production.json` | No credentials in committed config |
| 1.5 | WolverineConfiguration hardcoded RabbitMQ | Move compiled-code RabbitMQ fallback URI to IConfiguration binding | `src/Mimir.Infrastructure/WolverineConfiguration.cs` | All connection strings from config only |
| 1.6 | RequireHttpsMetadata | Add `RequireHttpsMetadata=true` in appsettings.Production.json (already configurable via JwtSettings) | `appsettings.Production.json` | Production enforces HTTPS metadata |

## Wave 2: Security Continued + Hardening
**Risk**: Exploitable gaps | **Effort**: 1 session

| # | Item | Fix | Files | Acceptance |
|---|------|-----|-------|------------|
| 2.1 | Request body size limits | Kestrel MaxRequestBodySize=10MB global, [RequestSizeLimit(1_048_576)] on chat/message endpoints | `Program.cs`, chat controllers | Oversized→413 |
| 2.2 | SignalR rate limiting | Add connection-level rate limiting to ChatHub (FixedWindow 30msg/min per user) | `ChatHub.cs`, new rate limit filter | >30msg/min→throttled, test confirms |
| 2.3 | Docker socket threat model | Document existing sandbox hardening (network:none, readonly, seccomp, caps dropped). Add PidsLimit=50 if missing | `SandboxService.cs`, `docs/security-threat-model.md` | Threat model documented, PidsLimit enforced |
| 2.4 | CORS configurable | Move CORS origins to `appsettings.json` `Cors:AllowedOrigins` array, env override | `Program.cs`, appsettings | Configurable per environment |
| 2.5 | TelegramBotService log PII | Redact message preview from logs (currently logs first 50 chars of user messages) | `src/Mimir.Telegram/Services/TelegramBotService.cs` | No user message content in logs |

## Wave 5: Core Testing Coverage 🧪 (MOVED UP — before refactoring)
**Risk**: Regression safety net needed BEFORE Waves 3-4 | **Effort**: 3-4 sessions

### Session 5A: Middleware + Infrastructure Tests
| # | Component | Type | Key Negative Tests |
|---|-----------|------|-------------------|
| 5.1 | GlobalExceptionHandlerMiddleware | Unit | Unhandled→500 generic msg, OperationCanceled→499, nested exception info not leaked, logging verified |
| 5.2 | OutputSanitizationMiddleware | Unit | XSS script tags→stripped, SQL keywords→handled, null body→passthrough, binary content→skipped, large payload→no OOM |
| 5.3 | CorrelationIdMiddleware | Unit | No header→generates UUID, existing valid→preserves, invalid format→regenerates, appears in response |
| 5.4 | CurrentUserService | Unit | No auth context→null/throws, expired JWT→handled, missing claims→graceful default, admin claim→IsAdmin true |
| 5.5 | AuditService | Unit+Integration | Entry created correctly, concurrent writes→no loss, null actor→system, invalid entity→logged |

### Session 5B: Service + Handler Tests
| # | Component | Type | Key Negative Tests |
|---|-----------|------|-------------------|
| 5.6 | MimirApiClient (Telegram) | Unit (WireMock) | 401→auth exception, timeout→descriptive error, 500→retry then throw, malformed JSON→ParseException, empty response→handled |
| 5.7 | TelegramBotService | Unit | Malformed update→no crash, null message→skip, rate limited→backoff, network failure→logged+resilient |
| 5.8 | PluginLoadContext | Unit | Invalid DLL→descriptive error, missing deps→isolated failure, path traversal→blocked, unload→cleanup verified |
| 5.9 | ChatHub message flow | Integration | Send→received by client, invalid conversation→error, unauthenticated→rejected, concurrent sends→ordered, disconnect mid-stream→no data loss |

### Session 5C: Controller Integration Tests
| # | Component | Type | Key Negative Tests |
|---|-----------|------|-------------------|
| 5.10 | SystemPromptsController | Integration | Full CRUD happy path, duplicate name→409, empty name→400, not found→404, unauthorized→401, non-admin→403 |
| 5.11 | PluginsController | Integration | Load valid→200, path traversal→400, non-admin→403, unknown plugin→404, unload active→handled |
| 5.12 | CodeExecutionController | Integration | Valid code→result, empty code→400, unauthorized→401, timeout→408 (if testable) |
| 5.13 | OpenAiCompatController | Integration | Valid completion→200+SSE, invalid model→400/404, auth required→401, streaming errors→partial response handled |

### Session 5D: Test Infrastructure Improvements
| # | Item | Fix | Acceptance |
|---|------|-----|------------|
| 5.14 | Flaky test remediation | Replace Thread.Sleep/Task.Delay with TaskCompletionSource. Replace DateTime.UtcNow with IDateTimeService/TimeProvider | Zero Thread.Sleep in tests, zero DateTime.Now/UtcNow outside TimeProvider |
| 5.15 | TimeProvider abstraction | Add TimeProvider registration, inject where DateTime.UtcNow used in production code | All time access via DI, testable with FakeTimeProvider |
| 5.16 | Code coverage configuration | Add Coverlet to test projects, generate coverage report, establish baseline | Coverage report generated, baseline documented |

## Wave 3: Data Model Correctness
**Risk**: Silent data corruption | **Effort**: 2 sessions (phased per Momus)

### 3A: Low-Risk Fixes (1 session)
| # | Item | Fix | Files | Acceptance |
|---|------|-----|-------|------------|
| 3.1 | Audit field public setters | Change to `private set`, add internal Set methods or EF SaveChanges interceptor | `BaseAuditableEntity.cs` | Audit fields immutable from app code, tests pass |
| 3.2 | AutoArchive default mismatch | Set ConversationSettings.Default.AutoArchiveAfterDays=30 (match DB) | `ConversationSettings.cs` | Single source of truth for defaults |
| 3.3 | GetByExternalIdAsync naming | Rename to GetByEmailAsync (matches implementation) | `IUserRepository.cs`, `UserRepository.cs`, callers | Name matches behavior |
| 3.4 | AsNoTracking inconsistency | Add AsNoTracking to ALL read-only queries (SystemPromptRepo.GetById, etc.) | All repository read methods | Consistent read behavior |
| 3.5 | EF migrations baseline | Generate InitialBaseline migration, add to source control | `Infrastructure/Persistence/Migrations/` | Migrations exist and apply cleanly |
| 3.6 | CodeExecutionEvent orphan | If used: wire handler. If not: delete event class | Event file + handler | No dead event code |

### 3B: Typed ID Adoption (1 session, phased rollout)
| # | Item | Fix | Files | Acceptance |
|---|------|-----|-------|------------|
| 3.7 | Phase 1: ConversationId adoption | Replace Guid with ConversationId in Conversation entity, repository, handlers, controllers. Add implicit Guid conversion | Conversation.cs, repo, handlers | Compiles, all tests pass |
| 3.8 | Phase 2: MessageId adoption | Same for Message entity chain | Message.cs, repo, handlers | Same |
| 3.9 | Phase 3: UserId + SystemPromptId | Same for User and SystemPrompt chains | Entity, repo, handler files | Same |
| 3.10 | Phase 4: Cleanup | Remove any remaining raw Guid usage, update DTOs | All files | Zero raw Guid in domain/application layers |

## Wave 4: Architecture & CQRS Compliance
**Risk**: Structural debt | **Effort**: 1-2 sessions

| # | Item | Fix | Files | Acceptance |
|---|------|-----|-------|------------|
| 4.1 | OpenAiCompat → CQRS | Extract ChatCompletionCommand/ListModelsQuery. Controller only uses ISender. Check Next.js frontend compatibility first | `OpenAiCompatController.cs`, new handlers | Zero service injection in controller |
| 4.2 | Models → CQRS | Same treatment for ModelsController | `ModelsController.cs`, new handler | Same |
| 4.3 | Tui DI container | Add Host.CreateDefaultBuilder with service registration | `Mimir.Tui/Program.cs` | Services injected, not manually newed |
| 4.4 | Telegram interface-based DI | Extract ICommandHandler, IMessageHandler interfaces | Telegram handler files, DI | Services registered by interface |
| 4.5 | IDockerClient lifetime | Verify thread-safety. If safe: document. If not: transient + factory | `DependencyInjection.cs` | Lifetime justified and documented |
| 4.6 | Deduplicate context window logic | Extract IContextWindowService, consume in ChatHub + SendMessageCommandHandler | New service, ChatHub, handler | Logic in exactly one place |
| 4.7 | DB system prompts | Replace hardcoded "You are Mimir" with SystemPromptRepository.GetDefault() fallback chain | ChatHub, SendMessageCommandHandler | Configurable without redeployment |

## Wave 6: Soft Delete + Recovery
**Risk**: Data loss prevention | **Effort**: 1-2 sessions

| # | Item | Fix | Acceptance |
|---|------|-----|------------|
| 6.1 | Soft delete infrastructure | Add IsDeleted(bool)+DeletedAt(DateTimeOffset?) to BaseAuditableEntity. EF global query filter. SaveChanges interceptor converts Delete→Update | Delete→sets flags, queries auto-exclude deleted |
| 6.2 | Migration for soft delete | Generate and apply AddSoftDelete migration | Migration applies cleanly |
| 6.3 | Admin recovery endpoint | POST api/admin/restore/{entityType}/{id}, [Authorize(RequireAdmin)] | Admin can restore, audit trail, non-admin→403 |

## Wave 7: Framework Upgrades (OPTIONAL — evaluate ROI first)
**Risk**: Low | **Effort**: 1 session

| # | Item | Fix | Acceptance |
|---|------|-----|------------|
| 7.1 | xUnit 2.8.1→xunit.v3 | Update NuGet, fix breaking API changes | All tests pass on v3 |
| 7.2 | AutoMapper→Mapperly (EVALUATE) | Only 6 mapping profiles — very low ROI. Skip unless perf-critical | If done: Mapperly source-gen, AutoMapper removed |

## Wave 8: Enterprise Documentation 📚
**Risk**: Knowledge loss | **Effort**: 1-2 sessions | Execute LAST (documents final state)

| # | Document | Outline |
|---|----------|---------|
| 8.1 | README.md overhaul | Fix .NET 10, prerequisites, local setup with user-secrets, running tests, project structure diagram, links to other docs |
| 8.2 | ADR-004: Architecture Overview | Context, Mermaid component diagram, request flow (HTTP→Controller→MediatR→Handler→Repo), SignalR flow, Wolverine async flow, federation independence rationale |
| 8.3 | Data Model Documentation | Mermaid ERD, entity fields/constraints/relationships, value objects, audit trail design, soft delete behavior |
| 8.4 | Security Architecture | Auth flow (Keycloak OIDC), middleware pipeline diagram, input validation catalog, secrets management guide, sandbox threat model, rate limiting config |
| 8.5 | Deployment Runbook | Prerequisites, environment variables catalog, DB migration procedure, Docker Compose profiles, health check endpoints, rollback procedure, monitoring setup |
| 8.6 | API Documentation Enhancement | XML doc comments on all public controller methods + DTOs, example request/response in Swagger, export OpenAPI spec to repo |

---

## Risk Matrix

| Wave | Blast Radius | Mitigation |
|------|-------------|------------|
| W1 Security | Low (additive) | Tests exist for auth flows |
| W2 Security | Low (config) | Mostly appsettings changes |
| W5 Testing | Zero (additive) | Only adds tests |
| W3A Data fixes | Low | Small isolated changes |
| W3B Typed IDs | **HIGH** | Phased rollout, one entity at a time, full test suite runs between phases |
| W4 CQRS | Medium | Check frontend compatibility before changing API behavior |
| W6 Soft Delete | Medium | Requires migration workflow established in W3.5 first |
| W7 Upgrades | Low | Optional, evaluate ROI |
| W8 Docs | Zero | Documentation only |

## Momus Review Corrections Applied
- ✅ Removed "add validators" item (21 already exist — Oracle was wrong)
- ✅ Reordered: Tests (W5) moved BEFORE data model refactoring (W3)
- ✅ Phased typed ID adoption into 4 sub-items (was monolithic)
- ✅ Downgraded RequireHttpsMetadata to config item (already configurable)
- ✅ Added appsettings.Production.json creation
- ✅ Added WolverineConfiguration hardcoded RabbitMQ fix
- ✅ Added TimeProvider abstraction as test infra item
- ✅ Added migration dependency for soft delete
- ✅ Marked W7 as OPTIONAL with ROI evaluation gate
- ✅ Fixed test count (716 not 625)
- ✅ Noted typed IDs already exist as ValueObjects (adoption, not creation)
