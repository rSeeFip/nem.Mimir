using FluentValidation;
using MediatR;
using Mimir.Application.Common.Interfaces;
using Mimir.Domain.Plugins;

namespace Mimir.Application.Plugins.Commands;

public sealed record ExecutePluginCommand(
    string PluginId,
    string UserId,
    Dictionary<string, object> Parameters) : ICommand<PluginResult>;

public sealed class ExecutePluginCommandValidator : AbstractValidator<ExecutePluginCommand>
{
    public ExecutePluginCommandValidator()
    {
        RuleFor(x => x.PluginId)
            .NotEmpty().WithMessage("Plugin ID is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");
    }
}

internal sealed class ExecutePluginCommandHandler : IRequestHandler<ExecutePluginCommand, PluginResult>
{
    private readonly IPluginService _pluginService;

    public ExecutePluginCommandHandler(IPluginService pluginService)
    {
        _pluginService = pluginService;
    }

    public async Task<PluginResult> Handle(ExecutePluginCommand request, CancellationToken cancellationToken)
    {
        var context = PluginContext.Create(request.UserId, request.Parameters);
        return await _pluginService.ExecutePluginAsync(request.PluginId, context, cancellationToken);
    }
}
