using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Elephentity.GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;

namespace Shelf.App.Tests;

public sealed partial class ShelfTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Id(JsonElement data, string field, string node) => data.GetProperty(field).GetProperty(node).GetProperty("id").GetString()!;

    private static Task<JsonElement> CreateBook(ShelfApp app, string slug, string title, string status = "READING", string? viewer = "ada") =>
        app.Data(
            "mutation($slug: String!, $title: String!, $status: ReadingStatus!) { createBook(input: { slug: $slug, title: $title, author: \"Anon\", status: $status }) { book { id slug createdAt updatedAt status } } }",
            new { slug, title, status },
            viewer);

    [Fact]
    public async Task ABookIsCreatedReadPagedAndDeleted()
    {
        await using var app = await ShelfApp.StartAsync();

        for (var n = 1; n <= 3; n++)
        {
            await CreateBook(app, $"book-{n}", $"Book {n}");
        }

        var page = (await app.Data("{ books(first: 2) { totalCount nodes { slug title status } pageInfo { hasNextPage endCursor } } }")).GetProperty("books");

        Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page.GetProperty("nodes").GetArrayLength());
        Assert.True(page.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean());

        var next = (await app.Data(
            "query($after: String) { books(first: 2, after: $after) { nodes { slug } pageInfo { hasNextPage } } }",
            new { after = page.GetProperty("pageInfo").GetProperty("endCursor").GetString() })).GetProperty("books");

        Assert.Single(next.GetProperty("nodes").EnumerateArray());
        Assert.False(next.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean());

        var id = page.GetProperty("nodes")[0].GetProperty("slug").GetString();
        var found = (await app.Data("query($id: ID!) { node(id: $id) { id } }", new { id = (await app.Data("{ books(first: 1) { nodes { id } } }")).GetProperty("books").GetProperty("nodes")[0].GetProperty("id").GetString() }));

        Assert.NotNull(found.GetProperty("node").GetProperty("id").GetString());

        var bookId = (await app.Data("{ books(first: 1) { nodes { id } } }")).GetProperty("books").GetProperty("nodes")[0].GetProperty("id").GetString();
        var deleted = (await app.Data("mutation($id: ID!) { deleteBook(input: { id: $id }) { deletedId } }", new { id = bookId })).GetProperty("deleteBook");

        Assert.Equal(bookId, deleted.GetProperty("deletedId").GetString());
        Assert.Equal(2, (await app.Data("{ books { totalCount } }")).GetProperty("books").GetProperty("totalCount").GetInt32());
        Assert.NotNull(id);
    }

    [Fact]
    public async Task TheTimestampsPatternIsFilledByTheFrameworkOnCreateAndOnEveryChange()
    {
        await using var app = await ShelfApp.StartAsync();

        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var created = (await CreateBook(app, "stamped", "Stamped")).GetProperty("createBook").GetProperty("book");

        // Timestamps travel to the second, so the change has to be at least a second later to be visible.
        await Task.Delay(1100, Token);

        var updated = (await app.Data(
            "mutation($id: ID!) { updateBook(input: { id: $id, title: \"Renamed\" }) { book { createdAt updatedAt title } } }",
            new { id = created.GetProperty("id").GetString() })).GetProperty("updateBook").GetProperty("book");

        Assert.Equal(created.GetProperty("createdAt").GetString(), updated.GetProperty("createdAt").GetString());
        Assert.True(DateTimeOffset.Parse(created.GetProperty("createdAt").GetString()!, System.Globalization.CultureInfo.InvariantCulture) > before);
        Assert.True(
            DateTimeOffset.Parse(updated.GetProperty("updatedAt").GetString()!, System.Globalization.CultureInfo.InvariantCulture)
            > DateTimeOffset.Parse(created.GetProperty("updatedAt").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task AnAnonymousViewerCannotWriteAndSeesAnErrorSayingSo()
    {
        await using var app = await ShelfApp.StartAsync();

        var result = await app.Graph(
            "mutation { createBook(input: { slug: \"nope\", title: \"Nope\", author: \"A\", status: READING }) { book { id } } }",
            viewer: null);
        var error = result.GetProperty("errors")[0];

        Assert.Equal("FORBIDDEN", error.GetProperty("extensions").GetProperty("code").GetString());
        Assert.Equal("Sign in to change the shelf.", error.GetProperty("message").GetString());
        Assert.Equal(0, (await app.Data("{ books { totalCount } }", viewer: "ada")).GetProperty("books").GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task AWishlistIsPrivateToSignedInViewers()
    {
        await using var app = await ShelfApp.StartAsync();
        var wished = Id(await CreateBook(app, "wished", "Someday", "WISHLIST"), "createBook", "book");

        var anonymous = await app.Graph("query($id: ID!) { book(id: $id) { title } }", new { id = wished }, viewer: null);
        var signedIn = await app.Data("query($id: ID!) { book(id: $id) { title } }", new { id = wished });

        Assert.Equal("FORBIDDEN", anonymous.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
        Assert.Equal("Someday", signedIn.GetProperty("book").GetProperty("title").GetString());
    }

    [Fact]
    public async Task APublishedQueryAnActionAndBothKindsOfEdgeWork()
    {
        await using var app = await ShelfApp.StartAsync();
        var book = Id(await CreateBook(app, "dune", "Dune"), "createBook", "book");

        await CreateBook(app, "other", "Other", "FINISHED");

        var tag = Id(await app.Data("mutation { createTag(input: { slug: \"sf\", name: \"Science fiction\" }) { tag { id } } }"), "createTag", "tag");

        await app.Data("mutation($b: ID!, $t: ID!) { updateBook(input: { id: $b, tags: [$t] }) { book { id } } }", new { b = book, t = tag });
        await app.Data("mutation($b: ID!) { createNote(input: { body: \"Spice.\", book: $b }) { note { id } } }", new { b = book }).ContinueWith(_ => 0, Token);

        var reading = (await app.Data("{ readingBooks { totalCount nodes { title tags { nodes { name } } } } }")).GetProperty("readingBooks");

        Assert.Equal(1, reading.GetProperty("totalCount").GetInt32());
        Assert.Equal("Dune", reading.GetProperty("nodes")[0].GetProperty("title").GetString());
        Assert.Equal(["Science fiction"], reading.GetProperty("nodes")[0].GetProperty("tags").GetProperty("nodes").EnumerateArray().Select(n => n.GetProperty("name").GetString()));

        var finished = (await app.Data("mutation($id: ID!) { finishBook(input: { id: $id }) { book { status } } }", new { id = book })).GetProperty("finishBook").GetProperty("book");

        Assert.Equal("FINISHED", finished.GetProperty("status").GetString());
        Assert.Equal(0, (await app.Data("{ readingBooks { totalCount } }")).GetProperty("readingBooks").GetProperty("totalCount").GetInt32());

        var inverse = (await app.Data("query($t: ID!) { tag(id: $t) { books { nodes { slug } } } }", new { t = tag })).GetProperty("tag").GetProperty("books");

        Assert.Equal(["dune"], inverse.GetProperty("nodes").EnumerateArray().Select(n => n.GetProperty("slug").GetString()));
    }

    [Fact]
    public async Task ASlugIsUnique()
    {
        await using var app = await ShelfApp.StartAsync();

        await CreateBook(app, "same", "One");

        var second = await app.Graph("mutation { createBook(input: { slug: \"same\", title: \"Two\", author: \"A\", status: READING }) { book { id } } }");

        Assert.Equal("BAD_USER_INPUT", second.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
        Assert.Equal(1, (await app.Data("{ books { totalCount } }")).GetProperty("books").GetProperty("totalCount").GetInt32());
    }

    [GeneratedRegex("id=\"tsquid-data\">(.*?)</script>", RegexOptions.Singleline)]
    private static partial Regex Payload();

    [Fact]
    public async Task TheShelfRouteIsServedWithItsDataInlinedForTheViewerWhoAsked()
    {
        await using var app = await ShelfApp.StartAsync();

        await CreateBook(app, "inline", "Inlined", "READING");
        await CreateBook(app, "hidden", "Hidden", "WISHLIST");

        foreach (var viewer in new string?[] { null, "ada" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/");
            request.Headers.Add("Accept", "text/html");

            if (viewer is not null)
            {
                request.Headers.Add("X-Viewer", viewer);
            }

            using var response = await app.Client.SendAsync(request, Token);
            var html = await response.Content.ReadAsStringAsync(Token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl!.Private);
            Assert.True(html.IndexOf("id=\"tsquid-data\"", StringComparison.Ordinal) < html.IndexOf("type=\"module\"", StringComparison.Ordinal));

            using var data = JsonDocument.Parse(Payload().Match(html).Groups[1].Value);
            var books = data.RootElement.GetProperty("responses")[0].GetProperty("response").GetProperty("data").GetProperty("readingBooks").GetProperty("nodes");

            Assert.Equal(["Inlined"], books.EnumerateArray().Select(n => n.GetProperty("title").GetString()));
        }

        // A route that is not server-rendered, and the GraphQL endpoint, are left alone.
        using var plain = new HttpRequestMessage(HttpMethod.Get, "/books/new");
        plain.Headers.Add("Accept", "text/html");

        Assert.DoesNotContain("tsquid-data", await (await app.Client.SendAsync(plain, Token)).Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    [Fact]
    public void TheCommittedClientSchemaIsTheSchemaTheServerBuilds()
    {
        using var provider = new ServiceCollection().AddShelf().AddSingleton(Backend.Adaptor()).BuildServiceProvider();

        var drift = ElephentitySchemaPrinter.Check(
            provider.GetRequiredService<ISchema>(),
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "client", "schema.graphql")));

        Assert.Null(drift);
    }
}
