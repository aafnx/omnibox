using Microsoft.Win32;
using OmniHub.Platform;

namespace OmniHub.Status.Collectors;

internal static class PlayhubCollector
{
    public static void Collect(StatusSection s)
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Playhub");
        string? version = Win.ReadString(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Playhub", "DisplayVersion");
        bool installed = Directory.Exists(folder) || version is not null;
        if (installed)
        {
            s.Add("app", StatusTexts.PlayhubApp, string.Join(", ", new[] { version, folder }.Where(x => !string.IsNullOrEmpty(x))))
                .With("installed", true)
                .With("version", version)
                .With("path", folder);
        }
        else
        {
            s.Add("app", StatusTexts.PlayhubApp, StatusTexts.PlayhubNotInstalled, Level.Missing).With("installed", false);
        }

        bool running = Win.Processes("Playhub").Any();
        if (installed || running)
        {
            s.Add("running", StatusTexts.PlayhubRunning, running ? StatusTexts.Yes : StatusTexts.No).With("running", running);
        }

        string? shell = Win.ReadString(RegistryHive.CurrentUser, @"Software\Microsoft\Windows NT\CurrentVersion\Winlogon", "Shell");
        bool shellReplaced = shell?.Contains("GamingMode", StringComparison.OrdinalIgnoreCase) == true;
        bool agent = Win.Processes("GamingMode").Any();
        bool disabledByUser = File.Exists(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GamingMode", "disabled-by-user"));

        if (!installed && !shellReplaced && !agent)
        {
            return;
        }
        string value = shellReplaced ? StatusTexts.GamingModeShell
            : agent ? StatusTexts.GamingModeAgent
            : disabledByUser ? StatusTexts.GamingModeDisabledByUser
            : StatusTexts.GamingModeOff;
        s.Add("gamingMode", StatusTexts.GamingMode, value, shellReplaced || agent ? Level.Warn : Level.Info)
            .With("shellReplaced", shellReplaced)
            .With("agentRunning", agent)
            .With("disabledByUser", disabledByUser);
    }
}
