using System.Text.Json;

namespace Hsimah.App.Tests;

public sealed class HsimahTests
{
    private const string CreatePage = "mutation($slug: String!, $title: String!, $kind: String, $home: Boolean, $accent: PageAccent) { createPage(input: { slug: $slug, title: $title, content: \"Words.\", defaultItemKind: $kind, featureOnHome: $home, accent: $accent }) { page { id slug status } } }";

    private const string CreateItem = "mutation($slug: String!, $page: ID!) { createPageItem(input: { slug: $slug, title: \"An item\", page: $page, kind: \"News\", content: \"Words.\" }) { pageItem { id kind status } } }";

    private static async Task<string> Page(HsimahApp app, string slug, bool publish = true, string? kind = "News", bool home = false, string accent = "INK")
    {
        var data = await app.Data(CreatePage, new { slug, title = slug, kind, home, accent }, HsimahApp.Editor);
        var id = data.GetProperty("createPage").GetProperty("page").GetProperty("id").GetString()!;

        if (publish)
        {
            await app.Data("mutation($id: ID!) { publishPage(input: { id: $id }) { page { status } } }", new { id }, HsimahApp.Editor);
        }

        return id;
    }

    [Fact]
    public async Task ContentStartsAsADraftThatVisitorsCannotSeeUntilItIsPublished()
    {
        await using var app = await HsimahApp.StartAsync();
        var id = await Page(app, "about", publish: false);
        const string Query = "{ pageBySlug(slug: \"about\") { slug status publishedAt } }";

        Assert.Equal("DRAFT", (await app.Data(Query, role: HsimahApp.Editor)).GetProperty("pageBySlug").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, (await app.Data(Query)).GetProperty("pageBySlug").ValueKind);

        await app.Data("mutation($id: ID!) { publishPage(input: { id: $id }) { page { status } } }", new { id }, HsimahApp.Editor);

        var published = (await app.Data(Query)).GetProperty("pageBySlug");

        Assert.Equal("PUBLISHED", published.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, published.GetProperty("publishedAt").ValueKind);
    }

    [Fact]
    public async Task PublicationChangesNeedThePublishAction()
    {
        await using var app = await HsimahApp.StartAsync();
        var id = await Page(app, "about", publish: false);

        var refusal = await app.Refusal(
            "mutation($id: ID!) { updatePage(input: { id: $id, status: PUBLISHED }) { page { status } } }",
            new { id },
            HsimahApp.Editor);

        Assert.Contains("publish/unpublish action", refusal);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("Has Capitals")]
    [InlineData("double--hyphen")]
    public async Task APageSlugMustBeCanonicalAndNotShadowARoute(string slug)
    {
        await using var app = await HsimahApp.StartAsync();

        await app.Refusal(CreatePage, new { slug, title = "Nope", kind = (string?)null, home = false, accent = "INK" }, HsimahApp.Editor);
    }

    [Fact]
    public async Task OnlyAnAdminDeletes()
    {
        await using var app = await HsimahApp.StartAsync();
        var id = await Page(app, "about");
        const string Delete = "mutation($id: ID!) { deletePage(input: { id: $id }) { deletedId } }";

        Assert.Contains("Staff permission", await app.Refusal(Delete, new { id }, HsimahApp.Editor));
        await app.Refusal(Delete, new { id });

        Assert.Equal(id, (await app.Data(Delete, new { id }, HsimahApp.Admin)).GetProperty("deletePage").GetProperty("deletedId").GetString());
    }

    [Fact]
    public async Task AnItemShowsOnlyWhileItsPageIsPublished()
    {
        await using var app = await HsimahApp.StartAsync();
        var page = await Page(app, "news", publish: false);

        var item = (await app.Data(CreateItem, new { slug = "hello", page }, HsimahApp.Editor)).GetProperty("createPageItem").GetProperty("pageItem");
        await app.Data("mutation($id: ID!) { publishPageItem(input: { id: $id }) { pageItem { status } } }", new { id = item.GetProperty("id").GetString() }, HsimahApp.Editor);

        const string Query = "{ pageItemsOfPage(where: { page: \"news\" }) { totalCount nodes { slug } } }";

        Assert.Equal(0, (await app.Data(Query)).GetProperty("pageItemsOfPage").GetProperty("totalCount").GetInt32());

        await app.Data("mutation($id: ID!) { publishPage(input: { id: $id }) { page { status } } }", new { id = page }, HsimahApp.Editor);

        Assert.Equal(1, (await app.Data(Query)).GetProperty("pageItemsOfPage").GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task PlacedPagesAreThePublishedOnesInTheirPlace()
    {
        await using var app = await HsimahApp.StartAsync();

        await Page(app, "services", home: true, accent: "GREEN");
        await Page(app, "draft", publish: false, home: true, accent: "BLUE");
        await Page(app, "about");

        var home = await app.Data("{ placedPages(where: { home: true }) { nodes { slug } } }");
        var navigation = await app.Data("{ placedPages(where: { home: false }) { nodes { slug } } }");

        Assert.Equal(["services"], home.GetProperty("placedPages").GetProperty("nodes").EnumerateArray().Select(node => node.GetProperty("slug").GetString()));
        Assert.Equal(["about", "services"], navigation.GetProperty("placedPages").GetProperty("nodes").EnumerateArray().Select(node => node.GetProperty("slug").GetString()).Order());
    }

    [Fact]
    public async Task APageOnTheHomePageNeedsAColour()
    {
        await using var app = await HsimahApp.StartAsync();

        Assert.Contains("Choose a colour", await app.Refusal(CreatePage, new { slug = "home", title = "Home", kind = (string?)null, home = true, accent = "INK" }, HsimahApp.Editor));
    }

    [Fact]
    public async Task AListOfItemsResolvesTheirEdgesTogether()
    {
        await using var app = await HsimahApp.StartAsync();
        var page = await Page(app, "news");

        for (var n = 1; n <= 8; n++)
        {
            var item = (await app.Data(CreateItem, new { slug = $"post-{n}", page }, HsimahApp.Editor)).GetProperty("createPageItem").GetProperty("pageItem");
            await app.Data("mutation($id: ID!) { publishPageItem(input: { id: $id }) { pageItem { status } } }", new { id = item.GetProperty("id").GetString() }, HsimahApp.Editor);
        }

        var items = (await app.Data("{ pageItems(first: 20) { nodes { slug page { slug items { totalCount } } } } }")).GetProperty("pageItems").GetProperty("nodes");

        Assert.Equal(8, items.GetArrayLength());
        Assert.All(items.EnumerateArray(), item => Assert.Equal("news", item.GetProperty("page").GetProperty("slug").GetString()));
    }
}
