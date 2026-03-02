# .NET 9 Enterprise Best Practices Reference

> **Purpose**: Production-grade enterprise patterns for a .NET 9 application using Clean Architecture, EF Core, JWT auth, SignalR, and rate limiting. Use this as a gap-analysis checklist against your codebase.
>
> **Sources**: All recommendations sourced from official Microsoft documentation and framework-specific official docs (linked per section).

---

## Table of Contents

1. [Clean Architecture](#1-clean-architecture)
2. [ASP.NET Core 9 Security](#2-aspnet-core-9-security)
3. [EF Core 9](#3-ef-core-9)
4. [Enterprise Testing Patterns](#4-enterprise-testing-patterns)
5. [Enterprise Documentation Standards](#5-enterprise-documentation-standards)
6. [Wolverine Messaging Framework](#6-wolverine-messaging-framework)
7. [SignalR Security & Scaling](#7-signalr-security--scaling)
8. [Observability — Serilog, Health Checks, Metrics](#8-observability--serilog-health-checks-metrics)

---

## 1. Clean Architecture

> **Source**: [Microsoft — Common Web Application Architectures](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures)

### 1.1 Project Structure (3-Project Minimum)

```
src/
├── MyApp.Core/              # Application Core (domain + app logic)
│   ├── Entities/            # Domain entities, aggregates
│   ├── Interfaces/          # Repository interfaces, service contracts
│   ├── Services/            # Domain services, business logic
│   ├── Specifications/      # Query specifications (if using Specification pattern)
│   ├── ValueObjects/        # DDD value objects
│   └── Exceptions/          # Domain exceptions
│
├── MyApp.Infrastructure/    # Infrastructure layer
│   ├── Data/                # DbContext, EF configurations, migrations
│   ├── Repositories/        # Repository implementations
│   ├── Identity/            # Auth service implementations
│   ├── Services/            # External service integrations (email, storage, etc.)
│   └── Logging/             # Logging infrastructure
│
├── MyApp.Web/               # UI / API layer (ASP.NET Core host)
│   ├── Controllers/         # API controllers (or Endpoints/ for minimal APIs)
│   ├── Middleware/           # Custom middleware
│   ├── Filters/             # Action/exception filters
│   ├── Models/              # DTOs, ViewModels, API request/response models
│   └── Program.cs           # Composition root
│
tests/
├── MyApp.UnitTests/
├── MyApp.IntegrationTests/
└── MyApp.FunctionalTests/
```

### 1.2 Dependency Rules

| Layer | Can Reference | MUST NOT Reference |
|-------|--------------|-------------------|
| **Core** | Nothing (self-contained) | Infrastructure, Web |
| **Infrastructure** | Core | Web |
| **Web** | Core, Infrastructure | — |

**Critical**: Dependencies flow inward only. The Core project has **zero** NuGet package dependencies on ASP.NET Core or EF Core.

### 1.3 Type Classification by Layer

**Application Core contains:**
- Entities, Aggregates, Value Objects
- Domain Events
- Interfaces (repositories, domain services)
- Domain Services (business logic)
- Specifications / Query objects
- Custom Exceptions
- DTOs (if shared across layers)
- Enums

**Infrastructure contains:**
- DbContext and EF Configurations (`IEntityTypeConfiguration<T>`)
- Repository implementations
- Cached repository decorators
- External service implementations (email, file storage, payment)
- Identity/Auth service implementations
- Data access (migrations, seeders)

**Web/API contains:**
- Controllers / Minimal API Endpoints
- Middleware (auth, error handling, logging)
- Filters (validation, exception)
- ViewModels / API Models
- Composition Root (DI registration in `Program.cs`)
- Background services (`IHostedService`)

### 1.4 Enterprise Checklist

- [ ] Core project has zero references to Infrastructure or Web
- [ ] All external dependencies accessed through interfaces defined in Core
- [ ] No `DbContext` usage in Controllers — always through repository/service abstraction
- [ ] Composition Root (DI wiring) only in Web project's `Program.cs`
- [ ] No business logic in Controllers — delegate to domain/application services
- [ ] Guard clauses validate inputs at domain boundaries
- [ ] Use `internal` visibility for infrastructure implementations; expose via DI only

---

## 2. ASP.NET Core 9 Security

> **Sources**:
> - [ASP.NET Core Security Overview](https://learn.microsoft.com/en-us/aspnet/core/security/?view=aspnetcore-9.0)
> - [Authentication Overview](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/?view=aspnetcore-9.0)
> - [Rate Limiting Middleware](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-9.0)

### 2.1 Authentication — JWT Bearer (Production Config)

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.FromMinutes(1) // Reduce from default 5min
        };

        // For SignalR WebSocket authentication
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
```

**Enterprise JWT Checklist:**
- [ ] Signing keys stored in Azure Key Vault or equivalent secret manager — **never** in `appsettings.json`
- [ ] Token lifetime ≤ 15 minutes for access tokens; use refresh tokens for session continuity
- [ ] `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` all `true`
- [ ] `ClockSkew` reduced from default 5 min to ≤ 1 min
- [ ] Refresh token rotation implemented (invalidate old refresh token on use)
- [ ] Revocation strategy in place (token blacklist or short-lived tokens)

### 2.2 Authorization — Policy-Based

```csharp
builder.Services.AddAuthorization(options =>
{
    // Require authenticated users globally
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    // Custom policies
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("CanManageUsers", policy =>
        policy.RequireClaim("permission", "users:manage"));
});
```

**Checklist:**
- [ ] `FallbackPolicy` requires authentication by default (opt-out with `[AllowAnonymous]`)
- [ ] Use policy-based authorization over role checks where possible
- [ ] Resource-based authorization for entity-level access control
- [ ] `[Authorize]` on controllers/hubs, `[AllowAnonymous]` only on specific endpoints

### 2.3 Middleware Pipeline Order

```csharp
app.UseHttpsRedirection();
app.UseHsts();                    // Security header
app.UseStaticFiles();
app.UseRouting();
app.UseCors();                    // Must be after UseRouting, before UseAuth
app.UseRateLimiter();             // Before auth to protect login endpoints
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
```

### 2.4 CORS (Production)

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins("https://yourdomain.com", "https://app.yourdomain.com")
              .AllowCredentials()
              .WithMethods("GET", "POST", "PUT", "DELETE")
              .WithHeaders("Authorization", "Content-Type", "X-Requested-With")
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});
```

**Checklist:**
- [ ] **Never** use `AllowAnyOrigin()` in production
- [ ] `AllowAnyOrigin()` and `AllowCredentials()` cannot be combined (browser security)
- [ ] List explicit allowed origins, methods, and headers
- [ ] SignalR requires CORS when client and server are on different origins

### 2.5 Rate Limiting

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global sliding window
    options.AddSlidingWindowLimiter("global", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.SegmentsPerWindow = 6;
        opt.QueueLimit = 0;
    });

    // Stricter for auth endpoints
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
    });

    // Per-user with partitioning
    options.AddPolicy("per-user", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 50,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsync(
            "Rate limit exceeded. Try again later.", cancellationToken);
    };
});
```

**Four algorithms available:**
| Algorithm | Use Case |
|-----------|----------|
| **Fixed Window** | Simple, predictable limits (auth endpoints) |
| **Sliding Window** | Smoother distribution, prevents burst at window boundaries |
| **Token Bucket** | Allow controlled bursts above steady-state rate |
| **Concurrency** | Limit simultaneous requests (file uploads, heavy ops) |

**Checklist:**
- [ ] Rate limiter applied globally via `app.UseRateLimiter()` before auth
- [ ] Stricter limits on authentication/registration endpoints (brute force protection)
- [ ] Per-user partitioning for authenticated endpoints
- [ ] Per-IP partitioning for anonymous endpoints
- [ ] `OnRejected` callback sets `Retry-After` header
- [ ] `[EnableRateLimiting("policy")]` / `[DisableRateLimiting]` attributes on endpoints

### 2.6 Security Headers

```csharp
// In middleware or via NuGet package like NetEscapades.AspNetCore.SecurityHeaders
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "0"); // Modern browsers: CSP replaces this
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy",
        "camera=(), microphone=(), geolocation=()");
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'");
    await next();
});

