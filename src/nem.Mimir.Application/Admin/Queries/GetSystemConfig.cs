using Microsoft.Extensions.Configuration;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Application.Admin.Queries;

public sealed record GetSystemConfigQuery() : IQuery<SystemConfigDto>;

public sealed record SystemConfigDto(
    string DefaultModel,
    int MaxTokensPerRequest,
    bool RagEnabled,
    bool CodeExecutionEnabled,
    int MaxUploadSizeMb,
    IReadOnlyList<string> AllowedFileTypes,
    bool MemoryEnabled,
    bool ArenaEnabled);

internal sealed class GetSystemConfigQueryHandler
{
    private static readonly IReadOnlyList<string> DefaultAllowedFileTypes =
    [
        ".txt",
        ".md",
        ".pdf",
        ".docx",
        ".json",
    ];

    private readonly IConfiguration _configuration;

    public GetSystemConfigQueryHandler(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<SystemConfigDto> Handle(GetSystemConfigQuery request, CancellationToken cancellationToken)
    {
        var section = _configuration.GetSection("Mimir:SystemConfig");
        var allowedFileTypes = ReadAllowedFileTypes(section);

        var response = new SystemConfigDto(
            DefaultModel: section["DefaultModel"] ?? _configuration["Mimir:DefaultModel"] ?? "gpt-4o-mini",
            MaxTokensPerRequest: section.GetValue<int?>("MaxTokensPerRequest") ?? 4096,
            RagEnabled: section.GetValue<bool?>("RagEnabled") ?? true,
            CodeExecutionEnabled: section.GetValue<bool?>("CodeExecutionEnabled") ?? false,
            MaxUploadSizeMb: section.GetValue<int?>("MaxUploadSizeMb") ?? 25,
            AllowedFileTypes: allowedFileTypes,
            MemoryEnabled: section.GetValue<bool?>("MemoryEnabled") ?? true,
            ArenaEnabled: section.GetValue<bool?>("ArenaEnabled") ?? true);

        return Task.FromResult(response);
    }

    private static IReadOnlyList<string> ReadAllowedFileTypes(IConfigurationSection section)
    {
        var valuesFromChildren = section
            .GetSection("AllowedFileTypes")
            .GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (valuesFromChildren.Length > 0)
        {
            return valuesFromChildren;
        }

        var commaSeparated = section["AllowedFileTypes"];
        if (!string.IsNullOrWhiteSpace(commaSeparated))
        {
            var parsed = commaSeparated
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (parsed.Length > 0)
            {
                return parsed;
            }
        }

        return DefaultAllowedFileTypes;
    }
}
