using Elephentity.Cosmos;
using Elephentity.GraphQL;
using Elephentity.GraphQL.Http;
using GraphQL.Types;
using Hsimah;
using Microsoft.Azure.Cosmos;

// hsimah install   creates the Cosmos container the spec describes (it refuses one that already holds documents)
// hsimah schema    prints the GraphQL schema
// hsimah           serves the GraphQL endpoint

var builder = WebApplication.CreateBuilder(args);

// A viewer taken from headers is for development: in production, register an IViewerProvider that reads a session or a token. Without one every
// request is anonymous, which sees published content and can write nothing.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHeaderViewers();
}

builder.Services.AddHsimah();

// The schema does not touch storage, so `schema` needs no account.
if (!args.Contains("schema"))
{
    builder.Services.AddCosmos(builder.Configuration);
}

var app = builder.Build();

if (args.Contains("install"))
{
    await CosmosInstaller.InstallAsync(
        app.Services.GetRequiredService<CosmosClient>(),
        builder.Configuration["Cosmos:Database"] ?? "hsimah",
        app.Services.GetRequiredService<Elephentity.Cosmos.Manifest.CosmosStorageManifest>());
    Console.WriteLine("Installed.");

    return;
}

if (args.Contains("schema"))
{
    Console.Write(ElephentitySchemaPrinter.Print(app.Services.GetRequiredService<ISchema>()));

    return;
}

app.MapElephentityGraphQL("/graphql");

await app.RunAsync();
