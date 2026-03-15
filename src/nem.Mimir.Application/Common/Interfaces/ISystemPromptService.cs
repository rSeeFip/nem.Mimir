namespace nem.Mimir.Application.Common.Interfaces;

/// <summary>
/// Service interface for system prompt template rendering.
/// </summary>
public interface ISystemPromptService
{
    /// <summary>
    /// Renders a template string by replacing {{variable}} placeholders with provided values.
    /// </summary>
    /// <param name="template">The template string containing {{variable}} placeholders.</param>
    /// <param name="variables">Dictionary of variable names to their values.</param>
    /// <returns>The rendered string with variables replaced.</returns>
    string RenderTemplate(string template, IDictionary<string, string> variables);
}
