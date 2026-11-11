# Builds Shrike in Release and packs it into a .yak file for the Rhino Package Manager.
# Run from the repository root:  powershell -ExecutionPolicy Bypass -File yak\build-yak.ps1
# Add -SkipBuild if you already built Release in Visual Studio.

param(
    [switch]$SkipBuild,
    [string]$Yak = "$env:ProgramFiles\Rhino 8\System\Yak.exe"
)

$ErrorActionPreference = "Stop"
$root    = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "Shrike\Shrike.csproj"
$output  = Join-Path $root "Shrike\bin\Release\net8.0-windows"
$dist    = Join-Path $PSScriptRoot "dist"

if (-not (Test-Path $Yak)) { throw "Yak.exe not found at '$Yak'. Pass -Yak <path>." }

# The package version must match the plug-in version.
$csprojVersion   = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$manifestVersion = (Select-String -Path (Join-Path $PSScriptRoot "manifest.yml") -Pattern '^version:\s*(\S+)').Matches[0].Groups[1].Value
if ($csprojVersion -ne $manifestVersion) {
    throw "Version mismatch: Shrike.csproj has $csprojVersion, manifest.yml has $manifestVersion."
}

if (-not $SkipBuild) {
    dotnet build $project -c Release
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed." }
}

$rhp = Join-Path $output "Shrike.rhp"
if (-not (Test-Path $rhp)) { throw "Shrike.rhp not found in $output. Build Release first." }

# Stage only what the package needs.
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Path $dist | Out-Null
Copy-Item $rhp $dist
$tools = Join-Path $output "Tools"
if (Test-Path $tools) { Copy-Item $tools $dist -Recurse }
Copy-Item (Join-Path $PSScriptRoot "manifest.yml") $dist
Copy-Item (Join-Path $PSScriptRoot "icon.png") $dist

Push-Location $dist
try {
    & $Yak build --platform win
    if ($LASTEXITCODE -ne 0) { throw "yak build failed." }
} finally {
    Pop-Location
}

Get-ChildItem $dist -Filter *.yak | ForEach-Object { Write-Host "Built $($_.FullName)" }
