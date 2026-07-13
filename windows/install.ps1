$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "artifacts\RoBaStatus-win-x64"
$destination = Join-Path $env:LOCALAPPDATA "Programs\RoBaStatus"
$startMenu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"

if (-not (Test-Path (Join-Path $source "RoBaStatus.exe"))) {
    throw "Run windows\publish.ps1 first. RoBaStatus.exe was not found in $source"
}

New-Item -ItemType Directory -Force -Path $destination | Out-Null
Copy-Item -Path (Join-Path $source "*") -Destination $destination -Recurse -Force

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $startMenu "roBa Status.lnk"))
$shortcut.TargetPath = Join-Path $destination "RoBaStatus.exe"
$shortcut.WorkingDirectory = $destination
$shortcut.Description = "roBa layer and battery status"
$shortcut.Save()

Write-Host "Installed: $destination"
Write-Host "Open 'roBa Status' from Start, then choose 'Pin to taskbar'."
