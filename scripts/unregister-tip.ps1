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
    Write-Host "Gỡ đăng ký HKLM thành công!" -ForegroundColor Green
} else {
    Write-Host "[FAIL] Gỡ đăng ký HKLM thất bại với mã lỗi: $($process.ExitCode)" -ForegroundColor Red
}

Write-Host "Đang dọn dẹp Registry HKCU và khởi động lại ctfmon..." -ForegroundColor Cyan
reg delete "HKCU\SOFTWARE\Microsoft\CTF\TIP\{B8A5A29D-68B1-4A59-B41E-D8B383D6F2C1}" /f | Out-Null
Stop-Process -Name ctfmon -Force -ErrorAction SilentlyContinue
Start-Process ctfmon
Write-Host "[OK] Đã gỡ đăng ký hoàn toàn BambooMintKey khỏi Windows." -ForegroundColor Green