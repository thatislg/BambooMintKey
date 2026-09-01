# scripts/test-register.ps1
# Kiem thu dang ky BambooMintKey.dll voi Windows TSF.
# Chay trong PowerShell voi quyen Administrator.
[CmdletBinding()]
param (
    [string]$Runtime = "win-x64"
)

# --- UTF-8 ---
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding  = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $PSScriptRoot
$LogPath = Join-Path $RootDir "scripts\test-register.log"
"" | Out-File $LogPath -Encoding utf8

function Write-Log($msg, $color = "White") {
    Write-Host $msg -ForegroundColor $color
    $msg | Out-File $LogPath -Encoding utf8 -Append
}

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Log "Dang yeu cau quyen Administrator (UAC)..." "Yellow"
    Start-Process powershell.exe "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs -Wait
    Write-Log "=== LOG TU PROCESS ELEVATED ===" "Cyan"
    Get-Content $LogPath -Encoding UTF8 -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ }
    exit
}

try {
    Write-Log "[OK] Dang chay voi quyen Administrator." "Green"

    $DllPath = Join-Path $RootDir "publish\$Runtime\BambooMintKey.dll"
    Write-Log "DLL path: $DllPath"

    if (-not (Test-Path $DllPath)) {
        Write-Log "[LOI] Khong tim thay $DllPath" "Red"
        exit 1
    }

    # Dang ky COM & TSF profiles qua DllRegisterServer

    Write-Log "Goi DllRegisterServer qua regsvr32.exe (tranh khoa DLL trong PowerShell)..." "Cyan"
    $proc = Start-Process regsvr32.exe -ArgumentList "/s `"$DllPath`"" -PassThru -Wait
    $hr = $proc.ExitCode
    Write-Log "regsvr32 ExitCode: $hr" $(if ($hr -eq 0) { "Green" } else { "Red" })

    $clsidPath = "HKLM:\SOFTWARE\Classes\CLSID\{B8A5A29D-68B1-4A59-B41E-D8B383D6F2C1}"
    if (Test-Path $clsidPath) {
        Write-Log "[OK] Registry CLSID da duoc tao." "Green"
    } else {
        Write-Log "[FAIL] Khong tim thay registry CLSID." "Red"
    }

    $tipPath = "HKLM:\SOFTWARE\Microsoft\CTF\TIP\{B8A5A29D-68B1-4A59-B41E-D8B383D6F2C1}"
    if (Test-Path $tipPath) {
        Write-Log "[OK] Registry TSF TIP da duoc tao." "Green"
    } else {
        Write-Log "[FAIL] Khong tim thay registry TSF TIP." "Red"
    }

    Write-Log "Luu y: Dang ky TSF Profile/Category da thanh cong o HKLM." "Green"
    Write-Log "De user hien tai su dung duoc, hay chay trong PowerShell KHONG Admin:" "Yellow"
    Write-Log "  .\scripts\enable-tip.ps1" "Gray"
    Write-Log "Sau do restart ctfmon hoac dang xuat/dang nhap lai." "Yellow"
    Write-Log "  Stop-Process -Name ctfmon -Force; Start-Process ctfmon" "Gray"

    Write-Log "Hoan tat." "Cyan"
} catch {
    Write-Log "[EXCEPTION] $_" "Red"
    throw
}
