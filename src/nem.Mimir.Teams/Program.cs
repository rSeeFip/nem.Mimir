using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using nem.Mimir.Teams.Configuration;
using nem.Mimir.Teams.Health;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TeamsSettings>(
    builder.Configuration.GetSection(TeamsSettings.SectionName));

builder.Services.AddHealthChecks()
    .AddCheck<TeamsHealthCheck>("teams", tags: ["ready"]);

var app = builder.Build();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.MapHealthChecks("/health/teams", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

await app.RunAsync();
