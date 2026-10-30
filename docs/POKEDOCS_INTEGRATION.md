# PokeDocs in GFTool develop

The [pkZukan/PokeDocs](https://github.com/pkZukan/PokeDocs) repository is integrated as a Git submodule at `PokeDocs/` on this branch.

PokeDocs hosts Pokémon FlatBuffers schemas, hash lists and other reference metadata. Its upstream main branch is configured in `.gitmodules`, but submodules are pinned to a reviewed commit for reproducible checkouts.

## Clone and use

```sh
git clone --branch develop --recurse-submodules https://github.com/969981/gftool.git
cd gftool
git submodule status
```

For an existing working copy:

```sh
git checkout develop
git submodule sync --recursive
git submodule update --init --recursive
```

To manually advance PokeDocs to the newest upstream main commit:

```sh
git submodule update --remote PokeDocs
git add PokeDocs
git commit -m "docs: update PokeDocs submodule"
```

## Scope

- `main` retains its standalone source and Windows Nightly release setup.
- `develop` integrates PokeDocs at `PokeDocs/`; no PokeDocs files are duplicated or modified here.
- The Git submodule entry records an upstream commit SHA, so builds and document references are reproducible.
- Sources: https://github.com/pkZukan/gftool and https://github.com/pkZukan/PokeDocs

## Automated upstream tracking

The default branch contains `.github/workflows/sync-develop.yml`. GitHub Actions runs it every 6 hours (and it can be manually dispatched from Actions). It merges the current `main` into `develop` without rewriting history, then fetches the latest `pkZukan/PokeDocs` `main` and advances the PokeDocs submodule pointer. It only pushes when something changes. Merge conflicts stop the workflow for manual resolution, rather than overwriting local work.
