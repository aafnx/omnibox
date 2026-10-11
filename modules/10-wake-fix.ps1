# OMNIBOX module: wake-fix
# Description: Wake only via WOL magic packet + Steam Controller (kills wake timers, task wakes, NIC pattern wake)
#   -Check   diagnostics only, no changes
param([switch]$Check)

[Console]::OutputEncoding = [Text.Encoding]::UTF8
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { Write-Host "Run as administrator" -ForegroundColor Red; return }

Write-Host "`n=== DIAGNOSTICS ===" -ForegroundColor Cyan
Write-Host "-- Sleep states:"; powercfg /a
Write-Host "-- Last wake:"; powercfg /lastwake
Write-Host "-- Active wake timers:"; powercfg /waketimers
Write-Host "-- Wake-armed devices:"; powercfg /devicequery wake_armed
Write-Host "-- Last 5 wake events:"
Get-WinEvent -FilterHashtable @{LogName='System'; ProviderName='Microsoft-Windows-Power-Troubleshooter'; Id=1} -MaxEvents 5 -ErrorAction SilentlyContinue |
    ForEach-Object { Write-Host ("[{0}]" -f $_.TimeCreated) -ForegroundColor Yellow; Write-Host $_.Message.Trim() }
Write-Host "-- Tasks allowed to wake the PC:"
Get-ScheduledTask | Where-Object { $_.Settings.WakeToRun } | ForEach-Object { Write-Host "  $($_.TaskPath)$($_.TaskName)" }
Write-Host "-- NIC wake settings:"
Get-NetAdapter -Physical | Where-Object { $_.MediaType -eq '802.3' } | ForEach-Object {
    Get-NetAdapterPowerManagement -Name $_.Name | Format-List Name, WakeOnMagicPacket, WakeOnPattern, ArpOffload, NSOffload
}

if ($Check) { Write-Host "`nCheck mode: nothing changed." -ForegroundColor Green; return }

Write-Host "`n=== FIXES ===" -ForegroundColor Cyan

# 1. Wake timers off (AC + DC) in the active plan
powercfg /setacvalueindex SCHEME_CURRENT SUB_SLEEP RTCWAKE 0
powercfg /setdcvalueindex SCHEME_CURRENT SUB_SLEEP RTCWAKE 0
powercfg /setactive SCHEME_CURRENT
Write-Host "[ok] Wake timers disabled"

# 2. Automatic Maintenance must not wake the PC
$m = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\Maintenance'
if (-not (Test-Path $m)) { New-Item $m -Force | Out-Null }
Set-ItemProperty $m -Name WakeUp -Value 0 -Type DWord
Write-Host "[ok] Maintenance wake disabled"

# 3. Windows Update must not wake the PC (policy)
$wu = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate'
if (-not (Test-Path $wu)) { New-Item $wu -Force | Out-Null }
Set-ItemProperty $wu -Name AUPowerManagement -Value 0 -Type DWord
Write-Host "[ok] Windows Update wake disabled"

# 4. Scheduled tasks with 'Wake the computer to run this task'
Get-ScheduledTask | Where-Object { $_.Settings.WakeToRun } | ForEach-Object {
    $t = $_
    $name = "$($t.TaskPath)$($t.TaskName)"
    try {
        $t.Settings.WakeToRun = $false
        $t | Set-ScheduledTask -ErrorAction Stop | Out-Null
        Write-Host "[ok] WakeToRun off: $name"
    } catch {
        Write-Host "[skip] protected (blocked by step 1 anyway): $name" -ForegroundColor DarkGray
    }
}

# 5. NIC: magic packet only, no pattern/link wake, ARP/NS offload on
Get-NetAdapter -Physical | Where-Object { $_.MediaType -eq '802.3' } | ForEach-Object {
    $n = $_.Name
    # -NoRestart: restarting the NIC would drop an SSH session; changes apply on reboot
    Set-NetAdapterPowerManagement -Name $n -WakeOnMagicPacket Enabled -WakeOnPattern Disabled -ArpOffload Enabled -NSOffload Enabled -NoRestart -ErrorAction SilentlyContinue
    Get-NetAdapterAdvancedProperty -Name $n -ErrorAction SilentlyContinue | ForEach-Object {
        $kw = $_.RegistryKeyword
        if ($kw -match '^\*?WakeOnPattern$' -or $kw -match '^\*?WakeOnLink$') {
            Set-NetAdapterAdvancedProperty -Name $n -RegistryKeyword $kw -RegistryValue 0 -NoRestart -ErrorAction SilentlyContinue
        } elseif ($kw -match '^\*?WakeOnMagicPacket$') {
            Set-NetAdapterAdvancedProperty -Name $n -RegistryKeyword $kw -RegistryValue 1 -NoRestart -ErrorAction SilentlyContinue
        }
    }
    Write-Host "[ok] NIC '$n': magic packet only (takes effect after reboot)"
    Get-NetAdapterPowerManagement -Name $n | Format-List WakeOnMagicPacket, WakeOnPattern, ArpOffload, NSOffload
}

Write-Host "`n-- Still wake-armed (should be only NIC + Steam Controller receiver):" -ForegroundColor Cyan
powercfg /devicequery wake_armed
Write-Host "`nDisarm extra:  powercfg /devicedisablewake `"<name>`""
Write-Host "Arm receiver:  powercfg /deviceenablewake `"<name>`"  (see: powercfg /devicequery wake_programmable)"
Write-Host "`nReboot, then test sleep. If it wakes again: powercfg /lastwake" -ForegroundColor Green
