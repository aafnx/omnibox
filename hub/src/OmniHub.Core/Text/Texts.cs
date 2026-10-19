namespace OmniHub.Text;

/// <summary>
/// Все тексты omnihub для человека. Русские строки живут только здесь,
/// а не разбросаны по коду. Идентификаторы и ключи JSON — на английском.
/// </summary>
public static class Texts
{
    public const string Usage =
        "Использование: omnihub <команда> [--json]\n" +
        "\n" +
        "Команды:\n" +
        "  version              версия omnihub\n" +
        "  selftest             проверка кодировки: русский текст, JSON, файлы\n" +
        "      --echo <текст>   вернуть текст как есть (проверка передачи аргументов)\n" +
        "  help                 эта справка\n" +
        "\n" +
        "Параметры:\n" +
        "  --json               машинный вывод: JSON, только ASCII\n";

    public const string SeeHelp = "Справка: omnihub help";
    public const string SamplesMissing = "Не найден встроенный файл образцов encoding-samples.json.";
    public const string JsonNotAscii = "JSON содержит символы вне ASCII.";

    public const string SelfTestTitle = "проверка кодировки";
    public const string SamplesHeader = "Образцы:";
    public const string ChecksHeader = "Проверки:";
    public const string Utf8Ok = "UTF-8: строки проходят туда и обратно без потерь";
    public const string JsonOk = "JSON: только ASCII, русский текст восстанавливается без потерь";
    public const string FilesOk = "Файлы: русские имена папок и файлов и их содержимое сохраняются";
    public const string FilesFailed = "Файлы: имя или содержимое файла изменилось при записи";
    public const string AllOk = "Итог: всё в порядке.";
    public const string SomeFailed = "Итог: есть ошибки, см. выше.";
    public const string EchoLabel = "Аргумент --echo:";

    public static string CheckFailedFor(string key) => $"Строка «{key}» изменилась при преобразовании";
    public static string FilesError(string message) => $"Файлы: ошибка записи или чтения: {message}";
    public static string UnknownCommand(string command) => $"Неизвестная команда: {command}";
    public static string UnknownOption(string option) => $"Неизвестный параметр: {option}";
    public static string MissingValue(string option) => $"Параметру {option} нужно значение";
    public static string ExtraArgument(string value) => $"Лишний аргумент: {value}";

    public static string ConsoleLine(uint? current, uint? original, bool changed, bool redirected)
    {
        if (redirected)
        {
            return "Консоль: вывод перенаправлен, текст пишется в UTF-8 без BOM";
        }
        if (current is null)
        {
            return "Консоль: не Windows или нет консоли, текст пишется в UTF-8";
        }
        return changed
            ? $"Консоль: кодовая страница {current} на время работы, исходная {original} вернётся при выходе"
            : $"Консоль: кодовая страница {current}";
    }
}
