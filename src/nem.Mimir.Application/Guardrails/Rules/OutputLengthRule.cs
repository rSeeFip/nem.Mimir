namespace nem.Mimir.Application.Guardrails.Rules;

using System.Text.RegularExpressions;
using nem.Contracts.TokenOptimization;

public sealed partial class OutputLengthRule : IGuardrailRule
{
    public const string Id = "output-length";
    private const int DefaultMaxOutputTokens = 4096;
    private const int CharactersPerTokenEstimate = 4;

    public string RuleId => Id;

    public Task<RuleResult> EvaluateAsync(GuardrailRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var requestedTokens = EstimateRequestedTokens(request);
        var passed = requestedTokens <= DefaultMaxOutputTokens;
        var result = passed
            ? new RuleResult(RuleId, true, null, GuardrailLevel.Standard)
            : new RuleResult(RuleId, false, $"Requested output length {requestedTokens} exceeds max {DefaultMaxOutputTokens}.", GuardrailLevel.Standard);

        return Task.FromResult(result);
    }

    private static int EstimateRequestedTokens(GuardrailRequest request)
    {
        var explicitTokenRequest = ExtractExplicitTokenRequest(request.Prompt);
        if (explicitTokenRequest > 0)
        {
            return explicitTokenRequest;
        }

        if (!string.IsNullOrWhiteSpace(request.Response))
        {
            return EstimateTokensFromCharacters(request.Response.Length);
        }

        return EstimateTokensFromCharacters(request.Prompt.Length);
    }

    private static int ExtractExplicitTokenRequest(string prompt)
    {
        var match = OutputLengthRegex().Match(prompt);
        if (!match.Success)
        {
            return 0;
        }

        var rawValue = match.Groups[1].Value.Replace(",", string.Empty, StringComparison.Ordinal);
        if (!int.TryParse(rawValue, out var parsed))
        {
            return 0;
        }

        return parsed;
    }

    private static int EstimateTokensFromCharacters(int characterCount)
        => Math.Max(0, (int)Math.Ceiling(characterCount / (double)CharactersPerTokenEstimate));

    [GeneratedRegex(@"(?:write|generate|return|respond\s+with|produce)\s+(\d{4,6})\s+(?:tokens|words|characters)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex OutputLengthRegex();
}
