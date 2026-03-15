namespace nem.Mimir.Infrastructure.Tests.LiteLlm;

using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nem.Mimir.Infrastructure.LiteLlm;
using NSubstitute;
using Shouldly;
using nem.Contracts.AspNetCore.Classification;
using nem.Contracts.Classification;

public sealed class LiteLlmClassificationInterceptorTests
{
    [Fact]
    public async Task ConfidentialPrompt_ToExternalHost_IsBlocked()
    {
        var classificationHandler = new StubHttpMessageHandler(async request =>
        {
            request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/classify");
            var body = await request.Content!.ReadAsStringAsync();
            body.ShouldContain("customer ssn");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"level\":\"Confidential\"}", Encoding.UTF8, "application/json"),
            };
        });

        var classificationClient = new HttpClient(classificationHandler)
        {
            BaseAddress = new Uri("http://classification"),
        };

        var terminal = new TerminalHandler();
        var pipeline = CreatePipeline(classificationClient, terminal);

        using var request = BuildLiteLlmRequest(
            "https://api.external-llm.com/v1/chat/completions",
            "customer ssn 123-45-6789");

        using var response = await pipeline.SendAsync(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        terminal.WasCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task PublicPrompt_ToExternalHost_IsAllowed()
    {
        var classificationHandler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"level\":\"Public\"}", Encoding.UTF8, "application/json"),
            }));

        var classificationClient = new HttpClient(classificationHandler)
        {
            BaseAddress = new Uri("http://classification"),
        };

        var terminal = new TerminalHandler();
        var pipeline = CreatePipeline(classificationClient, terminal);

        using var request = BuildLiteLlmRequest(
            "https://api.external-llm.com/v1/chat/completions",
            "hello world");

        using var response = await pipeline.SendAsync(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        terminal.WasCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task ConfidentialPrompt_ToInternalHost_IsAllowed()
    {
        var classificationHandler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"level\":\"Confidential\"}", Encoding.UTF8, "application/json"),
            }));

        var classificationClient = new HttpClient(classificationHandler)
        {
            BaseAddress = new Uri("http://classification"),
        };

        var terminal = new TerminalHandler();
        var pipeline = CreatePipeline(classificationClient, terminal);

        using var request = BuildLiteLlmRequest(
            "http://ollama/v1/chat/completions",
            "customer payroll file");

        using var response = await pipeline.SendAsync(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        terminal.WasCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task ClassificationApiUnavailable_FailsClosed_AsConfidential()
    {
        var classificationHandler = new StubHttpMessageHandler(_ => throw new HttpRequestException("classification down"));
        var classificationClient = new HttpClient(classificationHandler)
        {
            BaseAddress = new Uri("http://classification"),
        };

        var terminal = new TerminalHandler();
        var pipeline = CreatePipeline(classificationClient, terminal);

        using var request = BuildLiteLlmRequest(
            "https://api.external-llm.com/v1/chat/completions",
            "internal strategy notes");

        using var response = await pipeline.SendAsync(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        terminal.WasCalled.ShouldBeFalse();
    }

    private static HttpMessageInvoker CreatePipeline(HttpClient classificationClient, TerminalHandler terminal)
    {
        var httpClientFactory = new StubHttpClientFactory(classificationClient);
        var interceptor = new LiteLlmClassificationInterceptor(
            httpClientFactory,
            Options.Create(new ClassificationOptions
            {
                ClassificationApiBaseUrl = "http://classification",
            }),
            Substitute.For<ILogger<LiteLlmClassificationInterceptor>>());

        var gating = new ClassificationGatingHandler(
            null,
            Options.Create(new ClassificationGatingOptions
            {
                BlockedClassificationLevel = ClassificationLevel.Confidential,
                InternalHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "localhost",
                    "ollama",
                    "litellm",
                },
            }),
            Substitute.For<ILogger<ClassificationGatingHandler>>())
        {
            InnerHandler = terminal,
        };

        interceptor.InnerHandler = gating;
        return new HttpMessageInvoker(interceptor);
    }

    private static HttpRequestMessage BuildLiteLlmRequest(string uri, string text)
    {
        const string prefix = "{\"messages\":[{\"role\":\"system\",\"content\":\"you are helpful\"},{\"role\":\"user\",\"content\":\"";
        const string suffix = "\"}],\"conversationId\":\"conv-123\"}";
        return new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(prefix + text + suffix, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _classificationClient;

        public StubHttpClientFactory(HttpClient classificationClient)
        {
            _classificationClient = classificationClient;
        }

        public HttpClient CreateClient(string name)
        {
            if (string.Equals(name, "ClassificationApi", StringComparison.Ordinal))
            {
                return _classificationClient;
            }

            throw new InvalidOperationException($"Unexpected client name: {name}");
        }
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

    private sealed class TerminalHandler : HttpMessageHandler
    {
        public bool WasCalled { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
