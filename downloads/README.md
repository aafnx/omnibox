# Готовые сборки

Временное место для файлов, пока нет релизов на GitHub.

| Файл | Что это | SHA-256 |
|---|---|---|
| `com.omnibox.launcher_1.1.2_all.ipk` | OmniBox 1.1.2 для LG webOS ([`tv/webos`](../tv/webos)) | `5e6c9f797400a7d0cb9310cefd1c15a34c198205062b780616d0f056933f9b66` |
| `win-x64.exe` | WinCLI 1.4.0, Windows x64 | `61f16c86ae78cec405b4f7748ecc94cfec11b6ebe80987b0436fab69e616cdbe` |
| `win-arm64.exe` | WinCLI 1.4.0, Windows ARM64 | `469b4bb1bfff94e3e7de5e0181498655bac87c160e7a33930d6409bfa1032ed5` |

## WinCLI на OMNIBOX (по SSH)

```powershell
irm https://raw.githubusercontent.com/aafnx/omnibox/main/downloads/win-x64.exe -OutFile "$env:TEMP\win-x64.exe"
& "$env:TEMP\win-x64.exe" --install
& "$env:LOCALAPPDATA\Programs\win-cli\win.exe" --agent
```

`--agent` выведет код подключения для телевизора.

## OmniBox на телевизор (с любого компьютера с webOS CLI)

```powershell
irm https://raw.githubusercontent.com/aafnx/omnibox/main/downloads/com.omnibox.launcher_1.1.2_all.ipk -OutFile omnibox.ipk
ares-install --device tv omnibox.ipk
```

Проверить файл: `Get-FileHash <файл>` и сравнить с таблицей.
