# OMNIBOX installer
#
# All modules:
#   irm https://raw.githubusercontent.com/aafnx/omnibox/main/install.ps1 | iex
# Selected modules / check only:
#   & ([scriptblock]::Create((irm https://raw.githubusercontent.com/aafnx/omnibox/main/install.ps1))) -Module wake-fix -Check
#
# Modules live in modules\NN-name.ps1 and run in NN order. Name is matched without the NN- prefix.
param(
    [string[]]$Module,
    [switch]$Check,
    [string]$Branch = 'main'
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
[Console]::OutputEncoding = [Text.Encoding]::UTF8

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { Write-Host 'OMNIBOX: run from an elevated PowerShell or SSH as an admin user.' -ForegroundColor Red; return }

$repo = 'aafnx/omnibox'
$root = Join-Path $env:ProgramData 'OMNIBOX'
$zip  = Join-Path $env:TEMP 'omnibox.zip'
$tmp  = Join-Path $env:TEMP 'omnibox-src'

Write-Host "OMNIBOX: downloading $repo@$Branch" -ForegroundColor Cyan
Invoke-WebRequest -UseBasicParsing -Uri "https://github.com/$repo/archive/refs/heads/$Branch.zip" -OutFile $zip
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
Expand-Archive -Path $zip -DestinationPath $tmp -Force
$src = Get-ChildItem $tmp -Directory | Select-Object -First 1
if (-not (Test-Path $root)) { New-Item $root -ItemType Directory | Out-Null }
Copy-Item (Join-Path $src.FullName '*') $root -Recurse -Force
Remove-Item $zip, $tmp -Recurse -Force -ErrorAction SilentlyContinue

$mods = @(Get-ChildItem (Join-Path $root 'modules') -Filter '*.ps1' | Sort-Object Name)
if ($Module) {
    $mods = @($mods | Where-Object { $Module -contains ($_.BaseName -replace '^\d+-', '') })
}
if ($mods.Count -eq 0) { Write-Host "OMNIBOX: no matching modules ($($Module -join ', '))" -ForegroundColor Red; return }

foreach ($m in $mods) {
    Write-Host "`n##### $($m.BaseName -replace '^\d+-', '') #####" -ForegroundColor Magenta
    $a = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $m.FullName)
    if ($Check) { $a += '-Check' }
    & powershell.exe @a
}

Write-Host "`nOMNIBOX: done. Local copy: $root" -ForegroundColor Green
