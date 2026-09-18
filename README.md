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
../tools/php composer ci
```

`composer ci` runs style, PHPStan, and the four Elephentity gates. These checks detect
compiler, builder, runtime, and application-wiring incompatibilities together.

Clog currently pins the PHP generator revisions in composer.json. The released Rust
generators require a Cargo build after installation; adopting them, updating CI, and
regenerating Clog is tracked separately in
[issue #1](https://github.com/hsimah-services/elephentity-examples/issues/1).

Generated files are signed. Regenerate through `eleph generate`; never edit them by hand.
Review unexpected drift before replacing output.

## Cross-repository changes

See [`.llms/cross-repo.md`](.llms/cross-repo.md) for affected consumers and required
tracking issues. Follow [the issue template](.llms/issue-template.md) before pushing
contract changes.
