using System;
using System.IO;

namespace Trinity.Core.Cache;

/// <summary>
/// Locates one PokeDocs.sqlite for all five GFTool executables.
/// Does not copy or modify the database. Each application may use this shared helper.
/// </summary>
public static class PokeDocsDatabaseLocator
{
    public const string DatabaseFileName = "PokeDocs.sqlite";

    public static string? TryLocate(string? applicationDirectory = null)
    {
        string? overridePath = Environment.GetEnvironmentVariable("GFTOOL_POKEDOCS_DB");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
            return Path.GetFullPath(overridePath);

        string baseDirectory = Path.GetFullPath(applicationDirectory ?? AppContext.BaseDirectory);
        string local = Path.Combine(baseDirectory, "Data", DatabaseFileName);
        if (File.Exists(local)) return local;

        DirectoryInfo? parent = Directory.GetParent(baseDirectory);
        if (parent != null)
        {
            string shared = Path.Combine(parent.FullName, "Data", DatabaseFileName);
            if (File.Exists(shared)) return shared;
        }
        return null;
    }

    public static string LocateOrThrow(string? applicationDirectory = null) =>
        TryLocate(applicationDirectory)
        ?? throw new FileNotFoundException(
            "Cannot find shared Data/PokeDocs.sqlite. Keep the five tool folders and the shared Data folder together, or set GFTOOL_POKEDOCS_DB to an existing SQLite file.");
}
