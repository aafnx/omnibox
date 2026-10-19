using System.Text;
using System.Text.Json;

namespace OmniHub.Text;

public sealed record CheckResult(string Id, bool Ok, string Detail);

/// <summary>
/// Проверка кодировки на живой системе: строки, JSON и файлы с русскими именами.
/// Ничего не оставляет после себя: временная папка удаляется.
/// </summary>
public static class SelfTest
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static IReadOnlyList<CheckResult> Run(EncodingSamples samples) =>
    [
        Utf8RoundTrip(samples),
        JsonRoundTrip(samples),
        FileRoundTrip(samples),
    ];

    private static CheckResult Utf8RoundTrip(EncodingSamples samples)
    {
        foreach (var (key, value) in samples.All())
        {
            string back = StrictUtf8.GetString(StrictUtf8.GetBytes(value));
            if (!string.Equals(back, value, StringComparison.Ordinal))
            {
                return new CheckResult("utf8", false, Texts.CheckFailedFor(key));
            }
        }
        return new CheckResult("utf8", true, Texts.Utf8Ok);
    }

    private static CheckResult JsonRoundTrip(EncodingSamples samples)
    {
        byte[] json = JsonOut.Write(writer =>
        {
            writer.WriteStartObject();
            foreach (var (key, value) in samples.All())
            {
                writer.WriteString(key, value);
            }
            writer.WriteEndObject();
        });
        if (!Ascii.IsValid(json))
        {
            return new CheckResult("json", false, Texts.JsonNotAscii);
        }
        using JsonDocument doc = JsonDocument.Parse(json);
        foreach (var (key, value) in samples.All())
        {
            if (!string.Equals(doc.RootElement.GetProperty(key).GetString(), value, StringComparison.Ordinal))
            {
                return new CheckResult("json", false, Texts.CheckFailedFor(key));
            }
        }
        return new CheckResult("json", true, Texts.JsonOk);
    }

    private static CheckResult FileRoundTrip(EncodingSamples samples)
    {
        string root = Path.Combine(Path.GetTempPath(), "omnihub-selftest-" + Guid.NewGuid().ToString("N"));
        try
        {
            string[] parts = [root, .. samples.PathSegments()];
            string dir = Path.Combine(parts);
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, samples.FileName);
            File.WriteAllText(file, samples.Sample, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            string content = File.ReadAllText(file, StrictUtf8);
            string listed = Path.GetFileName(Directory.EnumerateFiles(dir).Single());
            bool ok = string.Equals(content, samples.Sample, StringComparison.Ordinal)
                && string.Equals(listed, samples.FileName, StringComparison.Ordinal);
            return new CheckResult("files", ok, ok ? Texts.FilesOk : Texts.FilesFailed);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            return new CheckResult("files", false, Texts.FilesError(e.Message));
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Временная папка останется в %TEMP%; на результат проверки это не влияет.
            }
        }
    }
}
