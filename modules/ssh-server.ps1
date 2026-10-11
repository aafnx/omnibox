# OMNIBOX module: ssh-server
# OpenSSH Server: install, autostart, firewall, PowerShell as default shell, key login.
#   -Check                      diagnostics only, no changes
#   -PublicKey "ssh-ed25519 ..."  add a key for admin accounts (stored on this PC only)
#   -Scope keep|lan|any         who may connect (firewall remote address)
#   -PasswordLogin keep|disable|enable
param(
    [switch]$Check,
    [string]$PublicKey = '',
    [ValidateSet('keep', 'lan', 'any')][string]$Scope = 'keep',
    [ValidateSet('keep', 'disable', 'enable')][string]$PasswordLogin = 'keep'
)

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { Write-Host "Run as administrator" -ForegroundColor Red; return }

$inSsh    = [bool]($env:SSH_CONNECTION -or $env:SSH_CLIENT)
$sshDir   = Join-Path $env:ProgramData 'ssh'
$cfg      = Join-Path $sshDir 'sshd_config'
$keys     = Join-Path $sshDir 'administrators_authorized_keys'
$psExe    = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$regKey   = 'HKLM:\SOFTWARE\OpenSSH'
$ruleName = 'OpenSSH-Server-In-TCP'
$keyRegex = '^(ssh-(ed25519|rsa|dss)|ecdsa-sha2-\S+|sk-\S+)\s+\S+'

function Get-KeyCount {
    if (-not (Test-Path $keys)) { return 0 }
    return @(Get-Content $keys | Where-Object { $_ -match $keyRegex }).Count
}

# Value of a global option (before the first Match block), $null if not set
function Get-SshdOption([string]$key) {
    if (-not (Test-Path $cfg)) { return $null }
    foreach ($l in (Get-Content $cfg)) {
        if ($l -match '^\s*Match\s') { break }
        if ($l -match "^\s*$key\s+(\S+)") { return $Matches[1] }
    }
    return $null
}

# Set a global option: replace the first (possibly commented) occurrence, comment out duplicates
function Set-SshdOption([string]$key, [string]$value) {
    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($l in (Get-Content $cfg)) { $lines.Add($l) }
    $matchAt = -1
    $done = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*Match\s') { $matchAt = $i; break }
        if ($lines[$i] -match "^\s*#?\s*$key\s") {
            if (-not $done) {
                $lines[$i] = "$key $value"
                $done = $true
            } elseif ($lines[$i] -notmatch '^\s*#') {
                $lines[$i] = '#' + $lines[$i]
            }
        }
    }
    if (-not $done) {
        if ($matchAt -ge 0) { $lines.Insert($matchAt, "$key $value") } else { $lines.Add("$key $value") }
    }
    Set-Content -Path $cfg -Value $lines -Encoding ascii
}

# --- Diagnostics ----------------------------------------------------------
Write-Host "`n=== DIAGNOSTICS ===" -ForegroundColor Cyan
$svc = Get-Service sshd -ErrorAction SilentlyContinue
if ($svc) {
    Write-Host ("sshd service    : {0}, {1}" -f $svc.Status, $svc.StartType)
} else {
    Write-Host "sshd service    : not installed" -ForegroundColor Yellow
}
$listen = Get-NetTCPConnection -State Listen -LocalPort 22 -ErrorAction SilentlyContinue
Write-Host ("Listening on 22 : {0}" -f [bool]$listen)
$rule = Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue
if ($rule) {
    $addr = (($rule | Get-NetFirewallAddressFilter).RemoteAddress) -join ','
    Write-Host ("Firewall rule   : enabled={0}, profile={1}, remote={2}" -f $rule.Enabled, $rule.Profile, $addr)
} else {
    Write-Host "Firewall rule   : missing" -ForegroundColor Yellow
}
$shell = (Get-ItemProperty $regKey -Name DefaultShell -ErrorAction SilentlyContinue).DefaultShell
if (-not $shell) { $shell = 'cmd.exe (default)' }
Write-Host "Default shell   : $shell"
Write-Host ("Admin keys      : {0}" -f (Get-KeyCount))
$pw = Get-SshdOption 'PasswordAuthentication'
if (-not $pw) { $pw = 'yes (default)' }
Write-Host "Password login  : $pw"
Get-NetConnectionProfile -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host ("Network profile : {0} = {1}" -f $_.InterfaceAlias, $_.NetworkCategory)
}
if ($inSsh) { Write-Host "Inside an SSH session: sshd will not be restarted." -ForegroundColor DarkGray }

if ($Check) { Write-Host "`nCheck mode: nothing changed." -ForegroundColor Green; return }

