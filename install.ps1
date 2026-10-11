# OMNIBOX installer
#
#   irm https://raw.githubusercontent.com/aafnx/omnibox/main/install.ps1 | iex
#
# Picks the frontend automatically: a window on the local desktop, a console menu over SSH.
# With parameters:
#   & ([scriptblock]::Create((irm https://raw.githubusercontent.com/aafnx/omnibox/main/install.ps1))) -Cli
#   & ([scriptblock]::Create((irm https://raw.githubusercontent.com/aafnx/omnibox/main/install.ps1))) -All
#   & ([scriptblock]::Create((irm https://raw.githubusercontent.com/aafnx/omnibox/main/install.ps1))) -Module wake-fix -Check
param(
    [switch]$Gui,
    [switch]$Cli,
    [string[]]$Module,
    [switch]$All,
    [switch]$Check,
    [string]$Branch = 'main'
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { Write-Host 'OMNIBOX: run from an elevated PowerShell or SSH as an admin user.' -ForegroundColor Red; return }

# --- Download -------------------------------------------------------------
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
# Replace code folders entirely so renamed or removed files do not linger
foreach ($d in 'lib', 'modules') { Remove-Item (Join-Path $root $d) -Recurse -Force -ErrorAction SilentlyContinue }
Copy-Item (Join-Path $src.FullName '*') $root -Recurse -Force
Remove-Item $zip, $tmp -Recurse -Force -ErrorAction SilentlyContinue

# --- Frontend -------------------------------------------------------------
$ps = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$inSsh = [bool]($env:SSH_CONNECTION -or $env:SSH_CLIENT)
$noDesktop = $inSsh -or ((Get-Process -Id $PID).SessionId -eq 0)
$nonInteractive = [bool]($Module -or $All -or $Check)

$useGui = -not $nonInteractive -and -not $Cli -and -not $noDesktop
if ($Gui -and $noDesktop) { Write-Host 'OMNIBOX: no desktop in this session (SSH), using the console menu.' -ForegroundColor Yellow }

if ($useGui) {
    $gui = Join-Path $root 'lib\gui.ps1'
    Start-Process -FilePath $ps -ArgumentList "-NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$gui`" -Root `"$root`""
    Write-Host 'OMNIBOX: setup window opened.' -ForegroundColor Green
    return
}

$cliArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $root 'lib\cli.ps1'), '-Root', $root)
if ($Module) { $cliArgs += '-Module'; $cliArgs += ($Module -join ',') }
if ($All)    { $cliArgs += '-All' }
if ($Check)  { $cliArgs += '-Check' }
& $ps @cliArgs
