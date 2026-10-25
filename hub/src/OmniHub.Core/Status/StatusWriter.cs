using System.Globalization;
using System.Text;
using System.Text.Json;
using OmniHub.Text;

namespace OmniHub.Status;

/// <summary>Отчёт для человека (текст) и для программ (JSON, только ASCII).</summary>
public static class StatusWriter
{
    public static string ToText(StatusReport report)
    {
        var sb = new StringBuilder();
        sb.Append(AppInfo.Tool).Append(' ').Append(AppInfo.Version).Append(" — ").Append(StatusTexts.Title).Append('\n');
        foreach (StatusSection section in report.Sections)
        {
            sb.Append('\n').Append(section.Title).Append('\n');
            foreach (StatusItem item in section.Items)
            {
                AppendItem(sb, item, depth: 0);
            }
        }
        sb.Append('\n').Append(StatusTexts.Legend).Append('\n');
        return sb.ToString();
    }

    private static void AppendItem(StringBuilder sb, StatusItem item, int depth)
    {
        sb.Append("  ").Append(' ', depth * 4).Append('[').Append(Mark(item.Level)).Append("] ").Append(item.Label);
        if (!string.IsNullOrEmpty(item.Value))
        {
            sb.Append(": ").Append(item.Value);
        }
        sb.Append('\n');
        foreach (StatusItem child in item.Children)
        {
            AppendItem(sb, child, depth + 1);
        }
    }

    public static string Mark(Level level) => level switch
    {
        Level.Ok => " OK ",
        Level.Warn => " !! ",
        Level.Missing => " -- ",
        Level.Error => "ERR ",
        _ => "    ",
    };

    public static string LevelId(Level level) => level switch
    {
        Level.Ok => "ok",
        Level.Warn => "warn",
        Level.Missing => "missing",
        Level.Error => "error",
        _ => "info",
    };

    public static string ToJson(StatusReport report) => JsonOut.WriteString(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("tool", AppInfo.Tool);
        writer.WriteString("version", AppInfo.Version);
        writer.WriteString("command", "status");
        writer.WriteBoolean("ok", report.Sections.All(s => s.WorstLevel() < Level.Error));
        writer.WriteString("time", report.Time.ToString("o", CultureInfo.InvariantCulture));
        writer.WriteStartArray("sections");
        foreach (StatusSection section in report.Sections)
        {
            writer.WriteStartObject();
            writer.WriteString("id", section.Id);
            writer.WriteString("title", section.Title);
            writer.WriteString("level", LevelId(section.WorstLevel()));
            writer.WriteStartArray("items");
            foreach (StatusItem item in section.Items)
            {
                WriteItem(writer, item);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    private static void WriteItem(Utf8JsonWriter writer, StatusItem item)
    {
        writer.WriteStartObject();
        writer.WriteString("id", item.Id);
        writer.WriteString("label", item.Label);
        writer.WriteString("value", item.Value);
        writer.WriteString("level", LevelId(item.Level));
        writer.WriteStartObject("data");
        foreach (var (key, value) in item.Data)
        {
            WriteValue(writer, key, value);
        }
        writer.WriteEndObject();
        if (item.Children.Count > 0)
        {
            writer.WriteStartArray("items");
            foreach (StatusItem child in item.Children)
            {
                WriteItem(writer, child);
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter writer, string key, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNull(key);
                break;
            case string s:
                writer.WriteString(key, s);
                break;
            case bool b:
                writer.WriteBoolean(key, b);
                break;
            case int i:
                writer.WriteNumber(key, i);
                break;
            case uint u:
                writer.WriteNumber(key, u);
                break;
            case long l:
                writer.WriteNumber(key, l);
                break;
            case ulong ul:
                writer.WriteNumber(key, ul);
                break;
            case double d:
                writer.WriteNumber(key, d);
                break;
            case IEnumerable<string> list:
                writer.WriteStartArray(key);
                foreach (string entry in list)
                {
                    writer.WriteStringValue(entry);
                }
                writer.WriteEndArray();
                break;
            default:
                writer.WriteString(key, Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }
}
