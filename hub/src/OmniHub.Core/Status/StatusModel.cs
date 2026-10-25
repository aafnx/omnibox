namespace OmniHub.Status;

/// <summary>Оценка строки отчёта. Порядок важен: раздел получает худшую оценку своих строк.</summary>
public enum Level
{
    Info = 0,
    Ok = 1,
    Missing = 2,
    Warn = 3,
    Error = 4,
}

/// <summary>
/// Строка отчёта: подпись и значение для человека, данные для программ.
/// Id и ключи данных — ASCII; подпись и значение — по-русски.
/// </summary>
public sealed class StatusItem
{
    public StatusItem(string id, string label, string? value = null, Level level = Level.Info)
    {
        Id = id;
        Label = label;
        Value = value;
        Level = level;
    }

    public string Id { get; }
    public string Label { get; }
    public string? Value { get; set; }
    public Level Level { get; set; }
    public List<KeyValuePair<string, object?>> Data { get; } = [];
    public List<StatusItem> Children { get; } = [];

    /// <summary>Значение для JSON: string, bool, int, long, uint, double, string[] или null.</summary>
    public StatusItem With(string key, object? value)
    {
        Data.Add(new(key, value));
        return this;
    }

    public StatusItem Add(StatusItem child)
    {
        Children.Add(child);
        return this;
    }

    public Level WorstLevel()
    {
        Level worst = Level;
        foreach (StatusItem child in Children)
        {
            Level c = child.WorstLevel();
            if (c > worst)
            {
                worst = c;
            }
        }
        return worst;
    }
}

public sealed class StatusSection
{
    public StatusSection(string id, string title)
    {
        Id = id;
        Title = title;
    }

    public string Id { get; }
    public string Title { get; }
    public List<StatusItem> Items { get; } = [];

    public StatusItem Add(StatusItem item)
    {
        Items.Add(item);
        return item;
    }

    public StatusItem Add(string id, string label, string? value = null, Level level = Level.Info) =>
        Add(new StatusItem(id, label, value, level));

    public Level WorstLevel()
    {
        Level worst = Level.Info;
        foreach (StatusItem item in Items)
        {
            Level l = item.WorstLevel();
            if (l > worst)
            {
                worst = l;
            }
        }
        return worst;
    }
}

public sealed class StatusReport
{
    public DateTimeOffset Time { get; } = DateTimeOffset.Now;
    public List<StatusSection> Sections { get; } = [];
}
