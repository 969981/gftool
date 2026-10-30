# PokeDocs SQLite - shared portable bundle

This branch bundles five independent GFTool executables and **one** PokeDocs SQLite database.
The database is generated in CI from the pinned PokeDocs submodule, without committing binary data to Git:

```sh
python tools/PokeDocsIndexer/build.py --root PokeDocs --output dist/GFTool-Windows-x64/Data/PokeDocs.sqlite --summary dist/pokedocs-index-summary.json
```

## Portable release layout

```text
GFTool-Windows-x64/
  Data/
    PokeDocs.sqlite
    PokeDocs.version.txt
  TrinityModLoader/
    TrinityModLoader.exe
  TrinityFileExplorer/
    TrinityFileExplorer.exe
  TrinityModelViewer/
    TrinityModelViewer.exe
  TrinitySceneView/
    TrinitySceneView.exe
  TrinityUikitEditor/
    TrinityUikitEditor.exe
  README.txt
  LICENSE.txt
```

The `GFTool.Core` class `Trinity.Core.Cache.PokeDocsDatabaseLocator` is available to **all five tools**. It first resolves `Data/PokeDocs.sqlite` beside the executable, then in the executable directory's **parent/Data** for the integrated bundle. An optional `GFTOOL_POKEDOCS_DB` environment variable points to a pre-existing SQLite file. No file is copied or written during read-only access.

At present Trinity File Explorer integrates SQLite hash resolution and offline search/export UI. The other four apps **can locate the same database via GFTool.Core**, but do not yet have their own PokeDocs search windows. This is a shared data foundation, not a claim that every app already uses the index in its workflows.

**Do not embed the SQLite database into EXEs.** Avoid copying it into any tool subdirectory: there should be exactly one `PokeDocs.sqlite` inside the integrated ZIP. The standalone SQLite ZIP is a separate convenience download, not a second copy within the integrated bundle.

## Index details and game assets

The Python indexer streams source TXT data and records game, scope (FD/TRPAK/Lua/audio), hash, optional name, source and line. Hashes are normalized to 16-digit hexadecimal text. FlatBuffers definitions are searchable. The build validates `PRAGMA integrity_check` and exports `pokedocs-index-summary.json`.

No `.trmdl`, `.trpak`, RomFS or Oodle game binaries are included. Search works offline without game data; actual game file extraction still requires the user to select their own compatible RomFS and supply the required Oodle DLL.

The `develop-nightly` Release is separate from stable `main` Nightly. On upstream PokeDocs changes, the develop synchronization workflow dispatches the develop build.
