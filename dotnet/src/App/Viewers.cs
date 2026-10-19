using Elephentity.Runtime.Policy;

namespace Shelf.App;

/// <summary>A viewer who is who a header says they are. A real application reads a session or a token here; the example reads
/// <c>X-Viewer</c> so a test can be anyone.</summary>
public sealed class NamedViewer(string id) : IViewer
{
    public string? Id => id;

    public bool IsAuthenticated => true;

    public bool HasRole(string role) => false;

    public bool Can(string capability) => false;
}

/// <summary>The runtime asks for the viewer on every read and write; this answers from the request being served.</summary>
public sealed class HeaderViewerProvider(IHttpContextAccessor http) : IViewerProvider
{
    public const string Header = "X-Viewer";

    private static readonly AnonymousViewer Anonymous = new();

    public IViewer Viewer() =>
        http.HttpContext?.Request.Headers[Header].ToString() is { Length: > 0 } name ? new NamedViewer(name) : Anonymous;
}
