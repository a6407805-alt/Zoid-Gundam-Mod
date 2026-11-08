# Downloads the latest Zoid-Gundam-Mod.zip from GitHub and runs install.ps1.
param([string]$Key, [switch]$DownloadOnly, [string]$OutDir)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$repo = 'a6407805-alt/Zoid-Gundam-Mod'
$dir = if ($OutDir) { $OutDir } else { Join-Path $env:TEMP 'ZoidGundamUpdate' }
if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$zip = Join-Path $dir 'pack.zip'

Write-Host '  Checking GitHub for the latest Zoid Gundam Mod build...' -ForegroundColor Cyan
if ($Key) {
    $h = @{ Authorization = "Bearer $Key"; 'User-Agent' = 'ZoidGundamUpdater'; Accept = 'application/vnd.github+json' }
    $rel = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest" -Headers $h
    $a = $rel.assets | Where-Object { $_.name -eq 'Zoid-Gundam-Mod.zip' } | Select-Object -First 1
    if (-not $a) { throw 'No Zoid-Gundam-Mod.zip found on the latest release.' }
    $req = [System.Net.HttpWebRequest]::Create($a.url)
    $req.AllowAutoRedirect = $false; $req.UserAgent = 'ZoidGundamUpdater'; $req.Accept = 'application/octet-stream'
    $req.Headers.Add('Authorization', "Bearer $Key")
    $resp = $req.GetResponse(); $loc = $resp.Headers['Location']; $resp.Close()
    Invoke-WebRequest -UseBasicParsing $loc -OutFile $zip
} else {
    Invoke-WebRequest -UseBasicParsing "https://github.com/$repo/releases/latest/download/Zoid-Gundam-Mod.zip" -OutFile $zip
}

if ($DownloadOnly) { Write-Host "  Downloaded to $zip" -ForegroundColor Green; exit 0 }
$ext = Join-Path $dir 'extracted'
Expand-Archive $zip $ext -Force
& (Join-Path $ext 'install.ps1')
