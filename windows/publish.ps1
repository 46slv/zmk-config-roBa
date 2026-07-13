param(
    [ValidateSet("framework-dependent", "self-contained")]
    [string]$Mode = "framework-dependent"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $PSScriptRoot "RoBaStatus\RoBaStatus.csproj"
$output = Join-Path $root "artifacts\RoBaStatus-win-x64"
$selfContained = if ($Mode -eq "self-contained") { "true" } else { "false" }

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained $selfContained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $output

Write-Host "Published: $output"
