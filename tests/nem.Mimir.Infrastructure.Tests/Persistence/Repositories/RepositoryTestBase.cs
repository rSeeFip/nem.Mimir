namespace nem.Mimir.Infrastructure.Tests.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Infrastructure.Persistence;
using nem.Mimir.Infrastructure.Persistence.Interceptors;
using NSubstitute;
using Testcontainers.PostgreSql;

public abstract class RepositoryTestBase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    protected MimirDbContext Context { get; private set; } = null!;

    private ICurrentUserService _currentUserService = null!;
    private IDateTimeService _dateTimeService = null!;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.UserId.Returns("test-user-id");
        _currentUserService.IsAuthenticated.Returns(true);

        _dateTimeService = Substitute.For<IDateTimeService>();
        _dateTimeService.UtcNow.Returns(new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero));

        Context = CreateContext();
        await Context.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected MimirDbContext CreateContext()
    {
        var interceptor = new AuditableEntityInterceptor(_currentUserService, _dateTimeService);

        var options = new DbContextOptionsBuilder<MimirDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(interceptor)
            .Options;

        return new MimirDbContext(options);
    }
}
