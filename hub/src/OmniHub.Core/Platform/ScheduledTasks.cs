using System.Xml;

namespace OmniHub.Platform;

public sealed record ScheduledTask(
    string Path,
    bool Enabled,
    IReadOnlyList<string> Triggers,
    IReadOnlyList<string> Commands,
    string? UserId,
    bool Highest);

/// <summary>
/// Задачи планировщика из XML-файлов в C:\Windows\System32\Tasks. Читать их может только администратор;
/// COM-интерфейс планировщика для одного чтения не нужен. Ничего не меняет.
/// </summary>
public static class ScheduledTasks
{
    private const string Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    public static string Root => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Tasks");

    /// <summary>Все задачи, которые удалось прочитать, и число недоступных файлов.</summary>
    public static (List<ScheduledTask> Tasks, int Unreadable) ReadAll()
    {
        var tasks = new List<ScheduledTask>();
        int unreadable = 0;
        string root = Root;
        if (!Directory.Exists(root))
        {
            return (tasks, 0);
        }
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 };
        foreach (string file in Directory.EnumerateFiles(root, "*", options))
        {
            try
            {
                using var reader = XmlReader.Create(file, ReaderSettings);
                var doc = new XmlDocument();
                doc.Load(reader);
                string relative = "\\" + System.IO.Path.GetRelativePath(root, file);
                tasks.Add(Parse(doc, relative));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or XmlException)
            {
                unreadable++;
            }
        }
        return (tasks, unreadable);
    }

    public static ScheduledTask ParseXml(string xml, string path)
    {
        using var reader = XmlReader.Create(new StringReader(xml), ReaderSettings);
        var doc = new XmlDocument();
        doc.Load(reader);
        return Parse(doc, path);
    }

    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
    };

    private static ScheduledTask Parse(XmlDocument doc, string path)
    {
        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("t", Ns);

        string? enabledText = doc.SelectSingleNode("/t:Task/t:Settings/t:Enabled", ns)?.InnerText;
        bool enabled = !string.Equals(enabledText?.Trim(), "false", StringComparison.OrdinalIgnoreCase);

        var triggers = new List<string>();
        XmlNode? triggersNode = doc.SelectSingleNode("/t:Task/t:Triggers", ns);
        if (triggersNode is not null)
        {
            foreach (XmlNode child in triggersNode.ChildNodes)
            {
                if (child.NodeType == XmlNodeType.Element)
                {
                    triggers.Add(child.LocalName);
                }
            }
        }

        var commands = new List<string>();
        XmlNodeList? execs = doc.SelectNodes("/t:Task/t:Actions/t:Exec", ns);
        if (execs is not null)
        {
            foreach (XmlNode exec in execs)
            {
                string command = exec.SelectSingleNode("t:Command", ns)?.InnerText.Trim() ?? string.Empty;
                string arguments = exec.SelectSingleNode("t:Arguments", ns)?.InnerText.Trim() ?? string.Empty;
                commands.Add(arguments.Length > 0 ? command + " " + arguments : command);
            }
        }

        string? userId = doc.SelectSingleNode("/t:Task/t:Principals/t:Principal/t:UserId", ns)?.InnerText.Trim();
        string? runLevel = doc.SelectSingleNode("/t:Task/t:Principals/t:Principal/t:RunLevel", ns)?.InnerText.Trim();
        bool highest = string.Equals(runLevel, "HighestAvailable", StringComparison.OrdinalIgnoreCase);

        return new ScheduledTask(path, enabled, triggers, commands, userId, highest);
    }
}
