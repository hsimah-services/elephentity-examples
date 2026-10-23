using Hsimah.Entity;
using Hsimah.Entity.Contract;
using Hsimah.Entity.Enums;

namespace Hsimah.Domain;

public sealed class Publish : IPagePublishAction, IPageItemPublishAction
{
    public ValueTask HandleAsync(PagePublishContext context, CancellationToken cancellationToken = default)
    {
        context.SetStatus(PublicationStatus.Published);
        context.SetPublishedAt(context.Context().OriginalPublishedAt() ?? DateTimeOffset.UtcNow);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(PageItemPublishContext context, CancellationToken cancellationToken = default)
    {
        context.SetStatus(PublicationStatus.Published);
        context.SetPublishedAt(context.Context().OriginalPublishedAt() ?? DateTimeOffset.UtcNow);
        return ValueTask.CompletedTask;
    }
}

public sealed class Unpublish : IPageUnpublishAction, IPageItemUnpublishAction
{
    public ValueTask HandleAsync(PageUnpublishContext context, CancellationToken cancellationToken = default)
    {
        context.SetStatus(PublicationStatus.Draft);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(PageItemUnpublishContext context, CancellationToken cancellationToken = default)
    {
        context.SetStatus(PublicationStatus.Draft);
        return ValueTask.CompletedTask;
    }
}