// HSTS (built-in)
app.UseHsts(); // Sends Strict-Transport-Security header in production
```

**Checklist:**
- [ ] HSTS enabled (`UseHsts()`) — **only in production**, not development
- [ ] `X-Content-Type-Options: nosniff` on all responses
- [ ] `X-Frame-Options: DENY` (or `SAMEORIGIN` if needed)
- [ ] Content Security Policy (CSP) defined
- [ ] `Referrer-Policy` set
- [ ] `Permissions-Policy` restricts unnecessary browser features
- [ ] HTTPS redirection enforced (`UseHttpsRedirection()`)
- [ ] Secrets never in `appsettings.json` — use Secret Manager (dev), Key Vault (prod)
- [ ] Avoid Resource Owner Password Credentials (ROPC) grant

---

## 3. EF Core 9

> **Source**: [EF Core — Efficient Querying](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying)

### 3.1 DbContext Configuration

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null);
        sqlOptions.CommandTimeout(30);
        sqlOptions.MigrationsAssembly("MyApp.Infrastructure");
    });

    // Production optimizations
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking); // Default no-tracking
});
```

### 3.2 Query Performance Rules

| Practice | Why |
|----------|-----|
| **Always use `.AsNoTracking()`** for read-only queries | ~30% performance gain; default to `NoTracking` globally |
| **Project with `.Select()`** instead of loading full entities | Reduces data transfer, avoids loading unused columns |
| **Use keyset pagination** (`WHERE Id > @lastId ORDER BY Id`) | O(1) vs O(N) for offset-based; critical for large datasets |
| **Avoid N+1** — use `.Include()` or projection | Prevents hundreds of queries for related data |
| **Use split queries** for multi-collection Includes | `AsSplitQuery()` avoids cartesian explosion |
| **Add indexes** on filter/sort/join columns | Single biggest performance improvement |
| **Use `async` everywhere** | `ToListAsync()`, `FirstOrDefaultAsync()`, `SaveChangesAsync()` |
| **Limit result sets** | Always use `.Take(n)` or pagination; never load unbounded data |

