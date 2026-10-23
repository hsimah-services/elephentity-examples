using Elephentity.Cosmos;
using Elephentity.GraphQL.Http;
using Elephentity.Runtime.Policy;
using Elephentity.Runtime.Storage;
using Hsimah.Domain;
using Hsimah.Entity;
using Hsimah.Entity.Contract;
using Hsimah.Entity.Pattern.Contract;
using Microsoft.Azure.Cosmos;

namespace Hsimah;

public static class HsimahServices
{
    /// <summary>The emulator's well-known key. It protects nothing, and is the default only when the endpoint is the emulator's.</summary>
    public const string EmulatorKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    /// <summary>Lets a request say who it is with <c>X-Viewer</c> and <c>X-Role</c> headers. Register it <em>before</em> <see cref="AddHsimah" />,
    /// which leaves a registered provider alone; without it every viewer is anonymous.</summary>
    public static IServiceCollection AddHeaderViewers(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IViewerProvider, HeaderViewerProvider>();

        return services;
    }

    /// <summary>Everything the generated code needs from the application, and the schema and executor built on it. Storage is separate:
    /// <see cref="AddCosmos" /> for the real thing, any <see cref="IStorageAdaptor" /> for a check or a test.</summary>
    public static IServiceCollection AddHsimah(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        // Policies, verifiers, actions and queries: the contracts the generated code asks the application for. A rule that serves
        // several entities is one class registered once per contract.
        services.AddSingleton<IPublishableContentStaffWritePolicy, ContentStaff>();
        services.AddSingleton<IPublishableContentVisibilityReadPolicy, ContentVisibility>();
        services.AddSingleton<IUserStaffProfileReadPolicy, StaffProfile>();
        services.AddSingleton<IUserAdminAccountsWritePolicy, AdminAccounts>();

        services.AddSingleton<TitleVerifier>().AddSingleton<IPageTitleVerifier>(p => p.GetRequiredService<TitleVerifier>()).AddSingleton<IPageItemTitleVerifier>(p => p.GetRequiredService<TitleVerifier>());
        services.AddSingleton<ContentVerifier>().AddSingleton<IPageContentVerifier>(p => p.GetRequiredService<ContentVerifier>()).AddSingleton<IPageItemContentVerifier>(p => p.GetRequiredService<ContentVerifier>());
        services.AddSingleton<StatusVerifier>().AddSingleton<IPageStatusVerifier>(p => p.GetRequiredService<StatusVerifier>()).AddSingleton<IPageItemStatusVerifier>(p => p.GetRequiredService<StatusVerifier>());
        services.AddSingleton<PublishedAtVerifier>().AddSingleton<IPagePublishedAtVerifier>(p => p.GetRequiredService<PublishedAtVerifier>()).AddSingleton<IPageItemPublishedAtVerifier>(p => p.GetRequiredService<PublishedAtVerifier>());
        services.AddSingleton<SlugVerifier>().AddSingleton<IPageSlugVerifier>(p => p.GetRequiredService<SlugVerifier>()).AddSingleton<IPageItemSlugVerifier>(p => p.GetRequiredService<SlugVerifier>());
        services.AddSingleton<KindVerifier>().AddSingleton<IPageDefaultItemKindVerifier>(p => p.GetRequiredService<KindVerifier>()).AddSingleton<IPageItemKindVerifier>(p => p.GetRequiredService<KindVerifier>());
        services.AddSingleton<IPageItemEnquiryUrlVerifier, EnquiryUrlVerifier>();
        services.AddSingleton<IPageItemImageUrlVerifier, ImageUrlVerifier>();
        services.AddSingleton<IPageItemImageAltVerifier, ImageAltVerifier>();
        services.AddSingleton<PagePlacement>().AddSingleton<IPageAccentVerifier>(p => p.GetRequiredService<PagePlacement>())
            .AddSingleton<IPageHomeOrderVerifier>(p => p.GetRequiredService<PagePlacement>())
            .AddSingleton<IPageNavigationOrderVerifier>(p => p.GetRequiredService<PagePlacement>());
        services.AddSingleton<IUserUsernameVerifier, UsernameVerifier>();
        services.AddSingleton<IUserDisplayNameVerifier, DisplayNameVerifier>();

        services.AddSingleton<Publish>().AddSingleton<IPagePublishAction>(p => p.GetRequiredService<Publish>()).AddSingleton<IPageItemPublishAction>(p => p.GetRequiredService<Publish>());
        services.AddSingleton<Unpublish>().AddSingleton<IPageUnpublishAction>(p => p.GetRequiredService<Unpublish>()).AddSingleton<IPageItemUnpublishAction>(p => p.GetRequiredService<Unpublish>());

        services.AddSingleton<IPageBySlugQuery, PageBySlug>();
        services.AddSingleton<IPageItemBySlugQuery, PageItemBySlug>();
        services.AddSingleton<IPageItemOfPageQuery, PageItemOfPage>();
        services.AddSingleton<IPageItemRecentQuery, PageItemRecent>();
        services.AddSingleton<IPageRecentQuery, PageRecent>();
        services.AddSingleton<IPagePlacedQuery, PagePlaced>();

        // The viewer is the application's to supply; the generated wiring registers the anonymous one only when nothing else is.
        services.AddHsimahEntities();
        services.AddElephentityGraphQL(Hsimah.GraphQL.GraphQLManifestFactory.Create(), options => options.DefaultPageSize = 20);

        return services;
    }

    /// <summary>Cosmos DB as storage: the account in <c>Cosmos:Endpoint</c>, its key in <c>Cosmos:Key</c> (the emulator's when omitted and the
    /// endpoint is local), the database in <c>Cosmos:Database</c>. Which account is configuration; the container's layout is the spec's.</summary>
    public static IServiceCollection AddCosmos(this IServiceCollection services, IConfiguration configuration)
    {
        var endpoint = configuration["Cosmos:Endpoint"] ?? throw new InvalidOperationException("Set Cosmos:Endpoint (the emulator is https://localhost:8081).");
        var local = new Uri(endpoint).IsLoopback;
        var key = configuration["Cosmos:Key"] ?? (local ? EmulatorKey : throw new InvalidOperationException("Set Cosmos:Key."));
        var database = configuration["Cosmos:Database"] ?? "hsimah";

        services.AddSingleton(_ => new CosmosClientSettings(AllowAnyServerCertificate: local, ApplicationName: "hsimah").CreateClient(endpoint, key));
        services.AddSingleton(Hsimah.Storage.CosmosStorageManifestFactory.Create());
        services.AddSingleton<IStorageAdaptor>(provider => new CosmosAdaptor(
            provider.GetRequiredService<CosmosClient>(),
            database,
            provider.GetRequiredService<Elephentity.Cosmos.Manifest.CosmosStorageManifest>()));

        return services;
    }
}
