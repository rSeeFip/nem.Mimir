namespace Mimir.Infrastructure.Persistence;

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Mimir.Domain.Entities;

public class MimirDbContext(DbContextOptions<MimirDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<SystemPrompt> SystemPrompts => Set<SystemPrompt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
