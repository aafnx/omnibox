using System.Globalization;
using OmniHub.Catalog;
using OmniHub.Platform;
using OmniHub.Status.Collectors;

namespace OmniHub.Status;

/// <summary>Общие сведения для сборщиков: собираются один раз.</summary>
public sealed class StatusContext
{
    public required OmniCatalog Catalog { get; init; }
    public required string? SteamFolder { get; init; }
    public required List<PortListener> Listeners { get; init; }
    public required bool Elevated { get; init; }
    public CultureInfo Russian { get; } = CultureInfo.GetCultureInfo("ru-RU");
}

/// <summary>
/// omnihub status: собирает отчёт о состоянии OMNIBOX. Только читает, ничего не меняет.
/// Ошибка в одном разделе не мешает остальным: раздел получает строку «Не удалось прочитать».
/// </summary>
public static class StatusRunner
{
    public static readonly string[] SectionIds =
        ["system", "steam", "decky", "plugins", "autostart", "playhub", "gamebar", "audio", "displays", "bluetooth"];

    public static StatusReport Collect()
    {
        var ctx = new StatusContext
        {
            Catalog = OmniCatalog.Load(),
            SteamFolder = Safe(Steam.Folder),
            Listeners = Safe(TcpListeners.All) ?? [],
            Elevated = Win.IsElevated(),
        };

        var report = new StatusReport();
        Run(report, "system", StatusTexts.SystemTitle, SystemCollector.Collect);
        Run(report, "steam", StatusTexts.SteamTitle, s => SteamCollector.Collect(s, ctx));
        Run(report, "decky", StatusTexts.DeckyTitle, s => DeckyCollector.Collect(s, ctx));
        Run(report, "plugins", StatusTexts.PluginsTitle, PluginsCollector.Collect);
        Run(report, "autostart", StatusTexts.AutostartTitle, s => AutostartCollector.Collect(s, ctx));
        Run(report, "playhub", StatusTexts.PlayhubTitle, PlayhubCollector.Collect);
        Run(report, "gamebar", StatusTexts.GameBarTitle, GameBarCollector.Collect);
        Run(report, "audio", StatusTexts.AudioTitle, AudioCollector.Collect);
        Run(report, "displays", StatusTexts.DisplaysTitle, s => DisplayCollector.Collect(s, ctx));
        Run(report, "bluetooth", StatusTexts.BluetoothTitle, BluetoothCollector.Collect);
        return report;
    }

    private static void Run(StatusReport report, string id, string title, Action<StatusSection> collect)
    {
        var section = new StatusSection(id, title);
        try
        {
            collect(section);
        }
        catch (Exception e)
        {
            section.Add("error", StatusTexts.CollectorFailed, $"{e.GetType().Name}: {e.Message}", Level.Error)
                .With("exception", e.GetType().FullName)
                .With("message", e.Message);
        }
        report.Sections.Add(section);
    }

    private static T? Safe<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception)
        {
            return default;
        }
    }
}
