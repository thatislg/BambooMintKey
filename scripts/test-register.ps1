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

    # Kiem tra moi truong TSF truoc
    $tsfProfilesPath = "HKLM:\SOFTWARE\Classes\CLSID\{33C53824-660F-457B-8B3E-5F4A9D87AC47}"
    $tsfCategoryPath  = "HKLM:\SOFTWARE\Classes\CLSID\{A4B54FC0-ACAA-49FB-BB87-4EB0260080F6}"
    if (-not (Test-Path $tsfProfilesPath) -or -not (Test-Path $tsfCategoryPath)) {
        Write-Log "[CANH BAO] Moi truong TSF chua san sang tren may nay (khong tim thay CLSID cua ITfInputProcessorProfiles / ITfCategoryMgr)." "Yellow"
        Write-Log "Ban co the van tao duoc Registry CLSID cua BambooMintKey, nhung dang ky TSF Profile/Category se that bai voi 0x80040154." "Yellow"
        Write-Log "Khac phuc: them bat ky input method nao (vi du: Microsoft Bopomofo / Microsoft Pinyin) trong Settings -> Language." "Yellow"
    }

    Add-Type -TypeDefinition @"
using System.Runtime.InteropServices;
public static class Win32Native {
    [DllImport(@"$DllPath", EntryPoint = "DllRegisterServer", CallingConvention = CallingConvention.StdCall)]
    public static extern int DllRegisterServer();
}
"@

    Write-Log "Goi DllRegisterServer..." "Cyan"
    $hr = [Win32Native]::DllRegisterServer()
    Write-Log "HRESULT: 0x$($hr.ToString('X8')) ($hr)" $(if ($hr -eq 0) { "Green" } else { "Red" })

    $clsidPath = "HKLM:\SOFTWARE\Classes\CLSID\{B8A5A29D-68B1-4A59-B41E-D8B383D6F2C1}"
    if (Test-Path $clsidPath) {
        Write-Log "[OK] Registry CLSID da duoc tao." "Green"
    } else {
        Write-Log "[FAIL] Khong tim thay registry CLSID." "Red"
    }

    Write-Log "Hoan tat." "Cyan"
} catch {
    Write-Log "[EXCEPTION] $_" "Red"
    throw
}
