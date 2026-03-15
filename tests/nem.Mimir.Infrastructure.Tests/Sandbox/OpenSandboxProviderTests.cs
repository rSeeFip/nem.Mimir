using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using nem.Mimir.Infrastructure.Sandbox;
using Shouldly;

namespace nem.Mimir.Infrastructure.Tests.Sandbox;

public sealed class OpenSandboxProviderTests
{
    [Fact]
    public async Task CreateSessionAsync_PostsAndReturnsSession()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            request.Method.ShouldBe(HttpMethod.Post);
            request.RequestUri!.AbsolutePath.ShouldBe("/sessions");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{\"sessionId\":\"session-1\"}", Encoding.UTF8, "application/json"),
            });
        });

        var provider = CreateProvider(handler);

        var session = await provider.CreateSessionAsync(new("python:3.12"));

        session.SessionId.ShouldBe("session-1");
        session.Config.Image.ShouldBe("python:3.12");
    }

    [Fact]
    public async Task DestroySessionAsync_NotFound_IsIgnored()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            request.Method.ShouldBe(HttpMethod.Delete);
            request.RequestUri!.AbsolutePath.ShouldBe("/sessions/missing");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var provider = CreateProvider(handler);

        await provider.DestroySessionAsync("missing");
    }

    [Fact]
    public async Task ListSessionsAsync_ReturnsSessions()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            request.Method.ShouldBe(HttpMethod.Get);
            request.RequestUri!.AbsolutePath.ShouldBe("/sessions");

            const string json = "[{\"id\":\"a\",\"image\":\"img\",\"memoryLimitMb\":1024,\"cpuLimit\":2.0,\"timeoutSeconds\":45,\"networkPolicy\":\"egress\"}]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        });

        var provider = CreateProvider(handler);

        var sessions = await provider.ListSessionsAsync();

        sessions.Count.ShouldBe(1);
        sessions[0].SessionId.ShouldBe("a");
        sessions[0].Config.Image.ShouldBe("img");
        sessions[0].Config.MemoryLimitMb.ShouldBe(1024);
    }

    [Fact]
    public async Task GetHealthAsync_WithStatusPayload_ReturnsTrue()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            request.Method.ShouldBe(HttpMethod.Get);
            request.RequestUri!.AbsolutePath.ShouldBe("/health");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"healthy\"}", Encoding.UTF8, "application/json"),
            });
        });

        var provider = CreateProvider(handler);

        var healthy = await provider.GetHealthAsync();

        healthy.ShouldBeTrue();
    }

    [Fact]
    public async Task GetHealthAsync_NonSuccess_ReturnsFalse()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var provider = CreateProvider(handler);

        var healthy = await provider.GetHealthAsync();

        healthy.ShouldBeFalse();
    }

    private static OpenSandboxProvider CreateProvider(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://sandbox.local/"),
        };

        var options = new TestOptionsMonitor<OpenSandboxOptions>(new OpenSandboxOptions
        {
            BaseUrl = "https://sandbox.local",
            DefaultImage = "default-image",
            TimeoutSeconds = 30,
        });

        return new OpenSandboxProvider(new StubHttpClientFactory(client), options);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request);
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public TestOptionsMonitor(T value)
        {
            CurrentValue = value;
        }

        public T CurrentValue { get; }

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
