using System.Globalization;
using Microsoft.Win32;
using OmniHub.Catalog;
using OmniHub.Platform;

namespace OmniHub.Status.Collectors;

internal static class SteamCollector
{
    public static void Collect(StatusSection s, StatusContext ctx)
    {
        string? folder = ctx.SteamFolder;
        if (folder is null)
        {
            s.Add("folder", StatusTexts.SteamFolder, StatusTexts.NotFound, Level.Missing);
            return;
        }
        s.Add("folder", StatusTexts.SteamFolder, folder, Level.Ok).With("path", folder);

        string manifest = Path.Combine(folder, "package", "steam_client_win64.manifest");
        long? version = File.Exists(manifest) ? Steam.ParseClientVersion(File.ReadAllText(manifest)) : null;
        if (version is long v)
        {
            DateTimeOffset? date = Steam.VersionDate(v);
            string text = date is DateTimeOffset d
                ? $"{v} ({d.ToLocalTime().ToString("d MMMM yyyy", ctx.Russian)})"
                : v.ToString(CultureInfo.InvariantCulture);
            s.Add("client", StatusTexts.SteamClientVersion, text).With("version", v)
                .With("date", date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        else
        {
            s.Add("client", StatusTexts.SteamClientVersion, StatusTexts.NotFound, Level.Missing);
        }

        bool running = Win.Processes("steam").Any(p => p.Name.Equals("steam", StringComparison.OrdinalIgnoreCase));
        bool bigPicture = Win.ReadInt(RegistryHive.CurrentUser, @"Software\Valve\Steam", "BigPictureInForeground") is int bp && bp != 0;
        s.Add("running", StatusTexts.SteamRunning,
                running ? (bigPicture ? StatusTexts.SteamBigPicture : StatusTexts.SteamDesktop) : StatusTexts.No)
            .With("running", running)
            .With("bigPicture", running && bigPicture);

        string cfg = Path.Combine(folder, "steam.cfg");
        bool blocked = File.Exists(cfg) && Steam.UpdatesBlocked(File.ReadAllText(cfg));
        s.Add("updates", StatusTexts.SteamUpdates, blocked ? StatusTexts.SteamUpdatesBlocked : StatusTexts.SteamUpdatesAllowed)
            .With("blocked", blocked);

        bool cef = File.Exists(Path.Combine(folder, ".cef-enable-remote-debugging"));
        s.Add("cef", StatusTexts.SteamCef, cef ? StatusTexts.SteamCefOn : StatusTexts.SteamCefOff).With("enabled", cef);

        s.Add(Ports.Describe("debugPort", StatusTexts.SteamDebugPort, ctx.Listeners, 8080, warnIfClosed: false));

        Movies(s, folder, ctx.Catalog);
    }

    private static void Movies(StatusSection s, string folder, OmniCatalog catalog)
    {
        string dir = Path.Combine(folder, "config", "uioverrides", "movies");
        string[] files = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.webm") : [];
        if (files.Length == 0)
        {
            s.Add("movies", StatusTexts.SteamMovies, StatusTexts.SteamMoviesNone).With("folder", dir).With("count", 0);
            return;
        }
        int ours = 0;
        var item = new StatusItem("movies", StatusTexts.SteamMovies).With("folder", dir);
        foreach (string file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            string hash = Win.Sha256(file);
            Movie? movie = catalog.FindMovie(hash);
            if (movie is not null)
            {
                ours++;
            }
            string value = movie is null
                ? StatusTexts.SteamMovieForeign
                : $"{StatusTexts.SteamMovieOurs} · {movie.Seconds} с · {movie.Width}×{movie.Height}";
            item.Add(new StatusItem("movie", Path.GetFileName(file), value, movie is null ? Level.Info : Level.Ok)
                .With("file", Path.GetFileName(file))
                .With("sha256", hash)
                .With("omnibox", movie?.Id));
        }
        item.Value = $"{files.Length}, OMNIBOX: {ours}";
        item.With("count", files.Length).With("omnibox", ours);
        s.Add(item);
    }
}