### 3.3 Entity Configuration (Fluent API over Attributes)

```csharp
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.OrderNumber).IsRequired().HasMaxLength(50);
        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.HasIndex(o => o.CreatedAt);  // Index for common queries

        builder.HasMany(o => o.LineItems)
               .WithOne(li => li.Order)
               .HasForeignKey(li => li.OrderId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(o => !o.IsDeleted); // Global soft-delete filter
    }
}
```

### 3.4 Repository Pattern vs. Direct DbContext

**Recommendation**: Use **thin repository interfaces** defined in Core, implemented in Infrastructure. Do NOT over-abstract.

```csharp
// In Core — interface only
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetRecentOrdersAsync(int count, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

// In Infrastructure — implementation
public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;

    public OrderRepository(AppDbContext context) => _context = context;

    public async Task<Order?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _context.Orders
            .Include(o => o.LineItems)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    // ... etc
}
```

**When to use direct DbContext**: Simple CRUD with no business logic; internal tools; query-heavy read models (CQRS read side).

### 3.5 Migrations

**Checklist:**
- [ ] Migrations in Infrastructure project (`sqlOptions.MigrationsAssembly("MyApp.Infrastructure")`)
- [ ] Each migration reviewed in PR — check for data loss, index additions, constraint changes
- [ ] `HasData()` only for reference/seed data; use `DbInitializer` service for complex seeding
- [ ] Never use `EnsureCreated()` in production — always `Migrate()`
- [ ] Use idempotent migration scripts for CI/CD: `dotnet ef migrations script --idempotent`
- [ ] Enable retry on transient failures (`EnableRetryOnFailure`)
- [ ] Connection string in environment variables or Key Vault, **not** `appsettings.json`

---

## 4. Enterprise Testing Patterns

> **Source**: [ASP.NET Core Integration Tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-9.0)

### 4.1 Test Project Structure

```
tests/
├── MyApp.UnitTests/             # Fast, isolated, no I/O
│   ├── Domain/                  # Entity/value object tests
│   ├── Services/                # Domain service tests
│   └── Validators/              # Input validation tests
│
├── MyApp.IntegrationTests/      # WebApplicationFactory-based
│   ├── Fixtures/                # Shared test fixtures (CustomWebApplicationFactory)
│   ├── Controllers/             # API endpoint integration tests
│   ├── Repositories/            # Data access integration tests
│   └── TestHelpers/             # Auth helpers, data builders
│
└── MyApp.FunctionalTests/       # End-to-end (optional)
    └── Scenarios/
```

