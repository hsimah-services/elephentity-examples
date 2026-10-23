using Elephentity.Runtime.Policy;

namespace Hsimah.Domain;

/// <summary>A signed-in staff member as the policies see them: an id and a role, <c>editor</c> or <c>admin</c>.</summary>
public sealed class StaffViewer(string id, string role) : IViewer
{
    public string? Id => id;

    public bool IsAuthenticated => true;

    public bool HasRole(string value) => value == role;

    public bool Can(string capability) => false;
}

/// <summary>The runtime asks for the viewer on every read and write; this answers from the request being served. It believes <c>X-Viewer</c>
/// (an id) and <c>X-Role</c> headers, which is honest only where nobody hostile can send them: a test host or <c>dotnet run</c> in
/// development. A real application reads a session or a token here. Without the headers the viewer is anonymous, a site visitor.</summary>
public sealed class HeaderViewerProvider(IHttpContextAccessor http) : IViewerProvider
{
    private static readonly AnonymousViewer Visitor = new();

    public IViewer Viewer()
    {
        var headers = http.HttpContext?.Request.Headers;
        var id = headers?["X-Viewer"].ToString();
        var role = headers?["X-Role"].ToString();

        return id is { Length: > 0 } && role is "editor" or "admin" ? new StaffViewer(id, role) : Visitor;
    }
}
