using Elephentity.Cosmos;
using Elephentity.GraphQL.Http;
using Elephentity.Tsquid.Http;
using Elephentity.Runtime.Policy;
using Elephentity.Runtime.Storage;
using Microsoft.Azure.Cosmos;
using Shelf.Entity;
using Shelf.Entity.Contract;

namespace Shelf.App;

public static class ShelfServices
{
    /// <summary>The emulator's well-known key. It protects nothing, and is the default only when the endpoint is the emulator's.</summary>
    public const string EmulatorKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    /// <summary>Everything the generated code needs from the application, and the schema and executor built on it. Storage is separate:
    /// <see cref="AddCosmos" /> for the real thing, any <see cref="IStorageAdaptor" /> for a check or a test.</summary>
    public static IServiceCollection AddShelf(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IViewerProvider, HeaderViewerProvider>();

        services.AddSingleton<IBookHideWishlistReadPolicy, HideWishlist>();
        services.AddSingleton<Shelf.Entity.Pattern.Contract.ISignedInWritesSignedInWritePolicy, SignedInWrites>();
        services.AddSingleton<IBookFinishAction, Finish>();
        services.AddSingleton<IBookReadingQuery, Reading>();

        // The generated wiring leaves a viewer provider and the rest alone when they are already registered.
        services.AddShelfEntities();
        services.AddElephentityGraphQL(Shelf.GraphQL.GraphQLManifestFactory.Create(), options => options.DefaultPageSize = 20);

        return services;
    }

    /// <summary>Cosmos DB as storage: the account in <c>Cosmos:Endpoint</c>, its key in <c>Cosmos:Key</c> (the emulator's when omitted and the
    /// endpoint is local), the database in <c>Cosmos:Database</c>. Which account is configuration; the container's layout is the spec's.</summary>
    public static IServiceCollection AddCosmos(this IServiceCollection services, IConfiguration configuration)
    {
        var endpoint = configuration["Cosmos:Endpoint"] ?? throw new InvalidOperationException("Set Cosmos:Endpoint (the emulator is https://localhost:8081).");
        var local = new Uri(endpoint).IsLoopback;
        var key = configuration["Cosmos:Key"] ?? (local ? EmulatorKey : throw new InvalidOperationException("Set Cosmos:Key."));
        var database = configuration["Cosmos:Database"] ?? "shelf";

        services.AddSingleton(_ => new CosmosClientSettings(AllowAnyServerCertificate: local, ApplicationName: "shelf").CreateClient(endpoint, key));
        services.AddSingleton(Shelf.Storage.CosmosStorageManifestFactory.Create());
        services.AddSingleton<IStorageAdaptor>(provider => new CosmosAdaptor(
            provider.GetRequiredService<CosmosClient>(),
            database,
            provider.GetRequiredService<Elephentity.Cosmos.Manifest.CosmosStorageManifest>()));

        return services;
    }

    /// <summary>The pipeline: the SPA shell with the route's data inlined where the URL has some, and the GraphQL endpoint.</summary>
    public static WebApplication UseShelf(this WebApplication app, string shell)
    {
        app.UseElephentityTsquid(Shelf.Tsquid.TsquidManifestFactory.Create(), _ => ValueTask.FromResult<string?>(shell));
        app.MapElephentityGraphQL("/graphql");

        return app;
    }
}
