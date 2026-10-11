# OMNIBOX module runner: decodes options and calls a module script with them.
#   -Capture  merge all output streams and write them as UTF-8 text to stdout (used by the GUI)
param(
    [Parameter(Mandatory = $true)][string]$Script,
    [string]$ArgsB64,
    [switch]$Check,
    [switch]$Capture
)

$p = @{}
if ($ArgsB64) {
    $json = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($ArgsB64))
    $o = $json | ConvertFrom-Json
    if ($o) { foreach ($prop in $o.PSObject.Properties) { $p[$prop.Name] = $prop.Value } }
}
if ($Check) { $p['Check'] = $true }

if ($Capture) {
    $out = New-Object IO.StreamWriter([Console]::OpenStandardOutput(), (New-Object Text.UTF8Encoding($false)))
    $out.AutoFlush = $true
    try {
        & $Script @p *>&1 | Out-String -Stream -Width 160 | ForEach-Object { $out.WriteLine($_) }
    } catch {
        $out.WriteLine("[fail] $($_.Exception.Message)")
    } finally {
        $out.Dispose()
    }
} else {
    & $Script @p
}
