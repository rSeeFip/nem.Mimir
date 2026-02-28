using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
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

// Bootstrap logger for startup logging (before host is built)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Mimir API");

    var builder = WebApplication.CreateBuilder(args);

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

    // ── CORS ─────────────────────────────────────────────────────────────────
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.WithOrigins("http://localhost:3000", "http://localhost:4200")
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

    // ── API Services ─────────────────────────────────────────────────────────
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
    builder.Services.AddMemoryCache();
    builder.Services.AddControllers();
    builder.Services.AddSignalR(options =>
    {
        options.KeepAliveInterval = TimeSpan.FromSeconds(30);
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
        options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
    });

    var app = builder.Build();

    // ── Middleware Pipeline (order matters) ───────────────────────────────────
    app.UseSerilogRequestLogging();
    app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    app.UseMiddleware<OutputSanitizationMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors("AllowFrontend");
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapControllers();
    app.MapHub<ChatHub>("/hubs/chat");
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
