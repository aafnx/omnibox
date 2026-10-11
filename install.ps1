# OMNIBOX installer
#
# Interactive (asks which modules and mode):
#   irm https://raw.githubusercontent.com/aafnx/omnibox/main/install.ps1 | iex
#
# Non-interactive:
#   & ([scriptblock]::Create((irm https://raw.githubusercontent.com/aafnx/omnibox/main/install.ps1))) -All
#   & ([scriptblock]::Create((irm https://raw.githubusercontent.com/aafnx/omnibox/main/install.ps1))) -Module wake-fix -Check
#
# Modules live in modules\NN-name.ps1, run in NN order, and declare "# Description: ..." in the header.
param(
    [string[]]$Module,
    [switch]$All,
    [switch]$Check,
    [string]$Branch = 'main'
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
[Console]::OutputEncoding = [Text.Encoding]::UTF8

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { Write-Host 'OMNIBOX: run from an elevated PowerShell or SSH as an admin user.' -ForegroundColor Red; return }

$interactive = -not ($Module -or $All -or $Check)

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
Copy-Item (Join-Path $src.FullName '*') $root -Recurse -Force
Remove-Item $zip, $tmp -Recurse -Force -ErrorAction SilentlyContinue

# --- Module list ----------------------------------------------------------
$mods = @(Get-ChildItem (Join-Path $root 'modules') -Filter '*.ps1' | Sort-Object Name | ForEach-Object {
    $desc = ''
    foreach ($l in (Get-Content $_.FullName -TotalCount 15)) {
        if ($l -match '^#\s*Description:\s*(.+)$') { $desc = $Matches[1].Trim(); break }
    }
    [pscustomobject]@{ Name = ($_.BaseName -replace '^\d+-', ''); Desc = $desc; Path = $_.FullName }
})
if ($mods.Count -eq 0) { Write-Host 'OMNIBOX: no modules found.' -ForegroundColor Red; return }

# --- Selection ------------------------------------------------------------
if ($interactive) {
    Write-Host "`n=== OMNIBOX setup ===" -ForegroundColor Cyan
    for ($i = 0; $i -lt $mods.Count; $i++) {
        Write-Host ("  [{0}] {1}" -f ($i + 1), $mods[$i].Name) -ForegroundColor White -NoNewline
        if ($mods[$i].Desc) { Write-Host (" - " + $mods[$i].Desc) -ForegroundColor DarkGray } else { Write-Host '' }
    }
    Write-Host ''

    $sel = $null
    while (-not $sel) {
        $ans = (Read-Host 'Modules to run (e.g. 1,3 | a = all | q = quit)').Trim().ToLower()
        if ($ans -eq 'q') { Write-Host 'Cancelled.'; return }
        if ($ans -eq 'a') {
            $sel = $mods
        } else {
            $idx = @($ans -split '[,\s]+' | Where-Object { $_ -match '^\d+$' } | ForEach-Object { [int]$_ } | Sort-Object -Unique)
            $bad = @($idx | Where-Object { $_ -lt 1 -or $_ -gt $mods.Count })
            if ($idx.Count -gt 0 -and $bad.Count -eq 0) {
                $sel = @($idx | ForEach-Object { $mods[$_ - 1] })
            } else {
                Write-Host 'Invalid input.' -ForegroundColor Yellow
            }
        }
    }

    $mode = ''
    while ($mode -ne 'a' -and $mode -ne 'c') {
        $mode = (Read-Host 'Mode: [a]pply changes or [c]heck only (a/c)').Trim().ToLower()
    }
    $checkMode = ($mode -eq 'c')

    $label = 'APPLY'
    if ($checkMode) { $label = 'CHECK' }
    Write-Host ("`nWill run in {0} mode: {1}" -f $label, (($sel | ForEach-Object { $_.Name }) -join ', ')) -ForegroundColor Cyan
    if ((Read-Host 'Continue? (y/n)').Trim().ToLower() -ne 'y') { Write-Host 'Cancelled.'; return }
} else {
    $sel = $mods
    if ($Module) { $sel = @($mods | Where-Object { $Module -contains $_.Name }) }
    if ($sel.Count -eq 0) { Write-Host "OMNIBOX: no matching modules ($($Module -join ', '))" -ForegroundColor Red; return }
    $checkMode = [bool]$Check
}

# --- Run ------------------------------------------------------------------
foreach ($m in $sel) {
    Write-Host "`n##### $($m.Name) #####" -ForegroundColor Magenta
    $a = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $m.Path)
    if ($checkMode) { $a += '-Check' }
    & powershell.exe @a
}

Write-Host "`nOMNIBOX: done. Local copy: $root" -ForegroundColor Green

if ($interactive -and -not $checkMode) {
    if ((Read-Host 'Reboot now to apply all changes? (y/n)').Trim().ToLower() -eq 'y') {
        Write-Host 'Rebooting in 5 seconds...' -ForegroundColor Yellow
        shutdown.exe /r /t 5
    }
}
