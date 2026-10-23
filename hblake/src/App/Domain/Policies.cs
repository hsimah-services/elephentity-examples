using Elephentity.Runtime.Policy;
using Hsimah.Entity;
using Hsimah.Entity.Contract;
using Hsimah.Entity.Enums;
using Hsimah.Entity.Pattern;
using Hsimah.Entity.Pattern.Contract;

namespace Hsimah.Domain;

public sealed class ContentStaff : IPublishableContentStaffWritePolicy
{
    public ValueTask<PolicyDecision> DecideAsync(IPublishable? entity, IWriteContext context, IViewer viewer, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(viewer.HasRole("admin") || (viewer.HasRole("editor") && context.Operation != WriteOperation.Delete)
            ? PolicyDecision.Allow() : PolicyDecision.Deny("Staff permission is required."));
}

public sealed class ContentVisibility : IPublishableContentVisibilityReadPolicy
{
    public async ValueTask<PolicyDecision> DecideAsync(IPublishable entity, IViewer viewer, CancellationToken cancellationToken = default)
    {
        var staff = viewer.HasRole("admin") || viewer.HasRole("editor");
        // An item is visible to visitors only while its page is published too.
        var visible = entity.Status == PublicationStatus.Published;
        if (visible && !staff && entity is PageItem item)
        {
            visible = (await item.GetPageAsync(cancellationToken).ConfigureAwait(false))?.Status == PublicationStatus.Published;
        }

        return visible || staff ? PolicyDecision.Allow() : PolicyDecision.Deny("Content is unavailable.");
    }
}

public sealed class StaffProfile : IUserStaffProfileReadPolicy
{
    public ValueTask<PolicyDecision> DecideAsync(User entity, IViewer viewer, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(viewer.HasRole("admin") || (viewer.IsAuthenticated && entity.Id.ToString() == viewer.Id)
            ? PolicyDecision.Allow() : PolicyDecision.Deny("Profile is unavailable."));
}

public sealed class AdminAccounts : IUserAdminAccountsWritePolicy
{
    public ValueTask<PolicyDecision> DecideAsync(User? entity, UserWriteContext context, IViewer viewer, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(viewer.HasRole("admin") ? PolicyDecision.Allow() : PolicyDecision.Deny("Admin permission is required."));
}
