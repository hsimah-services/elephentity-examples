# hblake

The content backend of a small consulting site (hbla.ke), written as Elephentity specs and built on the .NET path: C# entities, Cosmos DB storage and a
GraphQL.NET schema. It started as a vibe-coded CMS; the specs under `spec/` are the real ones, and everything around them is the smallest application
that proves them. There is no UI, no sign-in and no media handling.

| | |
|---|---|
| `spec/` | `Page`, `PageItem` and `User`; the `Content`, `Publishable`, `PageItem` and `Timestamps` patterns; the `PageAccent`, `PublicationStatus` and `UserRole` types. The `.md` beside each entity records intent. |
| `eleph.json` | Three targets with **pinned** builders: `csharp`, `cosmos` and `graphql-csharp`. |
| `Generated/` | The signed output, committed. Never edit it; `eleph generate`. |
| `src/App/Domain` | What the spec leaves to the application: the staff write and visibility policies, the field and cross-field verifiers, the `publish` and `unpublish` actions and the six published queries. |
| `src/App` | `AddHsimah()` registers those against the generated wiring; `AddCosmos()` adds storage; `Program.cs` serves `/graphql`. |
| `tests/Contracts` | The `ContractCheck` entry point that `eleph check` runs. |
| `tests/App.Tests` | The GraphQL endpoint in process on the memory driver: the publication lifecycle, role policies, verifiers and queries. |

## What the spec shows

- **A lifecycle in a pattern.** `Publishable` adds `status`, `publishedAt`, `publish` and `unpublish` and the read and write policies to anything that uses it. Visitors read
  only published content; editors write; only admins delete. An item is hidden while its page is a draft.
- **Rules across fields.** Status and `publishedAt` may change only through the actions, and published content cannot be blank. Each of these is one verifier class that reads the
  pending state of the other fields.
- **Pattern composition.** `PageItem` is a pattern that itself uses `Content`, `Publishable` and `Timestamps`, and gives its entity a `page` edge with an inverse `items`.
- **Published queries.** `pageBySlug`, `placedPages`, `recentPages`, `pageItemBySlug`, `pageItemsOfPage` and `recentPageItems` are hand-written finders returning lazy queries.

## Commands

Everything runs in containers; there is no host dotnet.

```bash
curl -fsSL https://github.com/hsimah-services/elephentity/releases/latest/download/install.sh | sh   # eleph, into ~/.local/bin

cd hblake
eleph install && eleph install --check      # fetch the builders eleph.json pins into tools/builders/
eleph fmt && eleph validate && eleph generate --check

./tools/dotnet dotnet build tests/Contracts
./tools/dotnet dotnet run --no-build --project tests/Contracts -- eleph-check
./tools/dotnet dotnet test --project tests/App.Tests
```

`eleph check` runs the same contract check, but needs `dotnet` on the host. To serve against Cosmos, set `Cosmos:Endpoint` (the emulator is
`https://localhost:8081`), `Cosmos:Key` and `Cosmos:Database`, run `dotnet run --project src/App -- install` once, then the app. In development the app believes `X-Viewer` and `X-Role`
(`editor` or `admin`) headers; a real deployment registers a viewer provider that reads a session or a token.

## Versions

| | Version |
|---|---|
| `eleph` | 0.16.1 |
| `eleph-codegen` | 0.7.0 |
| `eleph-gen-csharp`, `eleph-gen-cosmos`, `eleph-gen-graphql-csharp` | 0.1.0-alpha.1 |
| `Elephentity.*` | 0.1.0-alpha.2 (nuget.org), `net10.0` |

## Left out of the original

The original application worked around gaps in Elephentity. This example does not, so what follows is behaviour it does not have, and the gaps behind it.

- **Sign-in and accounts.** Users are rows the spec defines; nothing here creates one. `User` has no GraphQL integration, so there is no way to make an account over the API.
- **Query orderings on Cosmos.** `placedPages`, `recentPages`, `recentPageItems` and `pageItemsOfPage` order by more than one field. Cosmos needs a composite index for that, and the generated
  manifest declares only single-field orderings. The original added the missing ones by hand. The memory driver does not care, so the tests pass; against a real Cosmos account those queries
  probably need the same. Not verified here.
- **String lengths.** `maxLength` on fields without a verifier (`excerpt`, `imageUrl`, `imageAlt`) is not enforced by the C# runtime; the original checked it by hand.
- **Defaults across entities.** A new item must name its `kind`; the original copied the page's `defaultItemKind`. Nor is an author recorded: `author` is an edge, not a GraphQL input.
- **A public author name, item counts and a viewer field** were added to the schema by editing the generated manifest.
- **Serial query execution.** The original ran GraphQL serially to avoid a race in the edge cache when a list resolves many items at once. `AListOfItemsResolvesTheirEdgesTogether` exercises that
  on the memory driver and passes; it has not been run against Cosmos.
