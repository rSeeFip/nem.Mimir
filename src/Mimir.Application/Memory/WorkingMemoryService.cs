using Microsoft.Extensions.Logging;
using Mimir.Application.Common.Interfaces;
using nem.Contracts.Memory;

namespace Mimir.Application.Services.Memory;

public sealed class WorkingMemoryService : PersistentWorkingMemoryService, IWorkingMemory
{
    public WorkingMemoryService(
        IConversationRepository conversationRepository,
        IUnitOfWork unitOfWork,
        ILlmService llmService,
        WorkingMemoryOptions options,
        ILogger<WorkingMemoryService> logger)
        : base(conversationRepository, unitOfWork, llmService, options, logger)
    {
    }
}
