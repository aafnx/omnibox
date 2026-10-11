# OMNIBOX setup window (WinForms, Windows PowerShell 5.1).
# Saved as UTF-8 with BOM: contains Russian UI text.
param([Parameter(Mandatory = $true)][string]$Root)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# The window runs hidden from a console: show build errors instead of failing silently
$ErrorActionPreference = 'Stop'
$script:uiStarted = $false
trap {
    if ($script:uiStarted) { continue }
    [void][Windows.Forms.MessageBox]::Show("Не удалось открыть окно OMNIBOX:`n$($_.Exception.Message)", 'OMNIBOX', 'OK', 'Error')
    exit 1
}

. (Join-Path $PSScriptRoot 'common.ps1')

Add-Type -Namespace OmniGui -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
'@

# Runs one module process without a window and collects its output lines thread-safely
Add-Type -TypeDefinition @'
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

public class OmniProc
{
    public ConcurrentQueue<string> Lines = new ConcurrentQueue<string>();
    private Process p;

    public void Start(string file, string args)
    {
        ProcessStartInfo si = new ProcessStartInfo(file, args);
        si.UseShellExecute = false;
        si.CreateNoWindow = true;
        si.RedirectStandardOutput = true;
        si.RedirectStandardError = true;
        si.StandardOutputEncoding = Encoding.UTF8;
        si.StandardErrorEncoding = Encoding.UTF8;
        p = new Process();
        p.StartInfo = si;
        p.OutputDataReceived += (s, e) => { if (e.Data != null) Lines.Enqueue(e.Data); };
        p.ErrorDataReceived += (s, e) => { if (e.Data != null) Lines.Enqueue(e.Data); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
    }

    public bool Done
    {
        get
        {
            if (p == null || !p.HasExited) return false;
            p.WaitForExit();
            return true;
        }
    }
}
'@

# --- Look -----------------------------------------------------------------
[void][OmniGui.Native]::SetProcessDPIAware()
[Windows.Forms.Application]::EnableVisualStyles()

$g = [Drawing.Graphics]::FromHwnd([IntPtr]::Zero)
$script:Scale = $g.DpiX / 96
$g.Dispose()
function S([double]$v) { return [int][Math]::Round($v * $script:Scale) }
function Pad([int]$l, [int]$t, [int]$r, [int]$b) { return New-Object Windows.Forms.Padding((S $l), (S $t), (S $r), (S $b)) }
function Col([string]$hex) { return [Drawing.ColorTranslator]::FromHtml($hex) }

# OMNIBOX night theme
$cBg       = Col '#000000'   # void
$cSurface  = Col '#0A0C1F'   # deep night
$cRaised   = Col '#151936'
$cLine     = Col '#3A3F6B'
$cText     = Col '#DEE4EE'   # signal
$cMuted    = Col '#8C93AB'
$cAccent   = Col '#7C6CFF'   # iris
$cOnAccent = Col '#000000'

$families = @([Drawing.FontFamily]::Families | ForEach-Object { $_.Name })
function Get-FontName([string[]]$names) {
    foreach ($n in $names) { if ($families -contains $n) { return $n } }
    return 'Segoe UI'
}
$fnText    = Get-FontName @('Inter', 'Segoe UI')
$fnDisplay = Get-FontName @('Michroma', 'Segoe UI Semibold')
$fnMono    = Get-FontName @('JetBrains Mono', 'Cascadia Mono', 'Consolas')

$fontBase   = New-Object Drawing.Font($fnText, 10)
$fontStrong = New-Object Drawing.Font($fnText, 10, [Drawing.FontStyle]::Bold)
$fontTitle  = New-Object Drawing.Font($fnText, 12, [Drawing.FontStyle]::Bold)
$fontSmall  = New-Object Drawing.Font($fnText, 9)
$fontLogo   = New-Object Drawing.Font($fnDisplay, 20)
$fontLabel  = New-Object Drawing.Font($fnText, 8.5, [Drawing.FontStyle]::Bold)
$fontMono   = New-Object Drawing.Font($fnMono, 9)

# --- State ----------------------------------------------------------------
$mods      = @(Get-OmniManifest $Root)
$entries   = New-Object System.Collections.ArrayList   # Module, Check, Options[]
$cards     = New-Object System.Collections.ArrayList
$wrapCtl   = New-Object System.Collections.ArrayList   # follow the card's inner width
$textBoxes = New-Object System.Collections.ArrayList

$script:queue      = New-Object System.Collections.Queue
$script:cur        = $null
$script:curTitle   = ''
$script:checkMode  = $false
$script:needReboot = $false
$script:lastBlank  = $true

function New-Label([string]$text, $color, $font) {
    $l = New-Object Windows.Forms.Label
    $l.Text = $text
    $l.AutoSize = $true
    $l.ForeColor = $color
    if ($font) { $l.Font = $font }
    $l.Margin = Pad 0 0 0 4
    [void]$wrapCtl.Add($l)
    return $l
}

function New-Button([string]$text, [bool]$primary) {
    $b = New-Object Windows.Forms.Button
    $b.Text = $text
    $b.FlatStyle = 'Flat'
    $b.Size = New-Object Drawing.Size((S 170), (S 42))
    $b.Font = $fontStrong
    $b.Cursor = [Windows.Forms.Cursors]::Hand
    $b.FlatAppearance.BorderSize = 1
    $b.Margin = Pad 8 0 0 0
    if ($primary) {
        $b.BackColor = $cAccent
        $b.ForeColor = $cOnAccent
        $b.FlatAppearance.BorderColor = $cAccent
    } else {
        $b.BackColor = $cRaised
        $b.ForeColor = $cText
        $b.FlatAppearance.BorderColor = $cLine
    }
    return $b
}

# --- Window ---------------------------------------------------------------
$form = New-Object Windows.Forms.Form
$form.Text = 'OMNIBOX — настройка'
$form.BackColor = $cBg
$form.ForeColor = $cText
$form.Font = $fontBase
$form.StartPosition = 'CenterScreen'
$form.Size = New-Object Drawing.Size((S 920), (S 800))
$form.MinimumSize = New-Object Drawing.Size((S 640), (S 560))

# Header
$header = New-Object Windows.Forms.TableLayoutPanel
$header.Dock = 'Top'
$header.AutoSize = $true
$header.ColumnCount = 1
$header.Padding = Pad 24 20 24 12
$header.BackColor = $cBg
$logo = New-Label 'OMNIBOX' $cText $fontLogo
$logo.Margin = Pad 0 0 0 2
[void]$header.Controls.Add($logo)
[void]$header.Controls.Add((New-Label 'Первоначальная настройка. Отметьте нужные разделы, затем проверьте или примените.' $cMuted $null))

# Modules
$flow = New-Object Windows.Forms.FlowLayoutPanel
$flow.Dock = 'Fill'
$flow.FlowDirection = 'TopDown'
$flow.WrapContents = $false
$flow.AutoScroll = $true
$flow.Padding = Pad 24 4 24 4
$flow.BackColor = $cBg

foreach ($m in $mods) {
    $card = New-Object Windows.Forms.TableLayoutPanel
    $card.ColumnCount = 1
    $card.AutoSize = $true
    $card.AutoSizeMode = 'GrowAndShrink'
    $card.BackColor = $cSurface
    $card.Padding = Pad 20 16 20 16
    $card.Margin = Pad 0 0 0 12

    $chk = New-Object Windows.Forms.CheckBox
    $chk.Text = $m.title
    $chk.Checked = $true
    $chk.AutoSize = $true
    $chk.Font = $fontTitle
    $chk.ForeColor = $cText
    $chk.Margin = Pad 0 0 0 4
    [void]$card.Controls.Add($chk)
    [void]$card.Controls.Add((New-Label $m.description $cMuted $null))

    $optPanel = New-Object Windows.Forms.TableLayoutPanel
    $optPanel.ColumnCount = 1
    $optPanel.AutoSize = $true
    $optPanel.AutoSizeMode = 'GrowAndShrink'
    $optPanel.Margin = Pad 0 0 0 0
    $optEntries = @()

    foreach ($o in @($m.options)) {
        $ol = New-Label $o.label $cText $fontStrong
        $ol.Margin = Pad 0 12 0 2
        [void]$optPanel.Controls.Add($ol)
        if ($o.hint) { [void]$optPanel.Controls.Add((New-Label $o.hint $cMuted $fontSmall)) }

        $ch = @($o.choices)
        if ($o.type -eq 'text') {
            $ctl = New-Object Windows.Forms.TextBox
            $ctl.BackColor = $cRaised
            $ctl.ForeColor = $cText
            $ctl.BorderStyle = 'FixedSingle'
            $ctl.Font = $fontMono
            $ctl.Text = [string]$o.default
            $ctl.Margin = Pad 0 4 0 0
            [void]$textBoxes.Add($ctl)
        } elseif ($o.type -eq 'choice') {
            $ctl = New-Object Windows.Forms.ComboBox
            $ctl.DropDownStyle = 'DropDownList'
            $ctl.FlatStyle = 'Flat'
            $ctl.BackColor = $cRaised
            $ctl.ForeColor = $cText
            $ctl.Width = S 340
            $ctl.Margin = Pad 0 4 0 0
            $sel = 0
            for ($i = 0; $i -lt $ch.Count; $i++) {
                [void]$ctl.Items.Add($ch[$i].label)
                if ($ch[$i].id -eq $o.default) { $sel = $i }
            }
            $ctl.SelectedIndex = $sel
        } else {
            $ctl = New-Object Windows.Forms.FlowLayoutPanel
            $ctl.AutoSize = $true
            $ctl.WrapContents = $true
            $ctl.Margin = Pad 0 4 0 0
            [void]$wrapCtl.Add($ctl)
            $def = @($o.default)
            foreach ($c in $ch) {
                $cb = New-Object Windows.Forms.CheckBox
                $cb.Text = $c.label
                $cb.Tag = $c.id
                $cb.Checked = ($def -contains $c.id)
                $cb.AutoSize = $true
                $cb.ForeColor = $cText
                $cb.Margin = Pad 0 2 20 2
                [void]$ctl.Controls.Add($cb)
            }
        }
        [void]$optPanel.Controls.Add($ctl)
        $optEntries += [pscustomobject]@{ Opt = $o; Control = $ctl }
    }

    if ($optEntries.Count -gt 0) { [void]$card.Controls.Add($optPanel) }
    $chk.Tag = $optPanel
    $chk.Add_CheckedChanged({ param($s, $e) $s.Tag.Enabled = $s.Checked })

    [void]$entries.Add([pscustomobject]@{ Module = $m; Check = $chk; Options = $optEntries })
    [void]$cards.Add($card)
    [void]$flow.Controls.Add($card)
}

# Log
$logPanel = New-Object Windows.Forms.TableLayoutPanel
$logPanel.Dock = 'Bottom'
$logPanel.Height = S 230
$logPanel.ColumnCount = 1
$logPanel.RowCount = 2
[void]$logPanel.RowStyles.Add((New-Object Windows.Forms.RowStyle([Windows.Forms.SizeType]::AutoSize)))
[void]$logPanel.RowStyles.Add((New-Object Windows.Forms.RowStyle([Windows.Forms.SizeType]::Percent, 100)))
$logPanel.Padding = Pad 24 8 24 0
$logPanel.BackColor = $cBg
$logTitle = New-Object Windows.Forms.Label
$logTitle.Text = 'ЖУРНАЛ'
$logTitle.AutoSize = $true
$logTitle.Font = $fontLabel
$logTitle.ForeColor = $cAccent
$logTitle.Margin = Pad 0 0 0 6
$log = New-Object Windows.Forms.TextBox
$log.Multiline = $true
$log.ReadOnly = $true
$log.ScrollBars = 'Vertical'
$log.WordWrap = $false
$log.Dock = 'Fill'
$log.BackColor = $cSurface
$log.ForeColor = $cText
$log.BorderStyle = 'None'
$log.Font = $fontMono
[void]$logPanel.Controls.Add($logTitle, 0, 0)
[void]$logPanel.Controls.Add($log, 0, 1)

# Footer
$footer = New-Object Windows.Forms.TableLayoutPanel
$footer.Dock = 'Bottom'
$footer.AutoSize = $true
$footer.ColumnCount = 3
[void]$footer.ColumnStyles.Add((New-Object Windows.Forms.ColumnStyle([Windows.Forms.SizeType]::Percent, 100)))
[void]$footer.ColumnStyles.Add((New-Object Windows.Forms.ColumnStyle([Windows.Forms.SizeType]::AutoSize)))
[void]$footer.ColumnStyles.Add((New-Object Windows.Forms.ColumnStyle([Windows.Forms.SizeType]::AutoSize)))
$footer.Padding = Pad 24 16 24 20
$footer.BackColor = $cBg
$status = New-Object Windows.Forms.Label
$status.AutoSize = $true
$status.Anchor = 'Left'
$status.ForeColor = $cMuted
$status.Text = 'Готово к работе.'
$btnCheck = New-Button 'Проверить' $false
$btnApply = New-Button 'Применить' $true
[void]$footer.Controls.Add($status, 0, 0)
[void]$footer.Controls.Add($btnCheck, 1, 0)
[void]$footer.Controls.Add($btnApply, 2, 0)

$form.Controls.Add($flow)
$form.Controls.Add($header)
$form.Controls.Add($logPanel)
$form.Controls.Add($footer)
$flow.BringToFront()   # fill takes what is left after the docked panels

function Update-Widths {
    $w = $flow.ClientSize.Width - $flow.Padding.Horizontal - [Windows.Forms.SystemInformation]::VerticalScrollBarWidth
    if ($w -lt (S 200)) { return }
    foreach ($c in $cards) {
        $c.MinimumSize = New-Object Drawing.Size($w, 0)
        $c.MaximumSize = New-Object Drawing.Size($w, 0)
    }
    $inner = $w - (S 44)
    foreach ($c in $wrapCtl) { $c.MaximumSize = New-Object Drawing.Size($inner, 0) }
    foreach ($t in $textBoxes) { $t.Width = $inner }
}
$flow.Add_Resize({ Update-Widths })

# --- Run ------------------------------------------------------------------
function Add-Log([string]$text) {
    if ($text -match '[█▒]') { return }        # winget progress bars
    if ($text -match '^\s*[-\\|/]\s*$') { return }       # spinner
    $blank = ($text.Trim() -eq '')
    if ($blank -and $script:lastBlank) { return }
    $script:lastBlank = $blank
    $log.AppendText($text + "`r`n")
}

function Set-Busy([bool]$busy) {
    $btnCheck.Enabled = -not $busy
    $btnApply.Enabled = -not $busy
    $flow.Enabled = -not $busy
}

function Get-Selection {
    $list = @()
    foreach ($e in $entries) {
        if (-not $e.Check.Checked) { continue }
        $opts = @{}
        foreach ($oe in @($e.Options)) {
            $o = $oe.Opt
            if ($o.type -eq 'text') {
                $opts[$o.id] = $oe.Control.Text.Trim()
            } elseif ($o.type -eq 'choice') {
                $opts[$o.id] = @($o.choices)[$oe.Control.SelectedIndex].id
            } else {
                $opts[$o.id] = @($oe.Control.Controls | Where-Object { $_.Checked } | ForEach-Object { $_.Tag })
            }
        }
        $list += [pscustomobject]@{ Module = $e.Module; Options = $opts }
    }
    return $list
}

function Start-NextModule {
    if ($script:queue.Count -eq 0) { Complete-Run; return }
    $item = $script:queue.Dequeue()
    if (-not $script:checkMode -and $item.Module.reboot) { $script:needReboot = $true }
    $script:curTitle = $item.Module.title
    $status.Text = "Выполняется: $($script:curTitle)…"
    Add-Log ''
    Add-Log "##### $($script:curTitle) #####"
    try {
        $a = Get-OmniRunnerArgs $Root $item.Module $item.Options $script:checkMode $true
        $p = New-Object OmniProc
        $p.Start($script:OmniPowerShell, (Join-OmniArgs $a))
        $script:cur = $p
    } catch {
        Add-Log "[fail] $($_.Exception.Message)"
        $script:cur = $null
        Start-NextModule
    }
}

function Complete-Run {
    $timer.Stop()
    Set-Busy $false
    Add-Log ''
    if ($script:checkMode) {
        $status.Text = 'Проверка завершена. Ничего не изменено.'
        Add-Log 'Проверка завершена.'
        return
    }
    $status.Text = 'Готово.'
    Add-Log 'Готово.'
    if ($script:needReboot) {
        $r = [Windows.Forms.MessageBox]::Show($form, 'Часть изменений вступит в силу после перезагрузки. Перезагрузить сейчас?', 'OMNIBOX', 'YesNo', 'Question')
        if ($r -eq 'Yes') { shutdown.exe /r /t 5 }
    }
}

function Start-Run([bool]$check) {
    $sel = @(Get-Selection)
    if ($sel.Count -eq 0) { $status.Text = 'Отметьте хотя бы один раздел.'; return }
    if (-not $check) {
        $names = ($sel | ForEach-Object { $_.Module.title }) -join ', '
        $r = [Windows.Forms.MessageBox]::Show($form, "Применить: $names?", 'OMNIBOX', 'YesNo', 'Question')
        if ($r -ne 'Yes') { return }
    }
    $script:checkMode = $check
    $script:needReboot = $false
    $script:lastBlank = $true
    $log.Clear()
    $script:queue.Clear()
    foreach ($s in $sel) { $script:queue.Enqueue($s) }
    Set-Busy $true
    $timer.Start()
    Start-NextModule
}

$timer = New-Object Windows.Forms.Timer
$timer.Interval = 150
$timer.Add_Tick({
    if (-not $script:cur) { return }
    $line = $null
    while ($script:cur.Lines.TryDequeue([ref]$line)) { Add-Log $line }
    if ($script:cur.Done) {
        while ($script:cur.Lines.TryDequeue([ref]$line)) { Add-Log $line }
        $script:cur = $null
        Start-NextModule
    }
})

$btnCheck.Add_Click({ Start-Run $true })
$btnApply.Add_Click({ Start-Run $false })
$form.Add_HandleCreated({
    # Dark title bar on Windows 11 (DWMWA_USE_IMMERSIVE_DARK_MODE)
    $on = 1
    try { [void][OmniGui.Native]::DwmSetWindowAttribute($form.Handle, 20, [ref]$on, 4) } catch { }
})
$form.Add_Shown({ Update-Widths; $form.Activate() })
$form.Add_FormClosing({
    param($s, $e)
    if ($script:cur) {
        [void][Windows.Forms.MessageBox]::Show($form, "Дождитесь окончания: сейчас выполняется «$($script:curTitle)».", 'OMNIBOX', 'OK', 'Information')
        $e.Cancel = $true
    }
})

$ErrorActionPreference = 'Continue'
$script:uiStarted = $true
[void]$form.ShowDialog()
