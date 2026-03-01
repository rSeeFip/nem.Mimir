using FluentValidation;
using MediatR;
using Mimir.Application.Common.Interfaces;
using Mimir.Domain.Plugins;

namespace Mimir.Application.Plugins.Commands;

/// <summary>
/// Command to execute a loaded plugin with the specified parameters.
/// </summary>
/// <param name="PluginId">The unique identifier of the plugin to execute.</param>
/// <param name="UserId">The identifier of the user executing the plugin.</param>
/// <param name="Parameters">A dictionary of parameters to pass to the plugin.</param>
public sealed record ExecutePluginCommand(
    string PluginId,
    string UserId,
    Dictionary<string, object> Parameters) : ICommand<PluginResult>;

/// <summary>
/// Validates the <see cref="ExecutePluginCommand"/> ensuring plugin ID and user ID are provided.
/// </summary>
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
