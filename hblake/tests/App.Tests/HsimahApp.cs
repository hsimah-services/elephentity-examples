using System.Text;
using System.Text.Json;
using Elephentity.GraphQL.Http;
using Elephentity.Runtime.Memory;
using Elephentity.Runtime.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hsimah.App.Tests;

/// <summary>The application, in process, on a test server over the memory driver, with its own empty data.</summary>
public sealed class HsimahApp : IAsyncDisposable
{
    public const string Visitor = "";
    public const string Editor = "editor";
    public const string Admin = "admin";

    private readonly WebApplication app;
    private readonly HttpClient client;

    private HsimahApp(WebApplication app)
    {
        this.app = app;
        client = app.GetTestClient();
    }

    public static async Task<HsimahApp> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();
        builder.Services.AddHeaderViewers();
        builder.Services.AddHsimah();
        builder.Services.AddSingleton<IStorageAdaptor>(new MemoryAdaptor());

        var app = builder.Build();

        app.MapElephentityGraphQL("/graphql");
        await app.StartAsync();

        return new HsimahApp(app);
    }

    /// <summary>A GraphQL operation as <paramref name="role" /> (a site visitor by default); the whole response body.</summary>
    public async Task<JsonElement> Graph(string query, object? variables = null, string role = Visitor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { query, variables }), Encoding.UTF8, "application/json"),
        };

        if (role.Length > 0)
        {
            request.Headers.Add("X-Viewer", $"{role}-1");
            request.Headers.Add("X-Role", role);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement.Clone();
    }

    /// <summary>The <c>data</c> of an operation that must succeed.</summary>
    public async Task<JsonElement> Data(string query, object? variables = null, string role = Visitor)
    {
        var body = await Graph(query, variables, role);

        Assert.False(body.TryGetProperty("errors", out var errors), errors.ToString());

        return body.GetProperty("data");
    }

    /// <summary>The first error of an operation that must fail: its message, then the messages of the rule violations behind it.</summary>
    public async Task<string> Refusal(string query, object? variables = null, string role = Visitor)
    {
        var body = await Graph(query, variables, role);

        Assert.True(body.TryGetProperty("errors", out var errors), "The operation was accepted.");

        var error = errors[0];
        var violations = error.TryGetProperty("extensions", out var extensions) && extensions.TryGetProperty("violations", out var found)
            ? found.EnumerateArray().Select(violation => violation.GetProperty("message").GetString())
            : [];

        return string.Join(' ', [error.GetProperty("message").GetString(), .. violations]);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();
}
