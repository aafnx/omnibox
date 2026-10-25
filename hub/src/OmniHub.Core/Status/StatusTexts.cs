namespace OmniHub.Status;

/// <summary>Русские тексты отчёта omnihub status. Id разделов и строк — в коде сборщиков, на английском.</summary>
public static class StatusTexts
{
    public const string Title = "состояние OMNIBOX";
    public const string Legend = "[ OK ] в порядке   [ !! ] стоит посмотреть   [ -- ] нет   [ERR ] не удалось прочитать";
    public const string CollectorFailed = "Не удалось прочитать";

    public const string Yes = "да";
    public const string No = "нет";
    public const string NotFound = "не найдено";

    // Система
    public const string SystemTitle = "Система";
    public const string Windows = "Windows";
    public const string User = "Пользователь";
    public const string Admin = "администратор";
    public const string NotAdmin = "без прав администратора: задачи планировщика не видны";
    public const string ViaSsh = "через SSH";

    // Steam
    public const string SteamTitle = "Steam";
    public const string SteamFolder = "Папка Steam";
    public const string SteamClientVersion = "Версия клиента";
    public const string SteamRunning = "Запущен";
    public const string SteamBigPicture = "да, Big Picture";
    public const string SteamDesktop = "да, обычный режим";
    public const string SteamUpdates = "Обновления Steam";
    public const string SteamUpdatesBlocked = "запрещены (steam.cfg)";
    public const string SteamUpdatesAllowed = "разрешены";
    public const string SteamCef = "Отладка интерфейса для Decky";
    public const string SteamCefOn = "включена (.cef-enable-remote-debugging)";
    public const string SteamCefOff = "выключена";
    public const string SteamMovies = "Заставки запуска";
    public const string SteamMoviesNone = "своих нет";
    public const string SteamMovieOurs = "OMNIBOX";
    public const string SteamMovieForeign = "не OMNIBOX";
    public const string SteamDebugPort = "Порт отладки 8080";

    // Порты
    public const string PortClosed = "не слушается";
    public const string PortLoopback = "только этот компьютер";
    public const string PortAllInterfaces = "все сетевые интерфейсы — доступен из сети";

    // Decky
    public const string DeckyTitle = "Decky Loader";
    public const string DeckyInstall = "Установка";
    public const string DeckyNotInstalled = "не найдена";
    public const string DeckyFile = "Файл";
    public const string DeckyOfficial = "официальная сборка";
    public const string DeckyUnknown = "неизвестная сборка: форк или другая версия";
    public const string DeckyVersionFile = "Версия (.loader.version)";
    public const string DeckyPlayhubGuard = "Сторож автозапуска Playhub";
    public const string DeckyRunning = "Работает";
    public const string DeckyNotRunning = "нет";
    public const string DeckyPort = "Порт 1337";
    public const string DeckyNeedsCef = "Отладка интерфейса Steam";
    public const string DeckyNeedsCefOff = "выключена — Decky не подключится к Big Picture";

    // Плагины
    public const string PluginsTitle = "Плагины Decky";
    public const string PluginsInstalled = "Установлено";
    public const string PluginsFolderMissing = "папки плагинов нет";
    public const string PluginByPlayhub = "поставил Playhub";
    public const string PluginFolder = "папка";

    // Автозапуск
    public const string AutostartTitle = "Автозапуск";
    public const string Tasks = "Задачи планировщика (OMNIBOX, Steam, Decky, Playhub, WinCLI)";
    public const string TasksNeedAdmin = "часть задач не прочитана: нужны права администратора";
    public const string TaskDisabled = "выключена";
    public const string TaskHighest = "с повышенными правами";
    public const string RunKeys = "Ключи Run";
    public const string RunDeckyWarn = "Decky через Run: под Xbox FSE ненадёжно, переносим в планировщик";
    public const string StartupFolder = "Папка автозагрузки";
    public const string Shell = "Оболочка Windows";
    public const string ShellReplaced = "заменена";

    // Playhub
    public const string PlayhubTitle = "Playhub";
    public const string PlayhubApp = "Приложение";
    public const string PlayhubNotInstalled = "не установлено";
    public const string PlayhubRunning = "Запущено";
    public const string GamingMode = "Gaming Mode";
    public const string GamingModeShell = "включён: оболочка Windows заменена агентом";
    public const string GamingModeAgent = "агент запущен";
    public const string GamingModeOff = "выключен";
    public const string GamingModeDisabledByUser = "отключён пользователем";

    // Game Bar
    public const string GameBarTitle = "Game Bar";
    public const string GameBarGuide = "Кнопка Xbox на геймпаде";
    public const string GameBarGuideOff = "не открывает Game Bar";
    public const string GameBarGuideOn = "открывает Game Bar";

    // Звук
    public const string AudioTitle = "Звук";
    public const string AudioOutputs = "Выход";
    public const string AudioInputs = "Микрофон";
    public const string AudioNone = "устройств нет";
    public const string AudioDefault = "по умолчанию";
    public const string AudioDefaultComms = "по умолчанию для связи";
    public const string AudioMuted = "звук выключен";

    // Экраны
    public const string DisplaysTitle = "Экраны";
    public const string DisplaysCount = "Активных экранов";
    public const string DisplayUnnamed = "Экран";
    public const string DisplayPrimary = "основной";
    public const string HdrOn = "HDR вкл";
    public const string HdrOff = "HDR выкл";
    public const string HdrUnsupported = "без HDR";
    public const string Bits = "бит";
    public const string Hz = "Гц";
    public const string Vrr = "VRR в Windows (оконные игры)";
    public const string VrrOn = "включено";
    public const string VrrOff = "выключено";
    public const string VrrDefault = "по умолчанию";

    // Bluetooth
    public const string BluetoothTitle = "Bluetooth";
    public const string BluetoothRadio = "Адаптер";
    public const string BluetoothNoRadio = "не найден";
    public const string BluetoothUnavailable = "Bluetooth недоступен в этой системе";
    public const string BluetoothDevices = "Устройства";
    public const string BluetoothConnected = "подключено";
    public const string BluetoothPaired = "сопряжено, не подключено";
    public const string BluetoothRemembered = "запомнено, не сопряжено";

    public static string BluetoothClass(int major) => major switch
    {
        1 => "компьютер",
        2 => "телефон",
        4 => "аудио",
        5 => "геймпад, клавиатура или мышь",
        6 => "камера или принтер",
        7 => "носимое",
        _ => "другое",
    };

    public static string Count(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
