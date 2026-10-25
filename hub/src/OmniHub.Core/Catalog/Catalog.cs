using System.Text.Json;

namespace OmniHub.Catalog;

public sealed record DeckyBuild(string Version, string Asset, string Sha256);

public sealed record Movie(string Id, string File, string Sha256, int Seconds, int Width, int Height);

/// <summary>
/// hub/catalog.json, встроенный в omnihub.exe: что OmniHub знает и ставит,
/// с версиями и SHA-256. Меняется правкой файла, без правки кода.
/// </summary>
public sealed record OmniCatalog(string DeckyRepository, IReadOnlyList<DeckyBuild> DeckyBuilds, IReadOnlyList<Movie> Movies)
{
    public const string ResourceName = "catalog.json";

    public static OmniCatalog Load()
    {
        using Stream stream = typeof(OmniCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("catalog.json is not embedded");
        using JsonDocument doc = JsonDocument.Parse(stream);
        return FromJson(doc.RootElement);
    }

    public static OmniCatalog FromJson(JsonElement root)
    {
        JsonElement decky = root.GetProperty("decky");
        var builds = new List<DeckyBuild>();
        foreach (JsonElement b in decky.GetProperty("builds").EnumerateArray())
        {
            builds.Add(new DeckyBuild(Str(b, "version"), Str(b, "asset"), Str(b, "sha256").ToLowerInvariant()));
        }

        var movies = new List<Movie>();
        foreach (JsonElement m in root.GetProperty("movies").EnumerateArray())
        {
            movies.Add(new Movie(Str(m, "id"), Str(m, "file"), Str(m, "sha256").ToLowerInvariant(),
                m.GetProperty("seconds").GetInt32(), m.GetProperty("width").GetInt32(), m.GetProperty("height").GetInt32()));
        }

        return new OmniCatalog(Str(decky, "repository"), builds, movies);
    }

    public DeckyBuild? FindDecky(string sha256) =>
        DeckyBuilds.FirstOrDefault(b => string.Equals(b.Sha256, sha256, StringComparison.OrdinalIgnoreCase));

    public Movie? FindMovie(string sha256) =>
        Movies.FirstOrDefault(m => string.Equals(m.Sha256, sha256, StringComparison.OrdinalIgnoreCase));

    private static string Str(JsonElement e, string name) =>
        e.GetProperty(name).GetString() ?? throw new InvalidOperationException($"catalog.json: {name} is empty");
}
