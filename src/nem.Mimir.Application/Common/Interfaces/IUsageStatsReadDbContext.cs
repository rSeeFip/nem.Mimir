using nem.Mimir.Domain.Entities;

namespace nem.Mimir.Application.Common.Interfaces;

public interface IUsageStatsReadDbContext
{
    IQueryable<User> Users { get; }
    IQueryable<Conversation> Conversations { get; }
    IQueryable<Message> Messages { get; }
}
