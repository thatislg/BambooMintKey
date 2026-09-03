# scripts/build-native.ps1
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

# 1. Dọn dẹp thư mục output cũ (Dừng ctfmon & đổi tên file locked nếu cần)
Stop-Process -Name ctfmon -Force -ErrorAction SilentlyContinue
$TargetDll = Join-Path $OutputDir "BambooMintKey.dll"
if (Test-Path $TargetDll) {
    Write-Host "[1/3] Giải phóng file lock và dọn dẹp thư mục output..." -ForegroundColor Yellow
    Start-Process regsvr32.exe -ArgumentList "/u /s `"$TargetDll`"" -Wait -ErrorAction SilentlyContinue
    try {
        Remove-Item $TargetDll -Force -ErrorAction Stop
    } catch {
        $rndOld = "BambooMintKey.dll.$([System.Guid]::NewGuid().ToString('N')).old"
        Rename-Item -Path $TargetDll -NewName $rndOld -Force -ErrorAction SilentlyContinue
    }
}

# 2. Biên dịch NativeAOT DLL
Write-Host "[2/3] Bắt đầu xuất bản NativeAOT..." -ForegroundColor Yellow
dotnet publish $ProjectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $OutputDir `
    /p:NativeLib=Shared `
    /p:PublishAot=true

# 3. Kiểm tra file đầu ra
$TargetDll = Join-Path $OutputDir "BambooMintKey.dll"
if (Test-Path $TargetDll) {
    $fileSize = (Get-Item $TargetDll).Length / 1MB
    Write-Host "[3/3] Xuất bản thành công!" -ForegroundColor Green
    Write-Host " -> File: $TargetDll" -ForegroundColor Green
    Write-Host " -> Dung lượng: $([Math]::Round($fileSize, 2)) MB" -ForegroundColor Green
} else {
    Write-Host "[LỖI] Không tìm thấy file BambooMintKey.dll sau khi build!" -ForegroundColor Red
    exit 1
}