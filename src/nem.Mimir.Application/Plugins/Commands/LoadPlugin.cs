using FluentValidation;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Domain.Plugins;

namespace nem.Mimir.Application.Plugins.Commands;

/// <summary>
/// Command to load a plugin from the specified assembly path.
/// </summary>
/// <param name="AssemblyPath">The file system path to the plugin assembly.</param>
public sealed record LoadPluginCommand(string AssemblyPath) : ICommand<PluginMetadata>;

/// <summary>
/// Validates the <see cref="LoadPluginCommand"/> ensuring the assembly path is provided.
/// </summary>
public sealed class LoadPluginCommandValidator : AbstractValidator<LoadPluginCommand>
{
    public LoadPluginCommandValidator()
    {
        RuleFor(x => x.AssemblyPath)
            .NotEmpty().WithMessage("Assembly path is required.");
    }
}

internal sealed class LoadPluginCommandHandler
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
