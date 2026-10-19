using System.Runtime.InteropServices;
using System.Text;

namespace OmniHub.Platform;

/// <summary>
/// Вывод omnihub всегда идёт в UTF-8 без BOM.
///
/// Если stdout или stderr — консоль Windows (в том числе сессия SSH), на время
/// работы ставится кодовая страница 65001, а при выходе возвращается исходная
/// (на русской Windows обычно 866). Так русский текст читается и в консоли,
/// и у того, кто перенаправил вывод в файл или читает его как UTF-8.
/// Машинный вывод (--json) вообще только ASCII, см. <see cref="Text.JsonOut"/>.
/// </summary>
public static class ConsoleText
{
    private const uint Utf8CodePage = 65001;

    private static uint s_originalCodePage;
    private static int s_restored;

    /// <summary>Кодовая страница консоли до запуска omnihub; null, если консоли нет.</summary>
    public static uint? OriginalCodePage => s_originalCodePage == 0 ? null : s_originalCodePage;

    /// <summary>omnihub сменил кодовую страницу консоли и вернёт её при выходе.</summary>
    public static bool ChangedCodePage { get; private set; }

    public static bool Redirected => Console.IsOutputRedirected;

    public static void Init()
    {
        if (OperatingSystem.IsWindows() && (!Console.IsOutputRedirected || !Console.IsErrorRedirected))
        {
            uint current = Native.GetConsoleOutputCP();
            s_originalCodePage = current;
            if (current != 0 && current != Utf8CodePage && Native.SetConsoleOutputCP(Utf8CodePage) != 0)
            {
                ChangedCodePage = true;
            }
        }

        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true });

        Console.CancelKeyPress += (_, _) => Restore();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Restore();
    }

    /// <summary>Текущая кодовая страница консоли; null вне Windows или без консоли.</summary>
    public static uint? CurrentCodePage()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }
        uint cp = Native.GetConsoleOutputCP();
        return cp == 0 ? null : cp;
    }

    /// <summary>Сбрасывает буферы и возвращает исходную кодовую страницу. Повторный вызов ничего не делает.</summary>
    public static void Restore()
    {
        if (Interlocked.Exchange(ref s_restored, 1) != 0)
        {
            return;
        }
        try
        {
            Console.Out.Flush();
            Console.Error.Flush();
        }
        catch (IOException)
        {
            // Вывод уже закрыт (например, читатель конвейера завершился): возвращаем кодовую страницу всё равно.
        }
        if (ChangedCodePage && OperatingSystem.IsWindows())
        {
            Native.SetConsoleOutputCP(s_originalCodePage);
        }
    }

    private static class Native
    {
        [DllImport("kernel32.dll")]
        internal static extern uint GetConsoleOutputCP();

        [DllImport("kernel32.dll")]
        internal static extern int SetConsoleOutputCP(uint codePageId);
    }
}
