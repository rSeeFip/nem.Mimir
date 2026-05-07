namespace nem.Mimir.Infrastructure.Persistence;

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.McpServers;
using nem.Mimir.Domain.MultiTenancy;

public class MimirDbContext(DbContextOptions<MimirDbContext> options, ITenantContext? tenantContext = null) : DbContext(options)
{
    private readonly ITenantContext _tenantContext = tenantContext ?? EmptyTenantContext.Instance;

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

        modelBuilder.Entity<Conversation>()
            .HasQueryFilter(c => !c.IsDeleted && c.TenantId == CurrentTenantId);

        modelBuilder.Entity<Message>()
            .HasQueryFilter(m => m.TenantId == CurrentTenantId);

        modelBuilder.Entity<McpServerConfig>()
            .HasQueryFilter(c => c.TenantId == CurrentTenantId);
    }

    private string CurrentTenantId => _tenantContext.TenantId ?? string.Empty;

    private sealed class EmptyTenantContext : ITenantContext
    {
        public static readonly EmptyTenantContext Instance = new();

        public string? TenantId => "default";

        public string? TenantName => "Default";
    }
}
