# Clog, spec'd

The inventory model from [clog](https://github.com/hsimah/clog) — Item, Location, Site
and Inventory — written as Elephentity specs, with the generated output committed so
the two can be compared. Storage is SQLite and the API is GraphQL; there is no
WordPress. Runtime, SQLite and GraphQL packages are 0.17; `eleph` is 0.16.1.

From this directory:

```bash
../tools/php composer install
../tools/php composer build-generators
../tools/php composer ci
```

`ci` checks style, PHPStan (level max over hand-written and generated code), canonical
specs, validation, generated-file drift, integration conformance and a smoke test.
Regenerate with `../tools/php eleph generate` and commit the signed output.

```bash
CLOG_DB=clog.sqlite php standalone/cli.php install   # create the schema
CLOG_DB=clog.sqlite php standalone/cli.php schema    # print the GraphQL schema
```

## Reading it

| File | What it shows |
|---|---|
| `spec/` | Four entities, one `Record` pattern, one `ExpiryUnit` type. |
| `src/Runtime/RuntimeFactory.php` | The assembly order, and the contract bindings `BootCheck` proves complete. |
| `src/Contract/` | Hand-written halves of generated contracts: policies, a verifier, an action, a query. |
| `standalone/src/` | Installing the generated DDL, wiring the SQLite adaptor, registering the GraphQL manifest. |
| `tests/smoke.php` | A short end-to-end check against in-memory SQLite: writes, constraints, policies and the generated schema. |

## What the spec captures

**Real columns and relationships.** `Inventory.item` is `cardinality: one`, so the
framework places an indexed foreign key on `app_clog_inventory`. `Inventory.site` is
`cardinality: many` and becomes a join table; moving an entry between sites relinks
that edge.

**One barcode, one indexed column.** `barcode` is a unique `VARCHAR(64)`, so scanning is
an index lookup.

**`ExpiryUnit` is declared once** and appears as a PHP backed enum, a column and a
GraphQL enum.

**Shared behaviour in a pattern.** `Record` supplies timestamps (`managed: created` and
`modified`), a signed-in read policy and an editor write policy to every entity.

**A cross-field rule.** `defaultExpiry` is both-or-neither. Both fields carry
`verify: true` and `DefaultExpiryIsPaired` implements both generated contracts, reading
the other field's pending value from the mutation context.

**An action.** `Item.discontinue` clears the barcode so a scan stops resolving, while
existing inventory keeps pointing at the item.

**A finder.** `Item.search` is a hand-written query returning a lazy `EntityQuery`.

## Not here

The GraphQL schema exposes reads and creates; updates, deletes, the action and search are
reached through the PHP runtime (see `tests/smoke.php` for the runtime calls). Authentication, sessions and
a client belong to the application, not the example.
