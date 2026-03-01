# Compliance & Licensing Audit

**Date**: 2026-03-01
**Status**: ✅ PASSED

## Dependencies Summary

### Mimir.Api
| Package | License | Status |
|---------|---------|--------|
| Microsoft.AspNetCore.Authentication.JwtBearer | MIT | ✅ |
| Serilog.AspNetCore | Apache-2.0 | ✅ |
| Swashbuckle.AspNetCore | BSD-3-Clause | ✅ |
| AspNetCore.HealthChecks.NpgSql | Apache-2.0 | ✅ |
| AspNetCore.HealthChecks.Uris | Apache-2.0 | ✅ |

### Mimir.Application
| Package | License | Status |
|---------|---------|--------|
| MediatR | Apache-2.0 | ✅ |
| AutoMapper | MIT | ✅ |
| FluentValidation | Apache-2.0 | ✅ |
| FluentValidation.DependencyInjectionExtensions | Apache-2.0 | ✅ |

### Mimir.Infrastructure
| Package | License | Status |
|---------|---------|--------|
| Microsoft.EntityFrameworkCore | MIT | ✅ |
| Microsoft.EntityFrameworkCore.Relational | MIT | ✅ |
| Npgsql.EntityFrameworkCore.PostgreSQL | PostgreSQL | ✅ |
| Microsoft.Extensions.Http.Resilience | MIT | ✅ |
| Docker.DotNet.Enhanced | MIT | ✅ |

### Mimir.Sync
| Package | License | Status |
|---------|---------|--------|
| WolverineFx | MIT | ✅ |
| WolverineFx.RabbitMQ | MIT | ✅ |

## Security Notes

- All packages are from trusted sources (NuGet.org)
- No known vulnerabilities in current versions
- All packages have permissive licenses (MIT, Apache-2.0, BSD)
- No GPL/AGPL dependencies

## Recommendations

1. Keep packages updated via regular NuGet updates
2. Consider adding Dependabot for automated updates
3. Run `dotnet audit` periodically for vulnerability scanning
