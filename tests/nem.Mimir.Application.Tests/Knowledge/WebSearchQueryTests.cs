using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Knowledge;
using nem.Mimir.Application.Knowledge.Queries;
using NSubstitute;
using Shouldly;

namespace nem.Mimir.Application.Tests.Knowledge;

public sealed class WebSearchQueryTests
{
    [Fact]
    public async Task Handle_ShouldDelegateToSearxngClient()
    {
        var client = Substitute.For<ISearxngClient>();
        client.SearchAsync("latest ai", Arg.Any<CancellationToken>())
            .Returns([
                new WebSearchResultDto("AI News", "https://example.com", "snippet", "searx", null),
            ]);

        var handler = new WebSearchQueryHandler(client);
        var result = await handler.Handle(new WebSearchQuery("latest ai"), CancellationToken.None);

        result.Count.ShouldBe(1);
        result[0].Title.ShouldBe("AI News");
        await client.Received(1).SearchAsync("latest ai", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_EmptyQuery_ShouldFail()
    {
        var validator = new WebSearchQueryValidator();
        var validation = validator.Validate(new WebSearchQuery(string.Empty));

        validation.IsValid.ShouldBeFalse();
        validation.Errors.ShouldContain(x => x.PropertyName == "Query");
    }
}
