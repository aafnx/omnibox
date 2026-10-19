# Calls omnihub.exe from Windows PowerShell 5.1 and PowerShell 7.
#
# Output is read as UTF-8 through System.Diagnostics.Process, so the console
# code page (866 or 1251 on Russian Windows) never touches the text, and the
# caller's console is left as it was. Machine output (--json) is ASCII only:
# Russian text arrives as \u escapes and ConvertFrom-Json restores it.
#
# Compatible with Windows PowerShell 5.1. ASCII only.

function ConvertTo-OmniHubArgument {
    # Quotes one argument by the CommandLineToArgvW rules used by .NET.
    param([string]$Value)
    if ($Value.Length -gt 0 -and $Value -notmatch '[\s"]') { return $Value }
    $backslash = [char]92
    $quote = [char]34
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append($quote)
    $slashes = 0
    foreach ($ch in $Value.ToCharArray()) {
        if ($ch -eq $backslash) {
            $slashes++
            continue
        }
        if ($ch -eq $quote) {
            [void]$sb.Append($backslash, 2 * $slashes + 1)
            [void]$sb.Append($quote)
            $slashes = 0
            continue
        }
        if ($slashes -gt 0) {
            [void]$sb.Append($backslash, $slashes)
            $slashes = 0
        }
        [void]$sb.Append($ch)
    }
    if ($slashes -gt 0) { [void]$sb.Append($backslash, 2 * $slashes) }
    [void]$sb.Append($quote)
    return $sb.ToString()
}

function Invoke-OmniHub {
    # Runs omnihub.exe and returns ExitCode, Output, ErrorText and, with -Json, Data.
    param(
        [Parameter(Mandatory = $true)][string]$Exe,
        [string[]]$Arguments = @(),
        [switch]$Json,
        [int]$TimeoutSeconds = 120
    )
    $list = @($Arguments)
    if ($Json) { $list += '--json' }
    $quoted = @($list | ForEach-Object { ConvertTo-OmniHubArgument $_ })

    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $si = New-Object System.Diagnostics.ProcessStartInfo
    $si.FileName = $Exe
    $si.Arguments = ($quoted -join ' ')
    $si.UseShellExecute = $false
    $si.CreateNoWindow = $true
    $si.RedirectStandardOutput = $true
    $si.RedirectStandardError = $true
    $si.StandardOutputEncoding = $utf8
    $si.StandardErrorEncoding = $utf8

    $proc = [System.Diagnostics.Process]::Start($si)
    try {
        $outTask = $proc.StandardOutput.ReadToEndAsync()
        $errTask = $proc.StandardError.ReadToEndAsync()
        if (-not $proc.WaitForExit($TimeoutSeconds * 1000)) {
            try { $proc.Kill() } catch { }
            throw "omnihub: no answer in $TimeoutSeconds s"
        }
        $proc.WaitForExit()
        $result = [pscustomobject]@{
            ExitCode  = $proc.ExitCode
            Output    = $outTask.Result
            ErrorText = $errTask.Result
            Data      = $null
        }
    } finally {
        $proc.Dispose()
    }

    if ($Json -and $result.Output.Trim().Length -gt 0) {
        $result.Data = $result.Output | ConvertFrom-Json
    }
    return $result
}
