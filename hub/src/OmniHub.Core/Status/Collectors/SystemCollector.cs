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
        string? productName = Win.ReadString(RegistryHive.LocalMachine, CurrentVersionKey, "ProductName");
        string? installationType = Win.ReadString(RegistryHive.LocalMachine, CurrentVersionKey, "InstallationType");
        string product = WindowsName(productName, installationType, buildNumber);
        string value = $"{product} {display}, сборка {build}{(ubr is int u ? "." + u : string.Empty)}".Replace("  ", " ");
        s.Add("os", StatusTexts.Windows, value, Level.Ok)
            .With("product", product)
            .With("installationType", installationType)
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

    /// <summary>
    /// Имя системы. ProductName на Windows 11 до сих пор пишет «Windows 10», поэтому для настольной
    /// Windows смотрим на номер сборки; серверную оставляем как есть.
    /// </summary>
    public static string WindowsName(string? productName, string? installationType, int build)
    {
        if (string.Equals(installationType, "Server", StringComparison.OrdinalIgnoreCase)
            || productName?.Contains("Server", StringComparison.OrdinalIgnoreCase) == true)
        {
            return productName ?? "Windows Server";
        }
        return build >= 22000 ? "Windows 11" : "Windows 10";
    }
}
