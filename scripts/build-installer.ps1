# scripts/build-installer.ps1
[CmdletBinding()]
param (
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $PSScriptRoot
$UiOutputDir = Join-Path $RootDir "publish\ui"
$InstallerScript = Join-Path $RootDir "delivery\installer\installer.iss"

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "  BambooMintKey Installer Build" -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan

# 1. Build NativeAOT TSF COM DLL (output: publish\win-x64\BambooMintKey.dll)
$BuildNativeScript = Join-Path $RootDir "scripts\build-native.ps1"
Write-Host "[1/3] Building NativeAOT TSF bridge..." -ForegroundColor Yellow
& $BuildNativeScript -Configuration $Configuration -Runtime $Runtime

# 2. Publish Avalonia UI app (output: publish\ui\)
$UiProject = Join-Path $RootDir "src\BambooMintKey.UI\BambooMintKey.UI.fsproj"
Write-Host "[2/3] Publishing configuration GUI..." -ForegroundColor Yellow
if (Test-Path $UiOutputDir) {
    Remove-Item -Path $UiOutputDir -Recurse -Force
}
dotnet publish $UiProject `
    -c $Configuration `
    -r $Runtime `
    -p:PublishAot=true `
    -p:IlcOptimizationPreference=Size `
    -p:OptimizationPreference=Size `
    -p:StackTraceSupport=false `
    -p:InvariantGlobalization=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -p:GenerateDocumentationFile=false `
    -o $UiOutputDir

# 2.1. Dọn dẹp file không cần thiết trong UI publish
Write-Host "[2.1] Dọn dẹp UI artifacts thừa..." -ForegroundColor Yellow
$excludedUiFiles = @(
    "*.pdb"
    "*.xml"
    "*.deps.json"
    "createdump.exe"
    "Avalonia.FreeDesktop.dll"
    "Avalonia.FreeDesktop.AtSpi.dll"
    "Avalonia.Vulkan.dll"
    "Avalonia.X11.dll"
    "Tmds.DBus.Protocol.dll"
)
foreach ($pattern in $excludedUiFiles) {
    Get-ChildItem -Path $UiOutputDir -Filter $pattern -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue
}

# Xóa các thư mục resource locale không cần thiết (giữ lại tiếng Anh)
Get-ChildItem -Path $UiOutputDir -Directory | Where-Object { $_.Name -match '^(cs|de|es|fr|it|ja|ko|pl|pt-BR|ru|tr|zh-Hans|zh-Hant)$' } | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# 3. Compile Inno Setup installer
Write-Host "[3/3] Compiling installer with Inno Setup..." -ForegroundColor Yellow
$Iscc = Get-Command "iscc" -ErrorAction SilentlyContinue
if (-not $Iscc) {
    $IsccFallback = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    if (-not (Test-Path $IsccFallback)) {
        $IsccFallback = "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
    }
    if (-not (Test-Path $IsccFallback)) {
        $IsccFallback = "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    }
    if (-not (Test-Path $IsccFallback)) {
        Write-Host "[ERROR] Inno Setup compiler (ISCC.exe) not found." -ForegroundColor Red
        Write-Host "        Searched PATH, 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'," -ForegroundColor Red
        Write-Host "        'C:\Program Files\Inno Setup 6\ISCC.exe', and" -ForegroundColor Red
        Write-Host "        '%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe'." -ForegroundColor Red
        Write-Host "        Please install Inno Setup 6 or add ISCC.exe to PATH." -ForegroundColor Red
        exit 1
    }
    $Iscc = $IsccFallback
}

& $Iscc $InstallerScript
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Installer compilation failed." -ForegroundColor Red
    exit $LASTEXITCODE
}

$OutputExe = Join-Path $RootDir "bin\dist\BambooMintKey-Setup.exe"
if (Test-Path $OutputExe) {
    $size = (Get-Item $OutputExe).Length / 1MB
    Write-Host "----------------------------------------------------" -ForegroundColor Green
    Write-Host "  Installer built successfully!" -ForegroundColor Green
    Write-Host "  $OutputExe ($([Math]::Round($size, 2)) MB)" -ForegroundColor Green
    Write-Host "----------------------------------------------------" -ForegroundColor Green
} else {
    Write-Host "[ERROR] Installer output not found at $OutputExe" -ForegroundColor Red
    exit 1
}
