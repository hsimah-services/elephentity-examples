# Cross-repository work

The compiler and generators communicate over JSON. Runtime adaptors consume generated
PHP manifests. Compatibility across these boundaries requires integration checks.

| Repository | Ownership |
|---|---|
| `elephentity` | PHP compiler, CLI, runtime source, memory adaptor and builder |
| `elephentity-runtime` | Read-only mirror of runtime `src/` and `tests/`; owns its package metadata and tooling |
| `elephentity-codegen` | Rust orchestrator, protocol, signing, output management |
| `elephentity-codegen-php` | Rust PHP builder |
| `elephentity-codegen-wordpress` | Rust WordPress manifest builder and driver capabilities |
| `elephentity-codegen-wpgraphql` | Rust GraphQL manifest builder and integration capabilities |
| `elephentity-wordpress` | WordPress runtime storage adaptor |
| `elephentity-wpgraphql` | WPGraphQL runtime integration |
| `elephentity-examples` | Clog integration project |

Check actual repository contents before assuming additional builders are implemented.

## Contract changes

[The contract surface](cross-repo.md) is the closed list of changes requiring issues.
Open an issue on each affected repository before or with the push, using
[the issue template](issue-template.md). Internal changes need no issue.

Each issue must identify the source commit, affected files, required work, verification
commands, and merge ordering. Cross-link related issues. File runtime source work
against `elephentity`, not the runtime mirror.

Version constraints and pinned revisions differ by consumer. Inspect composer.json and
composer.lock; do not assume every consumer immediately follows main.

The three `.llms/` files must remain identical across the repositories carrying them.
Synchronise changes together and track affected repositories as required above.
