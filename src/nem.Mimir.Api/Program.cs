using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using nem.Mimir.Api.Configuration;
using nem.Mimir.Api.Middleware;
using nem.Mimir.Api.Services;
using nem.Mimir.Api.Telemetry;
using nem.Mimir.Application;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Infrastructure;
using Serilog;
using nem.Mimir.Api.Hubs;
using nem.Mimir.Sync.Configuration;
using Wolverine;
using nem.Mimir.Infrastructure.Serialization;
using nem.Mimir.Infrastructure.LiteLlm;
using nem.Mimir.Api.Swagger;
using nem.Mimir.Application.ChannelEvents;
using nem.Contracts.AspNetCore.Auth;
using nem.Contracts.AspNetCore.Classification;
using nem.Contracts.AspNetCore.DataRetention;
using nem.Contracts.AspNetCore.Cors;
using nem.Contracts.AspNetCore.Api;

// Bootstrap logger for startup logging (before host is built)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Mimir API");

    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddNemDevModeGuardrails(builder.Environment);
    builder.Services.AddNemDataRetention(builder.Configuration);

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

    var effectiveRequireHttpsMetadata = jwtSettings.RequireHttpsMetadata || !builder.Environment.IsDevelopment();
    var attemptedDisableHttpsMetadataInNonDevelopment = !jwtSettings.RequireHttpsMetadata && !builder.Environment.IsDevelopment();

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
            options.RequireHttpsMetadata = effectiveRequireHttpsMetadata;

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

    if (attemptedDisableHttpsMetadataInNonDevelopment)
    {
        Log.Warning(
            "Jwt:RequireHttpsMetadata=false was requested via configuration but ignored in {EnvironmentName}. HTTPS metadata is forced outside Development.",
            builder.Environment.EnvironmentName);
    }

    // ── Authorization ────────────────────────────────────────────────────────
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("RequireAdmin", policy => policy.RequireRole("admin"));
        options.AddPolicy("RequireUser", policy => policy.RequireRole("user", "admin"));
    });
    builder.Services.AddServiceAuthorization();

    builder.Services.AddNemCors(builder.Configuration, NemCorsProfile.NemTrusted);

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
    builder.Services.AddNemApiDocs(builder.Configuration);
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
    builder.Services.AddTransient<LiteLlmClassificationInterceptor>();
    builder.Services.AddTransient<ClassificationGatingHandler>();
    var classificationOptions = builder.Configuration
        .GetSection(ClassificationOptions.SectionName)
        .Get<ClassificationOptions>() ?? new ClassificationOptions();
    builder.Services.Configure<ClassificationGatingOptions>(options =>
    {
        options.InternalHosts.UnionWith(classificationOptions.InternalHosts);
    });
    builder.Services.AddHttpClient("LiteLlm")
        .AddHttpMessageHandler<LiteLlmClassificationInterceptor>()
        .AddHttpMessageHandler<ClassificationGatingHandler>();

    // ── Channel Event Routing ────────────────────────────────────────────────
    builder.Services.AddSingleton<ChannelEventRouter>();

    // ── WebWidget Channel Adapter ────────────────────────────────────────────
    builder.Services.AddSingleton<WebWidgetChannelAdapter>();
    builder.Services.AddSingleton<nem.Contracts.Channels.IChannelEventSource>(sp => sp.GetRequiredService<WebWidgetChannelAdapter>());
    builder.Services.AddSingleton<nem.Contracts.Channels.IChannelMessageSender>(sp => sp.GetRequiredService<WebWidgetChannelAdapter>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<WebWidgetChannelAdapter>());

    // ── Canvas Renderers ───────────────────────────────────────────────────────
    builder.Services.AddSingleton<nem.Contracts.Canvas.ICanvasRenderer, nem.Mimir.Api.Canvas.WebCanvasRenderer>();

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

    app.MapNemApiDocs();

    app.UseCors(NemCorsExtensions.PolicyName);

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
