using Elephentity.Cosmos;
using Elephentity.GraphQL;
using Elephentity.GraphQL.Http;
using GraphQL.Types;
using Microsoft.Azure.Cosmos;
using Shelf.App;

// shelf install   creates the Cosmos container the spec describes (it refuses one that already holds documents)
// shelf schema    prints the GraphQL schema, which client/schema.graphql is checked against
// shelf           serves the app

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddShelf();

// The schema does not touch storage, so `schema` needs no account.
if (!args.Contains("schema"))
{
    builder.Services.AddCosmos(builder.Configuration);
}

var app = builder.Build();

if (args.Contains("install"))
{
    var client = app.Services.GetRequiredService<CosmosClient>();

    await CosmosInstaller.InstallAsync(
        client,
        builder.Configuration["Cosmos:Database"] ?? "shelf",
        app.Services.GetRequiredService<Elephentity.Cosmos.Manifest.CosmosStorageManifest>());
    Console.WriteLine("Installed.");

    return;
}

if (args.Contains("schema"))
{
    Console.Write(ElephentitySchemaPrinter.Print(app.Services.GetRequiredService<ISchema>()));

    return;
}

var shell = await File.ReadAllTextAsync(Path.Combine(app.Environment.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot"), "index.html"));

app.UseShelf(shell);

await app.RunAsync();