### 4.2 WebApplicationFactory Setup

```csharp
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Replace real DB with SQLite in-memory
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite("DataSource=:memory:");
            });

            // Replace external services with test doubles
            services.AddScoped<IEmailService, FakeEmailService>();
            services.AddScoped<IPaymentGateway, FakePaymentGateway>();
        });

        builder.UseEnvironment("Testing");
    }
}
```

> **Important**: Microsoft recommends **SQLite in-memory** over EF Core InMemory provider for integration tests because it better simulates relational behavior (constraints, transactions).

### 4.3 Negative Testing Strategies

```csharp
[Trait("Category", "Integration")]
[Trait("Type", "Negative")]
public class OrdersControllerNegativeTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OrdersControllerNegativeTests(CustomWebApplicationFactory factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task CreateOrder_WithEmptyBody_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_Unauthenticated_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", ValidOrder());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_NonExistentId_Returns404()
    {
        var client = factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/orders/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_WithSqlInjectionInName_Returns400OrIsSanitized()
    {
        var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            Name = "'; DROP TABLE Orders; --"
        });
        // Should be safely handled — either validation error or safe persistence
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest
                  || response.IsSuccessStatusCode);
    }
}
```

### 4.4 Test Categories & Organization

```csharp
// Use [Trait] for xUnit, [Category] for NUnit
[Trait("Category", "Unit")]        // Fast, no I/O
[Trait("Category", "Integration")] // Needs database/services
[Trait("Category", "Functional")]  // End-to-end

[Trait("Type", "Positive")]        // Happy path
[Trait("Type", "Negative")]        // Error/edge cases
[Trait("Type", "Security")]        // Auth/authz tests
[Trait("Type", "Performance")]     // Load/perf tests
```

**Run categories selectively:**
```bash
dotnet test --filter "Category=Unit"           # CI fast gate
dotnet test --filter "Category=Integration"    # CI full pipeline
dotnet test --filter "Type=Security"           # Security audit
```

### 4.5 Test Data Builders (Factory Pattern)

```csharp
public static class TestOrderBuilder
{
    public static Order ValidOrder(Action<Order>? customize = null)
    {
        var order = new Order
        {
            Id = 1,
            OrderNumber = "ORD-2024-001",
            CustomerId = 1,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            LineItems = new List<LineItem>
            {
                new() { ProductId = 1, Quantity = 2, UnitPrice = 29.99m }
            }
        };
        customize?.Invoke(order);
        return order;
    }
}
```

### 4.6 Enterprise Testing Checklist

- [ ] Unit tests: ≥80% coverage on Core/domain logic
- [ ] Integration tests: Every API endpoint tested (happy path + error cases)
- [ ] Negative tests: 401, 403, 404, 400 (validation), 429 (rate limit) for every secured endpoint
- [ ] `WebApplicationFactory` with `ConfigureTestServices` for service replacement
- [ ] SQLite in-memory for database tests (not EF InMemory provider)
- [ ] Test authentication helper that generates valid JWT for test client
- [ ] `[Trait]` categories for selective CI/CD test runs
- [ ] Antiforgery tokens handled in tests that need them
- [ ] No test depends on external services (all mocked/faked)

---

## 5. Enterprise Documentation Standards

> **Sources**:
> - [OpenAPI in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-9.0)
> - [Swashbuckle & ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/tutorials/getting-started-with-swashbuckle?view=aspnetcore-9.0)

### 5.1 XML Documentation Comments

**Enable in .csproj:**
```xml
<PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);1591</NoWarn> <!-- Suppress missing-XML-comment warnings globally -->
</PropertyGroup>
```

