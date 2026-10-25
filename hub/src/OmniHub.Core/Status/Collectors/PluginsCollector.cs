using System.Text.Json;

namespace OmniHub.Status.Collectors;

public sealed record PluginInfo(string Folder, string Name, string? Version, string? Author, bool ByPlayhub);

internal static class PluginsCollector
{
    public static void Collect(StatusSection s)
    {
        string root = DeckyCollector.Plugins;
        if (!Directory.Exists(root))
        {
            s.Add("folder", StatusTexts.PluginsInstalled, StatusTexts.PluginsFolderMissing, Level.Missing).With("path", root);
            return;
        }

        List<PluginInfo> plugins = Directory.GetDirectories(root)
            .Select(PluginReader.Read)
            .OfType<PluginInfo>()
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var item = new StatusItem("installed", StatusTexts.PluginsInstalled, StatusTexts.Count(plugins.Count))
            .With("path", root)
            .With("count", plugins.Count);
        foreach (PluginInfo p in plugins)
        {
            var parts = new List<string>();
            if (p.Version is not null)
            {
                parts.Add(p.Version);
            }
            parts.Add($"{StatusTexts.PluginFolder} {p.Folder}");
            if (p.ByPlayhub)
            {
                parts.Add(StatusTexts.PluginByPlayhub);
            }
            item.Add(new StatusItem("plugin", p.Name, string.Join(" · ", parts))
                .With("folder", p.Folder)
                .With("name", p.Name)
                .With("version", p.Version)
                .With("author", p.Author)
                .With("playhub", p.ByPlayhub));
        }
        s.Add(item);
    }
}

public static class PluginReader
{
    /// <summary>Плагин Decky: plugin.json (имя, автор) и package.json (версия). Null, если это не плагин.</summary>
    public static PluginInfo? Read(string dir)
    {
        string pluginJson = Path.Combine(dir, "plugin.json");
        if (!File.Exists(pluginJson))
        {
            return null;
        }
        string folder = Path.GetFileName(dir);
        string name = folder;
        string? author = null;
        string? version = null;
        try
        {
            using FileStream pluginStream = File.OpenRead(pluginJson);
            using JsonDocument plugin = JsonDocument.Parse(pluginStream);
            name = Str(plugin.RootElement, "name") ?? folder;
            author = Str(plugin.RootElement, "author");
            version = Str(plugin.RootElement, "version");
        }
        catch (JsonException)
        {
            // Битый plugin.json: показываем папку.
        }

        string packageJson = Path.Combine(dir, "package.json");
        if (File.Exists(packageJson))
        {
            try
            {
                using FileStream packageStream = File.OpenRead(packageJson);
                using JsonDocument package = JsonDocument.Parse(packageStream);
                version = Str(package.RootElement, "version") ?? version;
            }
            catch (JsonException)
            {
                // Версию не узнать.
            }
        }

        bool playhub = File.Exists(Path.Combine(dir, ".playhub-release.json"));
        return new PluginInfo(folder, name, version, author, playhub);
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
