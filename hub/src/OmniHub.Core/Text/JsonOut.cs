using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OmniHub.Text;

/// <summary>
/// Машинный вывод omnihub (--json). Всё, что не ASCII, экранируется как \uXXXX:
/// поток получается чистым ASCII, и его не испортит ни одна кодовая страница
/// (866, 1251, 65001) между omnihub.exe и тем, кто его читает.
/// </summary>
public static class JsonOut
{
    public static readonly JsonWriterOptions Options = new()
    {
        Encoder = JavaScriptEncoder.Default,
        Indented = true,
        NewLine = "\n",
    };

    public static byte[] Write(Action<Utf8JsonWriter> body)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, Options))
        {
            body(writer);
        }
        return buffer.WrittenSpan.ToArray();
    }

    public static string WriteString(Action<Utf8JsonWriter> body)
    {
        byte[] bytes = Write(body);
        if (!Ascii.IsValid(bytes))
        {
            throw new InvalidOperationException(Texts.JsonNotAscii);
        }
        return Encoding.ASCII.GetString(bytes);
    }
}
