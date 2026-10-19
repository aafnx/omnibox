# End-to-end encoding test for omnihub.exe: Russian text must survive every path
# from omnihub.exe to Windows PowerShell 5.1 and PowerShell 7.
#   1. a plain call with the default console encoding (JSON is ASCII, so it must parse);
#   2. Invoke-OmniHub with --json and a Russian --echo argument;
#   3. Invoke-OmniHub with human-readable output;
#   4. omnihub.exe started from a folder with a Russian name;
#   5. the caller's console code page is unchanged afterwards.
# Expected strings come from hub\src\OmniHub.Core\Text\encoding-samples.json,
# the same file omnihub.exe embeds. Compatible with Windows PowerShell 5.1. ASCII only.
# Usage: powershell -NoProfile -File tools\test-encoding.ps1 -Exe hub\out\omnihub.exe

param([Parameter(Mandatory = $true)][string]$Exe)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'lib\omnihub.ps1')

$Exe = (Resolve-Path -LiteralPath $Exe).Path
$samplesFile = Join-Path $repo 'hub\src\OmniHub.Core\Text\encoding-samples.json'
$expected = [IO.File]::ReadAllText($samplesFile, (New-Object System.Text.UTF8Encoding($false))) | ConvertFrom-Json

$script:failures = 0

function Test-Case([string]$Title, [bool]$Ok, [string]$Detail = '') {
    if ($Ok) {
        Write-Output "  ok   $Title"
    } else {
        $script:failures++
        Write-Output "  FAIL $Title $Detail"
        if ($env:GITHUB_ACTIONS) { Write-Output "::error title=test-encoding (PS $($PSVersionTable.PSVersion.Major))::$Title $Detail" }
    }
}

function Test-Samples([string]$Title, $Data) {
    Test-Case "$Title / ok" ($Data.ok -eq $true)
    Test-Case "$Title / sample" ($Data.samples.sample -ceq $expected.sample)
    Test-Case "$Title / path" ($Data.samples.path -ceq $expected.path)
    Test-Case "$Title / fileName" ($Data.samples.fileName -ceq $expected.fileName)
    Test-Case "$Title / english" ($Data.samples.english -ceq $expected.english)
    $same = ($Data.samples.devices.Count -eq $expected.devices.Count)
    if ($same) {
        for ($i = 0; $i -lt $expected.devices.Count; $i++) {
            if ($Data.samples.devices[$i] -cne $expected.devices[$i]) { $same = $false }
        }
    }
    Test-Case "$Title / devices" $same
}

$codePageBefore = $null
try { $codePageBefore = [Console]::OutputEncoding.CodePage } catch { }
Write-Output "PowerShell $($PSVersionTable.PSVersion), console code page: $codePageBefore"
if ($env:GITHUB_ACTIONS) { Write-Output "::notice title=test-encoding::PowerShell $($PSVersionTable.PSVersion), console code page: $codePageBefore" }

Write-Output '1. Plain call under the console code pages of Russian Windows'
foreach ($cp in @(866, 1251, 65001)) {
    $saved = $null
    try {
        $saved = [Console]::OutputEncoding
        [Console]::OutputEncoding = [Text.Encoding]::GetEncoding($cp)
    } catch {
        Write-Output "  skip code page $cp (no console or no such encoding)"
        continue
    }
    try {
        $raw = (& $Exe selftest --json) -join "`n"
        Test-Case "cp $cp / exit code 0" ($LASTEXITCODE -eq 0) "got $LASTEXITCODE"
        $asciiOnly = $true
        foreach ($ch in $raw.ToCharArray()) { if ([int]$ch -gt 127) { $asciiOnly = $false; break } }
        Test-Case "cp $cp / JSON is ASCII only" $asciiOnly
        Test-Samples "cp $cp" ($raw | ConvertFrom-Json)
        $r = Invoke-OmniHub -Exe $Exe -Arguments @('selftest')
        Test-Case "cp $cp / Invoke-OmniHub text" ($r.Output.Contains($expected.sample))
        $now = [Console]::OutputEncoding.CodePage
        Test-Case "cp $cp / console code page kept" ($now -eq $cp) "got $now"
    } finally {
        [Console]::OutputEncoding = $saved
    }
}

