[CmdletBinding()]
param (
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $PSScriptRoot
$ProjectPath = Join-Path $RootDir "src\BambooMintKey.NativeBridge\BambooMintKey.NativeBridge.csproj"
$OutputDir = Join-Path $RootDir "publish\$Runtime"

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "  BambooMintKey NativeAOT Build: $Configuration ($Runtime)" -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan

# TODO: Implement build steps per 002_05:
# 1. Clean old output
# 2. dotnet publish with NativeAOT flags
# 3. Verify output DLL

throw "Script template not yet implemented. See 002_05_DevHarness_and_RegistrationScript.md"
