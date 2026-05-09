using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using nem.Mimir.Api.Controllers;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using Wolverine;

namespace nem.Mimir.Api.Tests;

public sealed class ConversationSearchEndpointsTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IConversationSearchService _searchService = Substitute.For<IConversationSearchService>();

    [Fact]
    public async Task ConversationSearch_MissingQuery_Returns400()
    {
        var controller = new ConversationsController(_bus);

        var response = await controller.Search(null, null, _currentUserService, _searchService, CancellationToken.None);

        var badRequest = response.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Detail.Should().Be("Query parameter 'q' is required.");
    }

    [Fact]
    public async Task ConversationSearch_ValidQuery_Returns200WithResultsArray()
    {
        var controller = new ConversationsController(_bus);
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString("D"));
        _searchService.SearchAsync(userId, "hello world", null, Arg.Any<CancellationToken>()).Returns(
        [
            new ConversationSearchResultDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Searchable conversation",
                "hello world snippet",
                DateTimeOffset.Parse("2026-01-01T00:00:00Z"))
        ]);

        var response = await controller.Search("hello world", null, _currentUserService, _searchService, CancellationToken.None);

        var ok = response.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeAssignableTo<IReadOnlyList<ConversationSearchResultDto>>().Subject;
        payload.Should().HaveCount(1);
        payload[0].ConversationTitle.Should().Be("Searchable conversation");
        payload[0].MessageSnippet.Should().Be("hello world snippet");
    }
}
