namespace nem.Mimir.Application.Knowledge;

using System.Text;
using nem.Mimir.Application.Common.Models;

public static class AnswerSourcesFormatter
{
    public static bool HasSources(IEnumerable<KnowledgeSearchResultDto> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        return EnumerateMarkdownLinks(sources).Any();
    }

    public static string AppendSources(string answer, IEnumerable<KnowledgeSearchResultDto> sources)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(sources);

        var sourcesSection = BuildSourcesSection(sources);
        return string.IsNullOrEmpty(sourcesSection)
            ? answer
            : answer + sourcesSection;
    }

    public static string BuildSourcesSection(IEnumerable<KnowledgeSearchResultDto> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var markdownLinks = EnumerateMarkdownLinks(sources).ToList();
        if (markdownLinks.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder("\n\n**Sources:**");
        foreach (var markdownLink in markdownLinks)
        {
            builder.Append("\n- ").Append(markdownLink);
        }

        return builder.ToString();
    }

    private static IEnumerable<string> EnumerateMarkdownLinks(IEnumerable<KnowledgeSearchResultDto> sources) =>
        sources
            .Select(static source => source.OriginLink?.ToMarkdown())
            .Where(static markdown => !string.IsNullOrWhiteSpace(markdown))
            .Distinct(StringComparer.Ordinal)!;
}
