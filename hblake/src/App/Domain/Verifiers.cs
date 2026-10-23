using Elephentity.Runtime.Verification;
using Hsimah.Entity;
using Hsimah.Entity.Contract;
using Hsimah.Entity.Enums;

namespace Hsimah.Domain;

internal static class Done
{
    public static ValueTask<Verification> Of(Verification verification) => ValueTask.FromResult(verification);
}

public sealed class TitleVerifier : IPageTitleVerifier, IPageItemTitleVerifier
{
    public ValueTask<Verification> VerifyAsync(string value, PageMutationContext context, CancellationToken cancellationToken = default) => Done.Of(Rules.Title(value));

    public ValueTask<Verification> VerifyAsync(string value, PageItemMutationContext context, CancellationToken cancellationToken = default) => Done.Of(Rules.Title(value));
}

public sealed class ContentVerifier : IPageContentVerifier, IPageItemContentVerifier
{
    public ValueTask<Verification> VerifyAsync(string value, PageMutationContext context, CancellationToken cancellationToken = default) => Done.Of(Rules.Content(value, context));

    public ValueTask<Verification> VerifyAsync(string value, PageItemMutationContext context, CancellationToken cancellationToken = default) => Done.Of(Rules.Content(value, context));
}

public sealed class StatusVerifier : IPageStatusVerifier, IPageItemStatusVerifier
{
    public ValueTask<Verification> VerifyAsync(PublicationStatus value, PageMutationContext context, CancellationToken cancellationToken = default) => Done.Of(Rules.Publication(context));

    public ValueTask<Verification> VerifyAsync(PublicationStatus value, PageItemMutationContext context, CancellationToken cancellationToken = default) => Done.Of(Rules.Publication(context));
}

public sealed class PublishedAtVerifier : IPagePublishedAtVerifier, IPageItemPublishedAtVerifier
{
    public ValueTask<Verification> VerifyAsync(DateTimeOffset? value, PageMutationContext context, CancellationToken cancellationToken = default) => Done.Of(Rules.Publication(context));

    public ValueTask<Verification> VerifyAsync(DateTimeOffset? value, PageItemMutationContext context, CancellationToken cancellationToken = default) => Done.Of(Rules.Publication(context));
}

/// <summary>Page slugs may not shadow a fixed route, because items live at /&lt;page-slug&gt;/&lt;item-slug&gt;.</summary>
public sealed class SlugVerifier : IPageSlugVerifier, IPageItemSlugVerifier
{
    public ValueTask<Verification> VerifyAsync(string value, PageMutationContext context, CancellationToken cancellationToken = default)
    {
        var slug = Rules.Slug(value);
        return Done.Of(slug.IsOk ? Rules.PageSlug(value) : slug);
    }

    public ValueTask<Verification> VerifyAsync(string value, PageItemMutationContext context, CancellationToken cancellationToken = default) => Done.Of(Rules.Slug(value));
}

/// <summary>The kind label of an item, and the kind a page holds (null: the page holds no items).</summary>
public sealed class KindVerifier : IPageDefaultItemKindVerifier, IPageItemKindVerifier
{
    public ValueTask<Verification> VerifyAsync(string? value, PageMutationContext context, CancellationToken cancellationToken = default) =>
        Done.Of(value is null ? Verification.Ok() : Rules.Kind(value));

    public ValueTask<Verification> VerifyAsync(string? value, PageItemMutationContext context, CancellationToken cancellationToken = default) =>
        Done.Of(value is null ? Verification.Ok() : Rules.Kind(value));
}

public sealed class EnquiryUrlVerifier : IPageItemEnquiryUrlVerifier
{
    public ValueTask<Verification> VerifyAsync(string? value, PageItemMutationContext context, CancellationToken cancellationToken = default) =>
        Done.Of(value is null ? Verification.Ok() : Rules.Enquiry(value));
}

/// <summary>An item's image: a stored upload (/media/&lt;name&gt;) that always has alt text.</summary>
public sealed class ImageUrlVerifier : IPageItemImageUrlVerifier
{
    public ValueTask<Verification> VerifyAsync(string? value, PageItemMutationContext context, CancellationToken cancellationToken = default) =>
        Done.Of(value is null ? Verification.Ok() : Rules.Image(value, context.PendingImageAlt()));
}

public sealed class ImageAltVerifier : IPageItemImageAltVerifier
{
    public ValueTask<Verification> VerifyAsync(string? value, PageItemMutationContext context, CancellationToken cancellationToken = default) =>
        Done.Of(Rules.Check(context.PendingImageUrl() is null || !string.IsNullOrWhiteSpace(context.PendingImageAlt()), "Describe the image in its alt text."));
}

/// <summary>Page placement: orders are 0 to 999, and a page featured on the home page needs a colour for its tile.</summary>
public sealed class PagePlacement : IPageAccentVerifier, IPageHomeOrderVerifier, IPageNavigationOrderVerifier
{
    public ValueTask<Verification> VerifyAsync(PageAccent value, PageMutationContext context, CancellationToken cancellationToken = default) =>
        Done.Of(Rules.Check(value != PageAccent.Ink || context.Pending("featureOnHome") is not true, "Choose a colour for a page featured on the home page."));

    ValueTask<Verification> IPageHomeOrderVerifier.VerifyAsync(long value, PageMutationContext context, CancellationToken cancellationToken) => Order(value);

    ValueTask<Verification> IPageNavigationOrderVerifier.VerifyAsync(long value, PageMutationContext context, CancellationToken cancellationToken) => Order(value);

    private static ValueTask<Verification> Order(long value) => Done.Of(Rules.Check(value is >= 0 and <= 999, "Use an order from 0 to 999."));
}

public sealed class UsernameVerifier : IUserUsernameVerifier
{
    public ValueTask<Verification> VerifyAsync(string value, UserMutationContext context, CancellationToken cancellationToken = default) => Done.Of(Rules.Username(value));
}

public sealed class DisplayNameVerifier : IUserDisplayNameVerifier
{
    public ValueTask<Verification> VerifyAsync(string value, UserMutationContext context, CancellationToken cancellationToken = default) => Done.Of(Rules.Title(value, 100));
}
