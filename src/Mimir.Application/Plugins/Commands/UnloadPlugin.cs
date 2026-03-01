using FluentValidation;
using MediatR;
using Mimir.Application.Common.Interfaces;

namespace Mimir.Application.Plugins.Commands;

/// <summary>
/// Command to unload a currently loaded plugin.
/// </summary>
/// <param name="PluginId">The unique identifier of the plugin to unload.</param>
public sealed record UnloadPluginCommand(string PluginId) : ICommand;

/// <summary>
/// Validates the <see cref="UnloadPluginCommand"/> ensuring the plugin ID is provided.
/// </summary>
public sealed class UnloadPluginCommandValidator : AbstractValidator<UnloadPluginCommand>
{
    public UnloadPluginCommandValidator()
    {
        RuleFor(x => x.PluginId)
            .NotEmpty().WithMessage("Plugin ID is required.");
    }
}

internal sealed class UnloadPluginCommandHandler : IRequestHandler<UnloadPluginCommand>
{
    private readonly IPluginService _pluginService;

    public UnloadPluginCommandHandler(IPluginService pluginService)
    {
        _pluginService = pluginService;
    }

    public async Task Handle(UnloadPluginCommand request, CancellationToken cancellationToken)
    {
        await _pluginService.UnloadPluginAsync(request.PluginId, cancellationToken);
    }
}
