# OMNIBOX

Первоначальная настройка OMNIBOX: Windows 11, Xbox Full Screen Experience → OmniConsole → Steam Big Picture.

## Запуск

PowerShell от администратора или SSH под учётной записью администратора:

```powershell
irm https://raw.githubusercontent.com/aafnx/omnibox/main/install.ps1 | iex
```

Скрипт скачивает себя в `C:\ProgramData\OMNIBOX` и сам выбирает режим:

- **Окно** — при запуске на самом OMNIBOX. Отметьте разделы, настройте параметры, нажмите «Проверить» или «Применить». Ход работы виден в журнале.
- **Консоль** — при подключении по SSH. Меню спрашивает разделы, режим (применить или только проверить) и параметры каждого раздела.

Без вопросов, для автоматизации:

```powershell
$o = [scriptblock]::Create((irm https://raw.githubusercontent.com/aafnx/omnibox/main/install.ps1))
& $o -Cli                          # консольное меню даже на рабочем столе
& $o -All                          # все разделы с параметрами по умолчанию
& $o -Module wake-fix -Check       # только диагностика выбранных разделов
```

## Разделы

| Раздел | Что делает |
|---|---|
| `ssh-server` | Ставит OpenSSH Server, включает автозапуск и правило брандмауэра, делает PowerShell оболочкой по умолчанию. Параметры: публичный ключ администратора (хранится только на OMNIBOX), откуда разрешить подключение, вход по паролю. Изнутри SSH-сессии sshd не перезапускается: изменения `sshd_config` применятся после перезагрузки. Вход по паролю не отключится, пока нет ни одного ключа. |
| `wake-fix` | Будит OMNIBOX только по Wake-on-LAN и с приёмника Steam Controller: отключает таймеры пробуждения, пробуждение для обслуживания, Windows Update и задач планировщика; сетевая карта — только magic packet. Нужна перезагрузка. |
| `apps` | Ставит или обновляет до последней версии через winget (источник `winget`, без Microsoft Store): Brave, Steam, Epic Games Launcher, GOG Galaxy. |

## Телевизор

[`tv/webos`](tv/webos) — приложение OmniBox для LG webOS: одна кнопка будит OMNIBOX по Wake-on-LAN и переключает телевизор на его вход HDMI. Умеет искать устройства в сети и находить MAC по IP. Установка через Developer Mode, подробности в [его README](tv/webos/README.md).

## Устройство

```
install.ps1          загрузка и выбор режима
lib/gui.ps1          окно (WinForms)
lib/cli.ps1          консольное меню
lib/runner.ps1       запуск раздела с параметрами
lib/common.ps1       общие функции
modules/modules.json названия, описания и параметры разделов
modules/<id>.ps1     сами разделы
tv/webos/            приложение OmniBox для LG webOS
```

Окно и меню строятся по `modules.json`, сами разделы ничего не спрашивают и получают все значения параметрами.

## Как добавить раздел

1. Скрипт `modules/<id>.ps1`:
   - `param([switch]$Check, ...)`: при `-Check` только диагностика, без изменений;
   - остальные значения — параметрами, без `Read-Host`;
   - повторный запуск безопасен;
   - совместим с Windows PowerShell 5.1 (`else` на одной строке с `}`);
   - только ASCII: PowerShell 5.1 читает файлы без BOM в ANSI, и кириллица ломается. Файлы с русским текстом (как `lib/gui.ps1`) — UTF-8 с BOM.
2. Запись в `modules/modules.json`: `id`, `title`, `description`, `reboot` и `options`. Типы параметров:
   - `text` — строка;
   - `choice` — один вариант из `choices`;
   - `multi` — несколько вариантов из `choices`, передаются массивом.
3. Список приложений для `apps` правится там же, в `choices`: `id` — это ID пакета winget.

Никаких личных данных в репозитории (IP, MAC, ключи, пароли, токены): он публичный.
