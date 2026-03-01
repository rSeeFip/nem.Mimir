using FluentValidation;
using MediatR;
using Mimir.Application.Common.Interfaces;
using Mimir.Domain.Plugins;

namespace Mimir.Application.Plugins.Commands;

public sealed record LoadPluginCommand(string AssemblyPath) : ICommand<PluginMetadata>;

public sealed class LoadPluginCommandValidator : AbstractValidator<LoadPluginCommand>
{
    public LoadPluginCommandValidator()
    {
        RuleFor(x => x.AssemblyPath)
            .NotEmpty().WithMessage("Assembly path is required.");
    }
}

internal sealed class LoadPluginCommandHandler : IRequestHandler<LoadPluginCommand, PluginMetadata>
{
    private readonly IPluginService _pluginService;

    public LoadPluginCommandHandler(IPluginService pluginService)
    {
        _pluginService = pluginService;
    }

    public async Task<PluginMetadata> Handle(LoadPluginCommand request, CancellationToken cancellationToken)
    {
        return await _pluginService.LoadPluginAsync(request.AssemblyPath, cancellationToken);
    }
}
