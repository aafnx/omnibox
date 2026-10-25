using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using OmniHub.Platform;

namespace OmniHub.Status.Collectors;

/// <summary>Где Steam и что о нём известно по файлам. Только чтение.</summary>
public static partial class Steam
{
    public static string? Folder()
    {
        string? path = Win.ReadString(RegistryHive.CurrentUser, @"Software\Valve\Steam", "SteamPath");
        if (!string.IsNullOrWhiteSpace(path))
        {
            string full = Path.GetFullPath(path.Replace('/', '\\'));
            if (Directory.Exists(full))
            {
                return full;
            }
        }
        path = Win.ReadString(RegistryHive.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath", RegistryView.Registry32);
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            return path;
        }
        string fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        return Directory.Exists(fallback) ? fallback : null;
    }

    /// <summary>Версия клиента из package\steam_client_win64.manifest: строка "version" "1760000000".</summary>
    public static long? ParseClientVersion(string manifest)
    {
        Match m = VersionRegex().Match(manifest);
        return m.Success && long.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out long v) ? v : null;
    }

    /// <summary>Версии клиента Steam — это время сборки в секундах Unix. Возвращает дату, если число на неё похоже.</summary>
    public static DateTimeOffset? VersionDate(long version) =>
        version is > 1_300_000_000 and < 2_500_000_000 ? DateTimeOffset.FromUnixTimeSeconds(version) : null;

    public static bool UpdatesBlocked(string steamCfg) =>
        steamCfg.Split('\n').Any(line =>
            line.Trim().Replace(" ", string.Empty).Equals("BootStrapperInhibitAll=enable", StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex("\"version\"\\s+\"(\\d+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex VersionRegex();
}
