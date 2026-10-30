#!/usr/bin/env python3
"""Generate a self-contained SQLite search index from a pinned PokeDocs checkout.

Uses only Python's standard library. No game assets are included.
"""
import argparse
import hashlib
import json
import re
import sqlite3
import subprocess
import time
from pathlib import Path

HASH_LINE = re.compile(r"^\s*(?:0[xX])?([0-9a-fA-F]{1,16})(?:\s+(.+?))?\s*$")

SCHEMA = """
CREATE TABLE metadata(key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE source_files(
    id INTEGER PRIMARY KEY, path TEXT NOT NULL UNIQUE, sha256 TEXT NOT NULL,
    file_type TEXT NOT NULL, line_count INTEGER NOT NULL DEFAULT 0,
    imported_count INTEGER NOT NULL DEFAULT 0, skipped_count INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE hash_entries(
    id INTEGER PRIMARY KEY, game TEXT NOT NULL, scope TEXT NOT NULL,
    hash_hex TEXT NOT NULL, name TEXT, extension TEXT,
    source_id INTEGER NOT NULL, line_number INTEGER NOT NULL,
    FOREIGN KEY(source_id) REFERENCES source_files(id)
);
CREATE TABLE text_entries(
    id INTEGER PRIMARY KEY, game TEXT NOT NULL, scope TEXT NOT NULL,
    name TEXT NOT NULL, source_id INTEGER NOT NULL, line_number INTEGER NOT NULL,
    FOREIGN KEY(source_id) REFERENCES source_files(id)
);
CREATE TABLE flatbuffer_schemas(
    id INTEGER PRIMARY KEY, game TEXT NOT NULL, path TEXT NOT NULL,
    content TEXT NOT NULL, source_id INTEGER NOT NULL UNIQUE,
    FOREIGN KEY(source_id) REFERENCES source_files(id)
);
"""
def classify(path: Path):
    parts = path.parts
    game = parts[0] if parts else "OTHER"
    if path.suffix.lower() == ".fbs":
        return game, "fbs", "schema"
    if path.suffix.lower() != ".txt":
        return game, "other", "other"
    if "Hashlists" in parts and "FileSystem" in parts:
        if path.name == "hashes_inside_fd.txt":
            return game, "fd", "hash"
        if path.name == "hashes_inside_trpak.txt":
            return game, "trpak", "hash"
    if "Hashlists" in parts and "Lua" in parts:
        return game, path.stem.removesuffix("_hashes"), "hash"
    if "Hashlists" in parts and "Audio" in parts:
        return game, f"audio:{path.stem}", "text"
    return game, "text", "text"

def parse_hash(line: str):
    match = HASH_LINE.match(line)
    if not match:
        return None
    hash_text, name = match.groups()
    name = name.strip() if name else None
    if name == "":
        name = None
    return f"{int(hash_text, 16):016X}", name

