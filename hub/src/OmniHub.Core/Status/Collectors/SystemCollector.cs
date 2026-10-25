using Microsoft.Win32;
using OmniHub.Platform;

namespace OmniHub.Status.Collectors;

internal static class SystemCollector
{
    private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    public static void Collect(StatusSection s)
    {
        string? display = Win.ReadString(RegistryHive.LocalMachine, CurrentVersionKey, "DisplayVersion");
        string? build = Win.ReadString(RegistryHive.LocalMachine, CurrentVersionKey, "CurrentBuild");
        int? ubr = Win.ReadInt(RegistryHive.LocalMachine, CurrentVersionKey, "UBR");
        int.TryParse(build, out int buildNumber);
        // ProductName на Windows 11 до сих пор пишет «Windows 10»: смотрим на номер сборки.
        string product = buildNumber >= 22000 ? "Windows 11" : "Windows 10";
        string value = $"{product} {display}, сборка {build}{(ubr is int u ? "." + u : string.Empty)}".Replace("  ", " ");
        s.Add("os", StatusTexts.Windows, value, Level.Ok)
            .With("product", product)
            .With("displayVersion", display)
            .With("build", buildNumber)
            .With("ubr", ubr);

        bool admin = Win.IsElevated();
        bool ssh = Win.InSsh();
        var parts = new List<string> { Environment.UserName, admin ? StatusTexts.Admin : StatusTexts.NotAdmin };
        if (ssh)
        {
            parts.Add(StatusTexts.ViaSsh);
        }
        s.Add("user", StatusTexts.User, string.Join(", ", parts), admin ? Level.Info : Level.Warn)
            .With("name", Environment.UserName)
            .With("elevated", admin)
            .With("ssh", ssh);
    }
}
