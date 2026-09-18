# elephentity-examples

Read [README.md](README.md). Each example owns its Composer dependencies, configuration,
generated tree, and CI checks. Shared infrastructure lives in `tools/` and `.docker/`.

Run commands from the example directory; there is no root composer.json:

```bash
cd clog
../tools/php composer install
../tools/php composer build-generators
../tools/php composer ci
```

Composer installs the tagged Rust generator sources. Run `composer build-generators`
after installation or generator updates, in the same environment used for `eleph`.
`tools/php` provides both PHP and Rust; CI uses the same build script natively.

`clog/generated/` is signed. Never edit it by hand. Regenerate with
`../tools/php vendor/bin/eleph generate` and review the diff. Investigate unexplained
`generate --check` drift before replacing output.

Run `composer ci` before committing. It includes style, PHPStan, and the four gates.

## Cross-repository changes

Consult [`.llms/cross-repo.md`](.llms/cross-repo.md) before changing a shared contract.
Open the required issues before or with the push, using `.llms/issue-template.md`.
