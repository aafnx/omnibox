# OMNIBOX shared helpers for the console and GUI frontends

$script:OmniPowerShell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'

# Modules from modules\modules.json, in file order, with .Path to the script
function Get-OmniManifest([string]$Root) {
    $path = Join-Path $Root 'modules\modules.json'
    $json = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8) | ConvertFrom-Json
    $list = @()
    foreach ($m in @($json.modules)) {
        $m | Add-Member -NotePropertyName Path -NotePropertyValue (Join-Path $Root "modules\$($m.id).ps1") -Force
        $list += $m
    }
    return $list
}

# Hashtable of option id -> default value
function Get-OmniDefaults($Module) {
    $h = @{}
    foreach ($o in @($Module.options)) {
        if ($o.type -eq 'multi') { $h[$o.id] = @($o.default) } else { $h[$o.id] = $o.default }
    }
    return $h
}

# Options are passed to the runner as base64(JSON) so keys, paths and quotes survive the command line
function ConvertTo-OmniB64([hashtable]$Options) {
    $json = ConvertTo-Json -InputObject $Options -Compress -Depth 5
    return [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($json))
}

# Argument array for powershell.exe to run one module through lib\runner.ps1
function Get-OmniRunnerArgs([string]$Root, $Module, [hashtable]$Options, [bool]$Check, [bool]$Capture) {
    $a = @('-NoProfile', '-ExecutionPolicy', 'Bypass',
           '-File', (Join-Path $Root 'lib\runner.ps1'),
           '-Script', $Module.Path,
           '-ArgsB64', (ConvertTo-OmniB64 $Options))
    if ($Check) { $a += '-Check' }
    if ($Capture) { $a += '-Capture' }
    return $a
}

# Same arguments as one command-line string (for ProcessStartInfo)
function Join-OmniArgs([string[]]$Arguments) {
    $parts = foreach ($x in $Arguments) {
        if ($x -match '[\s"]') { '"' + ($x -replace '"', '\"') + '"' } else { $x }
    }
    return ($parts -join ' ')
}
