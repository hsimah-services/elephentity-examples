# PageItem

An item attached to a [Page](Page.md): a news article, a product, a framework entry.
It replaces the separate Post and Product entities. Which page an item belongs to and its
`kind` (free text up to 80 characters, such as `Article`, `Product` or `Framework`) say
what it is; the seeded News page holds Articles and the Products page holds Products.

Uses Content, Publishable and Timestamps through the `PageItem` pattern.

- `page` is required and must be a page with a `defaultItemKind`. `kind` defaults to
  that page's `defaultItemKind`.
- `enquiryUrl` is optional: an HTTPS URL or a `mailto:` address, shown as an Enquire
  link.
- `imageUrl` and `imageAlt` are optional: one image shown beside the item in its page's
  list and under the title on the item route. `imageUrl` must be an image stored through
  `POST /media` (`/media/<32 hex>.<png|jpg|webp>`); `imageAlt` is required whenever an
  image is set and is its alt text.
- `author` is the signed-in staff member who created the item, set by the server and
  never an input. Only the display name is public.
- Slugs are unique across all items, so `/<page-slug>/<item-slug>` identifies one item.
  A slug may equal a page's slug. An item is found only under the page that holds it.
- A visitor sees an item only while the item and its page are both published. Deleting a
  page deletes its items.
- Items are listed per page by `pageItemsOfPage(where: { page: <slug> })`, newest
  published first then id descending. `recentPageItems(where: { kind, status })` lists
  newest-edited first for the admin dashboard.

Public routes: `/<page-slug>/<item-slug>`. Admin routes: `/admin/items/<page-slug>`, with
`/new`, `/<id>` and `/<id>/edit`. The News page's items also feed `/news/feed.xml`.

Migration `0003-page-item-image` adds the image columns.
Migration `0002-page-items` replaces the `hsimah_post` and `hsimah_product` tables;
existing rows are discarded.
