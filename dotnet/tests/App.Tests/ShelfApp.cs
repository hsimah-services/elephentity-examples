using System.Net;
using System.Text.Json;
using Elephentity.Cosmos;
using Elephentity.Cosmos.Manifest;
using Elephentity.Runtime.Memory;
using Elephentity.Runtime.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Shelf.App;

[assembly: AssemblyFixture(typeof(Shelf.App.Tests.Backend))]

namespace Shelf.App.Tests;

/// <summary>
/// Where the tests' data lives: the Cosmos emulator (or an account) when <c>ELEPH_COSMOS_ENDPOINT</c> is set, as in CI, and the memory driver
/// otherwise, so the suite also runs on a laptop with no emulator. One database per run, one logical partition per test.
/// </summary>
public sealed class Backend : IAsyncLifetime
{
    private static readonly Lazy<(CosmosClient Client, string Database, CosmosStorageManifest Manifest)?> Cosmos = new(Create);

    public static bool UsesCosmos => Cosmos.Value is not null;

    public static IStorageAdaptor Adaptor()
    {
        if (Cosmos.Value is not { } cosmos)
        {
            return new MemoryAdaptor();
        }

        var manifest = new CosmosStorageManifest(
            $"t-{Guid.NewGuid():N}",
            cosmos.Manifest.Entities,
            cosmos.Manifest.Orderings,
            cosmos.Manifest.Container);

        return new CosmosAdaptor(cosmos.Client, cosmos.Database, manifest);
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (Cosmos.IsValueCreated && Cosmos.Value is { } cosmos)
        {
            try
            {
                await cosmos.Client.GetDatabase(cosmos.Database).DeleteAsync();
            }
            catch (CosmosException)
            {
                // The run is over; a leftover test database is not worth failing it for.
            }

            cosmos.Client.Dispose();
        }
    }

    private static (CosmosClient, string, CosmosStorageManifest)? Create()
    {
        if (Environment.GetEnvironmentVariable("ELEPH_COSMOS_ENDPOINT") is not { Length: > 0 } endpoint)
        {
            return null;
        }

        var key = Environment.GetEnvironmentVariable("ELEPH_COSMOS_KEY") is { Length: > 0 } configured ? configured : ShelfServices.EmulatorKey;
        var client = new CosmosClientSettings(AllowAnyServerCertificate: key == ShelfServices.EmulatorKey, ApplicationName: "shelf-tests").CreateClient(endpoint, key);
        var database = $"shelf-tests-{Guid.NewGuid():N}";
        var manifest = Shelf.Storage.CosmosStorageManifestFactory.Create();

        // Installed exactly as `shelf install` does it, so the unique keys and indexes the spec asks for are the ones under test.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                CosmosInstaller.InstallAsync(client, database, manifest).GetAwaiter().GetResult();

                return (client, database, manifest);
            }
            catch (CosmosException failure) when (attempt < 5 && failure.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.ServiceUnavailable)
            {
                // The emulator is slow to create a container when busy; a real account is not.
                Thread.Sleep(TimeSpan.FromSeconds(5 * attempt));
            }
        }
    }
}

/// <summary>The application, in process, on a test server, with its own empty data.</summary>
public sealed class ShelfApp : IAsyncDisposable
{
    private const string Shell = "<html><head><script type=\"module\" src=\"/assets/app.js\"></script></head><body></body></html>";

    private readonly WebApplication app;

    private ShelfApp(WebApplication app, HttpClient client, IStorageAdaptor storage)
    {
        this.app = app;
        Client = client;
        Storage = storage;
    }

    public HttpClient Client { get; }

    public IStorageAdaptor Storage { get; }

    public IServiceProvider Services => app.Services;

    /// <param name="trustViewerHeader">Whether the host believes <c>X-Viewer</c>, as a test host and a development one do and a production one must not.</param>
    public static async Task<ShelfApp> StartAsync(bool trustViewerHeader = true)
    {
        var builder = WebApplication.CreateBuilder();
        var storage = Backend.Adaptor();

        builder.WebHost.UseTestServer();
        if (trustViewerHeader)
        {
            builder.Services.AddHeaderViewers();
        }

        builder.Services.AddShelf();
        builder.Services.AddSingleton(storage);

        var app = builder.Build();

        app.UseShelf(Shell);
        await app.StartAsync();

        return new ShelfApp(app, app.GetTestClient(), storage);
    }

    /// <summary>A GraphQL request as <paramref name="viewer" /> (anonymous when null); the whole response body.</summary>
    public async Task<JsonElement> Graph(string query, object? variables = null, string? viewer = "ada")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { query, variables }), System.Text.Encoding.UTF8, "application/json"),
        };

        if (viewer is not null)
        {
            request.Headers.Add(HeaderViewerProvider.Header, viewer);
        }

        using var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement.Clone();
    }

    /// <summary>The data of a request that must have succeeded.</summary>
    public async Task<JsonElement> Data(string query, object? variables = null, string? viewer = "ada")
    {
        var result = await Graph(query, variables, viewer);

        Assert.False(result.TryGetProperty("errors", out var errors), errors.ToString());

        return result.GetProperty("data");
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.DisposeAsync();
    }
}
