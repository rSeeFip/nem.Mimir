using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using nem.Mimir.Api.Hubs;
using Shouldly;

namespace nem.Mimir.Api.IntegrationTests.Hubs;

public sealed class HubPartitioningTests : IAsyncLifetime
{
    private WebApplication? _app;
    private HttpClient? _client;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSignalR();
        builder.Services.AddAuthentication("Test")
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                TestAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization(opts =>
        {
            opts.AddPolicy("RequireAdmin", p => p.RequireRole("admin"));
        });

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapHub<ChatHub>("/hubs/chat");
        _app.MapHub<CollaborationHub>("/hubs/collaboration");
        _app.MapHub<AdminHub>("/hubs/admin");

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null)
            await _app.StopAsync();
    }

    [Fact]
    public async Task ChatHub_IsMappedAt_HubsChat()
    {
        var response = await _client!.PostAsync("/hubs/chat/negotiate?negotiateVersion=1", null);
        response.StatusCode.ShouldNotBe(HttpStatusCode.NotFound, "ChatHub must be mapped at /hubs/chat");
    }

    [Fact]
    public async Task CollaborationHub_IsMappedAt_HubsCollaboration()
    {
        var response = await _client!.PostAsync("/hubs/collaboration/negotiate?negotiateVersion=1", null);
        response.StatusCode.ShouldNotBe(HttpStatusCode.NotFound, "CollaborationHub must be mapped at /hubs/collaboration");
    }

    [Fact]
    public async Task AdminHub_IsMappedAt_HubsAdmin()
    {
        var response = await _client!.PostAsync("/hubs/admin/negotiate?negotiateVersion=1", null);
        response.StatusCode.ShouldNotBe(HttpStatusCode.NotFound, "AdminHub must be mapped at /hubs/admin");
    }

    [Fact]
    public void AdminHub_HasAuthorizeAttribute_WithRequireAdminPolicy()
    {
        var attr = typeof(AdminHub).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        attr.ShouldNotBeNull("AdminHub must have [Authorize] attribute");
        attr!.Policy.ShouldBe("RequireAdmin", "AdminHub must require the RequireAdmin policy");
    }

    [Fact]
    public async Task HubRoutes_AreDistinct_AllThreeHubsReachable()
    {
        var chatResponse = await _client!.PostAsync("/hubs/chat/negotiate?negotiateVersion=1", null);
        var collabResponse = await _client!.PostAsync("/hubs/collaboration/negotiate?negotiateVersion=1", null);
        var adminResponse = await _client!.PostAsync("/hubs/admin/negotiate?negotiateVersion=1", null);

        chatResponse.StatusCode.ShouldNotBe(HttpStatusCode.NotFound, "ChatHub must be at /hubs/chat");
        collabResponse.StatusCode.ShouldNotBe(HttpStatusCode.NotFound, "CollaborationHub must be at /hubs/collaboration");
        adminResponse.StatusCode.ShouldNotBe(HttpStatusCode.NotFound, "AdminHub must be at /hubs/admin");
    }

    [Fact]
    public async Task ChatHub_And_CollaborationHub_AreIsolated_DifferentRoutes()
    {
        var chatResponse = await _client!.PostAsync("/hubs/chat/negotiate?negotiateVersion=1", null);
        var collabResponse = await _client!.PostAsync("/hubs/collaboration/negotiate?negotiateVersion=1", null);

        chatResponse.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
        collabResponse.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
        chatResponse.RequestMessage!.RequestUri!.AbsolutePath.ShouldNotBe(
            collabResponse.RequestMessage!.RequestUri!.AbsolutePath,
            "ChatHub and CollaborationHub must be on distinct routes");
    }
}

internal sealed class TestAuthHandler : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
        => Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
}
