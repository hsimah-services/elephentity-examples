# elephentity-examples

Worked examples for [Elephentity](https://github.com/hsimah-services/elephentity), each
built and tested as a real, independent product rather than a fixture inside the
framework's own repository.

| Example | What it is |
|---|---|
| [`clog/`](clog/) | A lending library for equipment, as a real WordPress plugin. Three post types, one taxonomy, one account-backed entity, exposed to WPGraphQL. |

## Why this repository exists

Elephentity's own CI used to carry one committed example (`examples/clog`) purely to
prove the pipeline works end to end. That coupled the framework's repository to
WordPress — every clone of `elephentity` pulled a WordPress plugin's worth of stubs and
dependencies along with the compiler, which is exactly the coupling
[elephentity#52](https://github.com/hsimah-services/elephentity/issues/52) exists to
remove.

The example still needs to exist somewhere, and needs to keep proving the same thing:
[`.llms/cross-repo.md`](.llms/cross-repo.md) records that regenerating it is what
catches a runtime class renamed in a builder repository without the matching adaptor
repository being updated. This repository is that somewhere — each example is a real
Composer project with its own dependencies, generated tree and CI, so the guarantee is
tested against a real product rather than a fixture nobody would deploy.

## Working on an example

Each example directory is a self-contained Composer project. There is no local PHP —
everything runs in a container, from the example's own directory:

```bash
cd clog
../tools/php composer install
../tools/php vendor/bin/eleph fmt
../tools/php vendor/bin/eleph validate
../tools/php vendor/bin/eleph generate --check
../tools/php vendor/bin/eleph check
../tools/php composer stan
```

## Before you commit anything that crosses a repository boundary

**[`.llms/cross-repo.md`](.llms/cross-repo.md) is the closed list of what crosses.** If
you changed something on it, open an issue on each repository it reaches, before or
with the push. [`.llms/README.md`](.llms/README.md) has the rule and
[`.llms/issue-template.md`](.llms/issue-template.md) the shape.