**Standard XML comment template for public APIs:**
```csharp
/// <summary>
/// Creates a new order for the specified customer.
/// </summary>
/// <param name="request">The order creation request containing customer and line item details.</param>
/// <returns>The created order with generated order number.</returns>
/// <remarks>
/// Sample request:
///
///     POST /api/orders
///     {
///        "customerId": 1,
///        "lineItems": [{ "productId": 5, "quantity": 2 }]
///     }
///
/// </remarks>
/// <response code="201">Order created successfully.</response>
/// <response code="400">Validation failed — see response body for details.</response>
/// <response code="401">Authentication required.</response>
/// <exception cref="InvalidOperationException">Thrown when the customer has no active account.</exception>
[HttpPost]
[ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
```

**What to document:**
- All public types and members in Core and Web projects
- `<summary>` — what it does (imperative mood)
- `<param>` — each parameter's purpose and constraints
- `<returns>` — what's returned and when
- `<remarks>` — sample requests, edge cases, usage notes
- `<response code>` — every possible HTTP status code
- `<exception>` — thrown exceptions and conditions

### 5.2 OpenAPI / Swagger (.NET 9 Built-in)

```csharp
// .NET 9 built-in OpenAPI (replaces Swashbuckle as default)
builder.Services.AddOpenApi();

// In pipeline (Development only)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                    // Serves /openapi/v1.json
    // Optionally add Swagger UI for interactive testing:
    // app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "v1"));
}
```

**Build-time document generation (CI/CD):**
```xml
<PropertyGroup>
    <OpenApiGenerateDocuments>true</OpenApiGenerateDocuments>
    <OpenApiDocumentsDirectory>$(MSBuildProjectDirectory)</OpenApiDocumentsDirectory>
</PropertyGroup>

<ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="9.0.*" />
    <PackageReference Include="Microsoft.Extensions.ApiDescription.Server" Version="9.0.*">
        <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
        <PrivateAssets>all</PrivateAssets>
    </PackageReference>
</ItemGroup>
```

**Checklist:**
- [ ] Use `.NET 9 built-in OpenAPI` (`AddOpenApi()` + `MapOpenApi()`) instead of Swashbuckle
- [ ] OpenAPI endpoint restricted to Development environment
- [ ] Build-time generation enabled for CI/CD validation (`OpenApiGenerateDocuments`)
- [ ] Every endpoint has `[ProducesResponseType]` for all possible status codes
- [ ] `[Produces("application/json")]` on controllers/endpoints
- [ ] Data annotations (`[Required]`, `[StringLength]`, `[Range]`) drive schema generation
- [ ] XML comments included in OpenAPI doc generation

### 5.3 Architecture Decision Records (ADRs)

Store in `docs/adr/` directory:

```
docs/adr/
├── 0001-use-clean-architecture.md
├── 0002-jwt-bearer-auth-over-cookie-auth.md
├── 0003-ef-core-repository-pattern.md
├── 0004-wolverine-for-messaging.md
├── 0005-signalr-for-realtime.md
├── 0006-sliding-window-rate-limiting.md
└── template.md
```

**ADR Template:**
```markdown
# ADR-NNNN: [Title]

## Status
Accepted | Superseded | Deprecated

## Context
What is the issue that we're seeing that is motivating this decision?

## Decision
What is the change that we're proposing/deciding?

## Consequences
What becomes easier or more difficult because of this change?

## Alternatives Considered
What other options were evaluated and why were they rejected?

## Date
YYYY-MM-DD
```

**Checklist:**
- [ ] ADR created for every significant architectural decision
- [ ] ADRs stored in version control alongside code
- [ ] ADRs numbered sequentially and never deleted (mark as Superseded)
- [ ] README references ADR directory

---

## 6. Wolverine Messaging Framework

