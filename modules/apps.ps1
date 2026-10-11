# OMNIBOX module: apps
# Install or update apps to the latest version with winget (source: winget only, no Microsoft Store).
#   -Check            show what is installed, no changes
#   -Apps id1,id2     winget package ids, e.g. Brave.Brave,Valve.Steam
param(
    [switch]$Check,
    [string[]]$Apps = @()
)

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { Write-Host "Run as administrator" -ForegroundColor Red; return }

# A single comma-separated string also works
$Apps = @($Apps | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if ($Apps.Count -eq 0) { Write-Host "No apps selected." -ForegroundColor Yellow; return }

$NO_UPDATE         = -1978335189   # 0x8A15002B: no applicable upgrade
$ALREADY_INSTALLED = -1978335135   # 0x8A150061

function Find-Winget {
    $c = Get-Command winget.exe -ErrorAction SilentlyContinue
    if ($c) { return $c.Source }
    $p = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\winget.exe'
    if (Test-Path $p) { return $p }
    # Elevated / SSH sessions often lack the per-user alias: use the package folder directly
    $r = @(Resolve-Path (Join-Path $env:ProgramFiles 'WindowsApps\Microsoft.DesktopAppInstaller_*_x64__8wekyb3d8bbwe\winget.exe') -ErrorAction SilentlyContinue)
    if ($r.Count -gt 0) { return ($r | Sort-Object Path | Select-Object -Last 1).Path }
    return $null
}

$wg = Find-Winget
if (-not $wg -and -not $Check) {
    Write-Host "winget not found, registering App Installer..."
    try { Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe -ErrorAction Stop } catch { }
    $wg = Find-Winget
}
if (-not $wg) {
    Write-Host "[fail] winget not found. Install 'App Installer' from Microsoft Store or github.com/microsoft/winget-cli" -ForegroundColor Red
    return
}

$common = @('--exact', '--source', 'winget', '--accept-source-agreements', '--disable-interactivity')

function Test-Installed([string]$id) {
    & $wg list --id $id @common | Out-Null
    return ($LASTEXITCODE -eq 0)
}

Write-Host "`n=== APPS ===" -ForegroundColor Cyan
Write-Host "winget: $wg"

foreach ($id in $Apps) {
    $installed = Test-Installed $id
    if ($Check) {
        $state = 'not installed'
        if ($installed) { $state = 'installed' }
        Write-Host ("{0,-30} : {1}" -f $id, $state)
        continue
    }

    if ($installed) {
        Write-Host "`n-- $id : checking for update" -ForegroundColor Cyan
        & $wg upgrade --id $id @common --silent --accept-package-agreements
        $rc = $LASTEXITCODE
        if ($rc -eq 0) {
            Write-Host "[ok] $id updated"
        } elseif ($rc -eq $NO_UPDATE) {
            Write-Host "[ok] $id is up to date"
        } else {
            Write-Host ("[fail] {0}: winget exit code 0x{1:X8}" -f $id, $rc) -ForegroundColor Red
        }
    } else {
        Write-Host "`n-- $id : installing" -ForegroundColor Cyan
        & $wg install --id $id @common --silent --accept-package-agreements
        $rc = $LASTEXITCODE
        if ($rc -eq 0 -or $rc -eq $ALREADY_INSTALLED) {
            Write-Host "[ok] $id installed"
        } else {
            Write-Host ("[fail] {0}: winget exit code 0x{1:X8}" -f $id, $rc) -ForegroundColor Red
        }
    }
}

if ($Check) { Write-Host "`nCheck mode: nothing changed." -ForegroundColor Green }
