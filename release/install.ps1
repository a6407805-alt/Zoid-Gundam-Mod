# Zoid Gundam Mod installer for Rusted Warfare - no mod manager needed.
# Finds Rusted Warfare (Steam), installs Zoid Gundam and any group unit/building mods straight into
# Rusted Warfare\mods\units\, and optionally hooks Steam's Play button to open the Zoid Gundam Launcher.
param(
    [switch]$Uninstall,
    [string]$GameDir,
    [switch]$SkipExtras,
    [string]$ExtrasKey,
    [string]$LauncherPath,
    [switch]$SetSteamPlay,
    [switch]$UnsetSteamPlay,
    [switch]$SteamPlayOnly,
    [switch]$ClearLaunchOptions,
    [string]$SteamRoot
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
try { Start-Transcript (Join-Path $here 'install_log.txt') -Force | Out-Null } catch { }
try { Get-ChildItem $here -Recurse -File | Unblock-File -ErrorAction SilentlyContinue } catch { }
trap {
    Write-Host ''
    Write-Host "Something went wrong: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host 'Send install_log.txt (in this folder) to whoever gave you the pack.' -ForegroundColor Red
    try { Stop-Transcript | Out-Null } catch { }
    exit 1
}

$modsSrc = Join-Path $here 'Mods'
$mapsSrc = Join-Path $here 'Maps'

function Say($text, $color = 'Gray') { Write-Host $text -ForegroundColor $color }

function Find-GameFolders {
    $found = @()
    $steamRoots = @()
    foreach ($key in 'HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam') {
        try {
            $p = Get-ItemProperty $key -ErrorAction Stop
            if ($p.SteamPath) { $steamRoots += $p.SteamPath }
            if ($p.InstallPath) { $steamRoots += $p.InstallPath }
        } catch { }
    }
    $libs = @()
    foreach ($s in ($steamRoots | Select-Object -Unique)) {
        $s = $s -replace '/', '\'
        $libs += $s
        $vdf = Join-Path $s 'steamapps\libraryfolders.vdf'
        if (Test-Path $vdf) {
            foreach ($m in (Select-String -Path $vdf -Pattern '"path"\s+"([^"]+)"' -AllMatches)) {
                foreach ($g in $m.Matches) { $libs += ($g.Groups[1].Value -replace '\\\\', '\') }
            }
        }
    }
    foreach ($l in ($libs | Select-Object -Unique)) {
        $game = Join-Path $l 'steamapps\common\Rusted Warfare'
        if ((Test-Path (Join-Path $game 'Rusted Warfare.exe')) -or (Test-Path (Join-Path $game 'Rusted Warfare - 64.exe'))) {
            $found += $game
        }
    }
    return @($found | Select-Object -Unique)
}

function Get-SteamRoot {
    if ($SteamRoot) { return $SteamRoot }
    foreach ($key in 'HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam') {
        try {
            $p = Get-ItemProperty $key -ErrorAction Stop
            foreach ($v in $p.SteamPath, $p.InstallPath) {
                if ($v -and (Test-Path (Join-Path $v 'userdata'))) { return ($v -replace '/', '\') }
            }
        } catch { }
    }
    return $null
}

# --- tiny VDF reader: finds a key's {block} or "value" at the top level of a range, respecting quotes and nesting
$script:VdfStr = [regex]'\G"((?:[^"\\]|\\.)*)"'
function Skip-Ws($t, $i) { while ($i -lt $t.Length -and [char]::IsWhiteSpace($t[$i])) { $i++ }; return $i }
function Skip-Block($t, $i) {
    $depth = 0
    while ($i -lt $t.Length) {
        $c = $t[$i]
        if ($c -eq '"') { $m = $script:VdfStr.Match($t, $i); $i += [Math]::Max(1, $m.Length); continue }
        if ($c -eq '{') { $depth++ } elseif ($c -eq '}') { $depth--; if ($depth -eq 0) { return $i + 1 } }
        $i++
    }
    return $t.Length
}
function Find-VdfKey($t, [int]$from, [int]$to, [string]$key) {
    $i = $from
    while ($i -lt $to) {
        $i = Skip-Ws $t $i
        if ($i -ge $to) { break }
        if ($t[$i] -ne '"') { $i++; continue }
        $k = $script:VdfStr.Match($t, $i); if (-not $k.Success) { $i++; continue }
        $i = Skip-Ws $t ($i + $k.Length)
        if ($i -lt $to -and $t[$i] -eq '{') {
            $end = Skip-Block $t $i
            if ($k.Groups[1].Value -ieq $key) { return @{ Kind = 'block'; Start = $i + 1; End = $end - 1 } }
            $i = $end; continue
        }
        if ($i -lt $to -and $t[$i] -eq '"') {
            $v = $script:VdfStr.Match($t, $i)
            if ($k.Groups[1].Value -ieq $key) { return @{ Kind = 'value'; Start = $i; End = $i + $v.Length; Value = $v.Groups[1].Value } }
            $i += [Math]::Max(1, $v.Length); continue
        }
    }
    return $null
}

# Rusted Warfare Steam App ID = 647960
function Set-RWLaunchOptions([string]$value) {
    $root = Get-SteamRoot
    if (-not $root) { Say '  STEAMPLAY: Steam not found.' 'DarkGray'; return }
    $vdfValue = if ($value) { ($value -replace '\\', '\\' -replace '"', '\"') } else { '' }
    $files = Get-ChildItem (Join-Path $root 'userdata') -Directory -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName 'config\localconfig.vdf' } | Where-Object { Test-Path $_ }
    if (-not $files) { Say '  STEAMPLAY: no Steam accounts found on this PC.' 'DarkGray'; return }
    if (Get-Process steam -ErrorAction SilentlyContinue) { Say '  STEAM_OPEN: close Steam first (it rewrites this file when it closes).' 'Yellow'; return }
    foreach ($f in $files) {
        $t = [IO.File]::ReadAllText($f)
        $node = @{ Start = 0; End = $t.Length }
        $ok = $true
        foreach ($k in 'UserLocalConfigStore', 'Software', 'Valve', 'Steam', 'apps') {
            $n = Find-VdfKey $t $node.Start $node.End $k
            if (-not $n -or $n.Kind -ne 'block') { $ok = $false; break }
            $node = $n
        }
        if (-not $ok) { Say "  STEAMPLAY: $f has no apps section; skipped." 'DarkGray'; continue }
        $app = Find-VdfKey $t $node.Start $node.End '647960'
        $new = $null
        if (-not $app) {
            if (-not $value) { continue }
            $new = $t.Insert($node.Start, "`n`t`t`t`t`t`"647960`"`n`t`t`t`t`t{`n`t`t`t`t`t`t`"LaunchOptions`"`t`t`"$vdfValue`"`n`t`t`t`t`t}")
        } else {
            $lo = Find-VdfKey $t $app.Start $app.End 'LaunchOptions'
            if ($value) {
                if ($lo -and $lo.Value -eq $vdfValue) { Say '  STEAMPLAY: already set.' 'Green'; continue }
                if ($lo -and $lo.Value -and $lo.Value -notmatch 'Zoid-Gundam') {
                    Say "  STEAMPLAY_KEPT: you already have your own launch options ($($lo.Value)); not changed." 'Yellow'; continue
                }
                $new = if ($lo) { $t.Remove($lo.Start, $lo.End - $lo.Start).Insert($lo.Start, "`"$vdfValue`"") }
                       else { $t.Insert($app.Start, "`n`t`t`t`t`t`t`"LaunchOptions`"`t`t`"$vdfValue`"") }
            } else {
                if (-not $lo -or $lo.Value -notmatch 'Zoid-Gundam') { continue }
                $new = $t.Remove($lo.Start, $lo.End - $lo.Start).Insert($lo.Start, '""')
            }
        }
        Copy-Item $f "$f.zg-backup" -Force
        [IO.File]::WriteAllText($f, $new)
        Say $(if ($value) { "  STEAMPLAY_ON: Steam's Play button now opens the Zoid Gundam Mod launcher." } else { '  STEAMPLAY_OFF: Steam''s Play button starts Rusted Warfare normally again.' }) 'Green'
    }
}

function Set-DesktopShortcut([string]$target, [bool]$remove) {
    $lnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Zoid Gundam Mod.lnk'
    if ($remove) { if (Test-Path $lnk) { Remove-Item $lnk -Force; Say '  Removed the desktop shortcut.' 'Green' }; return }
    if (Test-Path $lnk) { return }
    try {
        $sc = (New-Object -ComObject WScript.Shell).CreateShortcut($lnk)
        $sc.TargetPath = $target; $sc.WorkingDirectory = Split-Path $target; $sc.Description = 'Zoid Gundam Mod (Rusted Warfare)'
        $sc.Save(); Say '  Added a "Zoid Gundam Mod" shortcut to your desktop.' 'Green'
    } catch { }
}

Say ''
Say '  ====================================' 'Yellow'
Say ('  ZOID GUNDAM MOD ' + $(if ($Uninstall) { 'UNINSTALLER' } else { 'INSTALLER' })) 'Yellow'
if (($ClearLaunchOptions -or $SteamPlayOnly) -and -not $Uninstall) {
    if ($ClearLaunchOptions) { Set-RWLaunchOptions ''; Set-DesktopShortcut '' $true }
    else {
        if ($LauncherPath -and (Test-Path $LauncherPath)) { Set-DesktopShortcut $LauncherPath $false }
        if ($SetSteamPlay -and $LauncherPath) { Set-RWLaunchOptions "`"$LauncherPath`" %command%" }
        elseif ($UnsetSteamPlay) { Set-RWLaunchOptions '' }
    }
    try { Stop-Transcript | Out-Null } catch { }
    exit 0
}
Say '  ====================================' 'Yellow'
Say ''

if (-not $Uninstall -and -not (Test-Path $modsSrc)) {
    Say 'The Mods folder is missing. Extract the whole zip first (right-click > Extract All), then run INSTALL.bat.' 'Red'
    exit 1
}

$games = if ($GameDir) { @($GameDir) } else { Find-GameFolders }
if ($games.Count -eq 0) {
    Say "Couldn't find Rusted Warfare automatically. Pick your Rusted Warfare folder (the one with 'Rusted Warfare.exe')." 'Yellow'
    Add-Type -AssemblyName System.Windows.Forms
    $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
    $dlg.Description = 'Select your Rusted Warfare folder'
    $owner = New-Object System.Windows.Forms.Form -Property @{ TopMost = $true; ShowInTaskbar = $false }
    if ($dlg.ShowDialog($owner) -eq 'OK' -and ((Test-Path (Join-Path $dlg.SelectedPath 'Rusted Warfare.exe')) -or (Test-Path (Join-Path $dlg.SelectedPath 'Rusted Warfare - 64.exe')))) {
        $games = @($dlg.SelectedPath)
    } else {
        Say "Cancelled (or that folder doesn't have Rusted Warfare.exe)." 'Red'
        exit 1
    }
}

$done = 0
foreach ($game in $games) {
    Say "  Game folder: $game" 'Cyan'
    $unitsRoot = Join-Path $game 'mods\units'
    New-Item -ItemType Directory -Force -Path $unitsRoot | Out-Null

    if ($Uninstall) {
        if (Test-Path $modsSrc) {
            foreach ($m in Get-ChildItem $modsSrc -Directory) {
                $dest = Join-Path $unitsRoot $m.Name
                if (Test-Path $dest) { Remove-Item $dest -Recurse -Force; Say "  Removed $($m.Name)." 'Green'; $done++ }
            }
        }
        continue
    }

    if (Test-Path $modsSrc) {
        $mods = Get-ChildItem $modsSrc -Directory
        foreach ($m in $mods) {
            $mDest = Join-Path $unitsRoot $m.Name
            New-Item -ItemType Directory -Force -Path $mDest | Out-Null
            Copy-Item (Join-Path $m.FullName '*') $mDest -Recurse -Force
        }
        Say "  Installed $($mods.Count) Rusted Warfare mod(s): $(($mods | ForEach-Object Name) -join ', ')" 'Green'
    }

    if (Test-Path $mapsSrc) {
        $mapsDest = Join-Path $game 'mods\maps'
        New-Item -ItemType Directory -Force -Path $mapsDest | Out-Null
        Copy-Item (Join-Path $mapsSrc '*') $mapsDest -Recurse -Force
        Say '  Installed custom maps.' 'Green'
    }

    # If the user also keeps a working copy at Desktop\Zoid Gundam, keep it synced with newer files from the pack
    $desktopCopy = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Zoid Gundam'
    $packZoid = Join-Path $modsSrc 'Zoid Gundam'
    if ((Test-Path $desktopCopy) -and (Test-Path $packZoid)) {
        Copy-Item (Join-Path $packZoid '*') $desktopCopy -Recurse -Force
        Say '  Synced Desktop\Zoid Gundam folder.' 'Green'
    }

    $done++
}

if ($Uninstall) {
    Set-RWLaunchOptions ''
    Set-DesktopShortcut '' $true
} elseif ($LauncherPath -and (Test-Path $LauncherPath)) {
    Set-DesktopShortcut $LauncherPath $false
    if ($SetSteamPlay) { Set-RWLaunchOptions "`"$LauncherPath`" %command%" }
}

function Update-OldLaunchers {
    try {
        $targets = @()
        $parentId = (Get-CimInstance Win32_Process -Filter "ProcessId=$PID" -ErrorAction Stop).ParentProcessId
        $parent = Get-CimInstance Win32_Process -Filter "ProcessId=$parentId" -ErrorAction SilentlyContinue
        if ($parent -and $parent.ExecutablePath -and (Split-Path $parent.ExecutablePath -Leaf) -like 'Zoid-Gundam-*.exe') {
            $targets += $parent.ExecutablePath
        }
        $stable = Join-Path $env:LOCALAPPDATA 'ZoidGundam\Launcher\Zoid-Gundam-Mod.exe'
        if ((Test-Path $stable) -and ($targets -notcontains $stable)) { $targets += $stable }
        if ($targets.Count -eq 0) { return }

        $rel = Invoke-RestMethod 'https://api.github.com/repos/a6407805-alt/Zoid-Gundam-Mod/releases/latest' -TimeoutSec 15
        $asset = $rel.assets | Where-Object { $_.name -eq 'Zoid-Gundam-Mod.exe' } | Select-Object -First 1
        if (-not $asset) { return }
        $stale = @($targets | Where-Object { (Get-Item $_).Length -ne $asset.size })
        if ($stale.Count -eq 0) { return }

        Say 'Updating the launcher...' 'Cyan'
        $tmp = Join-Path $env:TEMP 'Zoid-Gundam-Mod.exe.download'
        Invoke-WebRequest $asset.browser_download_url -OutFile $tmp -UseBasicParsing -TimeoutSec 600
        if ((Get-Item $tmp).Length -ne $asset.size) { Remove-Item $tmp -Force; return }
        foreach ($t in $stale) {
            try {
                $old = "$t.old"
                if (Test-Path $old) { Remove-Item $old -Force -ErrorAction SilentlyContinue }
                Move-Item $t $old -Force
                try { Copy-Item $tmp $t -Force } catch { Move-Item $old $t -Force; throw }
            } catch { Say "  Couldn't update the launcher at $t ($($_.Exception.Message))" 'Yellow' }
        }
        Remove-Item $tmp -Force -ErrorAction SilentlyContinue
        Say '  Launcher updated. Close it and open it again to use the new one.' 'Green'
    } catch { }
}
if (-not $Uninstall -and -not $SteamPlayOnly -and -not $env:GITHUB_ACTIONS) { Update-OldLaunchers }

Say ''
if ($done -gt 0) {
    if ($Uninstall) { Say 'Done.' 'Green' }
    else {
        Say 'Done! Launch Rusted Warfare from Steam or the Launcher - Zoid Gundam loads in Mods!' 'Green'
        Say 'Multiplayer: everyone in the lobby gets the exact same units automatically.' 'Green'
    }
} else {
    Say $(if ($Uninstall) { 'Nothing to remove.' } else { 'Nothing was installed (see the messages above).' }) 'Red'
}
try { Stop-Transcript | Out-Null } catch { }