> **Source**: [Wolverine Best Practices](https://wolverine.netlify.app/introduction/best-practices.html)

### 6.1 Core Best Practices

| Practice | Details |
|----------|---------|
| **Prefer pure functions for handlers** | Message handlers should be simple functions taking a message and returning side effects. No ambient state. |
| **Don't abstract Wolverine away** | Don't wrap `IMessageBus` in your own `IMessageBroker`. Use Wolverine directly — it's designed to be the backbone. |
| **Lean on built-in error handling** | Use Wolverine's retry policies, circuit breakers, dead letter queues. Don't write custom try/catch in handlers. |
| **Pre-generate types for production** | Call `opts.CodeGeneration.TypeLoadMode = TypeLoadMode.Static` and run `dotnet run -- codegen write` to avoid JIT overhead at startup. |
| **Make side effects visible** | Return side effects (outgoing messages, DB operations) from handlers instead of executing them imperatively. |
| **Keep call stacks short** | Wolverine uses code generation; deep call stacks reduce its ability to optimize. |
| **Prefer method injection** | Use method-level injection over constructor injection for handler dependencies. |
| **Use vertical slice architecture** | Organize by feature/message type, not by technical concern. |
| **Graceful shutdown** | Configure drain timeout for in-flight messages during deployment. |

### 6.2 Handler Pattern

```csharp
// GOOD: Pure function with returned side effects
public static class CreateOrderHandler
{
    public static (OrderCreated, OutgoingMessages) Handle(CreateOrderCommand command, AppDbContext db)
    {
        var order = new Order { /* ... */ };
        db.Orders.Add(order);

        return (
            new OrderCreated(order.Id),
            new OutgoingMessages { new SendOrderConfirmationEmail(order.Id) }
        );
    }
}

// BAD: Imperative, side effects hidden
public class CreateOrderHandler
{
    private readonly IMessageBus _bus;
    private readonly AppDbContext _db;

    public CreateOrderHandler(IMessageBus bus, AppDbContext db)
    {
        _bus = bus;
        _db = db;
    }

    public async Task Handle(CreateOrderCommand command)
    {
        var order = new Order { /* ... */ };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        await _bus.PublishAsync(new OrderCreated(order.Id));         // Hidden side effect
        await _bus.SendAsync(new SendOrderConfirmationEmail(order.Id)); // Hidden side effect
    }
}
```

### 6.3 Error Handling Configuration

```csharp
opts.Handlers.OnException<SqlException>()
    .RetryWithCooldown(50.Milliseconds(), 100.Milliseconds(), 250.Milliseconds())
    .Then.Discard();    // Dead letter after retries

opts.Handlers.OnException<TimeoutException>()
    .RetryOnce()
    .Then.Requeue();
```

### 6.4 Checklist

- [ ] Handlers are pure functions returning side effects
- [ ] `IMessageBus` used directly (no wrapper abstraction)
- [ ] Error handling configured declaratively via Wolverine's policy API
- [ ] `TypeLoadMode.Static` in production + pre-generated code
- [ ] Method injection preferred over constructor injection for handlers
- [ ] Feature/vertical slice organization for message handlers
- [ ] Graceful shutdown configured for deployments

---

## 7. SignalR Security & Scaling

> **Sources**:
> - [SignalR Security](https://learn.microsoft.com/en-us/aspnet/core/signalr/security?view=aspnetcore-9.0)
> - [SignalR Scale](https://learn.microsoft.com/en-us/aspnet/core/signalr/scale?view=aspnetcore-9.0)

### 7.1 Security

**Authentication with JWT for SignalR:**
```csharp
// Client sends token as query string (WebSockets don't support headers)
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/chat", {
        accessTokenFactory: () => getAccessToken()
    })
    .build();
```

**Server-side hub authorization:**
```csharp
[Authorize]
public class ChatHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier; // From ClaimsPrincipal
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        await base.OnConnectedAsync();
    }
}
```

**Security Rules:**
| Rule | Details |
|------|---------|
| **Never expose ConnectionId to other users** | ConnectionId can be used to impersonate — use it only server-side |
| **Don't log access tokens** | Tokens appear in query strings for WebSockets; exclude from logs |
| **Disable `DetailedErrors` in production** | `options.DetailedErrors = false` — prevents stack trace leaks |
| **CORS required for cross-origin** | SignalR won't connect cross-origin without explicit CORS policy |
| **Validate origin for WebSockets** | WebSocket connections bypass CORS; validate `Origin` header manually if needed |
| **Buffer management** | Default 32KB max message size; increase only if needed |

### 7.2 Scaling

**Sticky sessions are REQUIRED** for multi-server deployments:
```nginx
# Nginx example
upstream signalr {
    ip_hash;  # Sticky sessions
    server app1:5000;
    server app2:5000;
}

server {
    location /hubs/ {
        proxy_pass http://signalr;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
```

**Backplane options:**

| Option | Best For | Notes |
|--------|----------|-------|
| **Azure SignalR Service** | Azure deployments | Fully managed, auto-scaling, recommended for Azure |
| **Redis backplane** | On-premises / multi-cloud | `Microsoft.AspNetCore.SignalR.StackExchangeRedis` |

```csharp
// Redis backplane
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis")!, options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("MyApp");
    });
```

**Scaling Checklist:**
- [ ] Sticky sessions enabled (load balancer)
- [ ] Backplane configured (Redis or Azure SignalR Service) for multi-server
- [ ] WebSocket transport preferred; Long Polling as fallback
- [ ] Nginx/reverse proxy configured for WebSocket upgrade headers
- [ ] Connection lifetime management (reconnection logic on client)
- [ ] Monitor TCP connection count (each SignalR connection = 1 TCP connection)

---

## 8. Observability — Serilog, Health Checks, Metrics

> **Sources**:
> - [ASP.NET Core Logging](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-9.0)
> - [Health Checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-9.0)
> - [Serilog Getting Started](https://github.com/serilog/serilog/wiki/Getting-Started)

### 8.1 Structured Logging with Serilog

**Setup (Program.cs):**
```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("Application", "MyApp")
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .WriteTo.Seq("http://localhost:5341")   // Or Elasticsearch, Datadog, etc.
    .CreateLogger();

builder.Host.UseSerilog();
```

**Request logging middleware (replaces verbose default):**
```csharp
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("UserId", httpContext.User.Identity?.Name ?? "anonymous");
        diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString());
    };
});
```

**Structured logging best practices:**
```csharp
// GOOD: Structured with named properties (searchable)
_logger.LogInformation("Order {OrderId} created for customer {CustomerId}",
    order.Id, order.CustomerId);

// BAD: String interpolation (not searchable)
_logger.LogInformation($"Order {order.Id} created for customer {order.CustomerId}");
```

**Enterprise enrichment checklist:**
- [ ] `Enrich.FromLogContext()` — captures scoped properties
- [ ] `Enrich.WithMachineName()` — identifies server in multi-instance
- [ ] `Enrich.WithEnvironmentName()` — distinguishes dev/staging/prod
- [ ] `Enrich.WithProperty("Application", ...)` — correlates across services
- [ ] `UseSerilogRequestLogging()` — single log per request (not 10+ default lines)
- [ ] Override `Microsoft.*` to `Warning` — suppress framework noise
- [ ] Structured JSON formatter for production sinks
- [ ] **Never log PII, tokens, passwords, connection strings**

### 8.2 Log Levels Guide

| Level | Use For | Example |
|-------|---------|---------|
| **Fatal** | App crash, unrecoverable | Unhandled exception, DB permanently unreachable |
| **Error** | Operation failed, needs attention | Payment processing failed, external API down |
| **Warning** | Unexpected but handled | Retry succeeded, approaching rate limit |
| **Information** | Business events, flow milestones | Order created, user logged in, job completed |
| **Debug** | Developer diagnostics | Query parameters, cache hit/miss |
| **Verbose** | Trace-level, very noisy | Method entry/exit, raw payloads |

**Production**: Set minimum level to `Information`. Override `Microsoft.*` to `Warning`.

### 8.3 Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(
        name: "database",
        tags: new[] { "ready" })
    .AddCheck<RedisHealthCheck>(
        "redis",
        tags: new[] { "ready" })
    .AddCheck("self",
        () => HealthCheckResult.Healthy(),
        tags: new[] { "live" });

// Map endpoints with tag filtering
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthCheckResponse
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = WriteHealthCheckResponse
});
```

**Kubernetes probe mapping:**
| Probe | Endpoint | What It Checks |
|-------|----------|---------------|
| **Liveness** | `/health/live` | App process is running (self-check only) |
| **Readiness** | `/health/ready` | App can serve traffic (DB, Redis, dependencies) |
| **Startup** | `/health/ready` | Same as readiness, with longer initial delay |

**Custom JSON response writer:**
```csharp
private static Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    var result = JsonSerializer.Serialize(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description,
            duration = e.Value.Duration.TotalMilliseconds
        }),
        totalDuration = report.TotalDuration.TotalMilliseconds
    });
    return context.Response.WriteAsync(result);
}
```

### 8.4 Metrics with OpenTelemetry

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddRuntimeInstrumentation()
               .AddMeter("MyApp.Orders")   // Custom meter
               .AddPrometheusExporter();    // Or OTLP
    })
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddEntityFrameworkCoreInstrumentation()
               .AddSource("MyApp")         // Custom activity source
               .AddOtlpExporter();
    });

app.MapPrometheusScrapingEndpoint(); // /metrics
```

