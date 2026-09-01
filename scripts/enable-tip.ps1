# scripts/enable-tip.ps1
# Enable BambooMintKey input tip cho user hien tai (KHONG chay Admin).
[CmdletBinding()]
param (
    [string]$Runtime = "win-x64"
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$RootDir = Split-Path -Parent $PSScriptRoot
$DllPath = Join-Path $RootDir "publish\$Runtime\BambooMintKey.dll"

$Clsid = "{B8A5A29D-68B1-4A59-B41E-D8B383D6F2C1}"
$ProfileGuid = "{C2F31A8E-92D0-4F81-9C3E-A52889211D44}"
$LangId = "0x0000042A"

Write-Host "Dang ghi Registry HKCU Profile & SortOrder cho BambooMintKey..." -ForegroundColor Cyan

# 1. Ghi HKCU CTF TIP Profile qua reg.exe (tranh loi Set-ItemProperty (Default) tu PowerShell)
reg add "HKCU\SOFTWARE\Microsoft\CTF\TIP\$Clsid" /ve /d "BambooMintKey Vietnamese Input" /f | Out-Null
reg add "HKCU\SOFTWARE\Microsoft\CTF\TIP\$Clsid\LanguageProfile\$LangId\$ProfileGuid" /ve /d "BambooMintKey Vietnamese Input" /f | Out-Null
reg add "HKCU\SOFTWARE\Microsoft\CTF\TIP\$Clsid\LanguageProfile\$LangId\$ProfileGuid" /v "Enable" /t REG_DWORD /d 1 /f | Out-Null
reg add "HKCU\SOFTWARE\Microsoft\CTF\TIP\$Clsid\LanguageProfile\$LangId\$ProfileGuid" /v "IconFile" /t REG_SZ /d "$DllPath" /f | Out-Null
reg add "HKCU\SOFTWARE\Microsoft\CTF\TIP\$Clsid\LanguageProfile\$LangId\$ProfileGuid" /v "IconIndex" /t REG_DWORD /d 0 /f | Out-Null

# 2. Ghi SortOrder AssemblyItem cho Vietnamese (0x0000042a) qua reg.exe
reg add "HKCU\Software\Microsoft\CTF\SortOrder\AssemblyItem\0x0000042a\00000000" /v "CLSID" /t REG_SZ /d "$Clsid" /f | Out-Null
reg add "HKCU\Software\Microsoft\CTF\SortOrder\AssemblyItem\0x0000042a\00000000" /v "Profile" /t REG_SZ /d "$ProfileGuid" /f | Out-Null

# 3. Add vao WinUserLanguageList (backup)
try {
    $tip = "042A:$Clsid$ProfileGuid"
    $list = Get-WinUserLanguageList
    $vi = $list | Where-Object { $_.LanguageTag -eq 'vi' }
    if ($vi -and -not ($vi.InputMethodTips -contains $tip)) {
        $vi.InputMethodTips.Add($tip)
        Set-WinUserLanguageList -LanguageList $list -Force -ErrorAction SilentlyContinue
    }
} catch {
    Write-Host "[CANH BAO] Khong the cap nhat LanguageList qua Set-WinUserLanguageList, da ghi Registry SortOrder." -ForegroundColor Yellow
}

# 4. Restart ctfmon de nhan profile moi
Write-Host "[OK] Da ghi Registry HKCU Profile & SortOrder thanh cong." -ForegroundColor Green
Write-Host "Dang khoi dong lai ctfmon.exe..." -ForegroundColor Yellow
Stop-Process -Name ctfmon -Force -ErrorAction SilentlyContinue
Start-Process ctfmon
Write-Host "[OK] Hoan tat kich hoat TIP cho user hien tai." -ForegroundColor Green

