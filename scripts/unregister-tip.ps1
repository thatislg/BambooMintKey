# scripts/unregister-tip.ps1
[CmdletBinding()]
param (
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Start-Process powershell.exe "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

$RootDir = Split-Path -Parent $PSScriptRoot
$DllPath = Join-Path $RootDir "publish\$Runtime\BambooMintKey.dll"

if (-not (Test-Path $DllPath)) {
    Write-Host "[CẢNH BÁO] Không tìm thấy $DllPath. Cố gắng hủy qua Registry nếu có..." -ForegroundColor Yellow
}

Write-Host "Đang gỡ đăng ký BambooMintKey khỏi Windows TSF..." -ForegroundColor Cyan

$process = Start-Process regsvr32.exe -ArgumentList "/u /s `"$DllPath`"" -PassThru -Wait

if ($process.ExitCode -eq 0) {
    Write-Host "Gỡ đăng ký thành công!" -ForegroundColor Green
} else {
    Write-Host "[FAIL] Gỡ đăng ký thất bại với mã lỗi: $($process.ExitCode)" -ForegroundColor Red
}