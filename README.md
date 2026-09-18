# elephentity-examples

Integration projects for Elephentity. Each example owns its Composer dependencies,
specs, generated tree, wiring, and CI checks.

| Example | Coverage |
|---|---|
| [Clog](clog/) | WordPress post, taxonomy, and account storage with WPGraphQL exposure. |

## Development

Run the container helper from the example directory:

```bash
cd clog
../tools/php composer install
../tools/php composer build-generators
../tools/php composer ci
```

`composer ci` runs style, PHPStan, and the four Elephentity gates. These checks detect
compiler, builder, runtime, and application-wiring incompatibilities together.

Clog uses the Rust orchestrator and all three Rust builders. `build-generators` builds
locked Cargo dependencies from the Composer-installed sources; repeat it after updating
generator packages. The container helper includes PHP, Composer, and Rust and caches
Cargo downloads outside the checkout.

With native PHP/Composer and Rust installed, run `composer install`,
`composer build-generators`, and `composer ci` directly from `clog/`. CI uses this path.
Build and run the binaries in the same environment; host binaries may not run inside
the Linux container.

Generated files are signed. Regenerate through `eleph generate`; never edit them by hand.
Review unexpected drift before replacing output.

## Cross-repository changes

See [`.llms/cross-repo.md`](.llms/cross-repo.md) for affected consumers and required
tracking issues. Follow [the issue template](.llms/issue-template.md) before pushing
contract changes.
