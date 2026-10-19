using System.Text.Json;
using OmniHub.Platform;
using OmniHub.Text;

namespace OmniHub.Cli;

/// <summary>
/// Команды omnihub. Коды выхода: 0 — успех, 1 — проверка не прошла, 2 — ошибка в аргументах.
/// </summary>
internal static class Commands
{
    public static int Run(string[] argv)
    {
        Options options = Options.Parse(argv);
        if (options.Error is not null)
        {
            Console.Error.WriteLine(options.Error);
            Console.Error.WriteLine(Texts.SeeHelp);
            return 2;
        }

        return options.Command switch
        {
            "help" => Help(),
            "version" => Version(options),
            "selftest" => SelfTestCommand(options),
            _ => Unknown(options.Command),
        };
    }

    private static int Help()
    {
        Console.Out.Write($"{AppInfo.Tool} {AppInfo.Version}\n\n{Texts.Usage}");
        return 0;
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine(Texts.UnknownCommand(command));
        Console.Error.WriteLine(Texts.SeeHelp);
        return 2;
    }

    private static int Version(Options options)
    {
        if (options.Json)
        {
            WriteJson(writer =>
            {
                writer.WriteStartObject();
                WriteHeader(writer, "version", ok: true);
                writer.WriteString("path", Environment.ProcessPath);
                writer.WriteEndObject();
            });
        }
        else
        {
            Console.Out.WriteLine($"{AppInfo.Tool} {AppInfo.Version}");
        }
        return 0;
    }

    private static int SelfTestCommand(Options options)
    {
        EncodingSamples samples = EncodingSamples.Load();
        IReadOnlyList<CheckResult> checks = SelfTest.Run(samples);
        bool ok = checks.All(c => c.Ok);
        uint? codePage = ConsoleText.CurrentCodePage();

        if (options.Json)
        {
            WriteJson(writer =>
            {
                writer.WriteStartObject();
                WriteHeader(writer, "selftest", ok);

                writer.WriteStartArray("checks");
                foreach (CheckResult check in checks)
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", check.Id);
                    writer.WriteBoolean("ok", check.Ok);
                    writer.WriteString("detail", check.Detail);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();

                writer.WriteStartObject("samples");
                writer.WriteString("sample", samples.Sample);
                writer.WriteString("path", samples.UserPath);
                writer.WriteString("fileName", samples.FileName);
                writer.WriteStartArray("devices");
                foreach (string device in samples.Devices)
                {
                    writer.WriteStringValue(device);
                }
                writer.WriteEndArray();
                writer.WriteString("english", samples.English);
                writer.WriteEndObject();

                if (options.Echo is not null)
                {
                    writer.WriteString("echo", options.Echo);
                }

                writer.WriteStartObject("console");
                writer.WriteBoolean("redirected", ConsoleText.Redirected);
                WriteNumberOrNull(writer, "codePage", codePage);
                WriteNumberOrNull(writer, "originalCodePage", ConsoleText.OriginalCodePage);
                writer.WriteEndObject();

                writer.WriteEndObject();
            });
            return ok ? 0 : 1;
        }

        TextWriter o = Console.Out;
        o.WriteLine($"{AppInfo.Tool} {AppInfo.Version} — {Texts.SelfTestTitle}");
        o.WriteLine();
        o.WriteLine(Texts.SamplesHeader);
        foreach (var (_, value) in samples.All())
        {
            o.WriteLine("  " + value);
        }
        o.WriteLine();
        o.WriteLine(Texts.ChecksHeader);
        foreach (CheckResult check in checks)
        {
            o.WriteLine($"  [{(check.Ok ? " OK " : "FAIL")}] {check.Detail}");
        }
        o.WriteLine();
        o.WriteLine(Texts.ConsoleLine(codePage, ConsoleText.OriginalCodePage, ConsoleText.ChangedCodePage, ConsoleText.Redirected));
        if (options.Echo is not null)
        {
            o.WriteLine($"{Texts.EchoLabel} {options.Echo}");
        }
        o.WriteLine(ok ? Texts.AllOk : Texts.SomeFailed);
        return ok ? 0 : 1;
    }

    private static void WriteHeader(Utf8JsonWriter writer, string command, bool ok)
    {
        writer.WriteString("tool", AppInfo.Tool);
        writer.WriteString("version", AppInfo.Version);
        writer.WriteString("command", command);
        writer.WriteBoolean("ok", ok);
    }

    private static void WriteNumberOrNull(Utf8JsonWriter writer, string name, uint? value)
    {
        if (value is uint number)
        {
            writer.WriteNumber(name, number);
        }
        else
        {
            writer.WriteNull(name);
        }
    }

    private static void WriteJson(Action<Utf8JsonWriter> body)
    {
        Console.Out.Write(JsonOut.WriteString(body));
        Console.Out.Write('\n');
    }
}
