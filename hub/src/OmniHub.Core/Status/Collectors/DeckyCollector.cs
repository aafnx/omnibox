using OmniHub.Catalog;
using OmniHub.Platform;

namespace OmniHub.Status.Collectors;

internal static class DeckyCollector
{
    public static string Services => Path.Combine(Win.UserProfile, "homebrew", "services");
    public static string Plugins => Path.Combine(Win.UserProfile, "homebrew", "plugins");

    public static void Collect(StatusSection s, StatusContext ctx)
    {
        string services = Services;
        string[] loaders = Directory.Exists(services) ? Directory.GetFiles(services, "PluginLoader*.exe") : [];
        if (loaders.Length == 0)
        {
            s.Add("install", StatusTexts.DeckyInstall, $"{StatusTexts.DeckyNotInstalled} ({services})", Level.Missing)
                .With("installed", false)
                .With("path", services);
            return;
        }
        s.Add("install", StatusTexts.DeckyInstall, services, Level.Ok).With("installed", true).With("path", services);

        foreach (string loader in loaders.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            string hash = Win.Sha256(loader);
            DeckyBuild? build = ctx.Catalog.FindDecky(hash);
            string value = build is null ? StatusTexts.DeckyUnknown : $"{StatusTexts.DeckyOfficial} v{build.Version} ({build.Asset})";
            s.Add("loader", $"{StatusTexts.DeckyFile} {Path.GetFileName(loader)}", value, build is null ? Level.Warn : Level.Ok)
                .With("file", Path.GetFileName(loader))
                .With("size", new FileInfo(loader).Length)
                .With("sha256", hash)
                .With("officialVersion", build?.Version);
        }

        string versionFile = Path.Combine(services, ".loader.version");
        if (File.Exists(versionFile))
        {
            string version = File.ReadAllText(versionFile).Trim();
            s.Add("versionFile", StatusTexts.DeckyVersionFile, version).With("version", version);
        }

        if (File.Exists(Path.Combine(services, "Playhub-StartDecky.ps1")))
        {
            s.Add("playhubGuard", StatusTexts.DeckyPlayhubGuard, "Playhub-StartDecky.ps1").With("present", true);
        }

        List<Win.ProcessEntry> running = Win.Processes("PluginLoader");
        s.Add("running", StatusTexts.DeckyRunning,
                running.Count == 0
                    ? StatusTexts.DeckyNotRunning
                    : string.Join(", ", running.Select(p => $"PID {p.Pid} ({p.Name})")),
                running.Count == 0 ? Level.Warn : Level.Ok)
            .With("running", running.Count > 0)
            .With("count", running.Count)
            .With("paths", running.Select(p => p.Path ?? string.Empty).Where(p => p.Length > 0).Distinct().ToArray());

        s.Add(Ports.Describe("port", StatusTexts.DeckyPort, ctx.Listeners, 1337, warnIfClosed: true));

        if (ctx.SteamFolder is string steam && !File.Exists(Path.Combine(steam, ".cef-enable-remote-debugging")))
        {
            s.Add("cef", StatusTexts.DeckyNeedsCef, StatusTexts.DeckyNeedsCefOff, Level.Warn);
        }
    }
}