Write-Host "`n=== FIXES ===" -ForegroundColor Cyan

# 1. Install
if (-not (Get-Service sshd -ErrorAction SilentlyContinue)) {
    Write-Host "Installing OpenSSH Server (may take a few minutes)..."
    try {
        Add-WindowsCapability -Online -Name 'OpenSSH.Server~~~~0.0.1.0' -ErrorAction Stop | Out-Null
        Write-Host "[ok] OpenSSH Server installed"
    } catch {
        Write-Host "[fail] Install failed: $($_.Exception.Message)" -ForegroundColor Red
        return
    }
}

# 2. Service: autostart and running (first start also creates sshd_config and host keys)
Set-Service sshd -StartupType Automatic
if ((Get-Service sshd).Status -ne 'Running') { Start-Service sshd }
Write-Host "[ok] sshd running, starts automatically"

# 3. Firewall
if (Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue) {
    Enable-NetFirewallRule -Name $ruleName
} else {
    New-NetFirewallRule -Name $ruleName -DisplayName 'OpenSSH Server (sshd)' -Enabled True -Direction Inbound -Protocol TCP -Action Allow -LocalPort 22 | Out-Null
}
Write-Host "[ok] Firewall rule enabled (TCP 22)"
if ($Scope -eq 'lan') {
    Get-NetFirewallRule -Name $ruleName | Set-NetFirewallAddressFilter -RemoteAddress LocalSubnet
    Write-Host "[ok] SSH limited to local subnet"
} elseif ($Scope -eq 'any') {
    Get-NetFirewallRule -Name $ruleName | Set-NetFirewallAddressFilter -RemoteAddress Any
    Write-Host "[ok] SSH allowed from any address"
}

# 4. PowerShell as default shell (applies to new connections, no restart needed)
if (-not (Test-Path $regKey)) { New-Item $regKey | Out-Null }
Set-ItemProperty $regKey -Name DefaultShell -Value $psExe -Type String
Write-Host "[ok] Default shell: Windows PowerShell"

# 5. Public key for admin accounts (stored on this PC only, never in the repo)
$k = $PublicKey.Trim()
if ($k) {
    if ($k -notmatch $keyRegex) {
        Write-Host "[skip] Does not look like a public key" -ForegroundColor Yellow
    } else {
        $existing = @()
        if (Test-Path $keys) { $existing = @(Get-Content $keys) }
        if ($existing -contains $k) {
            Write-Host "[ok] Key already present"
        } else {
            Add-Content -Path $keys -Value $k -Encoding ascii
            Write-Host "[ok] Key added"
        }
    }
}
if (Test-Path $keys) {
    # sshd ignores this file unless only Administrators and SYSTEM have access (SIDs work on any UI language)
    icacls $keys /inheritance:r /grant:r '*S-1-5-32-544:F' '*S-1-5-18:F' | Out-Null
    Write-Host "[ok] Key file permissions: Administrators + SYSTEM only"
}

# 6. Password login
$cfgChanged = $false
if ($PasswordLogin -ne 'keep' -and -not (Test-Path $cfg)) {
    Write-Host "[skip] sshd_config not found, password setting unchanged" -ForegroundColor Yellow
} elseif ($PasswordLogin -eq 'disable') {
    if ((Get-KeyCount) -eq 0) {
        Write-Host "[skip] No admin keys: password login stays on, otherwise SSH would be locked" -ForegroundColor Yellow
    } elseif ((Get-SshdOption 'PasswordAuthentication') -ne 'no') {
        Set-SshdOption 'PasswordAuthentication' 'no'
        $cfgChanged = $true
        Write-Host "[ok] Password login disabled"
    } else {
        Write-Host "[ok] Password login already disabled"
    }
} elseif ($PasswordLogin -eq 'enable') {
    if ((Get-SshdOption 'PasswordAuthentication') -eq 'no') {
        Set-SshdOption 'PasswordAuthentication' 'yes'
        $cfgChanged = $true
        Write-Host "[ok] Password login enabled"
    } else {
        Write-Host "[ok] Password login already enabled"
    }
}
if ($cfgChanged) {
    if ($inSsh) {
        Write-Host "[note] sshd_config changed: applies after reboot (sshd is not restarted inside SSH)" -ForegroundColor Yellow
    } else {
        Restart-Service sshd
        Write-Host "[ok] sshd restarted"
    }
}

# --- Summary --------------------------------------------------------------
$ips = @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } |
    ForEach-Object { $_.IPAddress })
Write-Host "`nConnect with:" -ForegroundColor Green
foreach ($ip in $ips) { Write-Host "  ssh $($env:USERNAME)@$ip" }
