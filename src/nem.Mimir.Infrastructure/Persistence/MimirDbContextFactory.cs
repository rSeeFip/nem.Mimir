namespace nem.Mimir.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public sealed class MimirDbContextFactory : IDesignTimeDbContextFactory<MimirDbContext>
{
    public MimirDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MimirDbContext>();

        var connectionString =
            Environment.GetEnvironmentVariable("MIMIR_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=mimir;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString);
        return new MimirDbContext(optionsBuilder.Options);
    }
}
