using OmniHub.Cli;
using OmniHub.Platform;

ConsoleText.Init();
try
{
    return Commands.Run(args);
}
finally
{
    ConsoleText.Restore();
}
