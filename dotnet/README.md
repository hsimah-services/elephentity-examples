# Shelf (the .NET example)

A small reading shelf that proves the whole .NET path end to end **on a machine with no PHP**: specs compiled by the Rust `eleph`, C# entities,
Cosmos DB storage, a GraphQL.NET schema and tsquid's initial route data, on ASP.NET Core. It does for .NET what [clog](../clog/) does for PHP: it
is a real product, CI'd, that catches what no single repository's tests can (a renamed runtime type, a drifted `describe` answer, the schema
parity breaking).

| | |
|---|---|
| `spec/` | `Book`, `Note` and `Tag`: the `HasSlug`, `HasTimestamps` and `SignedInWrites` patterns (copied into the project, as patterns are), a read policy, a write policy, one action (`finish`), one to-many edge (`notes`), one many-to-many edge (`tags`), an enum (`ReadingStatus`) and one published query (`readingBooks`). |
| `eleph.json` | Four targets with **pinned** builders: `csharp`, `cosmos`, `graphql-csharp`, and `tsquid` with `"language": "csharp"`. |
| `Generated/` | The signed output, committed. Never edit it; `eleph generate`. |
| `src/App` | The ASP.NET Core host: `AddShelfEntities()` (generated), `Elephentity.Cosmos`, `AddElephentityGraphQL`, `UseElephentityTsquid`, a viewer provider you supply (`AddHeaderViewers()` reads an `X-Viewer` header and is registered only by the tests and by `dotnet run` in development; with none, every request is anonymous), and the handlers for the contracts the spec generated. |
| `tests/Contracts` | The `ContractCheck` entry point that `eleph check` runs. |
| `tests/App.Tests` | GraphQL create, read, page and delete; policy denial; timestamps; uniqueness; edges and the action; tsquid inlining; and the `client/schema.graphql` drift check. |
| `client/` | What a tsquid client would hold: `schema.graphql` (printed by `shelf schema`) and the route manifest. |

## Commands

Everything runs in containers; there is no host dotnet or PHP.

```bash
cd dotnet
eleph install && eleph install --check      # fetch what eleph.json pins
eleph fmt && eleph validate && eleph generate --check && eleph check

ELEPH_DOTNET_SOURCE=../../elephentity-dotnet ./tools/dotnet dotnet test --project tests/App.Tests
```

`tests/App.Tests` runs on the Cosmos emulator when `ELEPH_COSMOS_ENDPOINT` is set (CI always sets it) and on the memory driver otherwise.
Against a real account, set `Cosmos:Endpoint`, `Cosmos:Key` and `Cosmos:Database`, run `dotnet run --project src/App -- install` once, then the app.

## Until the first release (step 22)

- **The programs.** `eleph.json` pins the versions the releases will have. There are no releases yet, so `tools/rehearse-releases` builds the
  commits in `tools/sources.txt` and lays them out exactly as GitHub releases would be, and `eleph install` runs against that (it downloads,
  checks `SHA256SUMS`, unpacks and records for real). When the releases exist, bootstrap `eleph` with `install.sh` instead, delete both files, and
  put the tags in `eleph.json`.
- **The runtime.** `src/App` references `Elephentity.*` by project from a checkout of elephentity-dotnet at the commit in
  `elephentity-dotnet.sha`; they become package references once the packages are on nuget.org.
- **`routes.manifest.json`** is written by hand here (a real app has tsquid generate it).

## Notes from building it

- The `HasTimestamps` pattern in `elephentity-patterns` stamps with a pre-commit trigger and makes the fields `required`, which makes the GraphQL
  create input require them too (the open framework issue about server-written fields). This project's copy uses `managed: created` and
  `managed: modified` instead, so the framework fills them.
- A published collection query with no arguments used to produce an invalid GraphQL schema (an empty `where` input); `readingBooks` is that case.
