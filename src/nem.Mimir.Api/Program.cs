using System.Security.Claims;
using System.Globalization;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using nem.Mimir.Domain.MultiTenancy;
using Microsoft.OpenApi;
using nem.Mimir.Api.Configuration;
using nem.Mimir.Api.Middleware;
using nem.Mimir.Api.Services;
using nem.Mimir.Application;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Infrastructure;
using nem.Mimir.Infrastructure.Organism;
using Serilog;
using nem.Mimir.Api.Hubs;
using nem.Mimir.Sync.Configuration;
using Wolverine;
using nem.Mimir.Api.Authentication;
using nem.Mimir.Domain.MultiTenancy;
using nem.Contracts.AspNetCore.Secrets;
using nem.Mimir.Infrastructure.MultiTenancy;

// Bootstrap logger for startup logging (before host is built)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Mimir API");

    var builder = WebApplication.CreateBuilder(args);

    // ── Kestrel Request Body Size Limits ────────────────────────────────────
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB global limit
    });

    // ── Serilog ──────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

    // ── Wolverine Messaging ────────────────────────────────────────────────
    builder.UseWolverine(opts =>
    {
        opts.AddMimirMessaging(builder.Configuration);
    });

    // ── Settings Binding ─────────────────────────────────────────────────────
    builder.Services.Configure<LiteLlmSettings>(builder.Configuration.GetSection(LiteLlmSettings.SectionName));
    builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection(DatabaseSettings.SectionName));

    var liteLlmSettings = builder.Configuration.GetSection(LiteLlmSettings.SectionName).Get<LiteLlmSettings>()
        ?? new LiteLlmSettings();

    // ── Options Binding ────────────────────────────────────────────────────
    var mimirApiOptions = builder.Configuration.GetSection(MimirApiOptions.SectionName).Get<MimirApiOptions>()
        ?? new MimirApiOptions();

    // ── Authentication ───────────────────────────────────────────────────────
    // StandaloneMode: auto-authenticates as dev admin (no Keycloak required)
    // Otherwise: Keycloak OIDC via shared nem.Contracts.AspNetCore
    builder.Services.AddMimirAuthentication(builder.Configuration, builder.Environment, mimirApiOptions.StandaloneMode);

    // ── Secrets Management (OpenBao via shared nem.Contracts.AspNetCore) ──────
    builder.Services.AddNemSecrets(builder.Configuration);

    // ── Authorization ────────────────────────────────────────────────────────
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("platform-admin", policy => policy.RequireRole("platform-admin", "admin"));
        options.AddPolicy("tenant-admin", policy => policy.RequireRole("tenant-admin", "platform-admin", "admin"));
        options.AddPolicy("user", policy => policy.RequireRole("user", "tenant-admin", "platform-admin", "admin"));
        options.AddPolicy("RequireAdmin", policy => policy.RequireRole("tenant-admin", "platform-admin", "admin"));
        options.AddPolicy("RequireUser", policy => policy.RequireRole("user", "tenant-admin", "platform-admin", "admin"));
    });

    builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters.NameClaimType = ClaimTypes.NameIdentifier;
        options.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;
    });

    // ── CORS ─────────────────────────────────────────────────────────────────
    var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? ["http://localhost:3000"];

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.WithOrigins(corsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    // ── Health Checks ────────────────────────────────────────────────────────
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? builder.Configuration.GetSection("Database:ConnectionString").Value
        ?? string.Empty;

    var healthChecksBuilder = builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "database");

    var liteLlmBaseUrl = liteLlmSettings.BaseUrl;
    if (!string.IsNullOrWhiteSpace(liteLlmBaseUrl))
    {
        healthChecksBuilder.AddUrlGroup(
            new Uri(liteLlmBaseUrl + "/health"),
            name: "litellm",
            timeout: TimeSpan.FromSeconds(5));
    }

    // ── Rate Limiting ────────────────────────────────────────────────────────
    var tenantPermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:PerTenant:PermitLimit") ?? 100;
    var tenantWindowSeconds = builder.Configuration.GetValue<int?>("RateLimiting:PerTenant:WindowSeconds") ?? 60;
    var tenantSegmentsPerWindow = builder.Configuration.GetValue<int?>("RateLimiting:PerTenant:SegmentsPerWindow") ?? 6;

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.OnRejected = async (rateLimitContext, cancellationToken) =>
        {
            if (rateLimitContext.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                rateLimitContext.HttpContext.Response.Headers["Retry-After"] =
                    retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            }

            if (!rateLimitContext.HttpContext.Response.HasStarted)
            {
                await rateLimitContext.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too Many Requests",
                    Detail = "Rate limit exceeded. Retry the request after the indicated delay.",
                }, cancellationToken);
            }
        };

        options.AddPolicy("per-tenant", context =>
        {
            var tenantId = context.RequestServices.GetRequiredService<ITenantContext>().TenantId;

            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                return RateLimitPartition.GetSlidingWindowLimiter(tenantId, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = tenantPermitLimit,
                    Window = TimeSpan.FromSeconds(tenantWindowSeconds),
                    SegmentsPerWindow = tenantSegmentsPerWindow,
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true,
                });
            }

            var remoteIpAddress = context.Connection.RemoteIpAddress?.ToString();
            var partitionKey = string.IsNullOrWhiteSpace(remoteIpAddress)
                ? "ip:unknown"
                : $"ip:{remoteIpAddress}";

            return RateLimitPartition.GetSlidingWindowLimiter(partitionKey, _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = tenantPermitLimit,
                Window = TimeSpan.FromSeconds(tenantWindowSeconds),
                SegmentsPerWindow = tenantSegmentsPerWindow,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true,
            });
        });
    });



    // ── Swagger ──────────────────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Mimir API",
            Version = "v1",
            Description = "AI-powered conversational assistant API"
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });

        options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecuritySchemeReference("Bearer", null),
                new List<string>()
            }
        });
    });

    // ── Application & Infrastructure Services ────────────────────────────────
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);
    builder.Services.AddMimirOrganism();

    // ── ProblemDetails (RFC 7807) ─────────────────────────────────────────────
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // ── API Services ─────────────────────────────────────────────────────────
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
    builder.Services.AddMemoryCache();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();
    builder.Services.AddControllers();
    builder.Services.AddSignalR(options =>
    {
        options.KeepAliveInterval = TimeSpan.FromSeconds(30);
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
        options.MaximumReceiveMessageSize = 256 * 1024; // 256 KB — prevent DoS via large messages
    });

    var app = builder.Build();

    // ── Middleware Pipeline (order matters) ───────────────────────────────────
    app.UseSerilogRequestLogging();
    app.UseCorrelationId();
    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseMiddleware<OutputSanitizationMiddleware>();
    app.UseMiddleware<TenantContextMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors("AllowFrontend");

    // ── Security Headers (HSTS, CSP) ───────────────────────────────────────────
    if (!app.Environment.IsDevelopment())
    {
        // HSTS: Force HTTPS in production
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    // Add security headers middleware
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        
        // Content Security Policy — strict policy for API-only service
        // No unsafe-inline/unsafe-eval needed since this is a REST/SSE/SignalR API, not serving HTML pages
        context.Response.Headers.Append(
            "Content-Security-Policy",
            "default-src 'none'; " +
            "connect-src 'self' ws: wss:; " +
            "frame-ancestors 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'"
        );
        
        await next();
    });

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapControllers().RequireRateLimiting("per-tenant");
    app.MapHub<ChatHub>("/hubs/chat").RequireRateLimiting("per-tenant");
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        AllowCachingResponses = false
    }).AllowAnonymous();

    app.Run();
}
catch (HostAbortedException)
{
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// Entry point class for the Mimir API. Made partial and public for integration testing.
/// </summary>
public partial class Program;
