using Microsoft.Win32;
using OmniHub.Platform;

namespace OmniHub.Status.Collectors;

internal static class AutostartCollector
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string WinlogonKey = @"Software\Microsoft\Windows NT\CurrentVersion\Winlogon";

    /// <summary>Что в задачах планировщика относится к OMNIBOX: папка \OMNIBOX\ и эти слова в команде.</summary>
    private static readonly string[] Keywords =
    [
        "pluginloader", "decky", "steam", "omnihub", "omnibox", "omniconsole", "playhub", "gamingmode", "win-cli", "wincli",
    ];

    public static bool IsRelevant(ScheduledTask task) =>
        task.Path.StartsWith(@"\OMNIBOX\", StringComparison.OrdinalIgnoreCase)
        || task.Commands.Any(c => Keywords.Any(k => c.Contains(k, StringComparison.OrdinalIgnoreCase)))
        || Keywords.Any(k => task.Path.Contains(k, StringComparison.OrdinalIgnoreCase));

    public static void Collect(StatusSection s, StatusContext ctx)
    {
        Tasks(s, ctx);
        Run(s);
        Startup(s);
        Shell(s);
    }

    private static void Tasks(StatusSection s, StatusContext ctx)
    {
        (List<ScheduledTask> all, int unreadable) = ScheduledTasks.ReadAll();
        List<ScheduledTask> relevant = all.Where(IsRelevant).OrderBy(t => t.Path, StringComparer.OrdinalIgnoreCase).ToList();
        bool partial = unreadable > 0 && !ctx.Elevated;
        var item = new StatusItem("tasks", StatusTexts.Tasks,
                partial ? $"{StatusTexts.Count(relevant.Count)}; {StatusTexts.TasksNeedAdmin}" : StatusTexts.Count(relevant.Count),
                partial ? Level.Warn : Level.Info)
            .With("count", relevant.Count)
            .With("total", all.Count)
            .With("unreadable", unreadable);
        foreach (ScheduledTask t in relevant)
        {
            var notes = new List<string>();
            notes.Add(t.Commands.Count > 0 ? string.Join(" ; ", t.Commands) : "—");
            if (!t.Enabled)
            {
                notes.Add(StatusTexts.TaskDisabled);
            }
            if (t.Highest)
            {
                notes.Add(StatusTexts.TaskHighest);
            }
            bool ours = t.Path.StartsWith(@"\OMNIBOX\", StringComparison.OrdinalIgnoreCase);
            item.Add(new StatusItem("task", t.Path, string.Join(" · ", notes), ours && t.Enabled ? Level.Ok : Level.Info)
                .With("path", t.Path)
                .With("enabled", t.Enabled)
                .With("triggers", t.Triggers.ToArray())
                .With("commands", t.Commands.ToArray())
                .With("highest", t.Highest)
                .With("user", t.UserId));
        }
        s.Add(item);
    }

    private static void Run(StatusSection s)
    {
        var item = new StatusItem("run", StatusTexts.RunKeys);
        var sources = new (string Scope, RegistryHive Hive, RegistryView View)[]
        {
            ("HKCU", RegistryHive.CurrentUser, RegistryView.Registry64),
            ("HKLM", RegistryHive.LocalMachine, RegistryView.Registry64),
            ("HKLM32", RegistryHive.LocalMachine, RegistryView.Registry32),
        };
        int count = 0;
        foreach (var (scope, hive, view) in sources)
        {
            foreach (var (name, command) in Win.ReadStrings(hive, RunKey, view))
            {
                count++;
                bool decky = command.Contains("PluginLoader", StringComparison.OrdinalIgnoreCase)
                    || command.Contains("StartDecky", StringComparison.OrdinalIgnoreCase);
                var entry = new StatusItem("entry", $"{scope}: {name}", command, decky ? Level.Warn : Level.Info)
                    .With("scope", scope)
                    .With("name", name)
                    .With("command", command)
                    .With("decky", decky);
                if (decky)
                {
                    entry.Add(new StatusItem("deckyWarn", StatusTexts.RunDeckyWarn, null, Level.Warn));
                }
                item.Add(entry);
            }
        }
        item.Value = StatusTexts.Count(count);
        item.With("count", count);
        s.Add(item);
    }

    private static void Startup(StatusSection s)
    {
        var item = new StatusItem("startup", StatusTexts.StartupFolder);
        int count = 0;
        foreach (var (scope, folder) in new[]
                 {
                     ("user", Environment.GetFolderPath(Environment.SpecialFolder.Startup)),
                     ("all", Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)),
                 })
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                continue;
            }
            foreach (string file in Directory.GetFiles(folder))
            {
                string name = Path.GetFileName(file);
                if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                count++;
                item.Add(new StatusItem("file", name, scope == "user" ? folder : $"{folder} (все пользователи)")
                    .With("scope", scope)
                    .With("path", file));
            }
        }
        item.Value = StatusTexts.Count(count);
        item.With("count", count);
        s.Add(item);
    }

    private static void Shell(StatusSection s)
    {
        string? user = Win.ReadString(RegistryHive.CurrentUser, WinlogonKey, "Shell");
        string machine = Win.ReadString(RegistryHive.LocalMachine, WinlogonKey, "Shell") ?? "explorer.exe";
        string effective = string.IsNullOrWhiteSpace(user) ? machine : user;
        bool replaced = !effective.Trim().Equals("explorer.exe", StringComparison.OrdinalIgnoreCase);
        s.Add("shell", StatusTexts.Shell, replaced ? $"{StatusTexts.ShellReplaced}: {effective}" : effective,
                replaced ? Level.Warn : Level.Info)
            .With("shell", effective)
            .With("userOverride", user)
            .With("replaced", replaced);
    }
}
