using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Mimir.Api.Configuration;
using Mimir.Api.Middleware;
using Mimir.Api.Services;
using Mimir.Api.Telemetry;
using Mimir.Application;
using Mimir.Application.Common.Interfaces;
using Mimir.Infrastructure;
using Serilog;
using Mimir.Api.Hubs;
using Mimir.Sync.Configuration;
using Wolverine;
using Mimir.Infrastructure.Serialization;
using Mimir.Api.Swagger;
using Mimir.Application.ChannelEvents;
using nem.Contracts.AspNetCore.Auth;

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
    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
    builder.Services.Configure<LiteLlmSettings>(builder.Configuration.GetSection(LiteLlmSettings.SectionName));
    builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection(DatabaseSettings.SectionName));

    var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
        ?? new JwtSettings();
    var liteLlmSettings = builder.Configuration.GetSection(LiteLlmSettings.SectionName).Get<LiteLlmSettings>()
        ?? new LiteLlmSettings();

    // ── Authentication (JWT Bearer via Keycloak) ─────────────────────────────
    builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.Authority = jwtSettings.Authority;
            options.Audience = jwtSettings.Audience;
            options.RequireHttpsMetadata = jwtSettings.RequireHttpsMetadata;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                NameClaimType = ClaimTypes.NameIdentifier,
                RoleClaimType = ClaimTypes.Role
            };

            // Map Keycloak realm_access.roles to standard role claims
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    MapKeycloakRoleClaims(context);
                    return Task.CompletedTask;
                },
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;

                    if (!string.IsNullOrEmpty(accessToken) &&
                        path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });

    // ── Authorization ────────────────────────────────────────────────────────
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("RequireAdmin", policy => policy.RequireRole("admin"));
        options.AddPolicy("RequireUser", policy => policy.RequireRole("user", "admin"));
    });
    builder.Services.AddServiceAuthorization();

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

        options.SchemaFilter<TypedIdSchemaFilter>();
    });

    // ── Application & Infrastructure Services ────────────────────────────────
    builder.Services.AddNemTelemetry();
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);

    // ── Channel Event Routing ────────────────────────────────────────────────
    builder.Services.AddSingleton<ChannelEventRouter>();

    // ── API Services ─────────────────────────────────────────────────────────
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
    builder.Services.AddMemoryCache();
    builder.Services.AddControllers()
        .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new TypedIdJsonConverterFactory()));
    builder.Services.AddSignalR(options =>
    {
        options.KeepAliveInterval = TimeSpan.FromSeconds(30);
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
        options.MaximumReceiveMessageSize = 256 * 1024; // 256 KB — prevent DoS via large messages
    })
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new TypedIdJsonConverterFactory()));

    var app = builder.Build();

    // ── Middleware Pipeline (order matters) ───────────────────────────────────
    app.UseSerilogRequestLogging();
    app.UseCorrelationId();
    app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
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
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// Maps Keycloak realm_access.roles claim to standard ClaimTypes.Role claims.
/// </summary>
static void MapKeycloakRoleClaims(TokenValidatedContext context)
{
    if (context.Principal is null)
        return;

    var realmAccessClaim = context.Principal.FindFirst("realm_access");
    if (realmAccessClaim is null)
        return;

    try
    {
        using var doc = System.Text.Json.JsonDocument.Parse(realmAccessClaim.Value);
        if (doc.RootElement.TryGetProperty("roles", out var roles) &&
            roles.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            var identity = context.Principal.Identity as ClaimsIdentity;
            foreach (var role in roles.EnumerateArray())
            {
                var roleValue = role.GetString();
                if (!string.IsNullOrWhiteSpace(roleValue))
                {
                    identity?.AddClaim(new Claim(ClaimTypes.Role, roleValue));
                }
            }
        }
    }
    catch (System.Text.Json.JsonException)
    {
        // Malformed realm_access claim — skip
    }
}

// Make the implicit Program class public so test projects can access it via WebApplicationFactory
/// <summary>
/// Entry point class for the Mimir API. Made partial and public for integration testing.
/// </summary>
public partial class Program;
