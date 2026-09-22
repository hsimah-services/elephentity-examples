# Clog, spec'd

The three post types from [clog](https://github.com/hsimah-services/clog) — Item,
Location and Inventory — written as Elephentity specs, with the generated output committed
so the two can be compared.

`src/` binds the generated contracts and assembles the runtime; `clog.php` connects it
to WordPress hooks. The generated tree and wiring are analysed together at PHPStan
level max.

From this directory:

```bash
../tools/php composer install
../tools/php composer build-generators
../tools/php composer ci
```

The four gates check canonical specs, validation, generated-file drift, and integration
conformance. The orchestrator and PHP builder use 0.6; the WordPress builder uses 0.4 and
the WPGraphQL builder uses 0.3. Compiler/runtime 0.10, WordPress integration 0.2.2,
and WPGraphQL integration 0.2 complete the IR 1.2 mutation lifecycle stack. Composer locks their versions and Cargo locks their Rust
dependencies. Rebuild after generator updates, then regenerate with
`../tools/php vendor/bin/eleph generate` and commit the signed output.

## Reading it

| File | What it shows |
|---|---|
| `clog.php` | Activation, registration, post-link cleanup, and runtime boot hooks. |
| `src/Bootstrap.php` | The assembly order, and why each step comes where it does. |
| `src/Container.php` | Minimal PSR-11 container. |
| `src/Contract/ItemSearch.php` | A hand-written finder, and how it gets a lazy query. |
| `src/Contract/DefaultExpiryIsPaired.php` | A cross-field rule, as one class implementing both halves. |

## What the spec captures cleanly

**The post types become entities with real columns.** `clog_item_id` and
`clog_location_id` stop being untyped postmeta strings and become indexed
`BIGINT UNSIGNED` columns on `wp_clog_inventory`, placed there by the framework because
`Inventory.item` is declared `cardinality: one` — nobody wrote a column name.

**One barcode, one indexed column.** `barcode` is a unique `VARCHAR(64)`, so finding an
item by scanning is an index lookup rather than a scan of serialized blobs.

**`ClogExpiryUnit` is declared once** as `types/ExpiryUnit.yml` and appears as a PHP
backed enum, a `VARCHAR(6)` column sized to its longest member, and a GraphQL enum
whose `DAYS`/`MONTHS` names sit over the stored `days`/`months`. The hand-written
version had that mapping in three files.

**Post linking is optional.** `integrations.wordpress.linkPosts` defaults to false.
Only Item enables it; the builder adds a storage-owned `wp_post_id` and the adaptor
creates a draft post when an Item is created. Domain entities have no post ID field.
Admin list/detail views are generated independently and enabled by default.

**Buildings are a taxonomy, not a duplicated Location.** `Site` (`Loft`, `Cave`) uses the
new `Taxonomy` pattern instead of `ClogPost`, so its rows are `wp_term_taxonomy` terms
rather than a table of their own. `Inventory.site` is declared `cardinality: many` — the
one relation the WordPress driver stores as a term relationship instead of a column — so
moving an item between buildings relinks that edge rather than retiring "Cave Pantry" and
creating "Loft Pantry". `Location` stays the shared vocabulary of storage kinds (`Pantry`,
`Fridge`, `Freezer`, `Deep Freezer`); the two axes only combine on the `Inventory` row
that holds an actual count.

**Not every combination is real, and that lives on an edge too.** There is one Deep
Freezer, in the Cave — `Loft`/`Deep Freezer` is not a thing. `Location.sites` (also
`cardinality: many` against the `Site` taxonomy) declares which buildings a kind of
storage actually exists in, so a client reading `location { sites { name } }` sees only
the sites worth offering for that location, with no query the spec did not already
expose. Checking that `Inventory.site` actually falls within `Inventory.location`'s
`sites` is a rule about two edges on the row plus an edge on a different entity — past
what a field `verify:` can reach — so it is a `preCommit` side effect instead:
`Inventory.siteAvailability`, implemented by `src/Contract/SiteAvailableAtLocation.php`,
which queries `Location.sites` and rejects the commit if the pending site is not in it.

## What it does not capture

Both serialization gaps are closed by changing the model rather than the framework:
`barcode` is now one indexed unique `VARCHAR(64)` per item, and `defaultExpiry` was
already two plain columns. Nothing is serialized except the `json` primitive, which
nothing here uses.

What remains is the WordPress surface.

### The post row stops being the entity

This started as "`post_title` becomes `name`", which is the least of it. The rename
itself is a free choice: Elephentity has no opinion about the field name, only that it must
be *declared*, because the custom table is authoritative and the post row is a
projection. Call it `title` in the spec and clients see `title`.

The real change is everything else that came free with being a post type.

**What WPGraphQL gave you.** `ClogItem` is a registered post type, so WPGraphQL
supplies `title`, `databaseId`, `date`, `modified`, `slug`, `status`, `content` and
`author` without anyone asking, plus root fields `clogItem(id:)` and
`clogItems(where:)` carrying WP's filtering, ordering and cursor pagination.

Under Elephentity, `Item` is built solely from the spec. You get precisely what you declared:
`id`, `createdAt`, `updatedAt`, `name`, `barcode`, `defaultExpiryUnit`,
`defaultExpiryValue`. `createdAt` and `updatedAt` cover what `date` and `modified` did;
the rest either need declaring or need to go.

Root fields and query exposure come from the `wpgraphql` integration. Singular and
plural names are explicit, including `ClogItem` / `ClogItems` and
`ClogInventory` / `ClogInventoryEntries`.

**The adaptor creates optional linked posts.** Item opts into post linking in its
spec. A creation failure fails the entity insertion; a post never supplies the
entity's identity or foreign keys. Existing records are not backfilled. Later post
title/content synchronization and post retention are application decisions.

**Admin views show the authoritative records.** `adminTemplates` defaults to true.
The generated list/detail pages use Elephentity's runtime, read policies, and IDs;
Clog registers them under its parent menu on `admin_menu`. Native post screens are
hidden for Item. Inventory and Location have the same record views without any
linked posts. Setting `adminTemplates: false` restores native post screens only for
entities that enable post linking.

Deleting a linked post clears its `wp_post_id` without deleting the entity. This
preserves Elephentity's relationship checks and prevents WordPress deletion from
silently removing authoritative records.

### Post type registration

The `ClogPost` pattern supplies labels and optional registration arguments through
`configure:`. `integrations.wordpress` separately controls template generation and
post linking, with project defaults and per-entity overrides.

## What this exercise confirmed

The "both or neither" rule on `defaultExpiry` — the resolver returns null unless unit
and value are both set — is exactly the cross-field invariant that has nowhere to live
in a per-value processor. Both fields carry `verify: true`, and the generated
`ItemDefaultExpiryUnitVerifier` receives an `ItemMutationContext` that can read the
other field's pending value. The rule stops being implicit in a resolver and becomes a
named, testable class the application cannot boot without.

## Out of scope

`snapshots.php` reads `wp_posts` and `wp_postmeta` directly to build a SQL dump. With
entities in custom tables it needs rewriting against those, which is a straightforward
change but not one the spec describes.
