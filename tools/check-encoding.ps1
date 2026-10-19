# Checks repository text files against the OMNIBOX encoding rules:
#   - every text file is valid UTF-8;
#   - .ps1/.psm1/.psd1 with non-ASCII characters start with a UTF-8 BOM
#     (Windows PowerShell 5.1 reads BOM-less scripts as ANSI, cp1251 on Russian Windows);
#   - other text files have no BOM;
#   - .ps1/.psm1/.psd1 use CRLF only, other text files use LF only.
# Compatible with Windows PowerShell 5.1. ASCII only.
# Usage: powershell -NoProfile -File tools\check-encoding.ps1

param([string]$Root = (Split-Path -Parent $PSScriptRoot))

$ErrorActionPreference = 'Stop'

$scriptExt = @('.ps1', '.psm1', '.psd1')
$textExt = @('.ps1', '.psm1', '.psd1', '.cs', '.csproj', '.props', '.targets', '.json', '.md',
    '.js', '.css', '.html', '.yml', '.yaml', '.txt', '.sh', '.xml', '.slnx')

function Get-TrackedFiles([string]$Dir) {
    # git output is read as UTF-8 through Process, never through the console code page.
    $si = New-Object System.Diagnostics.ProcessStartInfo
    $si.FileName = 'git'
    $si.Arguments = '-c core.quotepath=off ls-files -z'
    $si.WorkingDirectory = $Dir
    $si.UseShellExecute = $false
    $si.CreateNoWindow = $true
    $si.RedirectStandardOutput = $true
    $si.StandardOutputEncoding = New-Object System.Text.UTF8Encoding($false)
    $proc = [System.Diagnostics.Process]::Start($si)
    try {
        $text = $proc.StandardOutput.ReadToEnd()
        $proc.WaitForExit()
        if ($proc.ExitCode -ne 0) { throw "git ls-files failed with exit code $($proc.ExitCode)" }
    } finally {
        $proc.Dispose()
    }
    return @($text.Split([char]0) | Where-Object { $_.Length -gt 0 })
}

$strict = New-Object System.Text.UTF8Encoding($false, $true)
$problems = New-Object System.Collections.Generic.List[string]
$checked = 0

foreach ($rel in (Get-TrackedFiles $Root)) {
    $ext = [IO.Path]::GetExtension($rel).ToLowerInvariant()
    if ($textExt -notcontains $ext) { continue }
    $full = Join-Path $Root $rel
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { continue }

    $bytes = [IO.File]::ReadAllBytes($full)
    $checked++

    try {
        [void]$strict.GetString($bytes)
    } catch {
        $problems.Add("${rel}: not valid UTF-8")
        continue
    }

    $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    $nonAscii = $false
    $cr = 0
    $lf = 0
    $crlf = 0
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        $b = $bytes[$i]
        if ($b -gt 127) {
            $nonAscii = $true
        } elseif ($b -eq 13) {
            $cr++
            if ($i + 1 -lt $bytes.Length -and $bytes[$i + 1] -eq 10) { $crlf++ }
        } elseif ($b -eq 10) {
            $lf++
        }
    }

    if ($scriptExt -contains $ext) {
        if ($nonAscii -and -not $hasBom) {
            $problems.Add("${rel}: non-ASCII text needs a UTF-8 BOM for Windows PowerShell 5.1")
        }
        if ($cr -ne $crlf -or $lf -ne $crlf) {
            $problems.Add("${rel}: line endings must be CRLF only")
        }
    } else {
        if ($hasBom) {
            $problems.Add("${rel}: UTF-8 BOM is not allowed in this file type")
        }
        if ($cr -gt 0) {
            $problems.Add("${rel}: line endings must be LF only")
        }
    }
}

if ($problems.Count -gt 0) {
    foreach ($p in $problems) { Write-Output "FAIL $p" }
    Write-Output "Encoding check: $($problems.Count) problem(s) in $checked file(s)."
    exit 1
}
Write-Output "Encoding check: $checked file(s) OK."
exit 0
