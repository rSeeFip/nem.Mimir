using FluentValidation;
using Microsoft.Extensions.Logging;
using nem.Mimir.Application.Admin.Queries;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Application.Admin.Commands;

public sealed record UpdateSystemConfigCommand(SystemConfigDto Config) : ICommand;

public sealed class UpdateSystemConfigCommandValidator : AbstractValidator<UpdateSystemConfigCommand>
{
    public UpdateSystemConfigCommandValidator()
    {
        RuleFor(x => x.Config)
            .NotNull().WithMessage("Configuration payload is required.");

        RuleFor(x => x.Config.MaxTokensPerRequest)
            .GreaterThan(0).WithMessage("MaxTokensPerRequest must be greater than 0.");

        RuleFor(x => x.Config.MaxUploadSizeMb)
            .GreaterThan(0).WithMessage("MaxUploadSizeMb must be greater than 0.");
    }
}

internal sealed class UpdateSystemConfigCommandHandler
{
    private readonly ILogger<UpdateSystemConfigCommandHandler> _logger;

    public UpdateSystemConfigCommandHandler(ILogger<UpdateSystemConfigCommandHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(UpdateSystemConfigCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "System configuration updated: DefaultModel={DefaultModel}, MaxTokensPerRequest={MaxTokensPerRequest}, RagEnabled={RagEnabled}, CodeExecutionEnabled={CodeExecutionEnabled}, MaxUploadSizeMb={MaxUploadSizeMb}, AllowedFileTypes={AllowedFileTypes}, MemoryEnabled={MemoryEnabled}, ArenaEnabled={ArenaEnabled}",
            request.Config.DefaultModel,
            request.Config.MaxTokensPerRequest,
            request.Config.RagEnabled,
            request.Config.CodeExecutionEnabled,
            request.Config.MaxUploadSizeMb,
            string.Join(',', request.Config.AllowedFileTypes),
            request.Config.MemoryEnabled,
            request.Config.ArenaEnabled);

        return Task.CompletedTask;
    }
}
