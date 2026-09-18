# elephentity-examples

Read [README.md](README.md). Each example owns its Composer dependencies, configuration,
generated tree, and CI checks. Shared infrastructure lives in `tools/` and `.docker/`.

Run commands from the example directory; there is no root composer.json:

```bash
cd clog
../tools/php composer install
../tools/php composer ci
```

Clog currently pins PHP generator revisions. Migration to the Rust releases is tracked
in issue #1 and is separate from documentation cleanup; Composer installation alone
will not build the new Rust binaries.

`clog/generated/` is signed. Never edit it by hand. Regenerate with
`../tools/php vendor/bin/eleph generate` and review the diff. Investigate unexplained
`generate --check` drift before replacing output.

Run `composer ci` before committing. It includes style, PHPStan, and the four gates.

## Cross-repository changes

Consult [`.llms/cross-repo.md`](.llms/cross-repo.md) before changing a shared contract.
Open the required issues before or with the push, using `.llms/issue-template.md`.
