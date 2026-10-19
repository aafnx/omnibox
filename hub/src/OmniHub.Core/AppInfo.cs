namespace OmniHub;

public static class AppInfo
{
    public const string Tool = "omnihub";

    /// <summary>Версия из Directory.Build.props, три числа: 0.1.0.</summary>
    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
