namespace nem.Mimir.Infrastructure.Persistence;

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using nem.Mimir.Domain.Common;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Infrastructure.Identity;
using nem.Mimir.Infrastructure.Persistence.Converters;

public class MimirDbContext(DbContextOptions<MimirDbContext> options) : DbContext(options), IUsageStatsReadDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<ChannelMessage> ChannelMessages => Set<ChannelMessage>();
    public DbSet<ChannelMember> ChannelMembers => Set<ChannelMember>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<NoteVersion> NoteVersions => Set<NoteVersion>();
    public DbSet<NoteCollaborator> NoteCollaborators => Set<NoteCollaborator>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();
    public DbSet<LeaderboardEntry> LeaderboardEntries => Set<LeaderboardEntry>();
    public DbSet<EvaluationFeedback> EvaluationFeedbacks => Set<EvaluationFeedback>();
    public DbSet<ImageGeneration> ImageGenerations => Set<ImageGeneration>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<AgentMessage> AgentMessages => Set<AgentMessage>();
    public DbSet<ChannelEvent> ChannelEvents => Set<ChannelEvent>();
    public DbSet<BackgroundTask> BackgroundTasks => Set<BackgroundTask>();
    public DbSet<SystemPrompt> SystemPrompts => Set<SystemPrompt>();
    public DbSet<PromptTemplate> PromptTemplates => Set<PromptTemplate>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<ModelProfile> ModelProfiles => Set<ModelProfile>();
    public DbSet<ArenaConfig> ArenaConfigs => Set<ArenaConfig>();
    public DbSet<UserMemory> UserMemories => Set<UserMemory>();
    public DbSet<KnowledgeCollection> KnowledgeCollections => Set<KnowledgeCollection>();
    public DbSet<ActorIdentityDocument> ActorIdentities => Set<ActorIdentityDocument>();
    public DbSet<ChannelIdentityLinkDocument> ChannelIdentityLinks => Set<ChannelIdentityLinkDocument>();

    IQueryable<User> IUsageStatsReadDbContext.Users => Users;
    IQueryable<Conversation> IUsageStatsReadDbContext.Conversations => Conversations;
    IQueryable<Message> IUsageStatsReadDbContext.Messages => Messages;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        foreach (var (idType, valueType) in typeof(ITypedId<>).Assembly.GetTypedIds())
        {
            var converterType = typeof(TypedIdValueConverter<,>).MakeGenericType(idType, valueType);
            configurationBuilder.Properties(idType).HaveConversion(converterType);
        }

        foreach (var (idType, valueType) in typeof(nem.Contracts.Identity.ITypedId<>).Assembly.GetTypedIds())
        {
            var converterType = typeof(ContractsTypedIdValueConverter<,>).MakeGenericType(idType, valueType);
            configurationBuilder.Properties(idType).HaveConversion(converterType);
        }
    }
}
