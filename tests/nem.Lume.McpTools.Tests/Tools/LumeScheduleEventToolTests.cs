using System.Net;
using System.Text;
using System.Text.Json;
using NSubstitute;
using Shouldly;
using nem.Lume.McpTools.Tools;

namespace nem.Lume.McpTools.Tests.Tools;

public sealed class LumeScheduleEventToolTests
{
    private static IHttpClientFactory CreateFactory(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("lume-api").Returns(client);
        return factory;
    }

    [Fact]
    public async Task ScheduleEventAsync_HappyPath_ReturnsCreatedEvent()
    {
        var responseJson = """{"id":"event-123","title":"Team Meeting"}""";
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        });

        var result = await LumeScheduleEventTool.ScheduleEventAsync(
            "cal-1",
            "Team Meeting",
            "2026-05-01T09:00:00Z",
            "2026-05-01T10:00:00Z",
            null,
            CreateFactory(handler),
            CancellationToken.None);

        result.ShouldContain("event-123");
        result.ShouldContain("Team Meeting");
        handler.LastRequestUri!.AbsolutePath.ShouldBe("/api/lume/calendars/cal-1/events");
    }

    [Fact]
    public async Task ScheduleEventAsync_WithAttendees_IncludesAttendeesInRequest()
    {
        var responseJson = """{"id":"event-456"}""";
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        });

        var result = await LumeScheduleEventTool.ScheduleEventAsync(
            "cal-2",
            "Standup",
            "2026-05-01T08:00:00Z",
            "2026-05-01T08:30:00Z",
            "user-1,user-2",
            CreateFactory(handler),
            CancellationToken.None);

        result.ShouldContain("event-456");

        var bodyJson = await handler.LastRequestContent!.ReadAsStringAsync();
        bodyJson.ShouldContain("user-1,user-2");
    }

    [Fact]
    public async Task ScheduleEventAsync_StartAfterEnd_ReturnsValidationError()
    {
        var factory = Substitute.For<IHttpClientFactory>();

        var result = await LumeScheduleEventTool.ScheduleEventAsync(
            "cal-1",
            "Bad Event",
            "2026-05-01T11:00:00Z",
            "2026-05-01T09:00:00Z",
            null,
            factory,
            CancellationToken.None);

        result.ShouldContain("error");
        result.ShouldContain("startTimeUtc must be before endTimeUtc");
        factory.DidNotReceive().CreateClient(Arg.Any<string>());
    }

    [Fact]
    public async Task ScheduleEventAsync_StartEqualToEnd_ReturnsValidationError()
    {
        var factory = Substitute.For<IHttpClientFactory>();

        var result = await LumeScheduleEventTool.ScheduleEventAsync(
            "cal-1",
            "Equal Times",
            "2026-05-01T10:00:00Z",
            "2026-05-01T10:00:00Z",
            null,
            factory,
            CancellationToken.None);

        result.ShouldContain("error");
        result.ShouldContain("startTimeUtc must be before endTimeUtc");
    }

    [Fact]
    public async Task ScheduleEventAsync_ApiError_ReturnsErrorJson()
    {
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("Invalid calendar", Encoding.UTF8, "text/plain"),
        });

        var result = await LumeScheduleEventTool.ScheduleEventAsync(
            "cal-bad",
            "Test",
            "2026-05-01T09:00:00Z",
            "2026-05-01T10:00:00Z",
            null,
            CreateFactory(handler),
            CancellationToken.None);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().ShouldBe("error");
        doc.RootElement.GetProperty("error").GetString()!.ShouldContain("400");
    }

    [Fact]
    public async Task ScheduleEventAsync_NetworkException_ReturnsErrorJson()
    {
        var handler = new ThrowingHandler(new HttpRequestException("Connection refused"));

        var result = await LumeScheduleEventTool.ScheduleEventAsync(
            "cal-1",
            "Test",
            "2026-05-01T09:00:00Z",
            "2026-05-01T10:00:00Z",
            null,
            CreateFactory(handler),
            CancellationToken.None);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().ShouldBe("error");
        doc.RootElement.GetProperty("error").GetString()!.ShouldContain("Connection refused");
    }

    private sealed class FakeHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }
        public HttpContent? LastRequestContent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastRequestContent = request.Content;
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(exception);
    }
}
