namespace Mimir.Signal.Tests;

using System.Net;

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage _response;

    public FakeHttpMessageHandler(HttpResponseMessage response) => _response = response;

    public Uri? LastRequestUri { get; private set; }
    public HttpContent? LastRequestContent { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        LastRequestContent = request.Content;
        return Task.FromResult(_response);
    }
}

internal sealed class ThrowingHttpMessageHandler : HttpMessageHandler
{
    private readonly Exception _exception;

    public ThrowingHttpMessageHandler(Exception exception) => _exception = exception;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromException<HttpResponseMessage>(_exception);
}

internal sealed class SequencingHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage[] _responses;
    private int _callIndex;

    public SequencingHttpMessageHandler(HttpResponseMessage[] responses) => _responses = responses;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var index = Interlocked.Increment(ref _callIndex) - 1;
        var response = index < _responses.Length
            ? _responses[index]
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
            };
        return Task.FromResult(response);
    }
}
