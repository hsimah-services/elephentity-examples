using Elephentity.Runtime.Policy;
using Elephentity.Runtime.Query;
using Elephentity.Runtime.Storage;
using Shelf.Entity;
using Shelf.Entity.Contract;
using Shelf.Entity.Enums;
using GatewayRuntime = Elephentity.Runtime.Gateway.Runtime;

namespace Shelf.App;

// The application's side of the generated tree: one class per contract the spec asked for, and nothing the spec did not.

/// <summary>A wishlist is private to signed-in viewers; everything else is for anyone. A policy that has no opinion skips, and the spec's
/// terminal rule (read: allow) decides.</summary>
public sealed class HideWishlist : IBookHideWishlistReadPolicy
{
    public ValueTask<PolicyDecision> DecideAsync(Book entity, IViewer viewer, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(
            entity.Status == ReadingStatus.Wishlist && !viewer.IsAuthenticated
                ? PolicyDecision.Deny("A wishlist is private.")
                : PolicyDecision.Skip());
}

/// <summary>The write rule every entity gets from the <c>SignedInWrites</c> pattern.</summary>
public sealed class SignedInWrites : Shelf.Entity.Pattern.Contract.ISignedInWritesSignedInWritePolicy
{
    public ValueTask<PolicyDecision> DecideAsync(Shelf.Entity.Pattern.ISignedInWrites? entity, IWriteContext context, IViewer viewer, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(viewer.IsAuthenticated ? PolicyDecision.Allow() : PolicyDecision.Deny("Sign in to change the shelf."));
}

/// <summary>The <c>finish</c> action: it may write only <c>status</c>, and that is all it does.</summary>
public sealed class Finish : IBookFinishAction
{
    public ValueTask HandleAsync(BookFinishContext context, CancellationToken cancellationToken = default)
    {
        context.SetStatus(ReadingStatus.Finished);

        return ValueTask.CompletedTask;
    }
}

/// <summary>The published query <c>readingBooks</c>.</summary>
public sealed class Reading(GatewayRuntime runtime, BookHydrator hydrator) : IBookReadingQuery
{
    public IEntityQuery<Book> Find() =>
        runtime.Queries().Of(hydrator, Criteria.For("Book").Where(Filter.Equal("status", ReadingStatus.Reading.ToWire())));
}