**Custom business metrics:**
```csharp
public class OrderMetrics
{
    private readonly Counter<long> _ordersCreated;
    private readonly Histogram<double> _orderProcessingDuration;

    public OrderMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("MyApp.Orders");
        _ordersCreated = meter.CreateCounter<long>("orders.created", "orders");
        _orderProcessingDuration = meter.CreateHistogram<double>(
            "orders.processing.duration", "ms");
    }

    public void RecordOrderCreated() => _ordersCreated.Add(1);
    public void RecordProcessingDuration(double ms) => _orderProcessingDuration.Record(ms);
}
```

### 8.5 Observability Checklist

- [ ] Serilog configured with structured JSON output
- [ ] Request logging via `UseSerilogRequestLogging()` (single log per request)
- [ ] Microsoft framework logs overridden to Warning
- [ ] Environment, machine, application enrichers configured
- [ ] Centralized log aggregation (Seq, ELK, Datadog, etc.)
- [ ] Health check endpoints: `/health/live` and `/health/ready`
- [ ] Readiness checks include all critical dependencies (DB, Redis, external APIs)
- [ ] Liveness check is self-check only (no dependency checks)
- [ ] Health check JSON response writer for structured output
- [ ] OpenTelemetry metrics + tracing configured
- [ ] Custom business metrics for key operations
- [ ] Prometheus or OTLP exporter configured
- [ ] No PII in logs or metrics

