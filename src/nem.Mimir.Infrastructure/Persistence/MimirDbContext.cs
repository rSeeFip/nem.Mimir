namespace nem.Mimir.Infrastructure.Persistence;

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.McpServers;

public class MimirDbContext(DbContextOptions<MimirDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<SystemPrompt> SystemPrompts => Set<SystemPrompt>();
    public DbSet<McpServerConfig> McpServerConfigs => Set<McpServerConfig>();
    public DbSet<McpToolWhitelist> McpToolWhitelists => Set<McpToolWhitelist>();
    public DbSet<McpToolAuditLog> McpToolAuditLogs => Set<McpToolAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
