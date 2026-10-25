# OmniHub

`omnihub.exe` — один файл без установки .NET, который будет ставить и чинить Decky, плагины и Steam на OMNIBOX, а также управлять звуком и экранами. Его вызывают SSH-разделы `install.ps1`, окно OmniHub и Decky-плагин OmniHub в QAM.

Готово: `status` — отчёт о состоянии OMNIBOX, только чтение; `selftest` — проверка кодировки; `version`, `help`.

## Команды

```powershell
omnihub status             # состояние OMNIBOX, ничего не меняет
omnihub status --json      # то же для программ и Home Assistant
omnihub version            # версия
omnihub selftest           # проверка кодировки на этой машине
omnihub selftest --json    # то же для программ
omnihub selftest --echo "Наушники (Sony)"   # проверка передачи аргумента
```

Коды выхода: `0` — успех, `1` — проверка не прошла, `2` — ошибка в аргументах.

## omnihub status

Разделы отчёта: система, Steam, Decky, плагины Decky, автозапуск, Playhub, Game Bar, звук, экраны, Bluetooth. Каждая строка помечена: `[ OK ]` в порядке, `[ !! ]` стоит посмотреть, `[ -- ]` нет, `[ERR ]` не удалось прочитать. Ошибка одного раздела не мешает остальным.

- Decky и заставки Steam сверяются по SHA-256 с [`catalog.json`](catalog.json): видно, официальная ли сборка Decky стоит или форк.
- Порты 1337 (Decky) и 8080 (отладка Steam): кто слушает и доступен ли порт из сети.
- Автозапуск: задачи планировщика (OMNIBOX, Steam, Decky, Playhub, WinCLI), ключи `Run`, папка автозагрузки, оболочка Windows. Задачи планировщика видны только администратору.
- Звук — Core Audio, экраны — DisplayConfig (разрешение, частота, HDR, основной), Bluetooth — сопряжённые устройства и подключены ли они.

## Кодировка

Русский и английский текст должен выглядеть правильно в консоли, по SSH, в JSON, в логах, в окне и в QAM.

- Вывод `omnihub.exe` всегда идёт в UTF-8 без BOM. Если вывод попадает в консоль, на время работы ставится кодовая страница 65001, а при выходе возвращается исходная (на русской Windows обычно 866).
- Машинный вывод `--json` — только ASCII: русский текст экранируется как `\uXXXX`. Его не испортит ни одна кодовая страница, а `ConvertFrom-Json` и любой парсер JSON восстанавливают текст.
- Из PowerShell `omnihub.exe` вызывается через `Invoke-OmniHub` из [`lib/omnihub.ps1`](../lib/omnihub.ps1): вывод читается как UTF-8 через `System.Diagnostics.Process`, консоль вызывающего не меняется.
- Русские тексты интерфейса живут в `src/OmniHub.Core/Text/Texts.cs`. Образцы для проверок лежат в `src/OmniHub.Core/Text/encoding-samples.json`, это общий эталон для C# и PowerShell.
- `tools/check-encoding.ps1` проверяет файлы репозитория: UTF-8, BOM у `.ps1` с не-ASCII символами, отсутствие BOM в остальных файлах, CRLF у `.ps1` и LF у остальных.
- `tools/test-encoding.ps1` прогоняет `omnihub.exe` через Windows PowerShell 5.1 и PowerShell 7: обычный вызов, `Invoke-OmniHub`, русские аргументы, кавычки, путь с пробелами, запуск из папки с русским именем.

Эмодзи в старой консоли Windows может показываться квадратиком: это шрифт консоли, а не кодировка. В Windows Terminal и по SSH он виден нормально.

## Сборка

Нужен .NET SDK 10. Сборку, тесты и проверки на Windows делает GitHub Actions ([`.github/workflows/hub.yml`](../.github/workflows/hub.yml)), готовый `omnihub.exe` лежит в артефактах сборки.

```powershell
cd hub
dotnet test tests/OmniHub.Tests
dotnet publish src/OmniHub.Cli -c Release -r win-x64 -o out
..\tools\test-encoding.ps1 -Exe out\omnihub.exe
```

## Устройство

```
src/OmniHub.Core/   ядро: тексты, JSON, консоль, отчёт status, чтение Windows (Platform/)
src/OmniHub.Cli/    omnihub.exe (Native AOT)
tests/              модульные тесты xUnit
catalog.json        что OmniHub знает и ставит: версии и SHA-256
```