def build(root: Path, output: Path, summary: Path | None, min_rows: int):
    if not root.is_dir():
        raise SystemExit(f"Missing PokeDocs checkout: {root}")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.unlink(missing_ok=True)
    start = time.monotonic()
    connection = sqlite3.connect(output)
    try:
        connection.executescript("PRAGMA journal_mode=OFF; PRAGMA synchronous=OFF; PRAGMA temp_store=MEMORY; PRAGMA cache_size=-32768;" + SCHEMA)
        try:
            revision = subprocess.check_output(
                ["git", "-C", str(root), "rev-parse", "HEAD"],
                text=True, stderr=subprocess.DEVNULL).strip()
        except (OSError, subprocess.CalledProcessError):
            revision = "unknown"
        sources = sorted(x for x in root.rglob("*") if x.is_file() and x.suffix.lower() in (".txt", ".fbs"))
        totals = {"hashes": 0, "named": 0, "schemas": 0, "skipped": 0}
        audits = []
        for path in sources:
            rel = path.relative_to(root)
            game, scope, file_type = classify(rel)
            digest = hashlib.file_digest(path.open("rb"), "sha256").hexdigest() if hasattr(hashlib, "file_digest") else hashlib.sha256(path.read_bytes()).hexdigest()
            cur = connection.execute(
                "INSERT INTO source_files(path,sha256,file_type) VALUES(?,?,?)",
                (rel.as_posix(), digest, file_type))
            source_id = cur.lastrowid
            line_count = valid = skipped = 0
            if file_type == "schema":
                content = path.read_text(encoding="utf-8-sig", errors="replace")
                line_count = content.count("\n") + bool(content)
                connection.execute("INSERT INTO flatbuffer_schemas(game,path,content,source_id) VALUES(?,?,?,?)",
                                   (game, rel.as_posix(), content, source_id))
                valid = 1
                totals["schemas"] += 1
            else:
                batch = []
                with path.open("r", encoding="utf-8-sig", errors="replace") as stream:
                    for line_count, line in enumerate(stream, 1):
                        raw = line.strip()
                        if not raw or raw.startswith("#"):
                            skipped += 1
                            continue
                        if file_type == "hash":
                            parsed = parse_hash(raw)
                            if parsed is None:
                                skipped += 1
                                continue
                            hash_hex, name = parsed
                            ext = Path(name.replace("\\", "/")).suffix.lower() if name else ""
                            batch.append((game, scope, hash_hex, name, ext, source_id, line_count))
                            if len(batch) >= 10000:
                                connection.executemany("INSERT INTO hash_entries(game,scope,hash_hex,name,extension,source_id,line_number) VALUES(?,?,?,?,?,?,?)", batch)
                                valid += len(batch)
                                batch.clear()
                        elif file_type == "text":
                            batch.append((game, scope, raw, source_id, line_count))
                            if len(batch) >= 10000:
                                connection.executemany("INSERT INTO text_entries(game,scope,name,source_id,line_number) VALUES(?,?,?,?,?)", batch)
                                valid += len(batch)
                                batch.clear()
                    if batch:
                        if file_type == "hash":
                            connection.executemany("INSERT INTO hash_entries(game,scope,hash_hex,name,extension,source_id,line_number) VALUES(?,?,?,?,?,?,?)", batch)
                        else:
                            connection.executemany("INSERT INTO text_entries(game,scope,name,source_id,line_number) VALUES(?,?,?,?,?)", batch)
                        valid += len(batch)
                totals["hashes" if file_type == "hash" else "named"] += valid
            totals["skipped"] += skipped
            connection.execute("UPDATE source_files SET line_count=?,imported_count=?,skipped_count=? WHERE id=?",
                               (line_count, valid, skipped, source_id))
            audits.append({"source": rel.as_posix(), "format": file_type, "lines": line_count, "records": valid, "skipped": skipped})
            if file_type == "hash" and scope in ("fd", "trpak") and valid < min_rows:
                raise ValueError(f"Suspiciously few hash records in {rel}: {valid}")
        if totals["hashes"] < min_rows:
            raise ValueError(f"Only {totals['hashes']} total hashes parsed")
        connection.executescript("""
            CREATE INDEX idx_hash_exact ON hash_entries(game,scope,hash_hex);
            CREATE INDEX idx_hash_game ON hash_entries(game,scope,id);
            CREATE INDEX idx_text_game ON text_entries(game,scope,id);
        """)
        try:
            connection.execute("CREATE VIRTUAL TABLE hash_fts USING fts5(name, content='hash_entries',content_rowid='id',tokenize='trigram')")
            connection.execute("INSERT INTO hash_fts(hash_fts) VALUES('rebuild')")
            fts = "trigram"
        except sqlite3.OperationalError as exc:
            connection.execute("DROP TABLE IF EXISTS hash_fts")
            fts = f"unavailable: {exc}"
        for key, value in {
            "schema_version": "1", "pokedocs_revision": revision,
            "generated_at_unix": str(int(time.time())), "search_fts": fts,
            "hash_records": str(totals["hashes"]),
        }.items():
            connection.execute("INSERT INTO metadata(key,value) VALUES(?,?)", (key, value))
        connection.commit()
        check = connection.execute("PRAGMA integrity_check").fetchone()[0]
        if check != "ok":
            raise ValueError(f"SQLite integrity check failed: {check}")
        output_size = output.stat().st_size
        result = {"revision": revision, "files": len(sources), "totals": totals,
                  "fts": fts, "db_bytes": output_size, "duration_seconds": round(time.monotonic()-start, 2),
                  "sources": audits, "integrity_check": check}
        if summary:
            summary.parent.mkdir(parents=True, exist_ok=True)
            summary.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
        print(json.dumps({k:v for k,v in result.items() if k!="sources"}, indent=2))
    except BaseException:
        connection.close()
        output.unlink(missing_ok=True)
        raise
    finally:
        connection.close()

if __name__ == "__main__":
    arg = argparse.ArgumentParser()
    arg.add_argument("--root", type=Path, default=Path("PokeDocs"))
    arg.add_argument("--output", type=Path, default=Path("TrinityFileExplorer/Data/PokeDocs.sqlite"))
    arg.add_argument("--summary", type=Path, default=Path("dist/pokedocs-index-summary.json"))
    arg.add_argument("--min-rows", type=int, default=1000)
    opts = arg.parse_args()
    build(opts.root, opts.output, opts.summary, opts.min_rows)
