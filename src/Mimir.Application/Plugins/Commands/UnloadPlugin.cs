using FluentValidation;
using MediatR;
using Mimir.Application.Common.Interfaces;

namespace Mimir.Application.Plugins.Commands;

public sealed record UnloadPluginCommand(string PluginId) : ICommand;

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
