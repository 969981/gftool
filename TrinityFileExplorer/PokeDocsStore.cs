using Microsoft.Data.Sqlite;
using Trinity.Core.Cache;

namespace TrinityFileExplorer;

/// <summary>Read-only, portable, game-scoped PokeDocs search and hash resolver.</summary>
public sealed class PokeDocsStore
{
    public sealed record Hit(string Game, string Scope, string Hash, string Name, string Source, long Line, bool HasName);
    private readonly string path;
    private readonly Dictionary<string, string?> resolutionCache = new(StringComparer.Ordinal);
    private readonly object gate = new();
    private PokeDocsStore(string filename) => path = filename;

    /// <summary>Open the single shared SQLite file, not an embedded EXE resource.</summary>
    public static PokeDocsStore OpenShared()
    {
        string database = PokeDocsDatabaseLocator.LocateOrThrow();
        var result = new PokeDocsStore(database);
        using var conn = result.Connect();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM metadata WHERE key='schema_version'";
        if ((string?)cmd.ExecuteScalar() != "1")
            throw new InvalidDataException("Unsupported PokeDocs database schema.");
        return result;
    }

    private SqliteConnection Connect()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = true
        }.ToString());
        connection.Open();
        return connection;
    }

    public string? ResolveName(string game, string scope, ulong hash)
    {
        var hex = hash.ToString("X16");
        var key = $"{game}|{scope}|{hex}";
        lock (gate)
        {
            if (resolutionCache.TryGetValue(key, out var cached)) return cached;
        }
        using var conn = Connect();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT name FROM hash_entries
             WHERE game=@game AND scope=@scope AND hash_hex=@hash AND name IS NOT NULL
             ORDER BY id LIMIT 1";
        cmd.Parameters.AddWithValue("@game", game);
        cmd.Parameters.AddWithValue("@scope", scope);
        cmd.Parameters.AddWithValue("@hash", hex);
        var value = cmd.ExecuteScalar() as string;
        lock (gate) resolutionCache[key] = value;
        return value;
    }

    public IReadOnlyList<Hit> Search(string game, string scope, string query, int limit = 300)
    {
        query = query.Trim();
        limit = Math.Clamp(limit, 1, 1000);
        using var conn = Connect();
        using var cmd = conn.CreateCommand();
        cmd.Parameters.AddWithValue("@game", game);
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@query", "%" + EscapeLike(query) + "%");
        cmd.Parameters.AddWithValue("@scope", scope);
        bool isHash = query.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            || (query.Length == 16 && query.All(Uri.IsHexDigit));
        string plainHash = query.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? query[2..] : query;
        if (isHash && plainHash.Length is > 0 and <= 16 && plainHash.All(Uri.IsHexDigit))
            cmd.Parameters.AddWithValue("@hash", plainHash.ToUpperInvariant().PadLeft(16, '0'));
        else
            isHash = false;

        if (scope == "audio")
        {
            cmd.CommandText = @"SELECT t.game,t.scope,'' AS hash,t.name,s.path,t.line_number
                FROM text_entries t JOIN source_files s ON s.id=t.source_id
                WHERE t.game=@game AND t.scope LIKE 'audio:%'
                AND (@query='%%' OR t.name LIKE @query ESCAPE '\')
                ORDER BY t.id LIMIT @limit";
        }
        else if (scope == "fbs")
        {
            cmd.CommandText = @"SELECT f.game,'fbs' AS scope,'' AS hash,f.path AS name,s.path,0 AS line_number
                FROM flatbuffer_schemas f JOIN source_files s ON s.id=f.source_id
                WHERE f.game=@game AND (f.path LIKE @query ESCAPE '\' OR f.content LIKE @query ESCAPE '\')
                ORDER BY f.path LIMIT @limit";
        }
        else
        {
            string filter = isHash ? "h.hash_hex=@hash" :
                query.Length == 0 ? "1=1" :
                "(h.name LIKE @query ESCAPE '\\' OR h.hash_hex LIKE @query ESCAPE '\\')";
            // Trigram FTS keeps substring searches responsive for 3+ characters.
            if (!isHash && query.Length >= 3 && query.Length > 0 && !query.All(Uri.IsHexDigit)
                && HasTrigram(conn))
            {
                filter = @"h.id IN (SELECT rowid FROM hash_fts WHERE name LIKE @query ESCAPE '\')";
            }
            cmd.CommandText = $@"SELECT h.game,h.scope,h.hash_hex,COALESCE(h.name,''),s.path,h.line_number
                FROM hash_entries h JOIN source_files s ON s.id=h.source_id
                WHERE h.game=@game AND (@scope='all' OR h.scope=@scope) AND {filter}
                ORDER BY h.id LIMIT @limit";
        }

        var results = new List<Hit>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(3);
            results.Add(new Hit(reader.GetString(0), reader.GetString(1),
                reader.GetString(2), name, reader.GetString(4),
                reader.GetInt64(5), !string.IsNullOrEmpty(name)));
        }
        return results;
    }

    private static bool HasTrigram(SqliteConnection conn)
    {
        using var command = conn.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE name='hash_fts' LIMIT 1";
        return command.ExecuteScalar() != null;
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    public string? ReadSchema(string relativePath)
    {
        using var conn = Connect();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT content FROM flatbuffer_schemas WHERE path=@path LIMIT 1";
        cmd.Parameters.AddWithValue("@path", relativePath);
        return cmd.ExecuteScalar() as string;
    }

    public string Revision
    {
        get
        {
            using var conn = Connect();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM metadata WHERE key='pokedocs_revision'";
            return (string?)cmd.ExecuteScalar() ?? "unknown";
        }
    }
}
