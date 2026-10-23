using Elephentity.Runtime.Gateway;
using Elephentity.Runtime.Query;
using Elephentity.Runtime.Storage;
using Hsimah.Entity;
using Hsimah.Entity.Contract;
using Hsimah.Entity.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Hsimah.Domain;

/// <summary>Shared helpers for the application's spec queries. The runtime is resolved on use because the catalogue builds these.</summary>
public abstract class QueryBase(IServiceProvider services)
{
    protected Elephentity.Runtime.Gateway.Runtime Runtime => services.GetRequiredService<Elephentity.Runtime.Gateway.Runtime>();

    protected IEntityQuery<Page> Pages(Criteria criteria) => Runtime.Queries().Of(services.GetRequiredService<PageHydrator>(), criteria);

    protected IEntityQuery<PageItem> Items(Criteria criteria) => Runtime.Queries().Of(services.GetRequiredService<PageItemHydrator>(), criteria);
}

public sealed class PageBySlug(IServiceProvider services) : QueryBase(services), IPageBySlugQuery
{
    public ValueTask<Page?> FindAsync(string slug, CancellationToken cancellationToken = default) =>
        Pages(Criteria.For("Page").Where(Filter.Equal("slug", slug))).FirstAsync(cancellationToken);
}

public sealed class PageItemBySlug(IServiceProvider services) : QueryBase(services), IPageItemBySlugQuery
{
    public ValueTask<PageItem?> FindAsync(string slug, CancellationToken cancellationToken = default) =>
        Items(Criteria.For("PageItem").Where(Filter.Equal("slug", slug))).FirstAsync(cancellationToken);
}

/// <summary>The items of one page, found by its slug, newest published first (PageItem.md).</summary>
public sealed class PageItemOfPage(IServiceProvider services) : QueryBase(services), IPageItemOfPageQuery
{
    public IEntityQuery<PageItem> Find(string page) => new DeferredQuery<PageItem>(async token =>
    {
        var found = await Pages(Criteria.For("Page").Where(Filter.Equal("slug", page))).FirstAsync(token).ConfigureAwait(false);
        var criteria = Criteria.For("PageItem");
        // An unknown or unreadable page lists nothing: a slug is never null, so this matches no row.
        criteria = found is not null
            ? criteria.LinkedTo(EdgeFilter.Back("PageItem", "page", found.Id))
            : criteria.Where(Filter.IsNull("slug"));
        return Items(criteria.OrderBy(Order.Descending("publishedAt")).OrderBy(Order.Descending("id")));
    });
}

/// <summary>Items newest-edited first, optionally of one kind or status, for the admin dashboard.</summary>
public sealed class PageItemRecent(IServiceProvider services) : QueryBase(services), IPageItemRecentQuery
{
    public IEntityQuery<PageItem> Find(string? kind, PublicationStatus? status)
    {
        var criteria = Criteria.For("PageItem");
        if (kind is not null)
        {
            criteria = criteria.Where(Filter.Equal("kind", kind));
        }

        if (status is not null)
        {
            criteria = criteria.Where(Filter.Equal("status", status.Value.ToWire()));
        }

        return Items(criteria.OrderBy(Order.Descending("updatedAt")).OrderBy(Order.Descending("id")));
    }
}

/// <summary>Pages newest-edited first, optionally of one status, for the admin dashboard.</summary>
public sealed class PageRecent(IServiceProvider services) : QueryBase(services), IPageRecentQuery
{
    public IEntityQuery<Page> Find(PublicationStatus? status)
    {
        var criteria = Criteria.For("Page");
        if (status is not null)
        {
            criteria = criteria.Where(Filter.Equal("status", status.Value.ToWire()));
        }

        return Pages(criteria.OrderBy(Order.Descending("updatedAt")).OrderBy(Order.Descending("id")));
    }
}

/// <summary>Published pages placed on the home page or in the navigation, in placement order (Page.md).</summary>
public sealed class PagePlaced(IServiceProvider services) : QueryBase(services), IPagePlacedQuery
{
    public IEntityQuery<Page> Find(bool home) =>
        Pages(Criteria.For("Page")
            .Where(Filter.Equal("status", "published"))
            .Where(Filter.Equal(home ? "featureOnHome" : "showInNavigation", true))
            .OrderBy(Order.Ascending(home ? "homeOrder" : "navigationOrder"))
            .OrderBy(Order.Ascending("title")));
}

/// <summary>A query whose criteria need an awaited lookup first; the lookup runs when the query is used.</summary>
public sealed class DeferredQuery<T>(Func<CancellationToken, ValueTask<IEntityQuery<T>>> resolve) : IEntityQuery<T>
    where T : class
{
    public async ValueTask<long> CountAsync(CancellationToken cancellationToken = default) =>
        await (await resolve(cancellationToken).ConfigureAwait(false)).CountAsync(cancellationToken).ConfigureAwait(false);

    public async ValueTask<Elephentity.Runtime.Storage.Page<T>> PageAsync(int limit, ICursor? after = null, CancellationToken cancellationToken = default) =>
        await (await resolve(cancellationToken).ConfigureAwait(false)).PageAsync(limit, after, cancellationToken).ConfigureAwait(false);

    public async ValueTask<IReadOnlyList<T>> AllAsync(CancellationToken cancellationToken = default) =>
        await (await resolve(cancellationToken).ConfigureAwait(false)).AllAsync(cancellationToken).ConfigureAwait(false);

    public async ValueTask<T?> FirstAsync(CancellationToken cancellationToken = default) =>
        await (await resolve(cancellationToken).ConfigureAwait(false)).FirstAsync(cancellationToken).ConfigureAwait(false);

    public async ValueTask<bool> ExistsAsync(CancellationToken cancellationToken = default) =>
        await (await resolve(cancellationToken).ConfigureAwait(false)).ExistsAsync(cancellationToken).ConfigureAwait(false);
}
