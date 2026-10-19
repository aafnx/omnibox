using OmniHub.Text;

namespace OmniHub.Cli;

/// <summary>Разбор аргументов без сторонних библиотек: команда, --json, --echo.</summary>
internal sealed class Options
{
    public string Command { get; private set; } = "help";
    public bool Json { get; private set; }
    public string? Echo { get; private set; }
    public string? Error { get; private set; }

    public static Options Parse(IReadOnlyList<string> argv)
    {
        var options = new Options();
        bool commandSeen = false;
        for (int i = 0; i < argv.Count; i++)
        {
            string arg = argv[i];
            switch (arg)
            {
                case "--json":
                    options.Json = true;
                    break;
                case "-h":
                case "--help":
                    options.Command = "help";
                    commandSeen = true;
                    break;
                case "--echo":
                    if (i + 1 >= argv.Count)
                    {
                        options.Error = Texts.MissingValue(arg);
                        return options;
                    }
                    options.Echo = argv[++i];
                    break;
                default:
                    if (arg.StartsWith('-'))
                    {
                        options.Error = Texts.UnknownOption(arg);
                        return options;
                    }
                    if (commandSeen)
                    {
                        options.Error = Texts.ExtraArgument(arg);
                        return options;
                    }
                    options.Command = arg;
                    commandSeen = true;
                    break;
            }
        }
        return options;
    }
}
