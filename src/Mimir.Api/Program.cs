using System.Security.Claims;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using Mimir.Api.Configuration;
using Mimir.Api.Middleware;
using Mimir.Api.Services;
using Mimir.Application;
using Mimir.Application.Common.Interfaces;
using Mimir.Infrastructure;
using Serilog;
using Mimir.Api.Hubs;
using Mimir.Sync.Configuration;
using Wolverine;
using nem.Contracts.AspNetCore.Auth;
using nem.Contracts.AspNetCore.Secrets;

// Bootstrap logger for startup logging (before host is built)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

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

    // ── Authentication (Keycloak OIDC via shared nem.Contracts.AspNetCore) ───
    builder.Services.AddNemKeycloakAuth(builder.Configuration);

    // ── Secrets Management (OpenBao via shared nem.Contracts.AspNetCore) ──────
    builder.Services.AddNemSecrets(builder.Configuration);

    // ── Authorization ────────────────────────────────────────────────────────
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("RequireAdmin", policy => policy.RequireRole("admin"));
        options.AddPolicy("RequireUser", policy => policy.RequireRole("user", "admin"));
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
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("per-user", context =>
        {
            var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
            return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
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

    app.MapControllers().RequireRateLimiting("per-user");
    app.MapHub<ChatHub>("/hubs/chat").RequireRateLimiting("per-user");
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        AllowCachingResponses = false
    }).AllowAnonymous();

    app.Run();
}
catch (Exception ex) // Intentional catch-all: top-level application entry point; logs fatal startup/runtime errors
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// Entry point class for the Mimir API. Made partial and public for integration testing.
/// </summary>
public partial class Program;
