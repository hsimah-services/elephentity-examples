using System.Text.RegularExpressions;
using Elephentity.Runtime.Mutation;
using Elephentity.Runtime.Verification;
using Hsimah.Entity.Enums;

namespace Hsimah.Domain;

public static partial class Rules
{
    /// <summary>Fixed routes that a page slug would shadow in /&lt;page-slug&gt;/&lt;item-slug&gt;.</summary>
    public static readonly string[] ReservedSlugs = ["admin", "assets", "auth", "graphql", "media", "pages"];

    public static Verification Check(bool valid, string message) =>
        valid ? Verification.Ok() : Verification.Failed(new Violation("invalid", message));

    public static Verification Title(string value, int max = 200) =>
        Check(!string.IsNullOrWhiteSpace(value) && value.Length <= max, "A nonblank title/name within the length limit is required.");

    public static Verification Slug(string value) =>
        Check(value.Length <= 160 && SlugPattern().IsMatch(value), "Use a lowercase slug with single hyphens, up to 160 characters.");

    public static Verification PageSlug(string value) =>
        Check(!ReservedSlugs.Contains(value), "This slug is reserved for the site; choose another.");

    public static Verification Kind(string value) =>
        Check(!string.IsNullOrWhiteSpace(value) && value.Length <= 80, "Use a nonblank kind of at most 80 characters.");

    public static Verification Content(string value, IMutationContext context) =>
        Check(context.Pending("status") is not PublicationStatus.Published || !string.IsNullOrWhiteSpace(value), "Published content cannot be blank.");

    public static Verification Publication(IMutationContext context)
    {
        var names = context.Actions.Select(action => action.Name).ToList();
        var status = context.Pending("status") as PublicationStatus?;
        var time = context.Pending("publishedAt") as DateTimeOffset?;
        var original = context.Original("publishedAt") as DateTimeOffset?;
        bool valid;
        if (context.IsCreate)
        {
            valid = status == PublicationStatus.Draft && time is null;
        }
        else if (names.Contains("publish"))
        {
            valid = status == PublicationStatus.Published && time is not null
                && (original is null ? Math.Abs((time.Value - DateTimeOffset.UtcNow).TotalSeconds) < 10 : time == original);
        }
        else if (names.Contains("unpublish"))
        {
            valid = status == PublicationStatus.Draft && time == original;
        }
        else
        {
            valid = status == context.Original("status") as PublicationStatus? && time == original;
        }

        valid = valid && (status != PublicationStatus.Published || (context.Pending("content") is string body && !string.IsNullOrWhiteSpace(body)));
        return Check(valid, "Publication changes require a valid publish/unpublish action.");
    }

    public static Verification Enquiry(string value)
    {
        var valid = value.Length <= 2048
            && ((value.StartsWith("https://", StringComparison.Ordinal) && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Host.Length > 0)
                || (value.StartsWith("mailto:", StringComparison.Ordinal) && Email().IsMatch(value[7..])));
        return Check(valid, "Use a valid HTTPS URL or mailto email address.");
    }

    /// <summary>An uploaded image kept under /media (/media/&lt;32 hex&gt;.&lt;ext&gt;), with nonblank alt text.</summary>
    public static Verification Image(string url, string? alt) =>
        Check(MediaUrl().IsMatch(url) && !string.IsNullOrWhiteSpace(alt), "Use an image uploaded through the editor, with alt text.");

    public static Verification Username(string value) =>
        Check(UsernamePattern().IsMatch(value), "Use 3–32 lowercase letters, digits, dots, hyphens or underscores, starting with a letter or digit.");

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*\z")]
    private static partial Regex SlugPattern();

    [GeneratedRegex(@"^/media/[a-f0-9]{32}\.(png|jpg|webp)\z")]
    private static partial Regex MediaUrl();

    [GeneratedRegex(@"^[a-z0-9][a-z0-9._-]{2,31}\z")]
    private static partial Regex UsernamePattern();

    [GeneratedRegex(@"^[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)+\z")]
    private static partial Regex Email();
}
