# PokeDocs embedded SQLite data

PokeDocs is a pinned Git submodule. The build step converts source files to an offline SQLite database:
\`\`\`sh
python tools/PokeDocsIndexer/build.py --root PokeDocs --output TrinityFileExplorer/Data/PokeDocs.sqlite --summary dist/pokedocs-index-summary.json
\`\`\`
No game assets are included; the SQLite file contains public hashes, names, and FlatBuffers source. On Windows, the database is embedded in TrinityFileExplorer.exe during the develop build and automatically extracted to a portable Data directory on first use. The user never has to import the upstream .txt files.

The importer reads source lines in a streaming manner and preserves game, scope, hash, name, source file and source line. Hashes are stored as uppercase padded 16-digit hexadecimal TEXT to avoid signed SQLite integer overflow. It also indexes Lua symbols, audio names and FlatBuffers schemas. Hash lists with too few parsed rows fail the build rather than silently shipping an empty database.

A full audit is available as pokedocs-index-summary.json in the GitHub Actions build artifacts. The script validates PRAGMA integrity_check. The source git revision is recorded in metadata.

Search never requires RomFS, but exporting actual .trmdl/.trpak payloads requires the user's own compatible game files. Neither game assets nor Oodle libraries are redistributed.

To avoid bloating Git history, generated SQLite files and reports are excluded by .gitignore. Their authoritative release builds are produced by GitHub Actions from a pinned PokeDocs submodule commit.
