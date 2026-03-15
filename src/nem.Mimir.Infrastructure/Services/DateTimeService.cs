using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Infrastructure.Services;

internal sealed class DateTimeService : IDateTimeService
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
