# Готовые сборки

Временное место для файлов, пока нет релизов на GitHub.

| Файл | Что это | SHA-256 |
|---|---|---|
| `com.omnibox.launcher_1.1.4_all.ipk` | OmniBox 1.1.4 для LG webOS ([`tv/webos`](../tv/webos)) | `14f79890021917fe4e69745017b69025941a220218e4039ae37556bd378ec879` |
| `win-x64.exe` | WinCLI 1.4.1, Windows x64 | `e049e389c127362b90e83065220de5843734dccec0a5db489e41af95c7caa877` |
| `win-arm64.exe` | WinCLI 1.4.1, Windows ARM64 | `04331fdeb340e63557cc2d64e3598e31e2f7d89c056f2fe5a87d521e7e241542` |

## WinCLI на OMNIBOX (по SSH)

```powershell
irm https://raw.githubusercontent.com/aafnx/omnibox/main/downloads/win-x64.exe -OutFile "$env:TEMP\win-x64.exe"
& "$env:TEMP\win-x64.exe" --install
& "$env:LOCALAPPDATA\Programs\win-cli\win.exe" --agent
```

`--agent` выведет код подключения для телевизора.

## OmniBox на телевизор (с любого компьютера с webOS CLI)

```powershell
irm https://raw.githubusercontent.com/aafnx/omnibox/main/downloads/com.omnibox.launcher_1.1.4_all.ipk -OutFile omnibox.ipk
ares-install --device tv omnibox.ipk
```

Проверить файл: `Get-FileHash <файл>` и сравнить с таблицей.
