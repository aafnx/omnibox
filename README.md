# OMNIBOX

Скрипты для быстрой настройки игрового ПК на Windows 11 как консоли: Xbox Full Screen Experience → OmniConsole → Steam Big Picture.

## Запуск

PowerShell от администратора (или SSH под админом).

```powershell
irm https://raw.githubusercontent.com/aafnx/omnibox/main/install.ps1 | iex
```

Скрипт покажет список модулей и спросит, какие запустить, в каком режиме (применить или только проверить), попросит подтверждение, а после применения предложит перезагрузку.

Без вопросов, для автоматизации (`-All` — все модули, `-Module` — выбранные, `-Check` — только диагностика):

```powershell
& ([scriptblock]::Create((irm https://raw.githubusercontent.com/aafnx/omnibox/main/install.ps1))) -All
& ([scriptblock]::Create((irm https://raw.githubusercontent.com/aafnx/omnibox/main/install.ps1))) -Module wake-fix -Check
```

Скрипты скачиваются в `C:\ProgramData\OMNIBOX`.

## Модули

| Модуль | Что делает |
|---|---|
| `ssh-server` | Ставит OpenSSH Server, включает автозапуск и правило брандмауэра, делает PowerShell оболочкой по умолчанию. В интерактивном режиме спрашивает: публичный ключ для входа админов (хранится только на ПК), разрешить SSH только из локальной сети, отключить вход по паролю. Изнутри SSH-сессии sshd не перезапускается, изменения `sshd_config` применятся после перезагрузки. |
| `wake-fix` | Разрешает пробуждение только по WOL (magic packet) и с приёмника Steam Controller: отключает таймеры пробуждения, пробуждение для обслуживания, Windows Update и задач планировщика, а сетевую карту переводит в режим «только magic packet». |

## Как добавить модуль

- Файл `modules/NN-name.ps1`; модули выполняются по порядку `NN`.
- В шапке строка `# Description: ...` — её показывает меню установщика.
- Только ASCII внутри `.ps1`: Windows PowerShell 5.1 читает файлы без BOM в ANSI, и кириллица ломается.
- Совместимость с PowerShell 5.1 (`else` на одной строке с `}`).
- Поддерживать `param([switch]$Check)`: при `-Check` только диагностика.
- Задавать вопросы только при `$env:OMNIBOX_INTERACTIVE -eq '1'`, иначе брать безопасные значения по умолчанию.
- Повторный запуск должен быть безопасным.
- Никаких личных данных (IP, MAC, пароли, токены): репозиторий публичный.
