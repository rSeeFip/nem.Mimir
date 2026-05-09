using System.Text.Json;

namespace nem.Mimir.Infrastructure.Plugins.BuiltIn;

internal sealed record SkillToolDescriptorDto(
    string Name,
    string Description,
    JsonElement InputSchema,
    JsonElement OutputSchema);
