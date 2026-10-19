using System.Text;
using System.Text.Json;
using OmniHub.Text;

namespace OmniHub.Tests;

public class EncodingTests
{
    private static readonly EncodingSamples Samples = EncodingSamples.Load();

    [Fact]
    public void Samples_are_read_as_utf8()
    {
        // Те же буквы, записанные кодами: если файл или исходник прочитаны не в UTF-8, тест упадёт.
        Assert.StartsWith("\U0000041F\U00000440\U0000043E\U00000432\U00000435\U00000440\U0000043A\U00000430", Samples.Sample); // «Проверка»
        Assert.Contains("\U00000451\U00000401", Samples.Sample); // «ёЁ»
        Assert.Contains("\U0001F3AE", Samples.Sample); // эмодзи вне BMP (суррогатная пара)
        Assert.Equal("Проверка", Samples.Sample[..8]);
    }

    [Fact]
    public void Samples_cover_paths_and_devices()
    {
        Assert.Equal(new[] { "Тест Пользователь", "Игры" }, Samples.PathSegments());
        Assert.Contains(Samples.Devices, d => d.StartsWith("Динамики", StringComparison.Ordinal));
        Assert.Contains(Samples.Devices, d => d.StartsWith("Наушники", StringComparison.Ordinal));
    }

    [Fact]
    public void Json_output_is_ascii_and_round_trips()
    {
        string json = JsonOut.WriteString(writer =>
        {
            writer.WriteStartObject();
            foreach (var (key, value) in Samples.All())
            {
                writer.WriteString(key, value);
            }
            writer.WriteEndObject();
        });

        Assert.True(Ascii.IsValid(json));
        Assert.Contains("\\u041F", json, StringComparison.OrdinalIgnoreCase); // «П» экранирована
        Assert.Contains("\\uD83C\\uDFAE", json, StringComparison.OrdinalIgnoreCase); // эмодзи как суррогатная пара

        using JsonDocument doc = JsonDocument.Parse(json);
        foreach (var (key, value) in Samples.All())
        {
            Assert.Equal(value, doc.RootElement.GetProperty(key).GetString());
        }
    }

    [Fact]
    public void Json_survives_any_code_page()
    {
        // ASCII одинаково читается в 866, 1251 и UTF-8 — поэтому кодовая страница консоли JSON не портит.
        byte[] bytes = JsonOut.Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("sample", Samples.Sample);
            writer.WriteEndObject();
        });
        string asLatin1 = Encoding.Latin1.GetString(bytes);
        string asUtf8 = Encoding.UTF8.GetString(bytes);
        Assert.Equal(asUtf8, asLatin1);
    }

    [Fact]
    public void Selftest_passes_on_this_machine()
    {
        IReadOnlyList<CheckResult> checks = SelfTest.Run(Samples);
        Assert.All(checks, c => Assert.True(c.Ok, $"{c.Id}: {c.Detail}"));
        Assert.Equal(new[] { "utf8", "json", "files" }, checks.Select(c => c.Id).ToArray());
    }
}
