using Mimir.Application.Common.Interfaces;

namespace Mimir.Infrastructure.Services;

internal sealed class DateTimeService : IDateTimeService
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
