<#
.SYNOPSIS
  Builds the release zip: TaskManager.exe (a small starter) in the root, everything else in subfolders.
.EXAMPLE
  .\tools\pack-release.ps1 -Version 1.0.0
#>
param([Parameter(Mandatory)][string]$Version)

$ErrorActionPreference = 'Stop'

# The native starter is linked with Visual Studio's tools, which look for vswhere on the PATH.
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
if (Test-Path $vswhere) { $env:PATH += ";$vswhere" }

$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'artifacts'
$pack = Join-Path $out "pack-$Version"
$zip = Join-Path $out "TaskManager-v$Version-win-x64.zip"

Remove-Item $pack, $zip -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path "$pack\app", "$pack\info" | Out-Null

# The program (self-contained, so no .NET is needed on the other computer).
dotnet publish "$root\TaskManager.App\TaskManager.App.csproj" -f net10.0-windows10.0.19041.0 -c Release `
    -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true -o "$pack\app"
if ($LASTEXITCODE -ne 0) { throw 'Publishing the app failed.' }
Get-ChildItem "$pack\app" -Filter *.pdb | Remove-Item

# The starter.
dotnet publish "$root\tools\Launcher\Launcher.csproj" -c Release -r win-x64 -o "$out\launcher"
if ($LASTEXITCODE -ne 0) { throw 'Publishing the starter failed.' }
Copy-Item "$out\launcher\TaskManager.exe" $pack

# Texts.
(Get-Content "$PSScriptRoot\release-readme.txt" -Raw).Replace('{VERSION}', $Version) | Set-Content "$pack\info\README.txt" -Encoding utf8
Copy-Item "$root\LICENSE", "$root\THIRD-PARTY-NOTICES.md" "$pack\info"

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($pack, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
Write-Host ("{0} ({1:N0} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))