Write-Output '2. Invoke-OmniHub -Json with a Russian argument'
$r = Invoke-OmniHub -Exe $Exe -Arguments @('selftest', '--echo', $expected.sample) -Json
Test-Case 'exit code 0' ($r.ExitCode -eq 0) "got $($r.ExitCode): $($r.ErrorText)"
Test-Samples 'json' $r.Data
Test-Case 'echo argument' ($r.Data.echo -ceq $expected.sample)
$r = Invoke-OmniHub -Exe $Exe -Arguments @('selftest', '--echo', $expected.path) -Json
Test-Case 'echo path with spaces and trailing backslash' ($r.Data.echo -ceq $expected.path)
$quoted = 'say "' + $expected.fileName + '"'
$r = Invoke-OmniHub -Exe $Exe -Arguments @('selftest', '--echo', $quoted) -Json
Test-Case 'echo with quotes' ($r.Data.echo -ceq $quoted)

Write-Output '3. Invoke-OmniHub, human-readable output'
$r = Invoke-OmniHub -Exe $Exe -Arguments @('selftest')
Test-Case 'exit code 0' ($r.ExitCode -eq 0) "got $($r.ExitCode): $($r.ErrorText)"
Test-Case 'text contains sample' ($r.Output.Contains($expected.sample))
Test-Case 'text contains path' ($r.Output.Contains($expected.path))
Test-Case 'text contains devices' ($r.Output.Contains($expected.devices[0]))
$si = New-Object System.Diagnostics.ProcessStartInfo
$si.FileName = $Exe
$si.Arguments = 'selftest'
$si.UseShellExecute = $false
$si.CreateNoWindow = $true
$si.RedirectStandardOutput = $true
$proc = [System.Diagnostics.Process]::Start($si)
$ms = New-Object System.IO.MemoryStream
try {
    $proc.StandardOutput.BaseStream.CopyTo($ms)
    $proc.WaitForExit()
} finally {
    $proc.Dispose()
}
$bytes = $ms.ToArray()
$bom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
Test-Case 'stdout bytes: no UTF-8 BOM' (-not $bom)
$decoded = $null
try { $decoded = (New-Object System.Text.UTF8Encoding($false, $true)).GetString($bytes) } catch { }
Test-Case 'stdout bytes: valid UTF-8 with the sample' ($null -ne $decoded -and $decoded.Contains($expected.sample))
$r = Invoke-OmniHub -Exe $Exe -Arguments @('no-such-command')
Test-Case 'unknown command: exit code 2' ($r.ExitCode -eq 2) "got $($r.ExitCode)"
Test-Case 'unknown command: Russian error text' ($r.ErrorText.Length -gt 0 -and $r.ErrorText -match '[\u0400-\u04FF]')

Write-Output '4. omnihub.exe in a folder with a Russian name'
$segments = $expected.path.Split([char]92) | Where-Object { $_.Length -gt 0 }
$dir = Join-Path ([IO.Path]::GetTempPath()) ('omnihub-test-' + [guid]::NewGuid().ToString('N'))
foreach ($s in ($segments | Select-Object -Last 2)) { $dir = Join-Path $dir $s }
[void][IO.Directory]::CreateDirectory($dir)
try {
    $copy = Join-Path $dir 'omnihub.exe'
    [IO.File]::Copy($Exe, $copy)
    $r = Invoke-OmniHub -Exe $copy -Arguments @('version') -Json
    Test-Case 'exit code 0' ($r.ExitCode -eq 0) "got $($r.ExitCode): $($r.ErrorText)"
    $tail = [string]::Join([string][char]92, @(@($segments | Select-Object -Last 2) + 'omnihub.exe'))
    Test-Case 'reports its own Russian path' (([string]$r.Data.path).EndsWith($tail)) "got $($r.Data.path)"
    $r = Invoke-OmniHub -Exe $copy -Arguments @('selftest') -Json
    Test-Samples 'russian folder' $r.Data
} finally {
    $top = Split-Path -Parent (Split-Path -Parent $dir)
    Remove-Item -LiteralPath $top -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Output '5. Console code page unchanged'
$codePageAfter = $null
try { $codePageAfter = [Console]::OutputEncoding.CodePage } catch { }
Test-Case "code page $codePageBefore -> $codePageAfter" ($codePageBefore -eq $codePageAfter)

if ($script:failures -gt 0) {
    Write-Output "Encoding test: $($script:failures) failure(s)."
    exit 1
}
Write-Output 'Encoding test: all passed.'
exit 0
