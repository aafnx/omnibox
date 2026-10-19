using System.Text.Json;

namespace OmniHub.Text;

/// <summary>
/// Образцы текста для проверки кодировки: русский и английский текст, кавычки,
/// эмодзи, путь и имена устройств, как их показывает русская Windows.
/// Хранятся в одном файле encoding-samples.json: его же читает
/// tools/test-encoding.ps1, поэтому у C# и PowerShell один эталон.
/// </summary>
public sealed record EncodingSamples(
    string Sample,
    string UserPath,
    string FileName,
    IReadOnlyList<string> Devices,
    string English)
{
    public const string ResourceName = "encoding-samples.json";

    /// <summary>Все строки с ключами, как в JSON: sample, path, fileName, device0…, english.</summary>
    public IEnumerable<KeyValuePair<string, string>> All()
    {
        yield return new("sample", Sample);
        yield return new("path", UserPath);
        yield return new("fileName", FileName);
        for (int i = 0; i < Devices.Count; i++)
        {
            yield return new("device" + i, Devices[i]);
        }
        yield return new("english", English);
    }

    /// <summary>Две последние папки пути-образца: «Тест Пользователь», «Игры».</summary>
    public string[] PathSegments()
    {
        string[] parts = UserPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[^2..] : parts;
    }

    public static EncodingSamples Load()
    {
        using Stream stream = typeof(EncodingSamples).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(Texts.SamplesMissing);
        using JsonDocument doc = JsonDocument.Parse(stream);
        return FromJson(doc.RootElement);
    }

    public static EncodingSamples FromJson(JsonElement root)
    {
        var devices = new List<string>();
        foreach (JsonElement item in root.GetProperty("devices").EnumerateArray())
        {
            devices.Add(item.GetString() ?? string.Empty);
        }
        return new EncodingSamples(
            Required(root, "sample"),
            Required(root, "path"),
            Required(root, "fileName"),
            devices,
            Required(root, "english"));
    }

    private static string Required(JsonElement root, string name) =>
        root.GetProperty(name).GetString() ?? throw new InvalidOperationException(Texts.SamplesMissing);
}
