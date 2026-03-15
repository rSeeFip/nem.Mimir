using System.Text.RegularExpressions;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Infrastructure.Services;

internal sealed partial class SystemPromptService : ISystemPromptService
{
    public string RenderTemplate(string template, IDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template))
            return string.Empty;

        if (variables is null || variables.Count == 0)
            return template;

        return TemplateVariableRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value.Trim();
            return variables.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    [GeneratedRegex(@"\{\{(\s*\w+\s*)\}\}")]
    private static partial Regex TemplateVariableRegex();
}
