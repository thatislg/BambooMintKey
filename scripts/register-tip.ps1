# scripts/register-tip.ps1
[CmdletBinding()]
param (
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

# Đảm bảo chạy dưới quyền Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[CẢNH BÁO] Kịch bản cần quyền Administrator để đăng ký COM/TSF Server." -ForegroundColor Red
    Write-Host "Đang yêu cầu nâng quyền (UAC)..." -ForegroundColor Yellow
    Start-Process powershell.exe "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

$RootDir = Split-Path -Parent $PSScriptRoot
$DllPath = Join-Path $RootDir "publish\$Runtime\BambooMintKey.dll"

if (-not (Test-Path $DllPath)) {
    Write-Host "[LỖI] Không tìm thấy $DllPath. Hãy chạy scripts/build-native.ps1 trước." -ForegroundColor Red
    exit 1
}

Write-Host "Đang đăng ký BambooMintKey vào Windows TSF..." -ForegroundColor Cyan
Write-Host "Đường dẫn DLL: $DllPath" -ForegroundColor Gray

# Gọi regsvr32 trong chế độ im lặng
$process = Start-Process regsvr32.exe -ArgumentList "/s `"$DllPath`"" -PassThru -Wait

if ($process.ExitCode -eq 0) {
    Write-Host "Đăng ký thành công!" -ForegroundColor Green
    Write-Host "Hãy kiểm tra thanh Language Bar (Win + Space) để chọn 'BambooMintKey Vietnamese Input'." -ForegroundColor Green
} else {
    Write-Host "[FAIL] regsvr32 thất bại với mã lỗi: $($process.ExitCode)" -ForegroundColor Red
    exit 1
}