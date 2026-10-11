# OMNIBOX console frontend (SSH or no desktop)
#   no params            interactive menu
#   -All / -Module ids   run without questions, options = defaults from modules.json
#   -Check               diagnostics only
param(
    [Parameter(Mandatory = $true)][string]$Root,
    [string[]]$Module,
    [switch]$All,
    [switch]$Check
)

. (Join-Path $PSScriptRoot 'common.ps1')

$mods = @(Get-OmniManifest $Root)
# -Module arrives as one "a,b" string when passed through powershell.exe -File
$Module = @($Module | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$interactive = -not ($Module -or $All -or $Check)

# "1,3 2" -> sorted unique ints in 1..max, or $null if invalid
function ConvertFrom-NumberList([string]$text, [int]$max) {
    $n = @($text -split '[,\s]+' | Where-Object { $_ -match '^\d+$' } | ForEach-Object { [int]$_ } | Sort-Object -Unique)
    if ($n.Count -eq 0) { return $null }
    if (@($n | Where-Object { $_ -lt 1 -or $_ -gt $max }).Count -gt 0) { return $null }
    return $n
}

function Read-Options($m) {
    $opts = Get-OmniDefaults $m
    foreach ($o in @($m.options)) {
        Write-Host ''
        Write-Host ("  {0}" -f $o.label) -ForegroundColor White
        if ($o.hint) { Write-Host ("  {0}" -f $o.hint) -ForegroundColor DarkGray }
        $ch = @($o.choices)

        if ($o.type -eq 'text') {
            $opts[$o.id] = (Read-Host '  Value (Enter = skip)').Trim()
        } elseif ($o.type -eq 'choice') {
            for ($i = 0; $i -lt $ch.Count; $i++) {
                $mark = ''
                if ($ch[$i].id -eq $o.default) { $mark = '  (default)' }
                Write-Host ("    [{0}] {1}{2}" -f ($i + 1), $ch[$i].label, $mark)
            }
            while ($true) {
                $a = (Read-Host '  Choose (Enter = default)').Trim()
                if ($a -eq '') { break }
                $n = ConvertFrom-NumberList $a $ch.Count
                if ($n -and $n.Count -eq 1) { $opts[$o.id] = $ch[$n[0] - 1].id; break }
                Write-Host '  Invalid input.' -ForegroundColor Yellow
            }
        } elseif ($o.type -eq 'multi') {
            $def = @($o.default)
            for ($i = 0; $i -lt $ch.Count; $i++) {
                $mark = ' '
                if ($def -contains $ch[$i].id) { $mark = 'x' }
                Write-Host ("    [{0}] [{1}] {2}" -f ($i + 1), $mark, $ch[$i].label)
            }
            while ($true) {
                $a = (Read-Host '  Numbers, e.g. 1,3 (Enter = marked [x], n = none)').Trim().ToLower()
                if ($a -eq '') { break }
                if ($a -eq 'n') { $opts[$o.id] = @(); break }
                $n = ConvertFrom-NumberList $a $ch.Count
                if ($n) { $opts[$o.id] = @($n | ForEach-Object { $ch[$_ - 1].id }); break }
                Write-Host '  Invalid input.' -ForegroundColor Yellow
            }
        }
    }
    return $opts
}

# --- Selection ------------------------------------------------------------
$plan = @()   # items: @{ Module; Options }
if ($interactive) {
    Write-Host "`n=== OMNIBOX setup ===" -ForegroundColor Cyan
    for ($i = 0; $i -lt $mods.Count; $i++) {
        Write-Host ("  [{0}] {1}" -f ($i + 1), $mods[$i].title) -ForegroundColor White
        Write-Host ("      {0}" -f $mods[$i].description) -ForegroundColor DarkGray
    }
    Write-Host ''

    $sel = $null
    while (-not $sel) {
        $a = (Read-Host 'Modules to run (e.g. 1,3 | a = all | q = quit)').Trim().ToLower()
        if ($a -eq 'q') { Write-Host 'Cancelled.'; return }
        if ($a -eq 'a') {
            $sel = $mods
        } else {
            $n = ConvertFrom-NumberList $a $mods.Count
            if ($n) { $sel = @($n | ForEach-Object { $mods[$_ - 1] }) } else { Write-Host 'Invalid input.' -ForegroundColor Yellow }
        }
    }

    $mode = ''
    while ($mode -ne 'a' -and $mode -ne 'c') {
        $mode = (Read-Host 'Mode: [a]pply changes or [c]heck only (a/c)').Trim().ToLower()
    }
    $checkMode = ($mode -eq 'c')

    foreach ($m in $sel) {
        $opts = Get-OmniDefaults $m
        if (-not $checkMode -and @($m.options).Count -gt 0) {
            Write-Host "`n--- $($m.title) ---" -ForegroundColor Cyan
            $opts = Read-Options $m
        }
        $plan += @{ Module = $m; Options = $opts }
    }

    $label = 'APPLY'
    if ($checkMode) { $label = 'CHECK' }
    Write-Host ("`nWill run in {0} mode: {1}" -f $label, (($sel | ForEach-Object { $_.title }) -join ', ')) -ForegroundColor Cyan
    if ((Read-Host 'Continue? (y/n)').Trim().ToLower() -ne 'y') { Write-Host 'Cancelled.'; return }
} else {
    $sel = $mods
    if ($Module) { $sel = @($mods | Where-Object { $Module -contains $_.id }) }
    if ($sel.Count -eq 0) { Write-Host "OMNIBOX: no matching modules ($($Module -join ', '))" -ForegroundColor Red; return }
    $checkMode = [bool]$Check
    foreach ($m in $sel) { $plan += @{ Module = $m; Options = (Get-OmniDefaults $m) } }
}

# --- Run ------------------------------------------------------------------
$needReboot = $false
foreach ($item in $plan) {
    Write-Host "`n##### $($item.Module.title) #####" -ForegroundColor Magenta
    $a = Get-OmniRunnerArgs $Root $item.Module $item.Options $checkMode $false
    & $script:OmniPowerShell @a
    if ($item.Module.reboot) { $needReboot = $true }
}

Write-Host "`nOMNIBOX: done. Local copy: $Root" -ForegroundColor Green

if ($interactive -and -not $checkMode -and $needReboot) {
    if ((Read-Host 'Reboot now to apply all changes? (y/n)').Trim().ToLower() -eq 'y') {
        Write-Host 'Rebooting in 5 seconds...' -ForegroundColor Yellow
        shutdown.exe /r /t 5
    }
}