---

## Master Checklist Summary

Use this for gap analysis against your codebase:

### Architecture
- [ ] Clean Architecture 3-project structure with correct dependency direction
- [ ] No business logic in Controllers
- [ ] All external dependencies behind interfaces

### Security
- [ ] JWT Bearer with all validations enabled + reduced ClockSkew
- [ ] Secrets in Key Vault (not appsettings)
- [ ] FallbackPolicy requires authentication
- [ ] CORS with explicit origins (no `AllowAnyOrigin` in prod)
- [ ] Rate limiting on all endpoints (stricter on auth)
- [ ] Security headers (HSTS, CSP, X-Content-Type-Options, X-Frame-Options)
- [ ] HTTPS enforced

### Data Access
- [ ] Default `NoTracking` behavior
- [ ] Keyset pagination for large datasets
- [ ] Indexes on frequently queried columns
- [ ] Async everywhere
- [ ] Retry on transient failures
- [ ] Migrations reviewed in PRs

### Testing
- [ ] Unit + Integration + Negative test coverage
- [ ] WebApplicationFactory with SQLite in-memory
- [ ] Test categories for selective CI runs
- [ ] Every endpoint tested for 401/403/400/404

### Documentation
- [ ] XML docs on all public APIs
- [ ] OpenAPI with build-time generation
- [ ] ProducesResponseType on every endpoint
- [ ] ADRs for architectural decisions

### Messaging (Wolverine)
- [ ] Pure function handlers
- [ ] Built-in error handling (no manual try/catch)
- [ ] Pre-generated types for production
- [ ] Method injection in handlers

### Real-Time (SignalR)
- [ ] Hub authorized
- [ ] ConnectionId never exposed to clients
- [ ] Sticky sessions for multi-server
- [ ] Redis/Azure backplane configured
- [ ] DetailedErrors disabled in production

### Observability
- [ ] Structured logging with Serilog
- [ ] Centralized log aggregation
- [ ] Health checks (liveness + readiness)
- [ ] OpenTelemetry metrics + tracing
- [ ] No PII in logs

---

> **Document generated**: March 2026
> **Target framework**: .NET 9 / ASP.NET Core 9
> **All sources**: Official Microsoft documentation + framework-specific official docs (see per-section links)
